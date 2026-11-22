using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using CrewChiefV4.ACE;
using CrewChiefV4.ACE.aceData;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// Assetto Corsa EVO über die offiziellen Puffer „acevo_pmf_graphics“ und „acevo_pmf_static“ (nur lesend, Crew-Chief-Strukturen).
    /// EVO meldet für die Gegner nur Weltposition und Kennung, keine Streckenmeter, Namen oder Plätze. Die Streckenmeter kommen
    /// aus Crew Chiefs gespeicherter Streckenlinie, wenn es eine gibt, sonst aus der eigenen Fahrlinie, die das Lagebild aus Robs
    /// Runden lernt (<see cref="TrackPathMap"/>). Tempo der Gegner = Weg zwischen zwei Bildern durch die Zeit. Rundenunterschiede
    /// liefert EVO nicht; sie bleiben 0.
    /// </summary>
    internal sealed class ACEFieldSource : IFieldSource
    {
        private static readonly int gSize = Marshal.SizeOf(typeof(SPageFileGraphicEvo)), sSize = Marshal.SizeOf(typeof(SPageFileStaticEvo));
        private MemoryMappedFile gFile, sFile;
        private MemoryMappedViewAccessor gView, sView;
        private readonly byte[] gBuf = new byte[gSize], sBuf = new byte[sSize];
        private SPageFileStaticEvo staticPage;
        private bool hasStatic;
        private DateTime staticRead = DateTime.MinValue;
        private int lastPacket = int.MinValue;
        private readonly State state = new State();

        public string Name => "AC EVO";
        public bool IsOpen => gView != null && sView != null;

        public bool Open()
        {
            Dispose();
            try
            {
                gFile = MemoryMappedFile.OpenExisting(aceConstant.SharedMemoryNameGraphic, MemoryMappedFileRights.Read);
                sFile = MemoryMappedFile.OpenExisting(aceConstant.SharedMemoryNameStatic, MemoryMappedFileRights.Read);
                gView = gFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                sView = sFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                return true;
            }
            catch (Exception) { Dispose(); return false; }
        }

        public void Dispose()
        {
            try { gView?.Dispose(); } catch (Exception) { }
            try { sView?.Dispose(); } catch (Exception) { }
            try { gFile?.Dispose(); } catch (Exception) { }
            try { sFile?.Dispose(); } catch (Exception) { }
            gView = sView = null; gFile = sFile = null;
        }

        private static T Copy<T>(MemoryMappedViewAccessor view, byte[] buffer) where T : struct
        {
            int n = (int)Math.Min(buffer.Length, view.Capacity);
            Array.Clear(buffer, 0, buffer.Length);
            view.ReadArray(0, buffer, 0, n);
            var h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try { return (T)Marshal.PtrToStructure(h.AddrOfPinnedObject(), typeof(T)); }
            finally { h.Free(); }
        }

        public bool Read(FieldFrame f, DateTime utc)
        {
            if (!IsOpen) return false;
            try
            {
                if (!hasStatic || utc - staticRead > TimeSpan.FromSeconds(2) || utc < staticRead)
                { staticPage = Copy<SPageFileStaticEvo>(sView, sBuf); hasStatic = true; staticRead = utc; }
                var g = Copy<SPageFileGraphicEvo>(gView, gBuf);
                if (g.packetId == lastPacket && g.status == ACEVO_STATUS.AC_LIVE) return false; // nichts Neues
                lastPacket = g.packetId;
                return Fill(g, staticPage, f, utc, state);
            }
            catch (Exception) { Dispose(); return false; }
        }

        /// <summary>Zustand über die Bilder hinweg: gelernte Fahrlinie und letzte Position je Wagen (für Tempo und Suche).</summary>
        internal sealed class State
        {
            internal readonly TrackPathMap Path = new TrackPathMap();
            internal readonly Dictionary<int, Last> Cars = new Dictionary<int, Last>();
            internal string Key;
            internal Func<float, float, float, float> SplineProjector = DefaultSpline;
        }

        internal struct Last { public float X, Y, Z, Dist, Speed; public DateTime Utc; }

        /// <summary>Crew Chiefs gespeicherte Streckenlinie für EVO (Anteil 0–1 → Meter), NaN ohne Linie oder zu weit weg.</summary>
        private static float DefaultSpline(float x, float y, float z)
        {
            try
            {
                var p = TrackSpline.TrackSplineManager.Project(new aceVec3(x, y, z), -1f);
                return p.valid && p.distanceToSpline < TrackPathMap.MaxOffsetM && p.spLineLength >= 0 && p.spLineLength <= 1 ? p.spLineLength : float.NaN;
            }
            catch (Exception) { return float.NaN; }
        }

        private static int SlotOf(carId id) => (int)((id.carIdA ^ (id.carIdA >> 31) ^ (id.carIdB * 397) ^ (id.carIdB >> 29)) & 0x7fffffff);

        internal static FieldSession MapSession(ACEVO_SESSION_TYPE s) =>
            s == ACEVO_SESSION_TYPE.AC_RACE ? FieldSession.Race : s == ACEVO_SESSION_TYPE.AC_TIME_ATTACK ? FieldSession.Qualify :
            s == ACEVO_SESSION_TYPE.AC_HOT_STINT || s == ACEVO_SESSION_TYPE.AC_CRUISE ? FieldSession.Practice : FieldSession.Unknown;

        internal static bool Fill(SPageFileGraphicEvo g, SPageFileStaticEvo st, FieldFrame f, DateTime utc, State state)
        {
            if (g.carIds == null || g.carCoordinates == null) return false;
            float length = st.trackLengthM > 100 && st.trackLengthM < 100000 ? st.trackLengthM : float.NaN;
            string key = (st.track ?? "") + "|" + (st.trackConfiguration ?? "") + "|" + st.session + "|" + st.sessionId;
            if (key != state.Key) { state.Key = key; state.Cars.Clear(); }
            state.Path.Prepare((st.track ?? "") + "|" + (st.trackConfiguration ?? ""), length);

            f.Utc = utc; f.Sim = "AC EVO"; f.LayoutVerified = true;
            f.Track = st.track; f.Layout = st.trackConfiguration; f.TrackLength = length;
            f.RawSession = (int)st.session; f.SessionIteration = st.sessionId; f.Session = MapSession(st.session);
            f.InMenus = g.status == ACEVO_STATUS.AC_OFF; f.Paused = g.status == ACEVO_STATUS.AC_PAUSE; f.InReplay = g.status == ACEVO_STATUS.AC_REPLAY;
            f.Phase = g.status == ACEVO_STATUS.AC_LIVE ? (g.flag == ACEVO_FLAG_TYPE.AC_CHECKERED_FLAG ? FieldPhase.Checkered : FieldPhase.Green) : FieldPhase.Unknown;
            f.TimedSession = st.isTimedRace; f.LapsSession = !st.isTimedRace;
            float left = g.sessionState.timeLeftMs > 0 ? g.sessionState.timeLeftMs / 1000f : float.NaN;
            f.TimeRemaining = f.TimedSession ? left : float.NaN; f.TimeDuration = float.NaN; f.SessionLaps = -1;
            for (int i = 0; i < 3; i++) { f.RaceMinutes[i] = -1; f.RaceLaps[i] = -1; }
            f.PositionsToGiveBack = -1; f.SimTime = g.packetId;

            int n = (int)Math.Min(g.totalDrivers, (uint)Math.Min(aceConstant.MaxOpponents, Math.Min(g.carIds.Length, g.carCoordinates.Length)));
            int player = -1, found = 0;
            for (int i = 0; i < n; i++)
                if (g.carIds[i].carIdA == g.playerCarIdA && g.carIds[i].carIdB == g.playerCarIdB && (g.playerCarIdA != 0 || g.playerCarIdB != 0)) { player = i; found++; }
            if (found != 1) player = -1;

            var list = new List<FieldCarFrame>(n);
            FieldCarFrame me = null;
            float myDist = float.NaN; int myLaps = Math.Max(0, g.totalLapCount);
            if (player >= 0 && !float.IsNaN(length) && g.npos >= 0 && g.npos <= 1) myDist = g.npos * length;
            for (int i = 0; i < n; i++)
            {
                var id = g.carIds[i];
                if (id.carIdA == 0 && id.carIdB == 0) continue;
                var pos = g.carCoordinates[i];
                int slot = SlotOf(id);
                var c = new FieldCarFrame { Slot = slot, X = pos.x, Y = pos.y, Z = pos.z, IsPlayer = i == player };
                Last prev;
                bool had = state.Cars.TryGetValue(slot, out prev);
                if (c.IsPlayer)
                {
                    me = c;
                    c.LapDistance = myDist; c.CompletedLaps = myLaps; c.Place = (int)g.currentPos; c.PlaceClass = c.Place;
                    c.Speed = g.displaySpeedMs >= 0 && g.displaySpeedMs < 200 ? g.displaySpeedMs : float.NaN;
                    c.InPit = g.isInPitLane; c.CurrentLapValid = g.isValidLap;
                    c.LastLap = g.lastLapTimeMs > 0 ? g.lastLapTimeMs / 1000f : float.NaN; c.BestLap = g.bestLapTimeMs > 0 ? g.bestLapTimeMs / 1000f : float.NaN;
                    c.LapTimeCurrent = g.currentLapTimeMs > 0 ? g.currentLapTimeMs / 1000f : float.NaN;
                    c.Name = Join(g.driverName, g.driverSurname);
                    if (!c.InPit && g.isValidLap && !float.IsNaN(myDist) && c.Speed > 5) state.Path.Learn(pos.x, pos.y, pos.z, myDist);
                }
                else
                {
                    float hint = had ? prev.Dist : float.NaN;
                    float t = state.SplineProjector == null ? float.NaN : state.SplineProjector(pos.x, pos.y, pos.z);
                    c.LapDistance = !float.IsNaN(t) && !float.IsNaN(length) ? t * length : state.Path.Project(pos.x, pos.y, pos.z, hint);
                    c.Speed = had ? Speed(prev, pos, utc) : float.NaN;
                    c.Place = 0; c.PlaceClass = 0; c.CompletedLaps = myLaps;
                }
                c.FinishStatus = -1; c.ClassId = -1;
                state.Cars[slot] = new Last { X = pos.x, Y = pos.y, Z = pos.z, Dist = c.LapDistance, Speed = c.Speed, Utc = utc };
                list.Add(c);
            }
            // Rundenunterschied liefert EVO nicht: Gegner zählen als in Robs Runde, so dass nur der Abstand auf der Strecke zählt.
            if (me != null && !float.IsNaN(length) && !float.IsNaN(myDist))
                foreach (var c in list)
                {
                    if (c.IsPlayer || float.IsNaN(c.LapDistance)) continue;
                    float d = c.LapDistance - myDist;
                    if (d > length / 2) d -= length; else if (d <= -length / 2) d += length;
                    c.CompletedLaps = myLaps + (int)Math.Round((myDist + d - c.LapDistance) / length);
                }
            f.Cars = list.ToArray(); f.NumCars = f.Cars.Length;
            f.PlayerIndex = me == null ? -1 : Array.IndexOf(f.Cars, me);
            f.PlayerInGarage = false;
            return true;
        }

        private static string Join(string first, string last)
        {
            string s = ((first ?? "").Trim() + " " + (last ?? "").Trim()).Trim();
            return s.Length == 0 ? null : s;
        }

        /// <summary>Tempo aus dem Weg zwischen zwei Bildern; der alte Wert bleibt, wenn sich nichts bewegt hat (gleiches Bild).</summary>
        private static float Speed(Last prev, aceVec3 pos, DateTime utc)
        {
            double dt = (utc - prev.Utc).TotalSeconds;
            float dx = pos.x - prev.X, dy = pos.y - prev.Y, dz = pos.z - prev.Z;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist < 1e-3f) return dt > 1 ? 0 : prev.Speed;
            if (dt < .01 || dt > 1) return prev.Speed;
            float v = (float)(dist / dt);
            if (v > 150) return prev.Speed; // Sprung (Box, Neustart)
            return float.IsNaN(prev.Speed) ? v : prev.Speed * .5f + v * .5f;
        }
    }

    /// <summary>
    /// Fahrlinie aus den eigenen Runden: Zu jedem 4-m-Abschnitt der Strecke die gemessene Weltposition. Ein anderer Wagen bekommt
    /// seine Streckenmeter über den nächstgelegenen Abschnitt (zuerst nahe seiner letzten Stelle, das hält Brücken und
    /// Kreuzungen auseinander). Erst ab 85 % Abdeckung, und nur bis 30 m neben der Linie.
    /// </summary>
    internal sealed class TrackPathMap
    {
        internal const float CellM = 4f, MaxOffsetM = 30f, ReadyCoverage = .85f;
        private const int Window = 60;
        private float[] x = new float[0], y = new float[0], z = new float[0];
        private bool[] has = new bool[0];
        private int filled;
        private float length = float.NaN;
        private string key;

        internal float Coverage => has.Length == 0 ? 0 : filled / (float)has.Length;
        internal bool Ready => Coverage >= ReadyCoverage;

        internal void Prepare(string trackKey, float trackLength)
        {
            if (trackKey == key && (float.IsNaN(trackLength) || !float.IsNaN(length) && Math.Abs(trackLength - length) < 1)) return;
            key = trackKey; length = trackLength;
            int n = float.IsNaN(trackLength) ? 0 : (int)Math.Ceiling(trackLength / CellM);
            x = new float[n]; y = new float[n]; z = new float[n]; has = new bool[n]; filled = 0;
        }

        internal void Learn(float px, float py, float pz, float dist)
        {
            if (has.Length == 0 || float.IsNaN(dist) || float.IsNaN(px) || float.IsNaN(pz)) return;
            int i = (int)(dist / CellM);
            if (i < 0 || i >= has.Length) return;
            if (!has[i]) { x[i] = px; y[i] = py; z[i] = pz; has[i] = true; filled++; }
            else { x[i] = x[i] * .7f + px * .3f; y[i] = y[i] * .7f + py * .3f; z[i] = z[i] * .7f + pz * .3f; }
        }

        internal float Project(float px, float py, float pz, float hint)
        {
            if (!Ready || float.IsNaN(px)) return float.NaN;
            int n = has.Length, best = -1;
            float bestSq = float.MaxValue;
            if (!float.IsNaN(hint))
            {
                int c = (int)(hint / CellM);
                for (int k = -Window; k <= Window; k++) Check((c + k + n) % n, px, py, pz, ref best, ref bestSq);
            }
            if (best < 0 || bestSq > MaxOffsetM * MaxOffsetM)
                for (int i = 0; i < n; i++) Check(i, px, py, pz, ref best, ref bestSq);
            if (best < 0 || bestSq > MaxOffsetM * MaxOffsetM) return float.NaN;
            // Auf das Stück zum nächsten gemessenen Abschnitt projizieren.
            int next = (best + 1) % n;
            float t = 0;
            if (has[next])
            {
                float sx = x[next] - x[best], sz = z[next] - z[best], sl = sx * sx + sz * sz;
                if (sl > 1e-4f) t = Math.Max(0, Math.Min(1, ((px - x[best]) * sx + (pz - z[best]) * sz) / sl));
            }
            float d = (best + t) * CellM;
            return d >= length ? d - length : d;
        }

        private void Check(int i, float px, float py, float pz, ref int best, ref float bestSq)
        {
            if (!has[i]) return;
            float dx = px - x[i], dy = py - y[i], dz = pz - z[i];
            float sq = dx * dx + dz * dz + 4 * dy * dy; // Höhe zählt doppelt: Brücke über der Strecke
            if (sq < bestSq) { bestSq = sq; best = i; }
        }
    }
}
