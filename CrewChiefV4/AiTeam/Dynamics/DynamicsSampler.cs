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

        internal virtual bool IsOpen => view != null;

        internal virtual bool Open()
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

        internal virtual void Close()
        {
            try { view?.Dispose(); } catch (Exception) { }
            try { file?.Dispose(); } catch (Exception) { }
            view = null; file = null;
        }

        public void Dispose() => Close();

        /// <summary>Kopiert ein Bild und wertet es aus. Liefert true nur für ein neues, vollständiges Bild.</summary>
        internal virtual unsafe bool Read(DynamicsFrame f, double timeS)
        {
            if (view == null) return false;
            byte* pointer = null;
            bool failed = false;
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
            catch (Exception) { failed = true; }
            finally { if (pointer != null) { try { handle.ReleasePointer(); } catch (Exception) { } } }
            if (failed) { Close(); return false; } // erst nach dem Freigeben schließen
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
            oRps = Off("mTyreRPS"), oPit = Off("mPitMode"), oYaw = Off("mAngularVelocity") + 4;
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
            f.LatAccel = F(b, oAcc); f.LongAccel = F(b, oAcc + 8); f.YawRate = F(b, oYaw);
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
            oDist = At("LapDistance"), oLaps = At("CompletedLaps"), oPit = At("InPitlane"), oYaw = At("Player.LocalAngularVelocity.Y");
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
            f.LatAccel = F(b, oAccX); f.LongAccel = F(b, oAccZ); f.YawRate = (float)BitConverter.ToDouble(b, oYaw);
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
    /// Le Mans Ultimate über Crew Chiefs bestehende Puffer „$LMU.3.8.SMMP_Telemetry$“ und „…_Scoring$“ (gleiches Layout wie
    /// rFactor 2, Crew-Chief-Strukturen). Statt 240 KB je Abtastung liest die Quelle nur die Felder des eigenen Autos.
    /// Die Streckenposition kommt aus dem Scoring (seltener aktualisiert) und wird mit dem Tempo bis zum Telemetriebild
    /// fortgeschrieben.
    /// </summary>
    internal sealed class LMUDynamicsSource : DynamicsSource
    {
        private static int TOff(string f) => Marshal.OffsetOf(typeof(rF2SharedMemory.rFactor2Data.rF2Telemetry), f).ToInt32();
        private static int VOff(string f) => Marshal.OffsetOf(typeof(rF2SharedMemory.rFactor2Data.rF2VehicleTelemetry), f).ToInt32();
        private static int WOff(string f) => Marshal.OffsetOf(typeof(rF2SharedMemory.rFactor2Data.rF2Wheel), f).ToInt32();
        private static int SOff(string f) => Marshal.OffsetOf(typeof(rF2SharedMemory.rFactor2Data.rF2Scoring), f).ToInt32();
        private static int IOff(string f) => Marshal.OffsetOf(typeof(rF2SharedMemory.rFactor2Data.rF2ScoringInfo), f).ToInt32();
        private static int VSOff(string f) => Marshal.OffsetOf(typeof(rF2SharedMemory.rFactor2Data.rF2VehicleScoring), f).ToInt32();
        private static readonly int telemetrySize = Marshal.SizeOf(typeof(rF2SharedMemory.rFactor2Data.rF2Telemetry)),
            scoringSize = Marshal.SizeOf(typeof(rF2SharedMemory.rFactor2Data.rF2Scoring)),
            vSize = Marshal.SizeOf(typeof(rF2SharedMemory.rFactor2Data.rF2VehicleTelemetry)),
            wSize = Marshal.SizeOf(typeof(rF2SharedMemory.rFactor2Data.rF2Wheel)),
            sSize = Marshal.SizeOf(typeof(rF2SharedMemory.rFactor2Data.rF2VehicleScoring));
        private static readonly int tNum = TOff("mNumVehicles"), tVeh = TOff("mVehicles"), vId = VOff("mID"), vEt = VOff("mElapsedTime"),
            vVel = VOff("mLocalVel"), vAcc = VOff("mLocalAccel"), vGear = VOff("mGear"), vThrottle = VOff("mFilteredThrottle"),
            vBrake = VOff("mFilteredBrake"), vWheels = VOff("mWheels"), vRot = VOff("mLocalRot"), vSteer = VOff("mFilteredSteering"), wRot = WOff("mRotation"), wSurface = WOff("mSurfaceType"),
            sInfo = SOff("mScoringInfo"), sVeh = SOff("mVehicles"), iEt = IOff("mCurrentET"), iNum = IOff("mNumVehicles"), iRealtime = IOff("mInRealtime"),
            vsId = VSOff("mID"), vsPlayer = VSOff("mIsPlayer"), vsDist = VSOff("mLapDist"), vsLaps = VSOff("mTotalLaps"), vsPits = VSOff("mInPits");
        private const int Max = rF2SharedMemory.rFactor2Constants.MAX_MAPPED_VEHICLES;

        private MemoryMappedFile tFile, sFile;
        private MemoryMappedViewAccessor tView, sView;
        private double lastEt = double.NaN;

        internal override string Name => "LMU";
        protected override string MapName => rF2SharedMemory.LMUConstants.MM_TELEMETRY_FILE_NAME;
        protected override int Size => telemetrySize;
        protected override bool Parse(byte[] b, DynamicsFrame f) => false; // liest feldweise, siehe Read

        internal override bool IsOpen => tView != null && sView != null;

        internal override bool Open()
        {
            Close();
            try
            {
                tFile = MemoryMappedFile.OpenExisting(rF2SharedMemory.LMUConstants.MM_TELEMETRY_FILE_NAME, MemoryMappedFileRights.Read);
                tView = tFile.CreateViewAccessor(0, telemetrySize, MemoryMappedFileAccess.Read);
                sFile = MemoryMappedFile.OpenExisting(rF2SharedMemory.LMUConstants.MM_SCORING_FILE_NAME, MemoryMappedFileRights.Read);
                sView = sFile.CreateViewAccessor(0, scoringSize, MemoryMappedFileAccess.Read);
                return true;
            }
            catch (Exception) { Close(); return false; }
        }

        internal override void Close()
        {
            try { tView?.Dispose(); } catch (Exception) { }
            try { tFile?.Dispose(); } catch (Exception) { }
            try { sView?.Dispose(); } catch (Exception) { }
            try { sFile?.Dispose(); } catch (Exception) { }
            tView = sView = null; tFile = sFile = null;
        }

        internal static WheelSurface Surface(byte type)
        {
            switch (type)
            {
                case 0: case 1: return WheelSurface.Road;            // trocken, nass
                case 2: case 3: case 4: return WheelSurface.Off;     // Gras, Erde, Kies
                case 5: return WheelSurface.Kerb;                    // Randstein
                default: return WheelSurface.Unknown;                // „special“
            }
        }

        internal override bool Read(DynamicsFrame f, double timeS)
        {
            if (!IsOpen) return false;
            try
            {
                // Scoring: eigenes Auto finden (Slot-ID), Streckenposition, Runden, Box.
                uint sBegin = sView.ReadUInt32(0);
                int sNum = Math.Min(Max, sView.ReadInt32(sInfo + iNum));
                int id = -1; double dist = -1, scoringEt = sView.ReadDouble(sInfo + iEt); int laps = 0; bool pit = false;
                for (int i = 0; i < sNum; i++)
                {
                    int o = sVeh + i * sSize;
                    if (sView.ReadByte(o + vsPlayer) == 0) continue;
                    id = sView.ReadInt32(o + vsId); dist = sView.ReadDouble(o + vsDist); laps = sView.ReadInt16(o + vsLaps); pit = sView.ReadByte(o + vsPits) != 0;
                    break;
                }
                bool realtime = sView.ReadByte(sInfo + iRealtime) != 0;
                if (sView.ReadUInt32(4) != sBegin || id < 0) return false;

                uint tBegin = tView.ReadUInt32(0);
                if (tView.ReadUInt32(4) != tBegin) return false; // Spiel schreibt gerade
                int tNumVeh = Math.Min(Max, tView.ReadInt32(tNum));
                int v = -1;
                for (int i = 0; i < tNumVeh && v < 0; i++) if (tView.ReadInt32(tVeh + i * vSize + vId) == id) v = tVeh + i * vSize;
                if (v < 0) return false;
                double et = tView.ReadDouble(v + vEt);
                if (et == lastEt) return false;
                double vx = tView.ReadDouble(v + vVel), vy = tView.ReadDouble(v + vVel + 8), vz = tView.ReadDouble(v + vVel + 16);
                f.Speed = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);
                f.LatAccel = (float)tView.ReadDouble(v + vAcc); f.LongAccel = (float)tView.ReadDouble(v + vAcc + 16);
                f.Gear = tView.ReadInt32(v + vGear);
                f.Throttle = (float)tView.ReadDouble(v + vThrottle); f.Brake = (float)tView.ReadDouble(v + vBrake);
                f.Steering = (float)tView.ReadDouble(v + vSteer); f.YawRate = (float)tView.ReadDouble(v + vRot + 8);
                for (int w = 0; w < 4; w++)
                {
                    int o = v + vWheels + w * wSize;
                    f.WheelRotation[w] = (float)tView.ReadDouble(o + wRot);
                    f.Surfaces[w] = Surface(tView.ReadByte(o + wSurface));
                }
                if (tView.ReadUInt32(0) != tBegin) return false; // während des Lesens überschrieben
                lastEt = et;
                f.TimeS = timeS;
                f.Driving = realtime;
                f.InPit = pit;
                f.CompletedLaps = laps;
                f.AbsActive = false; f.AbsUnknown = true; // LMU meldet keinen ABS-Eingriff
                double ahead = et > scoringEt && et - scoringEt < 1 ? f.Speed * (et - scoringEt) : 0;
                f.LapDistance = dist >= 0 ? (float)(dist + ahead) : -1;
                return true;
            }
            catch (Exception) { Close(); return false; }
        }
    }

    /// <summary>
    /// Assetto Corsa EVO über die drei Puffer „acevo_pmf_physics“, „…_graphics“ und „…_static“ (Crew-Chief-Strukturen,
    /// Offsets per Marshal aus denselben Typen, mit denen Crew Chief liest). Neue Bilder erkennt die Quelle an der
    /// Paketnummer der Physik. AC EVO meldet nur, WIE VIELE Räder neben der Strecke sind, nicht welche: die Quelle setzt
    /// so viele Räder auf „neben der Strecke“ und die übrigen auf „unbekannt“ – die Ausritt-Erkennung zählt nur Räder.
    /// </summary>
    internal sealed class ACEDynamicsSource : DynamicsSource
    {
        private static int POff(string f) => Marshal.OffsetOf(typeof(CrewChiefV4.ACE.aceData.SPageFilePhysics), f).ToInt32();
        private static int GOff(string f) => Marshal.OffsetOf(typeof(CrewChiefV4.ACE.aceData.SPageFileGraphicEvo), f).ToInt32();
        private static int SOff(string f) => Marshal.OffsetOf(typeof(CrewChiefV4.ACE.aceData.SPageFileStaticEvo), f).ToInt32();
        private static readonly int pSize = Marshal.SizeOf(typeof(CrewChiefV4.ACE.aceData.SPageFilePhysics)),
            gSize = Marshal.SizeOf(typeof(CrewChiefV4.ACE.aceData.SPageFileGraphicEvo)), sSize = Marshal.SizeOf(typeof(CrewChiefV4.ACE.aceData.SPageFileStaticEvo));
        private static readonly int pPacket = POff("packetId"), pGas = POff("gas"), pBrake = POff("brake"), pGear = POff("gear"),
            pSteer = POff("steerAngle"), pSpeed = POff("speedKmh"), pAcc = POff("accG"), pWheel = POff("wheelAngularSpeed"),
            pOut = POff("numberOfTyresOut"), pYaw = POff("localAngularVel"), pAbs = POff("absInAction"), pAi = POff("isAIControlled"),
            gStatus = GOff("status"), gNpos = GOff("npos"), gLocation = GOff("carLocation"), gLaps = GOff("totalLapCount"), gPitLane = GOff("isInPitLane"),
            sLength = SOff("trackLengthM");
        private const int Live = 2, PitLane = 1, PitExit = 3;
        private const float G = 9.81f;

        private MemoryMappedFile pFile, gFile, sFile;
        private MemoryMappedViewAccessor pView, gView, sView;
        private int lastPacket = int.MinValue;

        internal override string Name => "AC EVO";
        protected override string MapName => CrewChiefV4.ACE.aceConstant.SharedMemoryNamePhysics;
        protected override int Size => pSize;
        protected override bool Parse(byte[] b, DynamicsFrame f) => false; // liest feldweise, siehe Read

        internal override bool IsOpen => pView != null && gView != null && sView != null;

        internal override bool Open()
        {
            Close();
            try
            {
                pFile = MemoryMappedFile.OpenExisting(CrewChiefV4.ACE.aceConstant.SharedMemoryNamePhysics, MemoryMappedFileRights.Read);
                pView = pFile.CreateViewAccessor(0, pSize, MemoryMappedFileAccess.Read);
                gFile = MemoryMappedFile.OpenExisting(CrewChiefV4.ACE.aceConstant.SharedMemoryNameGraphic, MemoryMappedFileRights.Read);
                gView = gFile.CreateViewAccessor(0, gSize, MemoryMappedFileAccess.Read);
                sFile = MemoryMappedFile.OpenExisting(CrewChiefV4.ACE.aceConstant.SharedMemoryNameStatic, MemoryMappedFileRights.Read);
                sView = sFile.CreateViewAccessor(0, sSize, MemoryMappedFileAccess.Read);
                return true;
            }
            catch (Exception) { Close(); return false; }
        }

        internal override void Close()
        {
            foreach (var d in new IDisposable[] { pView, pFile, gView, gFile, sView, sFile }) { try { d?.Dispose(); } catch (Exception) { } }
            pView = gView = sView = null; pFile = gFile = sFile = null;
        }

        /// <summary>Räder neben der Strecke aus der gemeldeten Anzahl; welches Rad, verrät AC EVO nicht.</summary>
        internal static void Surfaces(int tyresOut, WheelSurface[] target)
        {
            int n = Math.Max(0, Math.Min(4, tyresOut));
            for (int i = 0; i < 4; i++) target[i] = i < n ? WheelSurface.Off : WheelSurface.Unknown;
        }

        internal override bool Read(DynamicsFrame f, double timeS)
        {
            if (!IsOpen) return false;
            try
            {
                int packet = pView.ReadInt32(pPacket);
                if (packet == lastPacket) return false;
                float speed = pView.ReadSingle(pSpeed) / 3.6f;
                f.Speed = Math.Max(0, speed);
                f.Throttle = Math.Max(0, pView.ReadSingle(pGas)); f.Brake = Math.Max(0, pView.ReadSingle(pBrake));
                f.Steering = pView.ReadSingle(pSteer);
                f.Gear = pView.ReadInt32(pGear) - 1; // AC zählt 0 = Rückwärts, 1 = Leerlauf
                f.LatAccel = pView.ReadSingle(pAcc) * G; f.LongAccel = pView.ReadSingle(pAcc + 8) * G;
                f.YawRate = pView.ReadSingle(pYaw + 4);
                f.AbsActive = pView.ReadInt32(pAbs) != 0; f.AbsUnknown = false;
                for (int w = 0; w < 4; w++) f.WheelRotation[w] = pView.ReadSingle(pWheel + 4 * w);
                Surfaces(pView.ReadInt32(pOut), f.Surfaces);
                bool ai = pView.ReadInt32(pAi) != 0;
                if (pView.ReadInt32(pPacket) != packet) return false; // während des Lesens überschrieben

                int location = gView.ReadInt32(gLocation);
                f.InPit = gView.ReadByte(gPitLane) != 0 || location >= PitLane && location <= PitExit;
                f.CompletedLaps = gView.ReadInt32(gLaps);
                float length = sView.ReadSingle(sLength), npos = gView.ReadSingle(gNpos);
                f.LapDistance = length > 0 && npos >= 0 && npos <= 1 ? npos * length : -1;
                f.Driving = gView.ReadInt32(gStatus) == Live && !ai && speed >= 0;
                lastPacket = packet;
                f.TimeS = timeS;
                return true;
            }
            catch (Exception) { Close(); return false; }
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

        /// <summary>Empfänger für jedes neue Fahrbild (z. B. das Kurven-Coaching mit 100 Hz statt 10–20 Hz).</summary>
        internal static Action<DynamicsFrame> FrameSink;

        /// <summary>Liefert die schnelle Abtastung gerade Bilder an <see cref="FrameSink"/>?</summary>
        internal static bool FeedingFrames => FrameSink != null && DateTime.UtcNow - Analyzer.LastFrameUtc < TimeSpan.FromMilliseconds(500);

        /// <summary>Lagebild der Fahrdynamik für Sprachfragen – nur, wenn die Abtastung gerade Daten liefert.</summary>
        internal static Newtonsoft.Json.Linq.JObject Facts(int completedLaps) =>
            DateTime.UtcNow - Analyzer.LastFrameUtc < TimeSpan.FromSeconds(3) ? Analyzer.Snapshot(completedLaps) : null;

        internal static bool Supports(GameEnum game) => game == GameEnum.AMS2 || game == GameEnum.RACE_ROOM || game == GameEnum.LMU || game == GameEnum.ASSETTO_EVO;

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
            game == GameEnum.AMS2 ? new AMS2DynamicsSource() : game == GameEnum.RACE_ROOM ? new R3EDynamicsSource()
                : game == GameEnum.LMU ? (DynamicsSource)new LMUPreferredDynamicsSource()
                : game == GameEnum.ASSETTO_EVO ? new ACEDynamicsSource() : null;

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
                        if (frame.Driving) { try { FrameSink?.Invoke(frame); } catch (Exception) { } }
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
