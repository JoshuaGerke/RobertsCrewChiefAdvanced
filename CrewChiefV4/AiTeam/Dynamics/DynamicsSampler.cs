using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using CrewChiefV4.AiTeam.Probe;
using CrewChiefV4.AMS2;
using CrewChiefV4.RaceRoom.RaceRoomData;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Liest das Shared Memory einer Sim nur lesend und füllt ein <see cref="DynamicsFrame"/>. Jede Quelle kennt ihre
    /// Byte-Offsets aus Crew Chiefs eigenen Datenstrukturen (MIT) – keine fremden Header, keine Plugins.
    /// </summary>
    internal abstract class DynamicsSource : IDisposable
    {
        internal abstract string Name { get; }
        protected abstract string MapName { get; }
        protected abstract int Size { get; }
        /// <summary>Byte-Offset einer Sequenznummer (ungerade = Spiel schreibt gerade) oder -1.</summary>
        protected virtual int SequenceOffset => -1;
        /// <summary>Wertet ein vollständig kopiertes Bild aus; false, wenn es kein neues Bild ist.</summary>
        protected abstract bool Parse(byte[] b, DynamicsFrame f);

        private MemoryMappedFile file;
        private MemoryMappedViewAccessor view;
        private byte[] buffer;

        internal bool IsOpen => view != null;

        internal bool Open()
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

        internal void Close()
        {
            try { view?.Dispose(); } catch (Exception) { }
            try { file?.Dispose(); } catch (Exception) { }
            view = null; file = null;
        }

        public void Dispose() => Close();

        /// <summary>Kopiert ein Bild und wertet es aus. Liefert true nur für ein neues, vollständiges Bild.</summary>
        internal unsafe bool Read(DynamicsFrame f, double timeS)
        {
            if (view == null) return false;
            byte* pointer = null;
            var handle = view.SafeMemoryMappedViewHandle;
            try
            {
                handle.AcquirePointer(ref pointer);
                byte* start = pointer + view.PointerOffset;
                int seq = SequenceOffset;
                bool copied = false;
                for (int attempt = 0; attempt < 6 && !copied; attempt++)
                {
                    uint before = seq >= 0 ? *(uint*)(start + seq) : 0;
                    if (seq >= 0 && (before & 1) != 0) { Thread.Yield(); continue; }
                    Marshal.Copy(new IntPtr(start), buffer, 0, buffer.Length);
                    copied = seq < 0 || *(uint*)(start + seq) == before && BitConverter.ToUInt32(buffer, seq) == before;
                }
                if (!copied) return false;
            }
            catch (Exception) { Close(); return false; }
            finally { if (pointer != null) handle.ReleasePointer(); }
            f.TimeS = timeS;
            return Parse(buffer, f);
        }

        protected static float F(byte[] b, int o) => BitConverter.ToSingle(b, o);
        protected static int I(byte[] b, int o) => BitConverter.ToInt32(b, o);
        protected static uint U(byte[] b, int o) => BitConverter.ToUInt32(b, o);
    }

    /// <summary>Automobilista 2 über „$pcars2$“ (laut Rob-Messung 16.11.: 119 Bilder pro Sekunde).</summary>
    internal sealed class AMS2DynamicsSource : DynamicsSource
    {
        private const uint GamePlaying = 2;
        private static readonly int size = Marshal.SizeOf(typeof(ams2APIStruct));
        private static int Off(string f) => Marshal.OffsetOf(typeof(ams2APIStruct), f).ToInt32();
        private static int POff(string f) => Marshal.OffsetOf(typeof(ams2APIParticipantStruct), f).ToInt32();
        private static readonly int oSeq = Off("mSequenceNumber"), oGame = Off("mGameState"), oViewed = Off("mViewedParticipantIndex"),
            oParts = Off("mParticipantData"), pSize = Marshal.SizeOf(typeof(ams2APIParticipantStruct)), pDist = POff("mCurrentLapDistance"),
            pLaps = POff("mLapsCompleted"), oSpeed = Off("mSpeed"), oThrottle = Off("mThrottle"), oBrake = Off("mBrake"), oSteer = Off("mSteering"),
            oGear = Off("mGear"), oAbs = Off("mAntiLockActive"), oAcc = Off("mLocalAcceleration"), oTerrain = Off("mTerrain"),
            oRps = Off("mTyreRPS"), oPit = Off("mPitMode");
        private uint lastSeq = uint.MaxValue;

        internal override string Name => "AMS2";
        protected override string MapName => "$pcars2$";
        protected override int Size => size;
        protected override int SequenceOffset => oSeq;

        internal static WheelSurface Surface(uint terrain)
        {
            switch ((eTerrainMaterials)terrain)
            {
                case eTerrainMaterials.TERRAIN_RUMBLE_STRIPS: case eTerrainMaterials.TERRAIN_EXIT_RUMBLE_STRIPS:
                case eTerrainMaterials.TERRAIN_B1RUMBLES: case eTerrainMaterials.TERRAIN_B2RUMBLES: case eTerrainMaterials.TERRAIN_PAINT_CONCRETE:
                    return WheelSurface.Kerb;
                case eTerrainMaterials.TERRAIN_GRASSY_BERMS: case eTerrainMaterials.TERRAIN_GRASS: case eTerrainMaterials.TERRAIN_LONG_GRASS:
                case eTerrainMaterials.TERRAIN_SLOPE_GRASS: case eTerrainMaterials.TERRAIN_GRAVEL: case eTerrainMaterials.TERRAIN_BUMPY_GRAVEL:
                case eTerrainMaterials.TERRAIN_SAND: case eTerrainMaterials.TERRAIN_BUMPY_SAND: case eTerrainMaterials.TERRAIN_ROUGH_SAND_MEDIUM:
                case eTerrainMaterials.TERRAIN_ROUGH_SAND_HEAVY: case eTerrainMaterials.TERRAIN_DIRT: case eTerrainMaterials.TERRAIN_BUMPY_DIRT:
                case eTerrainMaterials.TERRAIN_DIRT_BANK: case eTerrainMaterials.TERRAIN_DRY_VERGE: case eTerrainMaterials.TERRAIN_GRASSCRETE:
                case eTerrainMaterials.TERRAIN_ASTROTURF: case eTerrainMaterials.TERRAIN_ILLEGAL_STRIP: case eTerrainMaterials.TERRAIN_PAINT_CONCRETE_ILLEGAL:
                    return WheelSurface.Off;
                case eTerrainMaterials.TERRAIN_TYREWALLS: case eTerrainMaterials.TERRAIN_CEMENTWALLS: case eTerrainMaterials.TERRAIN_GUARDRAILS:
                case eTerrainMaterials.TERRAIN_SNOWWALLS:
                    return WheelSurface.Wall;
                case eTerrainMaterials.TERRAIN_ROAD: case eTerrainMaterials.TERRAIN_LOW_GRIP_ROAD: case eTerrainMaterials.TERRAIN_BUMPY_ROAD1:
                case eTerrainMaterials.TERRAIN_BUMPY_ROAD2: case eTerrainMaterials.TERRAIN_BUMPY_ROAD3: case eTerrainMaterials.TERRAIN_MARBLES:
                case eTerrainMaterials.TERRAIN_DRAINS: case eTerrainMaterials.TERRAIN_PAVEMENT: case eTerrainMaterials.TERRAIN_COBBLES:
                case eTerrainMaterials.TERRAIN_DAMAGED_ROAD1: case eTerrainMaterials.TERRAIN_RALLY_TARMAC:
                    return WheelSurface.Road;
                default:
                    return WheelSurface.Unknown; // u. a. RUNOFF_ROAD: liegt laut Crew Chief teils mitten auf der Strecke
            }
        }

        protected override bool Parse(byte[] b, DynamicsFrame f)
        {
            uint seq = U(b, oSeq);
            if (seq == lastSeq) return false;
            lastSeq = seq;
            int viewed = I(b, oViewed);
            f.Driving = U(b, oGame) == GamePlaying && viewed >= 0 && viewed < 64;
            int p = oParts + Math.Max(0, Math.Min(63, viewed)) * pSize;
            f.LapDistance = F(b, p + pDist);
            f.CompletedLaps = (int)U(b, p + pLaps);
            f.Speed = F(b, oSpeed); f.Throttle = F(b, oThrottle); f.Brake = F(b, oBrake); f.Steering = F(b, oSteer);
            f.Gear = I(b, oGear); f.AbsActive = b[oAbs] != 0; f.InPit = U(b, oPit) != 0;
            f.LatAccel = F(b, oAcc); f.LongAccel = F(b, oAcc + 8);
            for (int i = 0; i < 4; i++)
            {
                f.WheelRotation[i] = F(b, oRps + 4 * i);
                f.Surfaces[i] = Surface(U(b, oTerrain + 4 * i));
            }
            return true;
        }
    }

    /// <summary>RaceRoom über „$R3E“. Neue Bilder erkennt die Quelle am Physik-Takt der Sim.</summary>
    internal sealed class R3EDynamicsSource : DynamicsSource
    {
        private static readonly Type Root = typeof(RaceRoomShared);
        private static int At(string path) => PackedLayout.Offset(Root, path);
        private static readonly int size = PackedLayout.Size(Root);
        private static readonly int oTicks = At("Player.GameSimulationTicks"), oPaused = At("GamePaused"), oMenus = At("GameInMenus"),
            oReplay = At("GameInReplay"), oSpeed = At("CarSpeed"), oThrottle = At("Throttle"), oBrake = At("Brake"), oSteer = At("SteerInputRaw"),
            oGear = At("Gear"), oAbs = At("AidSettings.Abs"), oAccX = At("LocalAcceleration.X"), oAccZ = At("LocalAcceleration.Z"),
            oDist = At("LapDistance"), oLaps = At("CompletedLaps"), oPit = At("InPitlane");
        private static readonly int[] oRps = { At("TireRps.FrontLeft"), At("TireRps.FrontRight"), At("TireRps.RearLeft"), At("TireRps.RearRight") };
        private static readonly int[] oMtrl = { At("TireOnMtrl.FrontLeft"), At("TireOnMtrl.FrontRight"), At("TireOnMtrl.RearLeft"), At("TireOnMtrl.RearRight") };
        private int lastTicks = int.MinValue;

        internal override string Name => "RaceRoom";
        protected override string MapName => RaceRoom.RaceRoomConstant.SharedMemoryName;
        protected override int Size => size;

        internal static WheelSurface Surface(int material)
        {
            switch (material)
            {
                case 1: case 6: return WheelSurface.Road;       // Asphalt, Beton
                case 5: return WheelSurface.Kerb;               // Randstein
                case 2: case 3: case 4: return WheelSurface.Off; // Gras, Erde, Kies
                default: return WheelSurface.Unknown;
            }
        }

        protected override bool Parse(byte[] b, DynamicsFrame f)
        {
            int ticks = I(b, oTicks);
            if (ticks == lastTicks) return false;
            lastTicks = ticks;
            float speed = F(b, oSpeed);
            f.Driving = I(b, oPaused) == 0 && I(b, oMenus) == 0 && I(b, oReplay) == 0 && speed >= 0;
            f.Speed = Math.Max(0, speed); f.Throttle = Math.Max(0, F(b, oThrottle)); f.Brake = Math.Max(0, F(b, oBrake)); f.Steering = F(b, oSteer);
            f.Gear = I(b, oGear); f.AbsActive = I(b, oAbs) == 5; f.InPit = I(b, oPit) == 1;
            f.LatAccel = F(b, oAccX); f.LongAccel = F(b, oAccZ);
            f.LapDistance = F(b, oDist); f.CompletedLaps = I(b, oLaps);
            for (int i = 0; i < 4; i++)
            {
                f.WheelRotation[i] = F(b, oRps[i]);
                f.Surfaces[i] = Surface(I(b, oMtrl[i]));
            }
            return true;
        }
    }

    /// <summary>
    /// Schnelle Abtastung (Rob 17.11.2026: 100 Hz) in einem eigenen Hintergrundfaden, unabhängig von Crew Chiefs
    /// Hauptschleife. Liest alle 4 ms und wertet jedes neue Bild der Sim aus – bei AMS2 also gut hundert Bilder pro Sekunde.
    /// Läuft nur, solange der Dienst ihn anfordert (<see cref="Ensure"/>), und beendet sich 15 s nach der letzten Anforderung.
    /// </summary>
    internal static class DynamicsSampler
    {
        internal static readonly DrivingDynamicsAnalyzer Analyzer = new DrivingDynamicsAnalyzer();
        private static readonly object gate = new object();
        private static Thread thread;
        private static GameEnum wanted = GameEnum.NONE; // nur unter gate lesen/schreiben (ulong-Aufzählung, kein volatile)
        private static long lastEnsure;
        private static volatile string status = "aus";

        internal static string Status => status;

        /// <summary>Lagebild der Fahrdynamik für Sprachfragen – nur, wenn die Abtastung gerade Daten liefert.</summary>
        internal static Newtonsoft.Json.Linq.JObject Facts(int completedLaps) =>
            DateTime.UtcNow - Analyzer.LastFrameUtc < TimeSpan.FromSeconds(3) ? Analyzer.Snapshot(completedLaps) : null;

        internal static bool Supports(GameEnum game) => game == GameEnum.AMS2 || game == GameEnum.RACE_ROOM;

        /// <summary>Vom Dienst je Takt aufgerufen: startet bzw. hält den Faden für das laufende Spiel.</summary>
        internal static void Ensure(GameEnum game)
        {
            if (!Supports(game)) return;
            Interlocked.Exchange(ref lastEnsure, Stopwatch.GetTimestamp());
            lock (gate)
            {
                wanted = game;
                if (thread != null && thread.IsAlive) return;
                thread = new Thread(Run) { IsBackground = true, Name = "RRCA Fahrdynamik 100 Hz", Priority = ThreadPriority.AboveNormal };
                thread.Start();
            }
        }

        private static DynamicsSource Create(GameEnum game) =>
            game == GameEnum.AMS2 ? new AMS2DynamicsSource() : game == GameEnum.RACE_ROOM ? (DynamicsSource)new R3EDynamicsSource() : null;

        private static void Run()
        {
            bool timer = false;
            DynamicsSource source = null;
            GameEnum sourceGame = GameEnum.NONE;
            var frame = new DynamicsFrame();
            var clock = Stopwatch.StartNew();
            try
            {
                try { timer = NativeMethods.timeBeginPeriod(1) == 0; } catch (Exception) { }
                while (true)
                {
                    long idle = (Stopwatch.GetTimestamp() - Interlocked.Read(ref lastEnsure)) / Stopwatch.Frequency;
                    if (idle > 15) break;
                    GameEnum want;
                    lock (gate) want = wanted;
                    if (source == null || sourceGame != want)
                    {
                        source?.Dispose();
                        sourceGame = want;
                        source = Create(sourceGame);
                        Analyzer.Reset();
                        Analyzer.Source = source?.Name;
                        if (source == null) { Thread.Sleep(1000); continue; }
                    }
                    if (!source.IsOpen && !source.Open()) { status = "wartet auf " + source.Name; Thread.Sleep(2000); continue; }
                    if (source.Read(frame, clock.Elapsed.TotalSeconds))
                    {
                        Analyzer.Observe(frame);
                        status = source.Name + " · " + Math.Round(Analyzer.Hz) + " Hz";
                    }
                    Thread.Sleep(4);
                }
            }
            catch (Exception e) { status = "Fehler · " + e.GetType().Name; }
            finally
            {
                source?.Dispose();
                if (timer) { try { NativeMethods.timeEndPeriod(1); } catch (Exception) { } }
                if (!status.StartsWith("Fehler", StringComparison.Ordinal)) status = "aus";
                lock (gate) thread = null;
            }
        }

        private static class NativeMethods
        {
            [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
            [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);
        }
    }
}
