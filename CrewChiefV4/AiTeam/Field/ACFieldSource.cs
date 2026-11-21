using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using CrewChiefV4.assetto;
using assettoData = CrewChiefV4.assetto.assettoData;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// Assetto Corsa über die Kunos-Puffer (Grafik, Statik, Physik) und den Puffer „acpmf_crewchief“ des Crew-Chief-Plugins
    /// (CrewChiefEx, MIT) für die Gegner. Ohne Plugin fehlt das Feld – dann liefert die Quelle nichts und das KI-Team bleibt
    /// beim bisherigen Weg. Crew-Chief-Strukturen, Offsets per Marshal.
    /// </summary>
    internal sealed class ACFieldSource : IFieldSource
    {
        private static readonly Type G = typeof(assettoData.SPageFileGraphic), S = typeof(assettoData.SPageFileStatic),
            P = typeof(assettoData.SPageFilePhysics), C = typeof(assettoData.SPageFileCrewChief), V = typeof(assettoData.acsVehicleInfo);
        private static int Off(Type t, string f) => Marshal.OffsetOf(t, f).ToInt32();
        internal static readonly int gSize = Marshal.SizeOf(G), sSize = Marshal.SizeOf(S), pSize = Marshal.SizeOf(P), cSize = Marshal.SizeOf(C), vSize = Marshal.SizeOf(V);
        internal static readonly int gPacket = Off(G, "packetId"), gStatus = Off(G, "status"), gSession = Off(G, "session"), gTimeLeft = Off(G, "sessionTimeLeft"),
            gLaps = Off(G, "numberOfLaps"), gInPit = Off(G, "isInPit"), gFlag = Off(G, "flag");
        internal static readonly int sLength = Off(S, "trackSPlineLength"), sTimed = Off(S, "isTimedRace"), sNumCars = Off(S, "numCars");
        internal static readonly int pAir = Off(P, "airTemp"), pRoad = Off(P, "roadTemp");
        internal static readonly int cNum = Off(C, "numVehicles"), cFocus = Off(C, "focusVehicle"), cVeh = Off(C, "vehicle");
        internal static readonly int vId = Off(V, "carId"), vName = Off(V, "driverName"), vSpeed = Off(V, "speedMS"), vBest = Off(V, "bestLapMS"),
            vLaps = Off(V, "lapCount"), vInvalid = Off(V, "currentLapInvalid"), vCurrent = Off(V, "currentLapTimeMS"), vLast = Off(V, "lastLapTimeMS"),
            vPos = Off(V, "worldPosition"), vPitLane = Off(V, "isCarInPitline"), vPit = Off(V, "isCarInPit"), vPlace = Off(V, "carLeaderboardPosition"),
            vRealtime = Off(V, "carRealTimeLeaderboardPosition"), vSpline = Off(V, "spLineLength"), vConnected = Off(V, "isConnected");
        private const int Live = 2, Replay = 1, Pause = 3, Checkered = 5;

        private MemoryMappedFile gFile, sFile, pFile, cFile;
        private MemoryMappedViewAccessor gView, sView, pView, cView;
        private byte[] gBuf = new byte[gSize], pBuf = new byte[pSize], cBuf = new byte[cSize], sBuf = new byte[sSize];
        private assettoData.SPageFileStatic staticPage;
        private DateTime staticRead = DateTime.MinValue;
        private int lastPacket = int.MinValue;
        private string sessionKey; private float maxTimeLeft = float.NaN;

        public string Name => "AC";
        public bool IsOpen => gView != null && sView != null && pView != null && cView != null;

        public bool Open()
        {
            Close();
            try
            {
                gFile = MemoryMappedFile.OpenExisting(assettoConstant.SharedMemoryNameGraphic, MemoryMappedFileRights.Read); gView = gFile.CreateViewAccessor(0, gSize, MemoryMappedFileAccess.Read);
                sFile = MemoryMappedFile.OpenExisting(assettoConstant.SharedMemoryNameStatic, MemoryMappedFileRights.Read); sView = sFile.CreateViewAccessor(0, sSize, MemoryMappedFileAccess.Read);
                pFile = MemoryMappedFile.OpenExisting(assettoConstant.SharedMemoryNamePhysics, MemoryMappedFileRights.Read); pView = pFile.CreateViewAccessor(0, pSize, MemoryMappedFileAccess.Read);
                cFile = MemoryMappedFile.OpenExisting(assettoConstant.SharedMemoryNameCrewChief, MemoryMappedFileRights.Read); cView = cFile.CreateViewAccessor(0, cSize, MemoryMappedFileAccess.Read);
                return true;
            }
            catch (Exception) { Close(); return false; }
        }

        private void Close()
        {
            foreach (var v in new IDisposable[] { gView, sView, pView, cView, gFile, sFile, pFile, cFile }) { try { v?.Dispose(); } catch (Exception) { } }
            gView = sView = pView = cView = null; gFile = sFile = pFile = cFile = null;
        }

        public void Dispose() => Close();

        public bool Read(FieldFrame f, DateTime utc)
        {
            if (!IsOpen) return false;
            try
            {
                gView.ReadArray(0, gBuf, 0, gSize);
                int packet = BitConverter.ToInt32(gBuf, gPacket);
                int status = BitConverter.ToInt32(gBuf, gStatus);
                if (status == Live && packet == lastPacket) return false;
                lastPacket = packet;
                if (utc - staticRead > TimeSpan.FromSeconds(2))
                {
                    sView.ReadArray(0, sBuf, 0, sSize);
                    var h = GCHandle.Alloc(sBuf, GCHandleType.Pinned);
                    try { staticPage = (assettoData.SPageFileStatic)Marshal.PtrToStructure(h.AddrOfPinnedObject(), S); } finally { h.Free(); }
                    staticRead = utc;
                }
                pView.ReadArray(0, pBuf, 0, pSize);
                cView.ReadArray(0, cBuf, 0, cSize);
                return Parse(gBuf, sBuf, pBuf, cBuf, staticPage.track, staticPage.trackConfiguration, f, utc, ref sessionKey, ref maxTimeLeft);
            }
            catch (Exception) { Close(); return false; }
        }

        internal static FieldSession MapSession(int s)
        {
            switch (s) { case 0: case 3: case 4: return FieldSession.Practice; case 1: return FieldSession.Qualify; case 2: return FieldSession.Race; default: return FieldSession.Unknown; }
        }

        internal static bool Parse(byte[] g, byte[] s, byte[] p, byte[] c, string track, string layout, FieldFrame f, DateTime utc, ref string sessionKey, ref float maxTimeLeft)
        {
            int status = BitConverter.ToInt32(g, gStatus);
            if (status <= 0) return false; // AC aus
            int num = BitConverter.ToInt32(c, cNum);
            if (num <= 0 || num > assettoConstant.MaxVehicles) return false; // ohne Crew-Chief-Plugin kein Feld
            f.Utc = utc; f.Sim = "AC"; f.LayoutVerified = true;
            f.Track = string.IsNullOrWhiteSpace(track) ? null : track.Trim(); f.Layout = string.IsNullOrWhiteSpace(layout) ? null : layout.Trim();
            float len = BitConverter.ToSingle(s, sLength); f.TrackLength = len > 100 && len < 100000 ? len : float.NaN;
            int session = BitConverter.ToInt32(g, gSession);
            f.RawSession = session; f.SessionIteration = -1; f.Session = MapSession(session);
            f.InReplay = status == Replay; f.Paused = status == Pause; f.InMenus = false;
            bool checkered = BitConverter.ToInt32(g, gFlag) == Checkered;
            f.Phase = checkered ? FieldPhase.Checkered : FieldPhase.Green;
            float leftMs = BitConverter.ToSingle(g, gTimeLeft);
            int laps = BitConverter.ToInt32(g, gLaps);
            bool timedRace = BitConverter.ToInt32(s, sTimed) != 0;
            f.TimedSession = f.Session != FieldSession.Race || timedRace;
            f.LapsSession = f.Session == FieldSession.Race && !timedRace && laps > 0 && laps < 10000;
            float left = leftMs > -1000 && leftMs < 360000000 ? Math.Max(0, leftMs / 1000f) : float.NaN;
            f.TimeRemaining = f.TimedSession ? left : float.NaN;
            string key = track + "|" + layout + "|" + session;
            if (key != sessionKey) { sessionKey = key; maxTimeLeft = float.NaN; }
            if (!float.IsNaN(left) && (float.IsNaN(maxTimeLeft) || left > maxTimeLeft)) maxTimeLeft = left;
            f.TimeDuration = f.TimedSession && maxTimeLeft > 30 ? maxTimeLeft : float.NaN; // AC meldet die Länge nicht, nur die Restzeit
            f.SessionLaps = f.LapsSession ? laps : -1;
            for (int i = 0; i < 3; i++) { f.RaceMinutes[i] = -1; f.RaceLaps[i] = -1; }
            if (f.Session == FieldSession.Race && f.LapsSession) f.RaceLaps[0] = laps;
            float air = BitConverter.ToSingle(p, pAir), road = BitConverter.ToSingle(p, pRoad);
            f.AirTempC = air > -60 && air < 70 && air != 0 ? air : float.NaN; f.TrackTempC = road > -60 && road < 90 && road != 0 ? road : float.NaN;
            f.PositionsToGiveBack = -1;
            int focus = BitConverter.ToInt32(c, cFocus);
            int connected = 0;
            for (int i = 0; i < num; i++) if (BitConverter.ToInt32(c, cVeh + i * vSize + vConnected) != 0) connected++;
            f.NumCars = connected;
            // Wie Crew Chief: Ist irgendwo 0 eingetragen, zählt die Echtzeit-Rangliste ab 0.
            int realtimeBase = 0;
            for (int i = 0; i < num; i++)
                if (BitConverter.ToInt32(c, cVeh + i * vSize + vConnected) != 0 && BitConverter.ToInt32(c, cVeh + i * vSize + vRealtime) == 0) { realtimeBase = 1; break; }
            bool liveOrder = f.Session == FieldSession.Race && f.Phase == FieldPhase.Green;
            if (f.Cars.Length != connected) f.Cars = new FieldCarFrame[connected];
            f.PlayerIndex = -1; f.PlayerInGarage = false;
            int k = 0;
            for (int i = 0; i < num && k < connected; i++)
            {
                int o = cVeh + i * vSize;
                if (BitConverter.ToInt32(c, o + vConnected) == 0) continue;
                var car = f.Cars[k] ?? (f.Cars[k] = new FieldCarFrame());
                car.Slot = BitConverter.ToInt32(c, o + vId); car.Name = MappedFieldSource.Utf8(c, o + vName, 64); car.ClassId = -1;
                float v = BitConverter.ToSingle(c, o + vSpeed); car.Speed = v >= 0 && v < 200 ? v : float.NaN;
                car.BestLap = Ms(BitConverter.ToInt32(c, o + vBest)); car.LastLap = Ms(BitConverter.ToInt32(c, o + vLast));
                car.LapTimeCurrent = Ms(BitConverter.ToInt32(c, o + vCurrent));
                car.CompletedLaps = BitConverter.ToInt32(c, o + vLaps);
                car.CurrentLapValid = BitConverter.ToInt32(c, o + vInvalid) == 0;
                car.X = BitConverter.ToSingle(c, o + vPos); car.Y = BitConverter.ToSingle(c, o + vPos + 4); car.Z = BitConverter.ToSingle(c, o + vPos + 8);
                car.InPit = BitConverter.ToInt32(c, o + vPitLane) != 0 || BitConverter.ToInt32(c, o + vPit) != 0;
                int place = BitConverter.ToInt32(c, o + vPlace), realtime = BitConverter.ToInt32(c, o + vRealtime);
                car.Place = PlaceOf(place, realtime, realtimeBase, liveOrder); car.PlaceClass = car.Place;
                float spline = BitConverter.ToSingle(c, o + vSpline);
                car.LapDistance = spline >= 0 && spline <= 1.001f && !float.IsNaN(f.TrackLength) ? spline * f.TrackLength : float.NaN;
                car.BestS1 = car.BestS12 = car.SplitBest1 = car.SplitBest2 = car.SplitBest3 = float.NaN;
                car.FinishStatus = 0; car.PenaltyType = car.PenaltyReason = -1; car.SlowDown = car.DriveThrough = car.StopAndGo = car.TimeDeduction = -1;
                car.IsPlayer = i == focus;
                if (car.IsPlayer) { f.PlayerIndex = k; f.PlayerInGarage = BitConverter.ToInt32(g, gInPit) != 0; }
                k++;
            }
            return true;
        }

        private static float Ms(int ms) => ms > 0 && ms < 36000000 ? ms / 1000f : float.NaN;

        /// <summary>
        /// AC Monza 21.11.: Die offizielle Rangliste („carLeaderboardPosition“) springt nur an Zeitlinien. Im Rennen lag Rob laut
        /// Streckenpositionen in 45 von 539 Bildern auf dem Platz der Rangliste, in 376 auf dem der Echtzeit-Rangliste. Im
        /// laufenden Rennen gilt deshalb die Echtzeit-Rangliste (wie bei Crew Chief), sonst die offizielle.
        /// </summary>
        internal static int PlaceOf(int leaderboard, int realtime, int realtimeBase, bool liveOrder)
        {
            if (liveOrder && realtime >= 0 && realtime + realtimeBase > 0) return realtime + realtimeBase;
            return leaderboard > 0 ? leaderboard : realtime >= 0 ? realtime + 1 : 0;
        }
    }
}
