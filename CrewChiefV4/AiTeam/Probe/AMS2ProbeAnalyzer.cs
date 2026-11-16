using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CrewChiefV4.AMS2;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// Automobilista 2: liest das Shared Memory „$pcars2$“ (nur lesen). Offsets kommen direkt vom Marshaller
    /// (ams2APIStruct hat Standard-Ausrichtung, kein Pack=1). Die Sequenznummer verhindert halb geschriebene Bilder.
    /// </summary>
    internal sealed class AMS2ProbeAnalyzer : ProbeAnalyzer
    {
        private const int MaxParticipants = 64;
        private const uint GamePlaying = 2, GamePaused = 3, GameInMenuTimeTicking = 4, GameReplay = 6, GameFrontEndReplay = 7;

        internal override string SimName { get { return "AMS2"; } }
        internal override string FilePrefix { get { return "AMS2"; } }
        internal override string SharedMemoryName { get { return "$pcars2$"; } }
        internal override int SequenceOffset { get { return oSequence; } }
        internal override bool HasSimClock { get { return false; } }

        private readonly int oVersion, oGameState, oSessionState, oViewed, oNumParticipants, oSequence, oTrackLocation, oTrackVariation, oTrackLength;
        private readonly int oParticipants, participantSize, oActive;

        private static readonly string[] CornerIds = { "vl", "vr", "hl", "hr" };

        internal AMS2ProbeAnalyzer() : this(Stopwatch.Frequency) { }

        internal AMS2ProbeAnalyzer(long tickFrequency) : base(tickFrequency)
        {
            Type root = typeof(ams2APIStruct);
            StructSize = Marshal.SizeOf(root);
            oVersion = Off("mVersion"); oGameState = Off("mGameState"); oSessionState = Off("mSessionState");
            oViewed = Off("mViewedParticipantIndex"); oNumParticipants = Off("mNumParticipants"); oSequence = Off("mSequenceNumber");
            oTrackLocation = Off("mTrackLocation"); oTrackVariation = Off("mTrackVariation"); oTrackLength = Off("mTrackLength");
            oParticipants = Off("mParticipantData");
            participantSize = Marshal.SizeOf(typeof(ams2APIParticipantStruct));
            oActive = POff("mIsActive");
            if (oSequence != AMS2SharedMemoryReader.SequenceOffset)
                LayoutError = "Sequenznummer bei Byte " + oSequence + " statt " + AMS2SharedMemoryReader.SequenceOffset;
            DefineChannels();
            Finish();
        }

        private static int Off(string field) { return Marshal.OffsetOf(typeof(ams2APIStruct), field).ToInt32(); }
        private static int POff(string field) { return Marshal.OffsetOf(typeof(ams2APIParticipantStruct), field).ToInt32(); }

        private static Type ElementType(string field)
        {
            Type t = typeof(ams2APIStruct).GetField(field).FieldType;
            return t.IsArray ? t.GetElementType() : t;
        }

        private static Type PElementType(string field)
        {
            Type t = typeof(ams2APIParticipantStruct).GetField(field).FieldType;
            return t.IsArray ? t.GetElementType() : t;
        }

        /// <summary>Einzelwert oder Element <paramref name="element"/> eines festen Arrays im Hauptblock.</summary>
        private void One(string id, string group, string field, string unit, ProbeScope scope, int element = 0, string note = null)
        {
            Type t = ElementType(field);
            int size = Marshal.SizeOf(t == typeof(bool) ? typeof(byte) : t);
            AddChannel(id, group, element == 0 && !typeof(ams2APIStruct).GetField(field).FieldType.IsArray ? field : field + "[" + element + "]",
                unit, scope, ProbeChannel.KindOf(t), Off(field) + element * size, 0, note);
        }

        private void Corners(string id, string group, string field, string unit)
        {
            for (int i = 0; i < 4; i++) One(id + "_" + CornerIds[i], group, field, unit, ProbeScope.Player, i);
        }

        /// <summary>Fahrzeugkanal aus mParticipantData[i].</summary>
        private void Car(string id, string group, string field, string unit, ProbeScope scope, int element = 0, string note = null)
        {
            Type t = PElementType(field);
            AddChannel(id, group, "mParticipantData[i]." + field + (typeof(ams2APIParticipantStruct).GetField(field).FieldType.IsArray ? "[" + element + "]" : ""),
                unit, scope, ProbeChannel.KindOf(t), oParticipants + POff(field) + element * 4, participantSize, note);
        }

        /// <summary>Fahrzeugkanal aus einem Array je Teilnehmer im Hauptblock (z. B. mSpeeds[i]).</summary>
        private void CarArray(string id, string group, string field, string unit, ProbeScope scope, int perCar = 1, int element = 0, string note = null)
        {
            Type t = ElementType(field);
            int size = Marshal.SizeOf(t);
            AddChannel(id, group, field + "[i" + (perCar > 1 ? "*" + perCar + "+" + element : "") + "]", unit, scope, ProbeChannel.KindOf(t),
                Off(field) + element * size, perCar * size, note);
        }

        private void DefineChannels()
        {
            const ProbeScope P = ProbeScope.Player, S = ProbeScope.Session, O = ProbeScope.Opponents, L = ProbeScope.OwnListEntry;
            One("quelle.sequenz", "Quelle", "mSequenceNumber", "Zähler", P, 0, "steigt um 2 je neuem Datenbild");
            One("quelle.zeit_runde", "Quelle", "mmfOnly_mCurrentTime", "s", P);

            One("bewegung.tempo", "Bewegung", "mSpeed", "m/s", P);
            One("bewegung.v_x", "Bewegung", "mWorldVelocity", "m/s", P, 0);
            One("bewegung.v_z", "Bewegung", "mWorldVelocity", "m/s", P, 2);
            One("bewegung.beschl_quer", "Bewegung", "mLocalAcceleration", "m/s²", P, 0);
            One("bewegung.beschl_vert", "Bewegung", "mLocalAcceleration", "m/s²", P, 1);
            One("bewegung.beschl_laengs", "Bewegung", "mLocalAcceleration", "m/s²", P, 2);
            One("bewegung.gier", "Bewegung", "mOrientation", "rad", P, 1);
            One("bewegung.gierrate", "Bewegung", "mAngularVelocity", "rad/s", P, 1);

            One("eingabe.gas", "Fahrer-Eingaben", "mThrottle", "0..1", P);
            One("eingabe.gas_roh", "Fahrer-Eingaben", "mUnfilteredThrottle", "0..1", P);
            One("eingabe.bremse", "Fahrer-Eingaben", "mBrake", "0..1", P);
            One("eingabe.bremse_roh", "Fahrer-Eingaben", "mUnfilteredBrake", "0..1", P);
            One("eingabe.kupplung", "Fahrer-Eingaben", "mClutch", "0..1", P);
            One("eingabe.lenkung", "Fahrer-Eingaben", "mSteering", "-1..1", P);
            One("eingabe.lenkung_roh", "Fahrer-Eingaben", "mUnfilteredSteering", "-1..1", P);
            One("eingabe.handbremse", "Fahrer-Eingaben", "mHandBrake", "0..1", P);
            One("eingabe.gang", "Fahrer-Eingaben", "mGear", "Gang", P);

            One("motor.drehzahl", "Motor", "mRpm", "U/min", P);
            One("motor.drehmoment", "Motor", "mEngineTorque", "Nm", P);
            One("motor.oel_temp", "Motor", "mOilTempCelsius", "°C", P);
            One("motor.oel_druck", "Motor", "mOilPressureKPa", "kPa", P);
            One("motor.wasser", "Motor", "mWaterTempCelsius", "°C", P);
            One("motor.wasser_druck", "Motor", "mWaterPressureKPa", "kPa", P);
            One("motor.sprit_druck", "Motor", "mFuelPressureKPa", "kPa", P);
            One("motor.turbo", "Motor", "mTurboBoostPressure", "bar", P);

            One("hilfe.abs_greift", "Fahrhilfen", "mAntiLockActive", "ja/nein", P, 0, "1 = ABS greift gerade");
            One("hilfe.abs_stufe", "Fahrhilfen", "mAntiLockSetting", "Stufe", P);
            One("hilfe.tc_stufe", "Fahrhilfen", "mTractionControlSetting", "Stufe", P);
            One("hilfe.bremsbalance", "Fahrhilfen", "mBrakeBias", "0..1", P);
            One("hilfe.fahrzeug_flags", "Fahrhilfen", "mCarFlags", "Bits", P, 0, "Licht, Motor, Limiter, ABS, Handbremse");
            One("hilfe.boost_aktiv", "Fahrhilfen", "mBoostActive", "ja/nein", P);

            Corners("reifen.temp", "Reifen", "mTyreTemp", "°C");
            Corners("reifen.temp_lauffl", "Reifen", "mTyreTreadTemp", "K");
            Corners("reifen.temp_karkasse", "Reifen", "mTyreCarcassTemp", "K");
            Corners("reifen.temp_luft", "Reifen", "mTyreInternalAirTemp", "K");
            Corners("reifen.druck", "Reifen", "mAirPressure", "kPa");
            Corners("reifen.verschleiss", "Reifen", "mTyreWear", "0..1");
            Corners("reifen.grip", "Reifen", "mTyreGrip", "0..1");
            Corners("reifen.drehzahl", "Reifen", "mTyreRPS", "rad/s");
            Corners("reifen.schlupf", "Reifen", "mTyreSlipSpeed", "m/s");
            Corners("reifen.untergrund", "Reifen", "mTerrain", "Code");
            Corners("bremse.temp", "Bremsen", "mBrakeTempCelsius", "°C");
            Corners("fahrwerk.federweg", "Fahrwerk", "mSuspensionTravel", "m");
            Corners("fahrwerk.bodenfreiheit", "Fahrwerk", "mRideHeight", "m");

            One("sprit.anteil", "Sprit", "mFuelLevel", "0..1", P, 0, "Anteil am Tank, nicht Liter");
            One("sprit.tank", "Sprit", "mFuelCapacity", "l", P);

            One("runde.letzte", "Runde", "mLastLapTime", "s", P);
            One("runde.abstand_vorne", "Runde", "mSplitTimeAhead", "s", P);
            One("runde.abstand_hinten", "Runde", "mSplitTimeBehind", "s", P);
            One("runde.split", "Runde", "mSplitTime", "s", P);
            One("runde.ungueltig", "Runde", "mLapInvalidated", "ja/nein", P);

            One("sitzung.restzeit", "Sitzung", "mEventTimeRemaining", "s", S);
            One("sitzung.spielzustand", "Sitzung", "mGameState", "Code", S);
            One("sitzung.art", "Sitzung", "mSessionState", "Code", S);
            One("sitzung.rennstatus", "Sitzung", "mRaceState", "Code", S);
            One("sitzung.flagge", "Sitzung", "mHighestFlagColour", "Code", S);
            One("sitzung.gelb", "Sitzung", "mYellowFlagState", "Code", S);
            One("sitzung.boxenmodus", "Sitzung", "mPitMode", "Code", S);
            One("sitzung.teilnehmer", "Sitzung", "mNumParticipants", "Anzahl", S);
            One("sitzung.kollision", "Sitzung", "mLastOpponentCollisionMagnitude", "Stärke", S);

            One("wetter.luft", "Wetter", "mAmbientTemperature", "°C", S);
            One("wetter.strecke", "Wetter", "mTrackTemperature", "°C", S);
            One("wetter.regen", "Wetter", "mRainDensity", "0..1", S);
            One("wetter.wind", "Wetter", "mWindSpeed", "m/s", S);

            One("schaden.aero", "Schaden", "mAeroDamage", "0..1", P);
            One("schaden.motor", "Schaden", "mEngineDamage", "0..1", P);
            One("schaden.crash", "Schaden", "mCrashState", "Code", P);
            Corners("schaden.bremse", "Schaden", "mBrakeDamage", "0..1");
            Corners("schaden.fahrwerk", "Schaden", "mSuspensionDamage", "0..1");

            Car("gegner.pos_x", "Gegner (je Auto)", "mWorldPosition", "m", O, 0, "Karte: Weltposition");
            Car("gegner.pos_z", "Gegner (je Auto)", "mWorldPosition", "m", O, 2, "Karte: Weltposition");
            Car("gegner.strecke_m", "Gegner (je Auto)", "mCurrentLapDistance", "m", O);
            Car("gegner.platz", "Gegner (je Auto)", "mRacePosition", "Platz", O);
            Car("gegner.runden", "Gegner (je Auto)", "mLapsCompleted", "Runden", O);
            Car("gegner.sektor", "Gegner (je Auto)", "mCurrentSector", "Sektor", O);
            CarArray("gegner.tempo", "Gegner (je Auto)", "mSpeeds", "m/s", O);
            CarArray("gegner.gier", "Gegner (je Auto)", "mOrientations", "rad", O, 3, 1);
            CarArray("gegner.boxenmodus", "Gegner (je Auto)", "mPitModes", "Code", O);
            CarArray("gegner.rennstatus", "Gegner (je Auto)", "mRaceStates", "Code", O);
            CarArray("gegner.letzte_runde", "Gegner (je Auto)", "mLastLapTimes", "s", O);
            CarArray("gegner.sektor1_aktuell", "Gegner (je Auto)", "mCurrentSector1Times", "s", O);

            Car("eigen_liste.pos_x", "Eigenes Auto in der Fahrerliste", "mWorldPosition", "m", L, 0, "einzige Weltposition des eigenen Autos");
            Car("eigen_liste.strecke_m", "Eigenes Auto in der Fahrerliste", "mCurrentLapDistance", "m", L);
            CarArray("eigen_liste.tempo", "Eigenes Auto in der Fahrerliste", "mSpeeds", "m/s", L, 1, 0, "Vergleich mit bewegung.tempo");
        }

        protected override void ReadState(byte[] b, out bool paused, out bool menu, out bool replay, out long clock, out double clockTime)
        {
            uint state = BitConverter.ToUInt32(b, oGameState);
            paused = state == GamePaused;
            replay = state == GameReplay || state == GameFrontEndReplay;
            menu = !paused && !replay && state != GamePlaying;
            clock = BitConverter.ToUInt32(b, oSequence) / 2;
            clockTime = 0;
        }

        protected override string CheckHeader(byte[] b)
        {
            uint version = BitConverter.ToUInt32(b, oVersion);
            DataVersion = version.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (version == 0) return "Spiel meldet noch keine Datenversion";
            return null;
        }

        protected override long SessionKey(byte[] b)
        {
            return ((long)BitConverter.ToUInt32(b, oSessionState) << 32) ^ BitConverter.ToInt32(b, oTrackLength);
        }

        protected override void ReadTrack(byte[] b, out string track, out string layout)
        {
            track = Utf8(b, oTrackLocation, 64);
            layout = Utf8(b, oTrackVariation, 64);
        }

        protected override int CollectEntries(byte[] b, int[] index, int[] slot, out int own)
        {
            int viewed = BitConverter.ToInt32(b, oViewed);
            int max = Math.Min(Math.Max(BitConverter.ToInt32(b, oNumParticipants), 0), MaxParticipants);
            own = viewed >= 0 && viewed < max ? viewed : -1;
            int n = 0;
            for (int i = 0; i < max && n < index.Length; i++)
            {
                if (i == own) continue;
                if (b[oParticipants + i * participantSize + oActive] == 0) continue;
                index[n] = i; slot[n] = i; n++;
            }
            return n;
        }
    }
}
