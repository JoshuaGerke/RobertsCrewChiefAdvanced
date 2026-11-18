using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using rF2SharedMemory;
using rF2SharedMemory.LMUData;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// Le Mans Ultimate: liest die Abbildungen „Telemetrie“ und „Wertung“ (nur lesen). Offsets kommen vom Marshaller
    /// (Pack = 4). Halb geschriebene Bilder erkennt der rF2-Versionsblock (Beginn und Ende müssen gleich sein und
    /// gleich bleiben). Kopiert wird nur der Kopf und die belegten Fahrzeugeinträge, nicht die vollen 128 Plätze.
    ///
    /// Rohbild im Puffer: [Telemetrie][Wertung][eigenes Auto Telemetrie][eigenes Auto Wertung][Wertung je Telemetrie-Eintrag].
    /// Die hinteren drei Blöcke füllt <see cref="Prepare"/>: Das Spiel führt das eigene Auto an wechselnder Stelle und
    /// Telemetrie und Wertung in eigener Reihenfolge; nach dem Umkopieren lesen alle Kanäle an festen Stellen.
    /// </summary>
    internal sealed class LMUProbeAnalyzer : ProbeAnalyzer
    {
        internal const int Max = LMUConstants.MAX_MAPPED_VEHICLES;
        private const int MaxIds = LMUConstants.MAX_MAPPED_IDS;
        private const byte PhasePaused = 9;

        internal static readonly int TelemetrySize = Marshal.SizeOf(typeof(rF2Telemetry)), ScoringSize = Marshal.SizeOf(typeof(rF2Scoring)),
            VehicleSize = Marshal.SizeOf(typeof(rF2VehicleTelemetry)), WheelSize = Marshal.SizeOf(typeof(rF2Wheel)),
            ScoredSize = Marshal.SizeOf(typeof(rF2VehicleScoring));
        internal static readonly int TelemetryCount = TOff("mNumVehicles"), TelemetryVehicles = TOff("mVehicles"),
            ScoringInfo = Marshal.OffsetOf(typeof(rF2Scoring), "mScoringInfo").ToInt32(), ScoringVehicles = Marshal.OffsetOf(typeof(rF2Scoring), "mVehicles").ToInt32(),
            ScoringCount = ScoringInfo + IOff("mNumVehicles");
        /// <summary>Lage der Blöcke im Rohbild.</summary>
        internal static readonly int OwnTelemetry = TelemetrySize + ScoringSize, OwnScoring = OwnTelemetry + VehicleSize, Aligned = OwnScoring + ScoredSize;

        private readonly int vId = VOff("mID"), vsId = SOff("mID"), vsPlayer = SOff("mIsPlayer");
        private readonly int iPhase = IOff("mGamePhase"), iRealtime = IOff("mInRealtime"), iSession = IOff("mSession"), iTrack = IOff("mTrackName"), iLapDist = IOff("mLapDist");
        private readonly int[] scoringOfId = new int[MaxIds], stamp = new int[MaxIds];
        private int generation;

        /// <summary>Telemetrie-Eintrag des eigenen Autos im letzten Bild, sonst -1.</summary>
        internal int OwnIndex { get; private set; } = -1;
        internal bool OwnFound { get; private set; }

        private static readonly string[] CornerIds = { "vl", "vr", "hl", "hr" };

        internal override string SimName { get { return "LMU"; } }
        internal override string FilePrefix { get { return "LMU"; } }
        internal override string SharedMemoryName { get { return LMUConstants.MM_TELEMETRY_FILE_NAME; } }
        internal override bool HasSimClock { get { return false; } }
        internal override ProbeSource CreateSource() { return new LMUProbeSource(); }

        internal LMUProbeAnalyzer() : this(Stopwatch.Frequency) { }

        internal LMUProbeAnalyzer(long tickFrequency) : base(tickFrequency)
        {
            StructSize = Aligned + Max * ScoredSize;
            if (TelemetrySize != LMUConstants.MM_TELEMETRY_STRUCT_SIZE)
                LayoutError = "Telemetrie " + TelemetrySize + " statt " + LMUConstants.MM_TELEMETRY_STRUCT_SIZE + " Byte";
            else if (ScoringSize != LMUConstants.MM_SCORING_STRUCT_SIZE)
                LayoutError = "Wertung " + ScoringSize + " statt " + LMUConstants.MM_SCORING_STRUCT_SIZE + " Byte";
            DefineChannels();
            Finish();
        }

        private static int TOff(string f) { return Marshal.OffsetOf(typeof(rF2Telemetry), f).ToInt32(); }
        private static int VOff(string f) { return Marshal.OffsetOf(typeof(rF2VehicleTelemetry), f).ToInt32(); }
        private static int WOff(string f) { return Marshal.OffsetOf(typeof(rF2Wheel), f).ToInt32(); }
        private static int IOff(string f) { return Marshal.OffsetOf(typeof(rF2ScoringInfo), f).ToInt32(); }
        private static int SOff(string f) { return Marshal.OffsetOf(typeof(rF2VehicleScoring), f).ToInt32(); }

        private static ProbeKind Kind(Type owner, string field)
        {
            Type t = owner.GetField(field).FieldType;
            if (t.IsArray) t = t.GetElementType();
            return t == typeof(rF2Vec3) ? ProbeKind.Double : ProbeChannel.KindOf(t);
        }

        private static int ElementSize(Type owner, string field)
        {
            Type t = owner.GetField(field).FieldType;
            if (!t.IsArray) return 0;
            t = t.GetElementType();
            return t == typeof(rF2Vec3) ? 24 : Marshal.SizeOf(t);
        }

        /// <summary>Feld des eigenen Autos (Telemetrie-Takt). <paramref name="axis"/> bei Vektoren 0 = x, 1 = y, 2 = z; bei Arrays das Element.</summary>
        private void Own(string id, string group, string field, string unit, int axis = -1, string note = null)
        {
            int extra = axis < 0 ? 0 : (typeof(rF2VehicleTelemetry).GetField(field).FieldType == typeof(rF2Vec3) ? axis * 8 : axis * ElementSize(typeof(rF2VehicleTelemetry), field));
            AddChannel(id, group, "Telemetrie.eigenes." + field + (axis < 0 ? "" : "[" + axis + "]"), unit, ProbeScope.Player,
                Kind(typeof(rF2VehicleTelemetry), field), OwnTelemetry + VOff(field) + extra, 0, note);
        }

        /// <summary>Feld des eigenen Autos aus der Wertung (Wertungs-Takt).</summary>
        private void Scored(string id, string group, string field, string unit, int axis = -1, string note = null)
        {
            AddChannel(id, group, "Wertung.eigenes." + field + (axis < 0 ? "" : "[" + axis + "]"), unit, ProbeScope.Player,
                Kind(typeof(rF2VehicleScoring), field), OwnScoring + SOff(field) + Math.Max(0, axis) * 8, 0, note);
        }

        private void Session(string id, string group, string field, string unit, string note = null)
        {
            AddChannel(id, group, "Wertung.mScoringInfo." + field, unit, ProbeScope.Session, Kind(typeof(rF2ScoringInfo), field),
                TelemetrySize + ScoringInfo + IOff(field), 0, note);
        }

        private void Corners(string id, string group, string field, string unit, int element = -1, string note = null)
        {
            for (int w = 0; w < 4; w++)
                AddChannel(id + "_" + CornerIds[w], group, "Telemetrie.eigenes.mWheels[" + w + "]." + field + (element < 0 ? "" : "[" + element + "]"), unit,
                    ProbeScope.Player, Kind(typeof(rF2Wheel), field),
                    OwnTelemetry + VOff("mWheels") + w * WheelSize + WOff(field) + Math.Max(0, element) * ElementSize(typeof(rF2Wheel), field), 0, w == 0 ? note : null);
        }

        /// <summary>Gegnerfeld aus der Telemetrie (Eintrag i).</summary>
        private void Rival(string id, string field, string unit, int axis = -1, string note = null)
        {
            AddChannel(id, "Gegner (je Auto)", "Telemetrie.mVehicles[i]." + field + (axis < 0 ? "" : "[" + axis + "]"), unit, ProbeScope.Opponents,
                Kind(typeof(rF2VehicleTelemetry), field), TelemetryVehicles + VOff(field) + Math.Max(0, axis) * 8, VehicleSize, note);
        }

        /// <summary>Gegnerfeld aus der Wertung, passend zum Telemetrie-Eintrag i umsortiert.</summary>
        private void RivalScored(string id, string field, string unit, int axis = -1, string note = null)
        {
            AddChannel(id, "Gegner (je Auto)", "Wertung.mVehicles[i]." + field + (axis < 0 ? "" : "[" + axis + "]"), unit, ProbeScope.Opponents,
                Kind(typeof(rF2VehicleScoring), field), Aligned + SOff(field) + Math.Max(0, axis) * 8, ScoredSize, note);
        }

        private void DefineChannels()
        {
            AddChannel("quelle.version_telemetrie", "Quelle", "Telemetrie.mVersionUpdateBegin", "Zähler", ProbeScope.Session, ProbeKind.UInt32, 0, 0, "steigt je Telemetrie-Bild");
            AddChannel("quelle.version_wertung", "Quelle", "Wertung.mVersionUpdateBegin", "Zähler", ProbeScope.Session, ProbeKind.UInt32, TelemetrySize, 0, "steigt je Wertungs-Bild");
            Own("quelle.zeit", "Quelle", "mElapsedTime", "s", -1, "Sitzungszeit des Telemetrie-Bilds");

            Own("bewegung.pos_x", "Bewegung", "mPos", "m", 0, "Karte: Weltposition");
            Own("bewegung.pos_z", "Bewegung", "mPos", "m", 2);
            Own("bewegung.v_quer", "Bewegung", "mLocalVel", "m/s", 0);
            Own("bewegung.v_laengs", "Bewegung", "mLocalVel", "m/s", 2, "Fahrzeugkoordinaten, vorwärts negativ");
            Own("bewegung.beschl_quer", "Bewegung", "mLocalAccel", "m/s²", 0);
            Own("bewegung.beschl_vert", "Bewegung", "mLocalAccel", "m/s²", 1);
            Own("bewegung.beschl_laengs", "Bewegung", "mLocalAccel", "m/s²", 2);
            Own("bewegung.gierrate", "Bewegung", "mLocalRot", "rad/s", 1);

            Own("eingabe.gas", "Fahrer-Eingaben", "mFilteredThrottle", "0..1");
            Own("eingabe.gas_roh", "Fahrer-Eingaben", "mUnfilteredThrottle", "0..1");
            Own("eingabe.bremse", "Fahrer-Eingaben", "mFilteredBrake", "0..1");
            Own("eingabe.bremse_roh", "Fahrer-Eingaben", "mUnfilteredBrake", "0..1");
            Own("eingabe.kupplung_roh", "Fahrer-Eingaben", "mUnfilteredClutch", "0..1");
            Own("eingabe.lenkung", "Fahrer-Eingaben", "mFilteredSteering", "-1..1");
            Own("eingabe.lenkung_roh", "Fahrer-Eingaben", "mUnfilteredSteering", "-1..1");
            Own("eingabe.gang", "Fahrer-Eingaben", "mGear", "Gang");

            Own("motor.drehzahl", "Motor", "mEngineRPM", "U/min");
            Own("motor.drehzahl_max", "Motor", "mEngineMaxRPM", "U/min");
            Own("motor.drehmoment", "Motor", "mEngineTorque", "Nm");
            Own("motor.wasser", "Motor", "mEngineWaterTemp", "°C");
            Own("motor.oel_temp", "Motor", "mEngineOilTemp", "°C");
            Own("motor.turbo", "Motor", "mTurboBoostPressure", "Druck");
            Own("motor.ueberhitzt", "Motor", "mOverheating", "ja/nein");

            Own("hilfe.abs_greift", "Fahrhilfen", "mABSActive", "ja/nein");
            Own("hilfe.abs_stufe", "Fahrhilfen", "mABS", "Stufe");
            Own("hilfe.tc_greift", "Fahrhilfen", "mTCActive", "ja/nein");
            Own("hilfe.tc_stufe", "Fahrhilfen", "mTC", "Stufe");
            Own("hilfe.tc_schlupf", "Fahrhilfen", "mTCSlip", "Stufe");
            Own("hilfe.tc_cut", "Fahrhilfen", "mTCCut", "Stufe");
            Own("hilfe.motorkennfeld", "Fahrhilfen", "mMotorMap", "Stufe");
            Own("hilfe.bremsbalance_hinten", "Fahrhilfen", "mRearBrakeBias", "0..1");
            Own("hilfe.begrenzer", "Fahrhilfen", "mSpeedLimiterActive", "ja/nein");
            Own("hilfe.heckklappe", "Fahrhilfen", "mRearFlapActivated", "ja/nein");

            Own("hybrid.batterie", "Hybrid", "mBatteryChargeFraction", "0..1");
            Own("hybrid.ladezustand", "Hybrid", "mStateOfCharge", "%");
            Own("hybrid.rekuperation", "Hybrid", "mRegen", "kW");
            Own("hybrid.e_motor_status", "Hybrid", "mElectricBoostMotorState", "Code", -1, "0 nicht vorhanden, 1 aus, 2 Antrieb, 3 Rekuperation");
            Own("hybrid.e_motor_moment", "Hybrid", "mElectricBoostMotorTorque", "Nm");

            Own("sprit.liter", "Sprit", "mFuel", "l");
            Own("sprit.tank", "Sprit", "mFuelCapacity", "l");
            Own("sprit.virtuelle_energie", "Sprit", "mVirtualEnergy", "Anteil", -1, "Energieregel (Hypercar/LMGT3)");
            Scored("sprit.anteil_wertung", "Sprit", "mFuelFraction", "0..255", -1, "Anteil Sprit oder Batterie laut Wertung");

            Corners("reifen.temp_links", "Reifen", "mTemperature", "K", 0, "Kelvin; links/mitte/rechts, nicht innen/außen");
            Corners("reifen.temp_mitte", "Reifen", "mTemperature", "K", 1);
            Corners("reifen.temp_rechts", "Reifen", "mTemperature", "K", 2);
            Corners("reifen.temp_karkasse", "Reifen", "mTireCarcassTemperature", "K");
            Corners("reifen.temp_optimal", "Reifen", "mOptimalTemp", "K", -1, "Einheit laut Spiel nicht beschrieben");
            Corners("reifen.druck", "Reifen", "mPressure", "kPa");
            Corners("reifen.verschleiss", "Reifen", "mWear", "0..1");
            Corners("reifen.drehzahl", "Reifen", "mRotation", "rad/s");
            Corners("reifen.last", "Reifen", "mTireLoad", "N");
            Corners("reifen.untergrund", "Reifen", "mSurfaceType", "Code", -1, "0 trocken, 1 nass, 2 Gras, 3 Erde, 4 Kies, 5 Randstein");
            Corners("reifen.platt", "Reifen", "mFlat", "ja/nein");
            Corners("bremse.temp", "Bremsen", "mBrakeTemp", "°C", -1, "Einheit laut Struktur; mit Messwerten prüfen");
            Corners("fahrwerk.federweg", "Fahrwerk", "mSuspensionDeflection", "m");
            Corners("fahrwerk.bodenfreiheit", "Fahrwerk", "mRideHeight", "m");

            Own("schaden.aufprall_zeit", "Schaden", "mLastImpactET", "s", -1, "Sitzungszeit des letzten Aufpralls");
            Own("schaden.aufprall_staerke", "Schaden", "mLastImpactMagnitude", "Stärke");
            Own("schaden.teile_ab", "Schaden", "mDetached", "ja/nein");
            Own("schaden.delle_0", "Schaden", "mDentSeverity", "0..2", 0);
            Own("schaden.delle_4", "Schaden", "mDentSeverity", "0..2", 4);

            Own("runde.ungueltig", "Runde", "mLapInvalidated", "ja/nein");
            Own("runde.delta_best", "Runde", "mDeltaBest", "s");
            Own("runde.streckenlimit", "Runde", "mTrackLimitsSteps", "Schritte");
            Own("runde.abstand_vorne", "Runde", "mTimeGapCarAhead", "s");
            Own("runde.abstand_hinten", "Runde", "mTimeGapCarBehind", "s");
            Scored("runde.strecke_m", "Runde", "mLapDist", "m");
            Scored("runde.runden", "Runde", "mTotalLaps", "Runden");
            Scored("runde.sektor", "Runde", "mSector", "Code", -1, "0 = Sektor 3, 1 = Sektor 1, 2 = Sektor 2");
            Scored("runde.letzte", "Runde", "mLastLapTime", "s");
            Scored("runde.beste", "Runde", "mBestLapTime", "s");
            Scored("runde.sektor1_aktuell", "Runde", "mCurSector1", "s");
            Scored("runde.zeit_in_runde", "Runde", "mTimeIntoLap", "s", -1, "Schätzung des Spiels");
            Scored("runde.zaehlt", "Runde", "mCountLapFlag", "Code");
            Scored("runde.platz", "Runde", "mPlace", "Platz");
            Scored("runde.abstand_vordermann", "Runde", "mTimeBehindNext", "s");
            Scored("wertung.v_laengs", "Runde", "mLocalVel", "m/s", 2, "Wertungs-Takt, Vergleich mit bewegung.v_laengs");

            Scored("box.status", "Box", "mPitState", "Code", -1, "0 nichts, 1 angefordert, 2 Einfahrt, 3 steht, 4 Ausfahrt");
            Scored("box.in_gasse", "Box", "mInPits", "ja/nein");
            Scored("box.stopps", "Box", "mNumPitstops", "Anzahl");
            Scored("box.strafen", "Box", "mNumPenalties", "Anzahl");
            Scored("box.flagge", "Box", "mFlag", "Code");

            Session("sitzung.art", "Sitzung", "mSession", "Code", "0 Test, 1–4 Training, 5–8 Quali, 9 Warm-up, 10–13 Rennen");
            Session("sitzung.phase", "Sitzung", "mGamePhase", "Code", "5 grün, 6 Full Course Yellow, 9 Pause");
            Session("sitzung.gelb", "Sitzung", "mYellowFlagState", "Code");
            Session("sitzung.echtzeit", "Sitzung", "mInRealtime", "ja/nein", "0 = Monitor/Garage");
            Session("sitzung.zeit", "Sitzung", "mCurrentET", "s");
            Session("sitzung.restzeit", "Sitzung", "mSessionTimeRemaining", "s");
            Session("sitzung.teilnehmer", "Sitzung", "mNumVehicles", "Anzahl");
            Session("sitzung.tageszeit", "Sitzung", "mTimeOfDay", "Code");
            Session("wetter.regen", "Wetter", "mRaining", "0..1");
            Session("wetter.luft", "Wetter", "mAmbientTemp", "°C");
            Session("wetter.strecke", "Wetter", "mTrackTemp", "°C");
            Session("wetter.naesse", "Wetter", "mAvgPathWetness", "0..1");
            Session("wetter.grip", "Wetter", "mTrackGripLevel", "Code");

            Rival("gegner.pos_x", "mPos", "m", 0, "Karte: Weltposition im Telemetrie-Takt");
            Rival("gegner.pos_z", "mPos", "m", 2);
            Rival("gegner.v_laengs", "mLocalVel", "m/s", 2);
            Rival("gegner.drehzahl", "mEngineRPM", "U/min");
            Rival("gegner.gas_roh", "mUnfilteredThrottle", "0..1", -1, "bei fremden Autos evtl. leer");
            RivalScored("gegner.wertung_pos_x", "mPos", "m", 0, "Weltposition im Wertungs-Takt");
            RivalScored("gegner.strecke_m", "mLapDist", "m");
            RivalScored("gegner.platz", "mPlace", "Platz");
            RivalScored("gegner.runden", "mTotalLaps", "Runden");
            RivalScored("gegner.sektor", "mSector", "Code");
            RivalScored("gegner.box", "mInPits", "ja/nein");
            RivalScored("gegner.letzte_runde", "mLastLapTime", "s");
            RivalScored("gegner.abstand_vordermann", "mTimeBehindNext", "s");
        }

        private int TelemetryVehicleCount(byte[] b) { return Math.Min(Math.Max(BitConverter.ToInt32(b, TelemetryCount), 0), Max); }
        private int ScoringVehicleCount(byte[] b) { return Math.Min(Math.Max(BitConverter.ToInt32(b, TelemetrySize + ScoringCount), 0), Max); }

        /// <summary>Eigenes Auto an feste Stellen kopieren und Wertung nach Telemetrie-Einträgen ordnen.</summary>
        protected override void Prepare(byte[] b)
        {
            int nS = ScoringVehicleCount(b), nT = TelemetryVehicleCount(b);
            if (++generation == int.MaxValue) { generation = 1; Array.Clear(stamp, 0, stamp.Length); }
            int ownId = -1, ownScoring = -1;
            for (int i = 0; i < nS; i++)
            {
                int o = TelemetrySize + ScoringVehicles + i * ScoredSize;
                int id = BitConverter.ToInt32(b, o + vsId);
                if ((uint)id < MaxIds && stamp[id] != generation) { stamp[id] = generation; scoringOfId[id] = i; }
                if (ownScoring < 0 && b[o + vsPlayer] != 0) { ownScoring = i; ownId = id; }
            }
            int own = -1;
            for (int i = 0; i < nT; i++)
            {
                int id = BitConverter.ToInt32(b, TelemetryVehicles + i * VehicleSize + vId);
                int target = Aligned + i * ScoredSize;
                if ((uint)id < MaxIds && stamp[id] == generation)
                    Buffer.BlockCopy(b, TelemetrySize + ScoringVehicles + scoringOfId[id] * ScoredSize, b, target, ScoredSize);
                else Array.Clear(b, target, ScoredSize);
                if (own < 0 && ownId >= 0 && id == ownId) own = i;
            }
            OwnIndex = own;
            OwnFound = own >= 0 && ownScoring >= 0;
            if (own >= 0) Buffer.BlockCopy(b, TelemetryVehicles + own * VehicleSize, b, OwnTelemetry, VehicleSize);
            if (ownScoring >= 0) Buffer.BlockCopy(b, TelemetrySize + ScoringVehicles + ownScoring * ScoredSize, b, OwnScoring, ScoredSize);
        }

        protected override void ReadState(byte[] b, out bool paused, out bool menu, out bool replay, out long clock, out double clockTime)
        {
            int info = TelemetrySize + ScoringInfo;
            paused = b[info + iPhase] == PhasePaused;
            // Monitor, Garage, Wiederholung und Zuschauen: kein eigenes Auto in Echtzeit. LMU meldet Wiederholungen nicht gesondert.
            menu = !paused && (b[info + iRealtime] == 0 || !OwnFound);
            replay = false;
            clock = BitConverter.ToUInt32(b, 0);
            clockTime = 0;
        }

        protected override string CheckHeader(byte[] b)
        {
            DataVersion = "SMMP 3.8";
            int nT = BitConverter.ToInt32(b, TelemetryCount), nS = BitConverter.ToInt32(b, TelemetrySize + ScoringCount);
            if (nT < 0 || nT > Max) return "Telemetrie meldet " + nT + " Fahrzeuge";
            if (nS < 0 || nS > Max) return "Wertung meldet " + nS + " Fahrzeuge";
            return null;
        }

        protected override long SessionKey(byte[] b)
        {
            int info = TelemetrySize + ScoringInfo;
            return ((long)BitConverter.ToInt32(b, info + iSession) << 32) ^ (uint)BitConverter.DoubleToInt64Bits(BitConverter.ToDouble(b, info + iLapDist)).GetHashCode();
        }

        protected override void ReadTrack(byte[] b, out string track, out string layout)
        {
            track = Utf8(b, TelemetrySize + ScoringInfo + iTrack, 64);
            layout = "";
        }

        protected override int CollectEntries(byte[] b, int[] index, int[] slot, out int own)
        {
            own = OwnIndex;
            int nT = TelemetryVehicleCount(b), n = 0;
            for (int i = 0; i < nT && n < index.Length; i++)
            {
                if (i == own) continue;
                int id = BitConverter.ToInt32(b, TelemetryVehicles + i * VehicleSize + vId);
                if (id < 0) continue;
                index[n] = i; slot[n] = id % ProbeChannel.MaxSlots; n++;
            }
            return n;
        }

        /// <summary>
        /// Kopiert eine rF2-Abbildung mit Versionsblock: nur wenn Beginn = Ende vor und nach dem Kopieren und im kopierten Bild.
        /// Kopiert wird der Bereich bis zum Fahrzeug-Array plus <c>Anzahl × Eintragsgröße</c>.
        /// </summary>
        internal static bool CopyVersioned(IProbeMap map, byte[] target, int targetOffset, int countOffset, int arrayOffset, int stride, out int torn)
        {
            torn = 0;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                uint begin = map.U32(0);
                if (begin == map.U32(4))
                {
                    int n = Math.Min(Math.Max(map.I32(countOffset), 0), Max);
                    map.Bytes(0, target, targetOffset, arrayOffset + n * stride);
                    if (map.U32(0) == begin && map.U32(4) == begin &&
                        BitConverter.ToUInt32(target, targetOffset) == begin && BitConverter.ToUInt32(target, targetOffset + 4) == begin)
                        return true;
                }
                torn++;
                Thread.Yield();
            }
            return false;
        }

        private sealed class ViewMap : IProbeMap, IDisposable
        {
            private readonly MemoryMappedFile file;
            private readonly MemoryMappedViewAccessor view;

            internal ViewMap(string name, int size)
            {
                file = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
                try { view = file.CreateViewAccessor(0, size, MemoryMappedFileAccess.Read); }
                catch (Exception) { file.Dispose(); throw; }
            }

            public uint U32(int at) { return view.ReadUInt32(at); }
            public int I32(int at) { return view.ReadInt32(at); }
            public void Bytes(int at, byte[] target, int targetOffset, int count) { view.ReadArray(at, target, targetOffset, count); }

            public void Dispose()
            {
                try { view.Dispose(); } catch (Exception) { }
                try { file.Dispose(); } catch (Exception) { }
            }
        }

        /// <summary>Beide LMU-Abbildungen, nur lesend.</summary>
        private sealed class LMUProbeSource : ProbeSource
        {
            private ViewMap telemetry, scoring;

            internal override bool Open()
            {
                Dispose();
                try
                {
                    telemetry = new ViewMap(LMUConstants.MM_TELEMETRY_FILE_NAME, TelemetrySize);
                    scoring = new ViewMap(LMUConstants.MM_SCORING_FILE_NAME, ScoringSize);
                    return true;
                }
                catch (Exception) { Dispose(); return false; }
            }

            internal override bool Copy(byte[] buffer, out int torn)
            {
                torn = 0;
                if (telemetry == null || scoring == null) return false;
                try
                {
                    int t1, t2 = 0;
                    bool ok = CopyVersioned(telemetry, buffer, 0, TelemetryCount, TelemetryVehicles, VehicleSize, out t1)
                        && CopyVersioned(scoring, buffer, TelemetrySize, ScoringCount, ScoringVehicles, ScoredSize, out t2);
                    torn = t1 + t2;
                    return ok;
                }
                catch (Exception) { torn = 0; return false; }
            }

            public override void Dispose()
            {
                if (telemetry != null) telemetry.Dispose();
                if (scoring != null) scoring.Dispose();
                telemetry = scoring = null;
            }
        }
    }
}
