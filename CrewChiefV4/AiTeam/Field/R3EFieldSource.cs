using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using CrewChiefV4.AiTeam.Probe;
using CrewChiefV4.RaceRoom.RaceRoomData;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>Eine Quelle für Rohbilder des ganzen Feldes direkt aus dem Speicher einer Sim (nur lesend).</summary>
    internal interface IFieldSource : IDisposable
    {
        string Name { get; }
        bool IsOpen { get; }
        bool Open();
        /// <summary>Liest ein Bild; false, wenn nichts Neues vorliegt oder die Sim nicht erreichbar ist.</summary>
        bool Read(FieldFrame frame, DateTime utc);
    }

    /// <summary>
    /// RaceRoom über „$R3E“, eigenständig neben Crew Chief. Die Lage der Fahrerliste kommt aus dem Kopf der Sim
    /// (AllDriversOffset, DriverDataSize); die Felder darin aus Crew Chiefs RaceRoom-Strukturen (MIT). Stimmt der Kopf nicht
    /// mit der eigenen Rechnung überein, bleibt das Bild als „nicht geprüft“ markiert.
    /// Liest auch im Boxenmenü: Strecke, Feld, Session-Art und -Länge stehen dort schon fest.
    /// </summary>
    internal sealed class R3EFieldSource : IFieldSource
    {
        private static readonly Type Root = typeof(RaceRoomShared);
        private static readonly Type Driver = typeof(DriverData);
        private static int At(string p) => PackedLayout.Offset(Root, p);
        private static int D(string p) => PackedLayout.Offset(Driver, p);
        internal static readonly int Size = PackedLayout.Size(Root);
        internal static readonly int DriverSize = PackedLayout.Size(Driver);
        internal static readonly int NumCarsOffset = At("NumCars");
        private static readonly int oAllDrivers = At("AllDriversOffset"), oDriverSize = At("DriverDataSize"), oMajor = At("VersionMajor"),
            oPaused = At("GamePaused"), oMenus = At("GameInMenus"), oReplay = At("GameInReplay"), oGarage = At("GamePlayerInGarage"),
            oSimTime = At("Player.GameSimulationTime"), oTicks = At("Player.GameSimulationTicks"),
            oTrack = At("TrackName"), oLayout = At("LayoutName"), oLength = At("LayoutLength"),
            oRaceLaps = At("RaceSessionLaps.Race1"), oRaceMin = At("RaceSessionMinutes.Race1"),
            oSessionType = At("SessionType"), oIteration = At("SessionIteration"), oFormat = At("SessionLengthFormat"),
            oPhase = At("SessionPhase"), oLaps = At("NumberOfLaps"), oDuration = At("SessionTimeDuration"), oRemaining = At("SessionTimeRemaining"),
            oGiveBack = At("Flags.YellowPositionsGained"), oVehicleSlot = At("VehicleInfo.SlotId"),
            oVehicleModel = At("VehicleInfo.ModelId");
        private static readonly int dName = D("DriverInfo.Name"), dSlot = D("DriverInfo.SlotId"), dClass = D("DriverInfo.ClassId"),
            dFinish = D("FinishStatus"), dPlace = D("Place"), dPlaceClass = D("PlaceClass"), dDist = D("LapDistance"),
            dX = D("Position.X"), dY = D("Position.Y"), dZ = D("Position.Z"), dLaps = D("CompletedLaps"), dValid = D("CurrentLapValid"),
            dCurrent = D("LapTimeCurrentSelf"), dPrevLap = D("SectorTimePreviousSelf.Sector3"),
            dBest1 = D("SectorTimeBestSelf.Sector1"), dBest2 = D("SectorTimeBestSelf.Sector2"), dBest3 = D("SectorTimeBestSelf.Sector3"),
            dInPit = D("InPitlane"), dSpeed = D("CarSpeed"), dPenType = D("PenaltyType"), dPenReason = D("PenaltyReason"),
            dDriveThrough = D("Penalties.DriveThrough"), dStopGo = D("Penalties.StopAndGo"), dTimeDed = D("Penalties.TimeDeduction"),
            dSlowDown = D("Penalties.SlowDown");

        private MemoryMappedFile file;
        private MemoryMappedViewAccessor view;
        private byte[] buffer;
        private long capacity;

        public string Name => "RaceRoom";
        public bool IsOpen => view != null;

        public bool Open()
        {
            Close();
            try
            {
                file = MemoryMappedFile.OpenExisting(RaceRoom.RaceRoomConstant.SharedMemoryName, MemoryMappedFileRights.Read);
                view = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                capacity = view.Capacity;
                buffer = new byte[(int)Math.Min(int.MaxValue, Math.Max(Size, capacity))];
                return true;
            }
            catch (Exception) { Close(); return false; }
        }

        private void Close()
        {
            try { view?.Dispose(); } catch (Exception) { }
            try { file?.Dispose(); } catch (Exception) { }
            view = null; file = null;
        }

        public void Dispose() => Close();

        /// <summary>Bilder, die beim Kopieren von der Sim überschrieben wurden und verworfen sind.</summary>
        internal int TornFrames { get; private set; }

        public bool Read(FieldFrame f, DateTime utc)
        {
            if (view == null) return false;
            bool consistent = false;
            try
            {
                // Zerrissene Bilder vermeiden: Der Simulationstakt muss vor, während und nach dem Kopieren gleich sein.
                for (int attempt = 0; attempt < 3 && !consistent; attempt++)
                {
                    int before = view.ReadInt32(oTicks);
                    view.ReadArray(0, buffer, 0, buffer.Length);
                    int after = view.ReadInt32(oTicks);
                    consistent = Consistent(before, I(buffer, oTicks), after);
                    if (!consistent) TornFrames++;
                }
            }
            catch (Exception) { Close(); return false; }
            return consistent && Parse(buffer, buffer.Length, f, utc);
        }

        internal static bool Consistent(int before, int copied, int after) => before == after && copied == after;

        private static int I(byte[] b, int o) => BitConverter.ToInt32(b, o);
        private static float F(byte[] b, int o) => BitConverter.ToSingle(b, o);
        private static string Text(byte[] b, int o, int max)
        {
            int n = 0;
            while (n < max && o + n < b.Length && b[o + n] != 0) n++;
            return n == 0 ? null : Encoding.UTF8.GetString(b, o, n).Trim();
        }
        private static float Time(float v) => float.IsNaN(v) || v <= 0 || v > 36000 ? float.NaN : v;

        internal static FieldSession MapSession(int raw)
        {
            switch (raw) { case 0: return FieldSession.Practice; case 1: return FieldSession.Qualify; case 2: return FieldSession.Race; case 3: return FieldSession.Warmup; default: return FieldSession.Unknown; }
        }

        internal static FieldPhase MapPhase(int raw)
        {
            switch (raw)
            {
                case 1: return FieldPhase.Garage; case 2: return FieldPhase.Gridwalk; case 3: return FieldPhase.Formation;
                case 4: return FieldPhase.Countdown; case 5: return FieldPhase.Green; case 6: return FieldPhase.Checkered;
                default: return FieldPhase.Unknown;
            }
        }

        /// <summary>Wertet ein kopiertes Speicherbild aus (auch für Tests mit selbst gebauten Bildern).</summary>
        internal static bool Parse(byte[] b, int length, FieldFrame f, DateTime utc)
        {
            if (b == null || length < NumCarsOffset + 4) return false;
            int major = I(b, oMajor);
            if (major <= 0 || major > 50) return false; // Speicher angelegt, aber von der Sim (noch) nicht beschrieben
            int allDrivers = I(b, oAllDrivers), driverSize = I(b, oDriverSize);
            bool verified = allDrivers == NumCarsOffset && driverSize == DriverSize;
            if (allDrivers <= 0 || allDrivers + 4 > length) allDrivers = NumCarsOffset;
            if (driverSize <= 0 || driverSize > 4096) driverSize = DriverSize;
            int numCars = I(b, allDrivers);
            if (numCars < 0 || numCars > 128) return false;

            f.Utc = utc; f.Sim = "RaceRoom"; f.LayoutVerified = verified;
            f.InMenus = I(b, oMenus) == 1; f.Paused = I(b, oPaused) == 1; f.InReplay = I(b, oReplay) == 1; f.PlayerInGarage = I(b, oGarage) == 1;
            f.SimTime = BitConverter.ToDouble(b, oSimTime);
            f.Track = Text(b, oTrack, 64); f.Layout = Text(b, oLayout, 64);
            float len = F(b, oLength); f.TrackLength = len > 100 && len < 100000 ? len : float.NaN;
            f.RawSession = I(b, oSessionType); f.SessionIteration = I(b, oIteration);
            f.Session = MapSession(f.RawSession); f.Phase = MapPhase(I(b, oPhase));
            int format = I(b, oFormat); // 0 = Zeit, 1 = Runden, 2 = Zeit plus eine Runde
            f.TimedSession = format == 0 || format == 2; f.LapsSession = format == 1;
            f.TimeDuration = Time(F(b, oDuration));
            float rem = F(b, oRemaining); f.TimeRemaining = float.IsNaN(rem) || rem < -1 || rem > 360000 ? float.NaN : Math.Max(0, rem);
            int laps = I(b, oLaps); f.SessionLaps = laps > 0 && laps < 10000 ? laps : -1;
            for (int i = 0; i < 3; i++)
            {
                int m = I(b, oRaceMin + 4 * i), l = I(b, oRaceLaps + 4 * i);
                f.RaceMinutes[i] = m > 0 && m < 10000 ? m : -1; f.RaceLaps[i] = l > 0 && l < 10000 ? l : -1;
            }
            int give = I(b, oGiveBack); f.PositionsToGiveBack = give >= 0 && give < 64 ? give : -1;
            f.NumCars = numCars;
            int playerSlot = I(b, oVehicleSlot);
            // Hockenheim 25.11.: Crew Chief setzt für RaceRoom keinen Autonamen – die Bestzeiten-Datenbank blieb leer.
            int model = I(b, oVehicleModel); f.PlayerCarModel = model > 0 ? "r3e-model-" + model.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            int start = allDrivers + 4;
            int usable = Math.Max(0, Math.Min(numCars, (length - start) / driverSize));
            if (f.Cars.Length != usable) f.Cars = new FieldCarFrame[usable];
            f.PlayerIndex = -1;
            for (int i = 0; i < usable; i++)
            {
                int o = start + i * driverSize;
                var c = f.Cars[i] ?? (f.Cars[i] = new FieldCarFrame());
                c.Slot = I(b, o + dSlot); c.Name = Text(b, o + dName, 64); c.ClassId = I(b, o + dClass);
                c.FinishStatus = I(b, o + dFinish); c.Place = I(b, o + dPlace); c.PlaceClass = I(b, o + dPlaceClass);
                float d = F(b, o + dDist); c.LapDistance = float.IsNaN(d) || d < 0 || d > 100000 ? float.NaN : d;
                c.X = F(b, o + dX); c.Y = F(b, o + dY); c.Z = F(b, o + dZ);
                c.CompletedLaps = I(b, o + dLaps); c.CurrentLapValid = I(b, o + dValid) == 1;
                c.LapTimeCurrent = Time(F(b, o + dCurrent)); c.LastLap = Time(F(b, o + dPrevLap));
                c.BestS1 = Time(F(b, o + dBest1)); c.BestS12 = Time(F(b, o + dBest2)); c.BestLap = Time(F(b, o + dBest3));
                c.InPit = I(b, o + dInPit) == 1;
                float v = F(b, o + dSpeed); c.Speed = float.IsNaN(v) || v < 0 || v > 200 ? float.NaN : v;
                c.PenaltyType = I(b, o + dPenType); c.PenaltyReason = I(b, o + dPenReason);
                c.DriveThrough = F(b, o + dDriveThrough); c.StopAndGo = F(b, o + dStopGo);
                c.TimeDeduction = F(b, o + dTimeDed); c.SlowDown = F(b, o + dSlowDown);
                c.IsPlayer = c.Slot == playerSlot && playerSlot >= 0;
                if (c.IsPlayer) f.PlayerIndex = i;
            }
            return true;
        }

        /// <summary>Simulationstakt (zum Erkennen neuer Bilder).</summary>
        internal static int Ticks(byte[] b) => b == null || b.Length < oTicks + 4 ? int.MinValue : I(b, oTicks);
    }
}
