using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.RegularExpressions;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// iRacing über „Local\IRSDKMemMapFileName“: Telemetrie-Variablen je Wagen (CarIdx…) und die Session-Beschreibung
    /// (Fahrernamen, Streckenlänge, Temperaturen, alle Sessions des Events). iRacing meldet weder Weltposition noch Tempo der
    /// Gegner: das Tempo kommt aus der Änderung der Streckenposition, die Fahrtrichtung aus ihrem Vorzeichen.
    /// Variablennamen laut öffentlicher iRacing-SDK-Liste; Kopf-Offsets wie in <see cref="IRacingDynamicsSource"/>.
    /// </summary>
    internal sealed class IRacingFieldSource : IFieldSource
    {
        internal const int SessionInfoUpdateOffset = 12, SessionInfoLenOffset = 16, SessionInfoOffsetOffset = 20, MaxCars = 64;
        private MemoryMappedFile file;
        private MemoryMappedViewAccessor view;
        private Dictionary<string, IRacingDynamicsSource.Slot> vars;
        private int varsCount = -1, varsOffset = -1, lastTick = int.MinValue, infoUpdate = int.MinValue;
        private byte[] buffer;
        private SessionInfo info;
        private readonly float[] lastPct = new float[MaxCars], speed = new float[MaxCars];
        private readonly DateTime[] lastAt = new DateTime[MaxCars];

        public string Name => "iRacing";
        public bool IsOpen => view != null;

        public bool Open()
        {
            Close();
            try
            {
                file = MemoryMappedFile.OpenExisting("Local\\IRSDKMemMapFileName", MemoryMappedFileRights.Read);
                view = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                for (int i = 0; i < MaxCars; i++) { lastPct[i] = float.NaN; speed[i] = float.NaN; }
                return true;
            }
            catch (Exception) { Close(); return false; }
        }

        private void Close()
        {
            try { view?.Dispose(); } catch (Exception) { }
            try { file?.Dispose(); } catch (Exception) { }
            view = null; file = null; vars = null; varsCount = varsOffset = -1; lastTick = infoUpdate = int.MinValue; info = null;
        }

        public void Dispose() => Close();

        /// <summary>Das Nötigste aus iRacings Session-Beschreibung (YAML), per Muster gelesen.</summary>
        internal sealed class SessionInfo
        {
            public string Track, Layout; public float LengthM = float.NaN, AirC = float.NaN, TrackC = float.NaN;
            public int PlayerIdx = -1;
            public readonly Dictionary<int, string> Names = new Dictionary<int, string>();
            public readonly List<Session> Sessions = new List<Session>();
        }
        internal sealed class Session { public int Num; public FieldSession Type; public float Seconds = float.NaN; public int Laps = -1; }

        private static string Field(string yaml, string key)
        {
            var m = Regex.Match(yaml, @"(?m)^\s*" + Regex.Escape(key) + @":\s*(.*?)\s*$");
            return m.Success ? m.Groups[1].Value.Trim().Trim('"') : null;
        }
        private static float Number(string s)
        {
            if (s == null) return float.NaN;
            var m = Regex.Match(s, @"-?\d+(?:\.\d+)?");
            return m.Success && float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : float.NaN;
        }

        internal static FieldSession MapType(string t)
        {
            t = (t ?? "").ToLowerInvariant();
            return t.Contains("race") ? FieldSession.Race : t.Contains("qual") ? FieldSession.Qualify : t.Contains("warm") ? FieldSession.Warmup :
                t.Contains("practice") || t.Contains("testing") ? FieldSession.Practice : FieldSession.Unknown;
        }

        internal static SessionInfo ParseInfo(string yaml)
        {
            var r = new SessionInfo();
            if (string.IsNullOrEmpty(yaml)) return r;
            r.Track = Field(yaml, "TrackDisplayName"); r.Layout = Field(yaml, "TrackConfigName");
            float km = Number(Field(yaml, "TrackLength")); r.LengthM = km > 0 ? km * 1000 : float.NaN;
            r.AirC = Number(Field(yaml, "TrackAirTemp")); r.TrackC = Number(Field(yaml, "TrackSurfaceTemp"));
            float player = Number(Field(yaml, "DriverCarIdx")); r.PlayerIdx = float.IsNaN(player) ? -1 : (int)player;
            foreach (Match m in Regex.Matches(yaml, @"(?m)^\s*-\s*CarIdx:\s*(\d+)\s*$((?:\r?\n(?!\s*-\s).*)*)"))
            {
                int idx = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                string body = m.Groups[2].Value, name = Field(body, "UserName");
                if (Field(body, "CarIsPaceCar") == "1" || Field(body, "IsSpectator") == "1") continue; // Pace Car und Zuschauer sind keine Gegner
                if (!string.IsNullOrWhiteSpace(name) && idx >= 0 && idx < MaxCars) r.Names[idx] = name;
            }
            foreach (Match m in Regex.Matches(yaml, @"(?m)^\s*-\s*SessionNum:\s*(\d+)\s*$((?:\r?\n(?!\s*-\s).*)*)"))
            {
                string body = m.Groups[2].Value;
                var s = new Session { Num = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Type = MapType(Field(body, "SessionType")) };
                string time = Field(body, "SessionTime"), laps = Field(body, "SessionLaps");
                if (time != null && !time.StartsWith("unlimited", StringComparison.OrdinalIgnoreCase)) s.Seconds = Number(time);
                if (laps != null && !laps.StartsWith("unlimited", StringComparison.OrdinalIgnoreCase)) { float l = Number(laps); s.Laps = float.IsNaN(l) ? -1 : (int)l; }
                r.Sessions.Add(s);
            }
            return r;
        }

        /// <summary>Element <paramref name="index"/> einer Feldvariablen; NaN, wenn sie fehlt.</summary>
        internal static double At(Dictionary<string, IRacingDynamicsSource.Slot> map, byte[] b, string name, int index)
        {
            if (map == null || !map.TryGetValue(name, out var s) || index < 0 || index >= s.Count) return double.NaN;
            int size = s.Type == 5 ? 8 : s.Type <= 1 ? 1 : 4, at = s.Offset + index * size;
            if (at < 0 || at + size > b.Length) return double.NaN;
            switch (s.Type)
            {
                case 0: case 1: return b[at];
                case 2: case 3: return BitConverter.ToInt32(b, at);
                case 4: return BitConverter.ToSingle(b, at);
                case 5: return BitConverter.ToDouble(b, at);
                default: return double.NaN;
            }
        }

        public bool Read(FieldFrame f, DateTime utc)
        {
            if (view == null) return false;
            try
            {
                if ((view.ReadInt32(IRacingDynamicsSource.StatusOffset) & 1) == 0) { lastTick = int.MinValue; return false; }
                int count = view.ReadInt32(IRacingDynamicsSource.NumVarsOffset), headers = view.ReadInt32(IRacingDynamicsSource.VarHeaderOffsetOffset);
                int bufs = view.ReadInt32(IRacingDynamicsSource.NumBufOffset), len = view.ReadInt32(IRacingDynamicsSource.BufLenOffset);
                long capacity = view.Capacity;
                if (count <= 0 || count > 4096 || headers <= 0 || headers + (long)count * IRacingDynamicsSource.VarHeaderSize > capacity ||
                    bufs < 1 || bufs > IRacingDynamicsSource.MaxBufs || len <= 0 || len > 1 << 20) return false;
                if (vars == null || count != varsCount || headers != varsOffset)
                {
                    var block = new byte[count * IRacingDynamicsSource.VarHeaderSize];
                    view.ReadArray(headers, block, 0, block.Length);
                    vars = IRacingDynamicsSource.ParseVarHeaders(block, count); varsCount = count; varsOffset = headers;
                }
                int update = view.ReadInt32(SessionInfoUpdateOffset);
                if (info == null || update != infoUpdate)
                {
                    int ilen = view.ReadInt32(SessionInfoLenOffset), ioff = view.ReadInt32(SessionInfoOffsetOffset);
                    if (ilen > 0 && ilen < 4 << 20 && ioff > 0 && ioff + (long)ilen <= capacity)
                    {
                        var raw = new byte[ilen]; view.ReadArray(ioff, raw, 0, ilen);
                        int n = Array.IndexOf(raw, (byte)0); if (n < 0) n = ilen;
                        info = ParseInfo(Encoding.GetEncoding(1252).GetString(raw, 0, n)); infoUpdate = update;
                    }
                }
                int best = -1, tick = 0;
                for (int i = 0; i < bufs; i++)
                {
                    int t = view.ReadInt32(IRacingDynamicsSource.VarBufOffset + i * IRacingDynamicsSource.VarBufSize);
                    if (best < 0 || unchecked(t - tick) > 0) { best = i; tick = t; }
                }
                if (tick == lastTick) return false;
                int at = IRacingDynamicsSource.VarBufOffset + best * IRacingDynamicsSource.VarBufSize, offset = view.ReadInt32(at + 4);
                if (offset <= 0 || offset + (long)len > capacity) return false;
                if (buffer == null || buffer.Length != len) buffer = new byte[len];
                view.ReadArray(offset, buffer, 0, len);
                if (view.ReadInt32(at) != tick) return false;
                lastTick = tick;
                return Fill(vars, buffer, info, f, utc, lastPct, lastAt, speed);
            }
            catch (Exception) { Close(); return false; }
        }

        private static double Val(Dictionary<string, IRacingDynamicsSource.Slot> map, byte[] b, string n) => IRacingDynamicsSource.Value(map, b, n);

        internal static bool Fill(Dictionary<string, IRacingDynamicsSource.Slot> map, byte[] b, SessionInfo info, FieldFrame f, DateTime utc,
            float[] lastPct, DateTime[] lastAt, float[] speed)
        {
            if (info == null || map == null || !map.ContainsKey("CarIdxLapDistPct")) return false;
            f.Utc = utc; f.Sim = "iRacing"; f.LayoutVerified = true;
            f.Track = info.Track; f.Layout = info.Layout; f.TrackLength = info.LengthM;
            int sessionNum = (int)Val(map, b, "SessionNum");
            var current = info.Sessions.Find(s => s.Num == sessionNum);
            f.RawSession = sessionNum; f.SessionIteration = -1; f.Session = current?.Type ?? FieldSession.Unknown;
            int state = (int)Val(map, b, "SessionState"); // 0 ungültig, 1 ins Auto, 2 Warm-up, 3 Paraderunden, 4 Rennen, 5 Zielflagge, 6 Auslaufen
            f.Phase = state == 4 ? FieldPhase.Green : state >= 5 ? FieldPhase.Checkered : state == 3 ? FieldPhase.Formation : state == 1 || state == 2 ? FieldPhase.Garage : FieldPhase.Unknown;
            if (f.Session != FieldSession.Race && state == 1) f.Phase = FieldPhase.Green; // Training/Quali laufen ab „ins Auto“
            double rem = Val(map, b, "SessionTimeRemain");
            f.TimedSession = current != null && !float.IsNaN(current.Seconds) && current.Seconds > 0;
            f.LapsSession = current != null && current.Laps > 0;
            f.TimeDuration = f.TimedSession ? current.Seconds : float.NaN;
            f.TimeRemaining = f.TimedSession && !double.IsNaN(rem) && rem > -10 && rem < 360000 ? (float)Math.Max(0, rem) : float.NaN;
            f.SessionLaps = f.LapsSession ? current.Laps : -1;
            for (int i = 0; i < 3; i++) { f.RaceMinutes[i] = -1; f.RaceLaps[i] = -1; }
            var upcoming = new List<Session>();
            int r = 0;
            foreach (var s in info.Sessions)
            {
                if (s.Type == FieldSession.Race && r < 3) { if (!float.IsNaN(s.Seconds)) f.RaceMinutes[r] = (int)Math.Round(s.Seconds / 60); else if (s.Laps > 0) f.RaceLaps[r] = s.Laps; r++; }
                if (s.Num > sessionNum) upcoming.Add(s);
            }
            f.Upcoming = upcoming.ConvertAll(s => new FieldUpcoming { Session = s.Type, Minutes = float.IsNaN(s.Seconds) ? -1 : (int)Math.Round(s.Seconds / 60), Laps = s.Laps }).ToArray();
            double air = Val(map, b, "AirTemp"), track = Val(map, b, "TrackTempCrew");
            f.AirTempC = !double.IsNaN(air) ? (float)air : info.AirC;
            // iRacing MX-5 28.11.: Begrüßung „Strecke 26 Grad“ aus der Session-Info (Startwert), 19 s später „verändert: jetzt 21,7“ aus der
            // Messung. Die Streckentemperatur kommt nur aus der Messung der Crew.
            f.TrackTempC = !double.IsNaN(track) ? (float)track : float.NaN;
            bool garage = Val(map, b, "IsInGarage") > 0, onTrack = Val(map, b, "IsOnTrack") > 0;
            f.InMenus = !onTrack; f.PlayerInGarage = garage; f.Paused = false; f.InReplay = Val(map, b, "IsReplayPlaying") > 0;
            f.PositionsToGiveBack = -1;
            int player = (int)Val(map, b, "PlayerCarIdx"); if (player < 0) player = info.PlayerIdx;
            var list = new List<FieldCarFrame>();
            for (int i = 0; i < MaxCars; i++)
            {
                double pct = At(map, b, "CarIdxLapDistPct", i);
                int surface = (int)At(map, b, "CarIdxTrackSurface", i);
                if (!info.Names.TryGetValue(i, out string name) || double.IsNaN(pct) || pct < 0 && surface < 0 && i != player) continue;
                double carClass = At(map, b, "CarIdxClass", i);
                var c = new FieldCarFrame { Slot = i, Name = name, ClassId = !double.IsNaN(carClass) && carClass >= 0 ? (int)carClass : -1, IsPlayer = i == player };
                c.LapDistance = pct >= 0 && pct <= 1.001 && !float.IsNaN(f.TrackLength) ? (float)(pct * f.TrackLength) : float.NaN;
                c.CompletedLaps = (int)Math.Max(0, At(map, b, "CarIdxLapCompleted", i));
                int pos = (int)At(map, b, "CarIdxPosition", i); c.Place = pos > 0 ? pos : 0;
                int cls = (int)At(map, b, "CarIdxClassPosition", i); c.PlaceClass = cls > 0 ? cls : c.Place;
                c.InPit = At(map, b, "CarIdxOnPitRoad", i) > 0 || surface == 1 || surface == 2;
                c.OffTrack = surface == 0 && i != player;
                c.BestLap = MappedFieldSource.Time((float)At(map, b, "CarIdxBestLapTime", i));
                double est = At(map, b, "CarIdxEstTime", i), repairs = At(map, b, "CarIdxFastRepairsUsed", i);
                c.EstTime = !double.IsNaN(est) && est >= 0 && est < 3600 ? (float)est : float.NaN;
                c.FastRepairsUsed = !double.IsNaN(repairs) && repairs >= 0 && repairs < 100 ? (int)repairs : -1;
                c.LastLap = MappedFieldSource.Time((float)At(map, b, "CarIdxLastLapTime", i));
                c.X = c.Y = c.Z = float.NaN;
                // Tempo aus der Streckenposition (über die Ziellinie hinweg), sanft geglättet.
                float p = (float)pct;
                if (!float.IsNaN(lastPct[i]) && lastAt[i] != default(DateTime) && p >= 0 && !float.IsNaN(f.TrackLength))
                {
                    double dt = (utc - lastAt[i]).TotalSeconds;
                    float dp = p - lastPct[i]; if (dp < -.5f) dp += 1; else if (dp > .5f) dp -= 1;
                    if (dt > .01 && dt < 2)
                    {
                        float v = Math.Abs(dp) * f.TrackLength / (float)dt;
                        if (v < 150) speed[i] = float.IsNaN(speed[i]) ? v : speed[i] * .6f + v * .4f;
                    }
                }
                if (p >= 0) { lastPct[i] = p; lastAt[i] = utc; }
                c.Speed = surface < 0 ? float.NaN : speed[i];
                c.CurrentLapValid = true; c.LapTimeCurrent = float.NaN;
                c.BestS1 = c.BestS12 = c.SplitBest1 = c.SplitBest2 = c.SplitBest3 = float.NaN;
                c.FinishStatus = 0; c.PenaltyType = c.PenaltyReason = -1; c.SlowDown = c.DriveThrough = c.StopAndGo = c.TimeDeduction = -1;
                list.Add(c);
            }
            f.Cars = list.ToArray();
            f.PlayerIndex = list.FindIndex(c => c.IsPlayer);
            f.NumCars = info.Names.Count;
            return true;
        }
    }
}
