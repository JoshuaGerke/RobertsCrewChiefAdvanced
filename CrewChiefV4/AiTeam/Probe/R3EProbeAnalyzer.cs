using System;
using System.Diagnostics;
using CrewChiefV4.RaceRoom.RaceRoomData;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// RaceRoom: liest das Shared Memory „$R3E“ (nur lesen, ohne Mapper, ohne Spielzustand).
    /// Offsets werden aus RaceRoomData.cs berechnet und beim Start gegen den Marshaller geprüft.
    /// </summary>
    internal sealed class R3EProbeAnalyzer : ProbeAnalyzer
    {
        private const double R3ENotAvailable = -1;

        internal override string SimName { get { return "RaceRoom"; } }
        internal override string FilePrefix { get { return "R3E"; } }
        internal override string SharedMemoryName { get { return RaceRoom.RaceRoomConstant.SharedMemoryName; } }

        private readonly int oVersionMajor, oVersionMinor, oAllDriversOffset, oDriverDataSize, oGamePaused, oGameInMenus, oGameInReplay,
            oSimTicks, oSimTime, oNumCars, oDriverData, oSlotPlayer, oTrackId, oLayoutId, oTrackName, oLayoutName, oSessionType;
        private readonly int driverSize, oEntryPlace, oEntrySlot;

        internal R3EProbeAnalyzer() : this(Stopwatch.Frequency) { }

        internal R3EProbeAnalyzer(long tickFrequency) : base(tickFrequency)
        {
            Type root = typeof(RaceRoomShared);
            LayoutError = PackedLayout.Verify(root);
            StructSize = PackedLayout.Size(root);
            Func<string, int> at = p => PackedLayout.Offset(root, p);
            oVersionMajor = at("VersionMajor"); oVersionMinor = at("VersionMinor");
            oAllDriversOffset = at("AllDriversOffset"); oDriverDataSize = at("DriverDataSize");
            oGamePaused = at("GamePaused"); oGameInMenus = at("GameInMenus"); oGameInReplay = at("GameInReplay");
            oSimTicks = at("Player.GameSimulationTicks"); oSimTime = at("Player.GameSimulationTime");
            oNumCars = at("NumCars"); oDriverData = at("DriverData");
            oSlotPlayer = at("VehicleInfo.SlotId");
            oTrackId = at("TrackId"); oLayoutId = at("LayoutId"); oTrackName = at("TrackName"); oLayoutName = at("LayoutName");
            oSessionType = at("SessionType");
            driverSize = PackedLayout.Size(typeof(DriverData));
            oEntryPlace = PackedLayout.Offset(typeof(DriverData), "Place");
            oEntrySlot = PackedLayout.Offset(typeof(DriverData), "DriverInfo.SlotId");
            DefineChannels();
            Finish();
        }

        private void Add(string id, string group, string path, string unit, ProbeScope scope, string note = null, bool sentinel = false)
        {
            bool perCar = scope == ProbeScope.Opponents || scope == ProbeScope.OwnListEntry;
            Type leaf;
            int offset = perCar ? oDriverData + PackedLayout.Offset(typeof(DriverData), path, out leaf) : PackedLayout.Offset(typeof(RaceRoomShared), path, out leaf);
            AddChannel(id, group, path, unit, scope, ProbeChannel.KindOf(leaf), offset, perCar ? driverSize : 0, note, sentinel ? R3ENotAvailable : double.NaN);
        }

        private static readonly string[] Corners = { "FrontLeft", "FrontRight", "RearLeft", "RearRight" };
        private static readonly string[] CornerIds = { "vl", "vr", "hl", "hr" };

        private void DefineChannels()
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

        protected override void ReadState(byte[] b, out bool paused, out bool menu, out bool replay, out long clock, out double clockTime)
        {
            paused = BitConverter.ToInt32(b, oGamePaused) == 1;
            menu = BitConverter.ToInt32(b, oGameInMenus) == 1;
            replay = BitConverter.ToInt32(b, oGameInReplay) == 1;
            clock = BitConverter.ToInt32(b, oSimTicks);
            clockTime = BitConverter.ToDouble(b, oSimTime);
        }

        protected override string CheckHeader(byte[] b)
        {
            int major = BitConverter.ToInt32(b, oVersionMajor), minor = BitConverter.ToInt32(b, oVersionMinor);
            DataVersion = major + "." + minor;
            int allDrivers = BitConverter.ToInt32(b, oAllDriversOffset);
            int driverDataSize = BitConverter.ToInt32(b, oDriverDataSize);
            int expected = (int)RaceRoom.RaceRoomConstant.VersionMajor.R3E_VERSION_MAJOR;
            if (major != expected) return "Datenformat-Version " + major + " statt " + expected;
            if (allDrivers != 0 && allDrivers != oNumCars) return "Spiel meldet Fahrerliste bei Byte " + allDrivers + ", erwartet " + oNumCars;
            if (driverDataSize != 0 && driverDataSize != driverSize) return "Spiel meldet Fahrereintrag " + driverDataSize + " Byte, erwartet " + driverSize;
            return null;
        }

        protected override long SessionKey(byte[] b)
        {
            return ((long)BitConverter.ToInt32(b, oTrackId) << 40) ^ ((long)BitConverter.ToInt32(b, oLayoutId) << 16) ^ BitConverter.ToInt32(b, oSessionType);
        }

        protected override void ReadTrack(byte[] b, out string track, out string layout)
        {
            track = Utf8(b, oTrackName, 64);
            layout = Utf8(b, oLayoutName, 64);
        }

        protected override int CollectEntries(byte[] b, int[] index, int[] slot, out int own)
        {
            own = -1;
            int max = Math.Min(Math.Max(BitConverter.ToInt32(b, oNumCars), 0), 128);
            int playerSlot = BitConverter.ToInt32(b, oSlotPlayer);
            int n = 0;
            for (int i = 0; i < max && n < index.Length; i++)
            {
                int entry = oDriverData + i * driverSize;
                if (entry + driverSize > b.Length) break;
                if (BitConverter.ToInt32(b, entry + oEntryPlace) <= 0) continue;
                int s = BitConverter.ToInt32(b, entry + oEntrySlot);
                if (s == playerSlot) { own = i; continue; }
                index[n] = i; slot[n] = s; n++;
            }
            return n;
        }

        /// <summary>Für Tests: schreibt einen Wert an einen Strukturpfad.</summary>
        internal static void Write(byte[] buffer, string path, double value)
        {
            Type leaf;
            int at = PackedLayout.Offset(typeof(RaceRoomShared), path, out leaf);
            byte[] bytes = leaf == typeof(int) ? BitConverter.GetBytes((int)value) : leaf == typeof(float) ? BitConverter.GetBytes((float)value) : BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, at, bytes.Length);
        }
    }
}
