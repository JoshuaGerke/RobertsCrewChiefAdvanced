using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using CrewChiefV4.AMS2;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>Gemeinsamer Leser für eine Speicherdatei der Sim (nur lesend) mit optionaler Sequenznummer.</summary>
    internal abstract class MappedFieldSource : IFieldSource
    {
        public abstract string Name { get; }
        protected abstract string MapName { get; }
        protected abstract int Size { get; }
        /// <summary>Byte-Offset einer Sequenznummer (ungerade = die Sim schreibt gerade) oder -1.</summary>
        protected virtual int SequenceOffset => -1;
        protected abstract bool Parse(byte[] b, FieldFrame f, DateTime utc);

        private MemoryMappedFile file;
        private MemoryMappedViewAccessor view;
        private byte[] buffer;

        public bool IsOpen => view != null;

        public virtual bool Open()
        {
            Close();
            try
            {
                file = MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.Read);
                view = file.CreateViewAccessor(0, Size, MemoryMappedFileAccess.Read);
                buffer = new byte[Size];
                return true;
            }
            catch (Exception) { Close(); return false; }
        }

        protected virtual void Close()
        {
            try { view?.Dispose(); } catch (Exception) { }
            try { file?.Dispose(); } catch (Exception) { }
            view = null; file = null;
        }

        public void Dispose() => Close();

        public virtual bool Read(FieldFrame f, DateTime utc)
        {
            if (view == null) return false;
            try
            {
                int seq = SequenceOffset;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    uint before = seq >= 0 ? view.ReadUInt32(seq) : 0;
                    if (seq >= 0 && (before & 1) != 0) { System.Threading.Thread.Yield(); continue; }
                    view.ReadArray(0, buffer, 0, buffer.Length);
                    if (seq < 0 || view.ReadUInt32(seq) == before && BitConverter.ToUInt32(buffer, seq) == before)
                        return Parse(buffer, f, utc);
                }
                return false;
            }
            catch (Exception) { Close(); return false; }
        }

        internal static string Utf8(byte[] b, int o, int max)
        {
            int n = 0;
            while (n < max && o + n < b.Length && b[o + n] != 0) n++;
            return n == 0 ? null : Encoding.UTF8.GetString(b, o, n).Trim();
        }
        internal static float Time(float v) => float.IsNaN(v) || v <= 0 || v > 36000 ? float.NaN : v;
        internal static float Time(double v) => Time((float)v);
    }

    /// <summary>
    /// Automobilista 2 über „$pcars2$“ (Crew-Chief-Struktur, Offsets per Marshal). Alle Teilnehmer mit Position, Tempo,
    /// Runde, Box, Rundenzeiten und Bestsektoren; dazu Strecke, Session-Art und -Länge, Luft- und Streckentemperatur.
    /// </summary>
    internal sealed class AMS2FieldSource : MappedFieldSource
    {
        private static readonly Type Root = typeof(ams2APIStruct), Part = typeof(ams2APIParticipantStruct);
        private static int Off(string f) => Marshal.OffsetOf(Root, f).ToInt32();
        private static int POff(string f) => Marshal.OffsetOf(Part, f).ToInt32();
        internal static readonly int StructSize = Marshal.SizeOf(Root), PartSize = Marshal.SizeOf(Part);
        internal static readonly int oSeq = Off("mSequenceNumber"), oGame = Off("mGameState"), oSession = Off("mSessionState"), oRace = Off("mRaceState"),
            oViewed = Off("mViewedParticipantIndex"), oNum = Off("mNumParticipants"), oParts = Off("mParticipantData"),
            oLapsInEvent = Off("mLapsInEvent"), oTrack = Off("mTrackLocation"), oVariation = Off("mTrackVariation"), oLength = Off("mTrackLength"),
            oRemaining = Off("mEventTimeRemaining"), oDuration = Off("mSessionDuration"), oAir = Off("mAmbientTemperature"), oTrackTemp = Off("mTrackTemperature"),
            oFast1 = Off("mFastestSector1Times"), oFast2 = Off("mFastestSector2Times"), oFast3 = Off("mFastestSector3Times"),
            oFastLap = Off("mFastestLapTimes"), oLastLap = Off("mLastLapTimes"), oInvalid = Off("mLapsInvalidated"), oRaceStates = Off("mRaceStates"),
            oPitModes = Off("mPitModes"), oSpeeds = Off("mSpeeds");
        internal static readonly int pActive = POff("mIsActive"), pName = POff("mName"), pPos = POff("mWorldPosition"), pDist = POff("mCurrentLapDistance"),
            pPlace = POff("mRacePosition"), pLaps = POff("mLapsCompleted");
        private const int MaxParticipants = 64;

        public override string Name => "AMS2";
        protected override string MapName => "$pcars2$";
        protected override int Size => StructSize;
        protected override int SequenceOffset => oSeq;

        private static int I(byte[] b, int o) => BitConverter.ToInt32(b, o);
        private static uint U(byte[] b, int o) => BitConverter.ToUInt32(b, o);
        private static float F(byte[] b, int o) => BitConverter.ToSingle(b, o);

        internal static FieldSession MapSession(uint s)
        {
            switch ((eSessionState)s)
            {
                case eSessionState.SESSION_PRACTICE: case eSessionState.SESSION_TEST: case eSessionState.SESSION_TIME_ATTACK: return FieldSession.Practice;
                case eSessionState.SESSION_QUALIFY: return FieldSession.Qualify;
                case eSessionState.SESSION_FORMATION_LAP: case eSessionState.SESSION_RACE: return FieldSession.Race;
                default: return FieldSession.Unknown;
            }
        }

        protected override bool Parse(byte[] b, FieldFrame f, DateTime utc) => ParseBuffer(b, f, utc);

        internal static bool ParseBuffer(byte[] b, FieldFrame f, DateTime utc)
        {
            if (b == null || b.Length < StructSize) return false;
            uint game = U(b, oGame);
            if (game == (uint)eGameState.GAME_EXITED) return false;
            f.Utc = utc; f.Sim = "AMS2"; f.LayoutVerified = true;
            f.InMenus = game == (uint)eGameState.GAME_FRONT_END || game == (uint)eGameState.GAME_INGAME_INMENU_TIME_TICKING;
            f.Paused = game == (uint)eGameState.GAME_INGAME_PAUSED;
            f.InReplay = game == (uint)eGameState.GAME_INGAME_REPLAY || game == (uint)eGameState.GAME_FRONT_END_REPLAY;
            f.Track = MappedFieldSource.Utf8(b, oTrack, 64); f.Layout = MappedFieldSource.Utf8(b, oVariation, 64);
            float len = F(b, oLength); f.TrackLength = len > 100 && len < 100000 ? len : float.NaN;
            uint session = U(b, oSession);
            f.RawSession = (int)session; f.SessionIteration = -1; f.Session = MapSession(session);
            uint race = U(b, oRace);
            f.Phase = race == (uint)eRaceState.RACESTATE_RACING ? FieldPhase.Green :
                race == (uint)eRaceState.RACESTATE_FINISHED ? FieldPhase.Checkered :
                f.Session == FieldSession.Race ? FieldPhase.Countdown : f.InMenus ? FieldPhase.Garage : FieldPhase.Green;
            float duration = F(b, oDuration), rem = F(b, oRemaining);
            uint lapsInEvent = U(b, oLapsInEvent);
            f.TimedSession = duration > 0; f.LapsSession = !f.TimedSession && lapsInEvent > 0 && lapsInEvent < 10000;
            f.TimeDuration = duration > 0 ? duration : float.NaN;
            f.TimeRemaining = f.TimedSession && rem >= -1 && rem < 360000 ? Math.Max(0, rem) : float.NaN;
            f.SessionLaps = f.LapsSession ? (int)lapsInEvent : -1;
            for (int i = 0; i < 3; i++) { f.RaceMinutes[i] = -1; f.RaceLaps[i] = -1; }
            if (f.Session == FieldSession.Race) { if (f.TimedSession) f.RaceMinutes[0] = (int)Math.Round(duration / 60); else if (f.LapsSession) f.RaceLaps[0] = (int)lapsInEvent; }
            float air = F(b, oAir), track = F(b, oTrackTemp);
            f.AirTempC = air > -60 && air < 70 ? air : float.NaN; f.TrackTempC = track > -60 && track < 90 ? track : float.NaN;
            f.PositionsToGiveBack = -1;
            int num = I(b, oNum), viewed = I(b, oViewed);
            if (num < 0 || num > MaxParticipants) num = 0;
            f.NumCars = num;
            int count = 0;
            for (int i = 0; i < MaxParticipants && i < num; i++) if (b[oParts + i * PartSize + pActive] != 0) count++;
            if (f.Cars.Length != count) f.Cars = new FieldCarFrame[count];
            f.PlayerIndex = -1;
            int k = 0;
            for (int i = 0; i < MaxParticipants && i < num && k < count; i++)
            {
                int o = oParts + i * PartSize;
                if (b[o + pActive] == 0) continue;
                var c = f.Cars[k] ?? (f.Cars[k] = new FieldCarFrame());
                c.Slot = i; c.Name = MappedFieldSource.Utf8(b, o + pName, 64); c.ClassId = -1;
                c.X = F(b, o + pPos); c.Y = F(b, o + pPos + 4); c.Z = F(b, o + pPos + 8);
                float d = F(b, o + pDist); c.LapDistance = d >= 0 && d < 100000 ? d : float.NaN;
                c.Place = (int)U(b, o + pPlace); c.PlaceClass = c.Place; c.CompletedLaps = (int)U(b, o + pLaps);
                float v = F(b, oSpeeds + 4 * i); c.Speed = v >= 0 && v < 200 ? v : float.NaN;
                c.CurrentLapValid = b[oInvalid + i] == 0;
                c.LapTimeCurrent = float.NaN;
                c.BestLap = MappedFieldSource.Time(F(b, oFastLap + 4 * i)); c.LastLap = MappedFieldSource.Time(F(b, oLastLap + 4 * i));
                c.SplitBest1 = MappedFieldSource.Time(F(b, oFast1 + 4 * i)); c.SplitBest2 = MappedFieldSource.Time(F(b, oFast2 + 4 * i));
                c.SplitBest3 = MappedFieldSource.Time(F(b, oFast3 + 4 * i));
                c.BestS1 = c.BestS12 = float.NaN;
                uint rs = U(b, oRaceStates + 4 * i);
                c.FinishStatus = rs == (uint)eRaceState.RACESTATE_FINISHED ? 1 : rs == (uint)eRaceState.RACESTATE_DISQUALIFIED ? 3 :
                    rs == (uint)eRaceState.RACESTATE_RETIRED || rs == (uint)eRaceState.RACESTATE_DNF ? 2 : 0;
                uint pit = U(b, oPitModes + 4 * i);
                c.InPit = pit != (uint)ePitMode.PIT_MODE_NONE && pit < (uint)ePitMode.PIT_MODE_MAX;
                c.PenaltyType = c.PenaltyReason = -1; c.SlowDown = c.DriveThrough = c.StopAndGo = c.TimeDeduction = -1;
                c.IsPlayer = i == viewed;
                if (c.IsPlayer) { f.PlayerIndex = k; f.PlayerInGarage = pit == (uint)ePitMode.PIT_MODE_IN_GARAGE; }
                k++;
            }
            return true;
        }
    }
}
