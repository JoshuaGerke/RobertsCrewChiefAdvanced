using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CrewChiefV4.RaceRoom.RaceRoomData;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// Wertet Rohbilder des RaceRoom-Shared-Memory aus (nur Lesen, ohne Mapper, ohne Spielzustand).
    /// Misst je Kanal: vorhanden?, Wertebereich, wie oft er sich ändert und wie gleichmäßig.
    /// Testbar mit künstlichen Puffern; der Lesefaden liegt in <see cref="TelemetryProbe"/>.
    /// </summary>
    internal sealed class R3EProbeAnalyzer
    {
        internal const string SimName = "RaceRoom";
        private const double R3ENotAvailable = -1;

        internal readonly List<ProbeChannel> Channels = new List<ProbeChannel>();
        internal readonly int StructSize;
        internal readonly string LayoutError;
        private readonly double ticksPerMs;

        // Feste Offsets
        private readonly int oVersionMajor, oVersionMinor, oAllDriversOffset, oDriverDataSize, oGamePaused, oGameInMenus, oGameInReplay,
            oSimTicks, oSimTime, oNumCars, oDriverData, oSlotPlayer, oTrackId, oLayoutId, oTrackName, oLayoutName, oSessionType;
        private readonly int driverSize, oEntryPlace, oEntrySlot, numCarsOffset;

        // Zustand
        private bool hasFrame;
        private int lastSimTicks;
        private double lastSimTime;
        private long lastFrameTicks, lastPollTicks;
        private ProbeChannel[] singles, opponents, ownEntry;
        private bool wasActive;
        private bool frameIntervalValid; // erst nach einem neuen Bild im aktiven Zustand
        private readonly bool[] presentNow = new bool[ProbeChannel.MaxSlots];
        private readonly bool[] presentBefore = new bool[ProbeChannel.MaxSlots];
        private int lastTrackId = int.MinValue, lastLayoutId = int.MinValue, lastSessionType = int.MinValue;

        // Ergebnisse (für Bericht)
        internal readonly IntervalHistogram FrameIntervals = new IntervalHistogram();
        internal readonly IntervalHistogram PollIntervals = new IntervalHistogram();
        internal long Polls, ActivePolls, Frames, ActiveFrames, CostTicksSum, CostTicksMax;
        internal double ActiveMs, PausedMs, MenuMs, ReplayMs, StalledMs;
        internal long SimTicksAdvanced;
        internal double SimTicksWallMs;
        internal int MaxOpponents, VersionMajor, VersionMinor, Sessions;
        internal double OpponentSlotMs;
        internal string TrackName, LayoutName, HeaderError;
        internal long HeaderChecks;

        internal R3EProbeAnalyzer() : this(Stopwatch.Frequency) { }

        internal R3EProbeAnalyzer(long tickFrequency)
        {
            ticksPerMs = tickFrequency / 1000.0;
            Type root = typeof(RaceRoomShared);
            LayoutError = PackedLayout.Verify(root);
            StructSize = PackedLayout.Size(root);
            Func<string, int> at = p => PackedLayout.Offset(root, p);
            oVersionMajor = at("VersionMajor"); oVersionMinor = at("VersionMinor");
            oAllDriversOffset = at("AllDriversOffset"); oDriverDataSize = at("DriverDataSize");
            oGamePaused = at("GamePaused"); oGameInMenus = at("GameInMenus"); oGameInReplay = at("GameInReplay");
            oSimTicks = at("Player.GameSimulationTicks"); oSimTime = at("Player.GameSimulationTime");
            oNumCars = at("NumCars"); numCarsOffset = oNumCars; oDriverData = at("DriverData");
            oSlotPlayer = at("VehicleInfo.SlotId");
            oTrackId = at("TrackId"); oLayoutId = at("LayoutId"); oTrackName = at("TrackName"); oLayoutName = at("LayoutName");
            oSessionType = at("SessionType");
            driverSize = PackedLayout.Size(typeof(DriverData));
            oEntryPlace = PackedLayout.Offset(typeof(DriverData), "Place");
            oEntrySlot = PackedLayout.Offset(typeof(DriverData), "DriverInfo.SlotId");
            DefineChannels(root);
            var s = new List<ProbeChannel>(); var o = new List<ProbeChannel>(); var l = new List<ProbeChannel>();
            foreach (ProbeChannel c in Channels)
            {
                if (c.Scope == ProbeScope.Opponents) o.Add(c);
                else if (c.Scope == ProbeScope.OwnListEntry) l.Add(c);
                else s.Add(c);
            }
            singles = s.ToArray(); opponents = o.ToArray(); ownEntry = l.ToArray();
        }

        private void Add(string id, string group, string path, string unit, ProbeScope scope, string note = null, bool sentinel = false)
        {
            Type root = scope == ProbeScope.Opponents || scope == ProbeScope.OwnListEntry ? typeof(DriverData) : typeof(RaceRoomShared);
            Type leaf;
            int offset = PackedLayout.Offset(root, path, out leaf);
            ProbeKind kind = leaf == typeof(int) ? ProbeKind.Int32 : leaf == typeof(float) ? ProbeKind.Single : leaf == typeof(double) ? ProbeKind.Double
                : throw new NotSupportedException(path + " ist kein Zahlenfeld");
            Channels.Add(new ProbeChannel(id, group, path, unit, scope, kind, offset, note, sentinel ? R3ENotAvailable : double.NaN));
        }

        private static readonly string[] Corners = { "FrontLeft", "FrontRight", "RearLeft", "RearRight" };
        private static readonly string[] CornerIds = { "vl", "vr", "hl", "hr" };

        private void DefineChannels(Type root)
        {
            const ProbeScope P = ProbeScope.Player, S = ProbeScope.Session, O = ProbeScope.Opponents, L = ProbeScope.OwnListEntry;
            Add("quelle.sim_ticks", "Quelle", "Player.GameSimulationTicks", "Ticks", P, "Takt der Physik");
            Add("quelle.sim_zeit", "Quelle", "Player.GameSimulationTime", "s", P);

            Add("bewegung.tempo", "Bewegung", "CarSpeed", "m/s", P, null, true);
            Add("bewegung.pos_x", "Bewegung", "Player.Position.X", "m", P);
            Add("bewegung.pos_z", "Bewegung", "Player.Position.Z", "m", P);
            Add("bewegung.hoehe_y", "Bewegung", "Player.Position.Y", "m", P);
            Add("bewegung.v_x", "Bewegung", "Player.Velocity.X", "m/s", P);
            Add("bewegung.v_z", "Bewegung", "Player.Velocity.Z", "m/s", P);
            Add("bewegung.g_quer", "Bewegung", "Player.LocalGforce.X", "g", P);
            Add("bewegung.g_vert", "Bewegung", "Player.LocalGforce.Y", "g", P);
            Add("bewegung.g_laengs", "Bewegung", "Player.LocalGforce.Z", "g", P);
            Add("bewegung.gier", "Bewegung", "Player.Orientation.Y", "rad", P);
            Add("bewegung.gierrate", "Bewegung", "Player.LocalAngularVelocity.Y", "rad/s", P);

            Add("eingabe.gas", "Fahrer-Eingaben", "Throttle", "0..1", P, null, true);
            Add("eingabe.gas_roh", "Fahrer-Eingaben", "ThrottleRaw", "0..1", P, null, true);
            Add("eingabe.bremse", "Fahrer-Eingaben", "Brake", "0..1", P, null, true);
            Add("eingabe.bremse_roh", "Fahrer-Eingaben", "BrakeRaw", "0..1", P, null, true);
            Add("eingabe.kupplung", "Fahrer-Eingaben", "Clutch", "0..1", P, null, true);
            Add("eingabe.lenkung_roh", "Fahrer-Eingaben", "SteerInputRaw", "-1..1", P);
            Add("eingabe.gang", "Fahrer-Eingaben", "Gear", "Gang", P, "-2 = keine Angabe");
            Add("motor.drehzahl", "Motor", "EngineRps", "rad/s", P, null, true);
            Add("motor.drehmoment", "Motor", "Player.EngineTorque", "Nm", P);

            Add("hilfe.abs_status", "Fahrhilfen", "AidSettings.Abs", "Code", P, "-1 n/v, 0 aus, 1 an, 5 greift gerade", true);
            Add("hilfe.tc_status", "Fahrhilfen", "AidSettings.Tc", "Code", P, "-1 n/v, 0 aus, 1 an, 5 greift gerade", true);
            Add("hilfe.esp_status", "Fahrhilfen", "AidSettings.Esp", "Code", P, "-1 n/v, 0 aus, 1 an, 5 greift gerade", true);
            Add("hilfe.tc_prozent", "Fahrhilfen", "TractionControlPercent", "%", P, null, true);
            Add("hilfe.tc_stufe", "Fahrhilfen", "TractionControlSetting", "Stufe", P, null, true);
            Add("hilfe.abs_stufe", "Fahrhilfen", "AbsSetting", "Stufe", P, null, true);
            Add("hilfe.motorkennfeld", "Fahrhilfen", "EngineMapSetting", "Stufe", P, null, true);
            Add("hilfe.bremsbalance", "Fahrhilfen", "BrakeBias", "0..1", P, null, true);
            Add("hilfe.pitlimiter", "Fahrhilfen", "PitLimiter", "Code", P, null, true);

            for (int i = 0; i < Corners.Length; i++)
            {
                string c = Corners[i], k = CornerIds[i];
                Add("reifen.temp_mitte_" + k, "Reifen", "TireTemp." + c + ".CurrentTemp.Center", "°C", P, null, true);
                Add("reifen.druck_" + k, "Reifen", "TirePressure." + c, "kPa", P, null, true);
                Add("reifen.verschleiss_" + k, "Reifen", "TireWear." + c, "0..1", P, null, true);
                Add("reifen.last_" + k, "Reifen", "TireLoad." + c, "N", P, null, true);
                Add("reifen.grip_" + k, "Reifen", "TireGrip." + c, "0..1", P, null, true);
                Add("reifen.schmutz_" + k, "Reifen", "TireDirt." + c, "0..1", P, null, true);
                Add("reifen.drehzahl_" + k, "Reifen", "TireRps." + c, "rad/s", P, null, true);
                Add("reifen.untergrund_" + k, "Reifen", "TireOnMtrl." + c, "Code", P, null, true);
                Add("bremse.temp_" + k, "Bremsen", "BrakeTemp." + c + ".CurrentTemp", "°C", P, null, true);
                Add("bremse.druck_" + k, "Bremsen", "BrakePressure." + c, "kN", P, null, true);
                Add("fahrwerk.einfederung_" + k, "Fahrwerk", "Player.SuspensionDeflection." + c, "m", P);
                Add("fahrwerk.bodenfreiheit_" + k, "Fahrwerk", "Player.RideHeight." + c, "m", P);
            }

            Add("motor.wasser", "Motor", "EngineWaterTemp", "°C", P, null, true);
            Add("motor.oel_temp", "Motor", "EngineOilTemp", "°C", P, null, true);
            Add("motor.oel_druck", "Motor", "EngineOilPressure", "kPa", P, null, true);
            Add("motor.sprit_druck", "Motor", "FuelPressure", "kPa", P, null, true);
            Add("motor.turbo", "Motor", "TurboPressure", "bar", P, null, true);

            Add("sprit.liter", "Sprit", "FuelLeft", "l", P, null, true);
            Add("sprit.pro_runde_spiel", "Sprit", "FuelPerLap", "l", P, "Wert des Spiels, nicht von RRCA gemessen", true);
            Add("sprit.virtuelle_energie", "Sprit", "VirtualEnergyLeft", "MJ", P, null, true);

            Add("runde.strecke_m", "Runde", "LapDistance", "m", P, null, true);
            Add("runde.anteil", "Runde", "LapDistanceFraction", "0..1", P, null, true);
            Add("runde.laufende_zeit", "Runde", "LapTimeCurrentSelf", "s", P, null, true);
            Add("runde.abstand_vorne", "Runde", "TimeDeltaFront", "s", P, null, true);
            Add("runde.abstand_hinten", "Runde", "TimeDeltaBehind", "s", P, null, true);
            Add("runde.delta_bestzeit", "Runde", "TimeDeltaBestSelf", "s", P, null, true);
            Add("runde.platz", "Runde", "Position", "Platz", P, null, true);
            Add("runde.platz_klasse", "Runde", "PositionClass", "Platz", P, null, true);
            Add("runde.runden", "Runde", "CompletedLaps", "Runden", P, null, true);
            Add("runde.sektor", "Runde", "TrackSector", "Sektor", P, null, true);
            Add("runde.gueltig", "Runde", "CurrentLapValid", "Code", P, null, true);

            Add("sitzung.restzeit", "Sitzung", "SessionTimeRemaining", "s", S, null, true);
            Add("sitzung.phase", "Sitzung", "SessionPhase", "Code", S, null, true);
            Add("sitzung.art", "Sitzung", "SessionType", "Code", S, null, true);
            Add("sitzung.gelb", "Sitzung", "Flags.Yellow", "Code", S, null, true);
            Add("sitzung.gelb_abstand", "Sitzung", "Flags.ClosestYellowDistanceIntoTrack", "m", S, null, true);
            Add("sitzung.blau", "Sitzung", "Flags.Blue", "Code", S, null, true);
            Add("sitzung.tracklimits", "Sitzung", "CutTrackWarnings", "Anzahl", S, null, true);
            Add("sitzung.vorfallpunkte", "Sitzung", "IncidentPoints", "Punkte", S, null, true);
            Add("sitzung.boxengasse", "Sitzung", "InPitlane", "Code", S, null, true);
            Add("sitzung.boxenstatus", "Sitzung", "PitState", "Code", S, null, true);
            Add("sitzung.fahrzeuge", "Sitzung", "NumCars", "Anzahl", S);

            Add("schaden.motor", "Schaden", "CarDamage.Engine", "0..1", P, null, true);
            Add("schaden.getriebe", "Schaden", "CarDamage.Transmission", "0..1", P, null, true);
            Add("schaden.aero", "Schaden", "CarDamage.Aerodynamics", "0..1", P, null, true);
            Add("schaden.fahrwerk", "Schaden", "CarDamage.Suspension", "0..1", P, null, true);

            Add("gegner.pos_x", "Gegner (je Auto)", "Position.X", "m", O, "Karte: Weltposition");
            Add("gegner.pos_z", "Gegner (je Auto)", "Position.Z", "m", O, "Karte: Weltposition");
            Add("gegner.strecke_m", "Gegner (je Auto)", "LapDistance", "m", O, null, true);
            Add("gegner.tempo", "Gegner (je Auto)", "CarSpeed", "m/s", O, null, true);
            Add("gegner.gier", "Gegner (je Auto)", "Orientation.Y", "rad", O);
            Add("gegner.platz", "Gegner (je Auto)", "Place", "Platz", O, null, true);
            Add("gegner.platz_klasse", "Gegner (je Auto)", "PlaceClass", "Platz", O, null, true);
            Add("gegner.runden", "Gegner (je Auto)", "CompletedLaps", "Runden", O, null, true);
            Add("gegner.sektor", "Gegner (je Auto)", "TrackSector", "Sektor", O, null, true);
            Add("gegner.laufende_zeit", "Gegner (je Auto)", "LapTimeCurrentSelf", "s", O, null, true);
            Add("gegner.abstand_vorne", "Gegner (je Auto)", "TimeDeltaFront", "s", O, null, true);
            Add("gegner.abstand_hinten", "Gegner (je Auto)", "TimeDeltaBehind", "s", O, null, true);
            Add("gegner.boxengasse", "Gegner (je Auto)", "InPitlane", "Code", O, null, true);
            Add("gegner.boxenstatus", "Gegner (je Auto)", "PitStopStatus", "Code", O, null, true);

            Add("eigen_liste.pos_x", "Eigenes Auto in der Fahrerliste", "Position.X", "m", L, "Vergleich mit Bewegung.pos_x");
            Add("eigen_liste.strecke_m", "Eigenes Auto in der Fahrerliste", "LapDistance", "m", L, "Vergleich mit Runde.strecke_m", true);
        }

        private bool ReadyFor(byte[] buffer) { return buffer != null && buffer.Length >= StructSize; }

        /// <summary>Wertet ein Rohbild aus. <paramref name="now"/> und <paramref name="costTicks"/> in Stopwatch-Ticks.</summary>
        internal void Observe(byte[] buffer, long now, long costTicks)
        {
            if (!ReadyFor(buffer)) return;
            Polls++;
            CostTicksSum += costTicks;
            if (costTicks > CostTicksMax) CostTicksMax = costTicks;
            double sinceLastPollMs = lastPollTicks == 0 ? 0 : (now - lastPollTicks) / ticksPerMs;
            if (lastPollTicks != 0) PollIntervals.Add(sinceLastPollMs);
            lastPollTicks = now;

            CheckHeader(buffer);

            bool paused = BitConverter.ToInt32(buffer, oGamePaused) == 1;
            bool menu = BitConverter.ToInt32(buffer, oGameInMenus) == 1;
            bool replay = BitConverter.ToInt32(buffer, oGameInReplay) == 1;
            int simTicks = BitConverter.ToInt32(buffer, oSimTicks);
            double simTime = BitConverter.ToDouble(buffer, oSimTime);
            bool newFrame = !hasFrame || simTicks != lastSimTicks || !simTime.Equals(lastSimTime);
            bool active = !paused && !menu && !replay;

            if (newFrame)
            {
                Frames++;
                if (active && frameIntervalValid) FrameIntervals.Add((now - lastFrameTicks) / ticksPerMs);
                if (active && frameIntervalValid && simTicks > lastSimTicks)
                {
                    // Nur zusammenhängende aktive Abschnitte zählen (keine Pausen, kein Sitzungswechsel).
                    SimTicksAdvanced += simTicks - lastSimTicks;
                    SimTicksWallMs += (now - lastFrameTicks) / ticksPerMs;
                }
                lastFrameTicks = now;
                lastSimTicks = simTicks;
                lastSimTime = simTime;
                hasFrame = true;
                frameIntervalValid = active;
            }
            if (!active) frameIntervalValid = false;

            if (paused) PausedMs += sinceLastPollMs;
            else if (menu) MenuMs += sinceLastPollMs;
            else if (replay) ReplayMs += sinceLastPollMs;
            else if (!newFrame && hasFrame && (now - lastFrameTicks) / ticksPerMs > 1000) StalledMs += sinceLastPollMs;
            else ActiveMs += sinceLastPollMs;

            if (!active)
            {
                if (wasActive) InterruptAll();
                wasActive = false;
                return;
            }
            if (!wasActive) InterruptAll();
            wasActive = true;
            ActivePolls++;
            if (newFrame) ActiveFrames++;

            NoteSession(buffer);

            for (int i = 0; i < singles.Length; i++)
                singles[i].ObserveSingle(singles[i].Read(buffer, 0), now, newFrame, ticksPerMs);
            ObserveDrivers(buffer, now, newFrame, sinceLastPollMs);
        }

        private void InterruptAll()
        {
            foreach (ProbeChannel c in Channels) c.Interrupt();
            Array.Clear(presentBefore, 0, presentBefore.Length);
        }

        private void ObserveDrivers(byte[] buffer, long now, bool newFrame, double sinceLastPollMs)
        {
            int numCars = BitConverter.ToInt32(buffer, oNumCars);
            int maxEntries = Math.Min(Math.Max(numCars, 0), 128);
            int playerSlot = BitConverter.ToInt32(buffer, oSlotPlayer);
            Array.Clear(presentNow, 0, presentNow.Length);
            int count = 0;
            for (int i = 0; i < maxEntries; i++)
            {
                int entry = oDriverData + i * driverSize;
                if (entry + driverSize > buffer.Length) break;
                if (BitConverter.ToInt32(buffer, entry + oEntryPlace) <= 0) continue;
                int slot = BitConverter.ToInt32(buffer, entry + oEntrySlot);
                if (slot < 0 || slot >= ProbeChannel.MaxSlots) continue;
                bool own = slot == playerSlot;
                if (own)
                {
                    for (int k = 0; k < ownEntry.Length; k++) ownEntry[k].ObserveSingle(ownEntry[k].Read(buffer, entry), now, newFrame, ticksPerMs);
                    continue;
                }
                if (presentNow[slot]) continue; // doppelter Slot im selben Bild: nur den ersten zählen
                presentNow[slot] = true;
                count++;
                for (int k = 0; k < opponents.Length; k++) opponents[k].ObserveSlot(slot, opponents[k].Read(buffer, entry), now, newFrame, ticksPerMs);
            }
            for (int slot = 0; slot < ProbeChannel.MaxSlots; slot++)
            {
                if (presentBefore[slot] && !presentNow[slot])
                    for (int k = 0; k < opponents.Length; k++) opponents[k].ForgetSlot(slot);
                presentBefore[slot] = presentNow[slot];
            }
            if (count > MaxOpponents) MaxOpponents = count;
            OpponentSlotMs += sinceLastPollMs * count;
        }

        private void CheckHeader(byte[] buffer)
        {
            HeaderChecks++;
            VersionMajor = BitConverter.ToInt32(buffer, oVersionMajor);
            VersionMinor = BitConverter.ToInt32(buffer, oVersionMinor);
            int allDrivers = BitConverter.ToInt32(buffer, oAllDriversOffset);
            int driverDataSize = BitConverter.ToInt32(buffer, oDriverDataSize);
            if (VersionMajor != (int)RaceRoom.RaceRoomConstant.VersionMajor.R3E_VERSION_MAJOR)
                HeaderError = "Datenformat-Version " + VersionMajor + " statt " + (int)RaceRoom.RaceRoomConstant.VersionMajor.R3E_VERSION_MAJOR;
            else if (allDrivers != 0 && allDrivers != numCarsOffset)
                HeaderError = "Spiel meldet Fahrerliste bei Byte " + allDrivers + ", erwartet " + numCarsOffset;
            else if (driverDataSize != 0 && driverDataSize != driverSize)
                HeaderError = "Spiel meldet Fahrereintrag " + driverDataSize + " Byte, erwartet " + driverSize;
            else
                HeaderError = null;
        }

        private void NoteSession(byte[] buffer)
        {
            int track = BitConverter.ToInt32(buffer, oTrackId), layout = BitConverter.ToInt32(buffer, oLayoutId), type = BitConverter.ToInt32(buffer, oSessionType);
            if (track == lastTrackId && layout == lastLayoutId && type == lastSessionType) return;
            lastTrackId = track; lastLayoutId = layout; lastSessionType = type;
            Sessions++;
            TrackName = Utf8(buffer, oTrackName, 64);
            LayoutName = Utf8(buffer, oLayoutName, 64);
        }

        private static string Utf8(byte[] buffer, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && buffer[end] != 0) end++;
            return Encoding.UTF8.GetString(buffer, offset, end - offset);
        }

        internal double SourceFramesPerSecond { get { return ActiveMs <= 0 ? double.NaN : ActiveFrames / (ActiveMs / 1000.0); } }
        internal double SimTicksPerSecond { get { return SimTicksWallMs <= 0 ? double.NaN : SimTicksAdvanced / (SimTicksWallMs / 1000.0); } }

        /// <summary>Änderungen je Sekunde: Spieler/Sitzung auf aktive Zeit, Gegner je Auto.</summary>
        internal double ChangesPerSecond(ProbeChannel c)
        {
            double ms = c.Scope == ProbeScope.Opponents ? OpponentSlotMs : ActiveMs;
            return ms <= 0 ? double.NaN : c.Changes / (ms / 1000.0);
        }

        internal double FrameShare(ProbeChannel c) { return c.Frames == 0 ? double.NaN : (double)c.Changes / c.Frames; }

        internal double MsSinceLastFrame(long now) { return !hasFrame ? double.PositiveInfinity : (now - lastFrameTicks) / ticksPerMs; }

        internal bool Paused(byte[] buffer)
        {
            return ReadyFor(buffer) && (BitConverter.ToInt32(buffer, oGamePaused) == 1 || BitConverter.ToInt32(buffer, oGameInMenus) == 1 || BitConverter.ToInt32(buffer, oGameInReplay) == 1);
        }

        internal double TicksPerMs { get { return ticksPerMs; } }

        /// <summary>Für Tests: schreibt einen Wert an einen Strukturpfad.</summary>
        internal static void Write(byte[] buffer, string path, double value, int baseOffset = 0, Type root = null)
        {
            Type leaf;
            int at = baseOffset + PackedLayout.Offset(root ?? typeof(RaceRoomShared), path, out leaf);
            byte[] bytes = leaf == typeof(int) ? BitConverter.GetBytes((int)value) : leaf == typeof(float) ? BitConverter.GetBytes((float)value) : BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, at, bytes.Length);
        }

        internal int DriverEntryOffset(int index) { return oDriverData + index * driverSize; }
    }
}
