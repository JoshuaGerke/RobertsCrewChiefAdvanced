using CrewChiefV4.NumberProcessing;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

namespace CrewChiefV4.iRacing
{
    [Serializable]
    public class DriverLiveInfo
    {
        private const float SPEED_CALC_INTERVAL = 0.1f;  
        public DriverLiveInfo(Driver driver)
        {
            _driver = driver;
            PreviousLapWasValid = false;
            HasCrossedSFLine = false;
            LapTimePrevious = -1;
            _prevSector = 0;
            LiveLapsCompleted = 0;
            LapsCompleted = 0;
            insertStartLap = false;
            startLapInserted = false;
            Lap = 0;
            hasCrossedSFLineToStartRace = false;
        }

        private readonly Driver _driver;

        public Driver Driver
        {
            get { return _driver; }
        }
        
        public float SessionTime { get; set; }
        public int Position { get; set; }
        public int PositionRaw { get; set; }
        public int ClassPosition { get; set; }
        public int Lap { get; private set; }
        public float LapDistance { get; private set; }
        public float CorrectedLapDistance { get; private set; }
        public float TotalLapDistance
        {
            get { return this.LiveLapsCompleted + CorrectedLapDistance; }
        }

        public float TotalLapDistanceCorrected { get; set; }

        public TrackSurfaces TrackSurface { get; private set; }
        
        public int Gear { get; private set; }
        public float Rpm { get; private set; }
        public float SteeringAngle { get; private set; }

        public double Speed { get; private set; }
        public double SpeedKph { get; private set; }
        public float GameTimeWhenLastCrossedSFLine { get; private set; }
        public int CurrentSector  { get; set; }
        public int LiveLapsCompleted { get; set; }
        public int LapsCompleted { get; set; }
        public bool IsNewLap { get; set; }
        public bool PreviousLapWasValid { get; set; }
        public float LapTimePrevious { get; set; }
        public bool HasCrossedSFLine { get; set; }
        public bool IsInPilLane { get; set; }
        public int CurrentTireCompound { get; set; }
        private double _prevSpeedUpdateTime;
        private double _prevSpeedUpdateDist;
        private int _prevLap = 0;
        private int _prevSector;
        private Boolean insertStartLap;
        public Boolean startLapInserted;
        private Boolean hasCrossedSFLineToStartRace;
        private Boolean isLapTimePreviousSet = false;
        public void ParseTelemetry(iRacingData e, string sessionType)
        {   
            LapDistance = Math.Abs(e.CarIdxLapDistPct[Driver.Id]);
            
            IsInPilLane = e.CarIdxOnPitRoad[Driver.Id];
            TrackSurface = e.CarIdxTrackSurface[Driver.Id];
            // We need to make sure we start the first lap(IsNewLap), we cant use LapsCompleted for this as it does not increas first time we cross the s/f line but CarIdxLap does so we use that.

            // In lone qualify we want to start the first flying lap when we are not in the pit lane(first flying)
            //if (sessionType == "Lone Qualify")
            //{
            //    insertStartLap = !IsInPilLane && startLapInserted == false && e.CarIdxLap[Driver.Id] > 0 && Lap < e.CarIdxLap[Driver.Id] || e.CarIdxLap[Driver.Id] == -1;
            //}
            //else
            //{
            insertStartLap = startLapInserted == false && ((e.CarIdxLap[Driver.Id] > 0 && Lap < e.CarIdxLap[Driver.Id]) || e.CarIdxLap[Driver.Id] == -1) && sessionType != "Race"; // handle inserting a start lap in the mapper when we are in race
            //}
            
            
      
            CurrentSector = GetCurrentSector();
            IsNewLap = false;
            
            if (_prevSector == 3 && CurrentSector == 1 ||
                (LapsCompleted < _driver.CurrentResults.LapsComplete && !_driver.IsCurrentDriver && TrackSurface == TrackSurfaces.NotInWorld) || (insertStartLap && !Driver.IsCurrentDriver && !hasCrossedSFLineToStartRace)) // this is needed to detect opponents in lone qualify as we do not have reliable lap distance in this case.   
            {
                // make sure we this on the opening lap more then once
                hasCrossedSFLineToStartRace = true;
                
                HasCrossedSFLine = true;
                // This is not accurate for playes that are not in live telemetry but its not used in any calculations in this case.
                GameTimeWhenLastCrossedSFLine = (float)e.SessionTime;
            }
            else
            {
                HasCrossedSFLine = false;
            }
            if (_prevSector != CurrentSector)
            {
                _prevSector = CurrentSector;
            }
            isLapTimePreviousSet = false;
                        
            var lapTimeToUse = Driver.Id == e.PlayerCarIdx ? e.LapLastLapTime : e.CarIdxLastLapTime != null ? e.CarIdxLastLapTime[Driver.Id] : LapTimePrevious;
            if (_driver.CurrentResults.LapsComplete > LapsCompleted || insertStartLap || (Lap == 0 && e.CarIdxLap[Driver.Id] == 1) ||
                (e.CarIdxLapCompleted[Driver.Id] == 1 && _driver.CurrentResults.LapsComplete == 0 && LapTimePrevious != lapTimeToUse && !(sessionType == "Race" || sessionType == "Lone Qualify"))) //this catches the odd case where _driver.CurrentResults.LapsComplete jumps by 2
            {                
                /*if(insertStartLap && !startLapInserted)
                {
                    Console.WriteLine("Start Lap trigger data:{0}", Driver.Name);
                    Console.WriteLine("TrackSurface {0}", TrackSurface);
                    Log.Exception( e, "CarIdxLap {0}");
                    Console.WriteLine("Lap {0}", Lap);
                }
                else
                {
                    Console.WriteLine("New Lap trigger data:{0}", Driver.Name);
                    Console.WriteLine("_driver.CurrentResults.LapsComplete {0} LapsCompleted {1}", _driver.CurrentResults.LapsComplete, LapsCompleted);
                    Log.Exception( e, "CarIdxLapCompleted {0}");
                    Log.Exception( e, "CarIdxLap {0}");
                    Console.WriteLine("Lap {0}", Lap);
                }*/
                if(e.CarIdxLapCompleted[Driver.Id] == 1 && _driver.CurrentResults.LapsComplete == 0 && LapTimePrevious != lapTimeToUse && !(sessionType == "Race" || sessionType == "Lone Qualify"))
                {
                    isLapTimePreviousSet = true;
                    LapTimePrevious = lapTimeToUse;
                }
                startLapInserted = true;                
                IsNewLap = true;
            }

            Lap = e.CarIdxLap[Driver.Id] >= 0 ? e.CarIdxLap[Driver.Id] : 0;
            LapsCompleted = _driver.CurrentResults.LapsComplete;
            
            CorrectedLapDistance = FixPercentagesOnLapChange(LapDistance);
            LiveLapsCompleted = e.CarIdxLapCompleted[Driver.Id] != -1 && e.CarIdxLapCompleted[Driver.Id] < LapsCompleted ? LapsCompleted : e.CarIdxLapCompleted[Driver.Id];
                  
            Gear = e.CarIdxGear[Driver.Id];
            Rpm = e.CarIdxRPM[Driver.Id];
            SteeringAngle = e.CarIdxSteer[Driver.Id];
            if (e.CarIdxTireCompound != null)
            {
                CurrentTireCompound = e.CarIdxTireCompound[Driver.Id];
            }
            //for local player we use data from telemetry as its updated faster then session info,
            //we do not have lastlaptime from opponents available in telemetry so we use data from sessioninfo.
            if (Driver.Id == e.PlayerCarIdx)
            {
                LapTimePrevious = e.LapLastLapTime;
                if(e.RPM == 0)
                {
                    e.RPM = Rpm;
                }
            }
            else if(!isLapTimePreviousSet && IsNewLap)
            {
                LapTimePrevious = _driver.CurrentResults.LastTime;
            }           
            PreviousLapWasValid = LapTimePrevious > 1;
            TotalLapDistanceCorrected = TotalLapDistance;
            PositionRaw = e.CarIdxPosition[Driver.Id];
        }

        private float FixPercentagesOnLapChange(float carIdxLapDistPct)
        {
            if (this.Lap > _prevLap && carIdxLapDistPct > 0.90f)
                return 0;
            else
                _prevLap = this.Lap;

            return Math.Abs(carIdxLapDistPct);
        }

        public void CalculateSpeed(iRacingData telemetry, double? trackLengthKm)
        {
            if (telemetry == null) return;
            if (trackLengthKm == null) return;

            try
            {
                var t1 = telemetry.SessionTime;
                var t0 = _prevSpeedUpdateTime;
                var time = t1 - t0;

                if (time < SPEED_CALC_INTERVAL)
                {
                    // Ignore
                    return;
                }

                var p1 = telemetry.CarIdxLapDistPct[this.Driver.Id];
                var p0 = _prevSpeedUpdateDist;

                if (p1 < -0.5 || _driver.Live.TrackSurface == TrackSurfaces.NotInWorld)
                {
                    // Not in world?
                    return;
                }

                if (p0 - p1 > 0.5)
                {
                    // Lap crossing
                    p1 += 1;
                }
                var distancePct = p1 - p0;

                var distance = distancePct * trackLengthKm.GetValueOrDefault() * 1000; //meters


                if (time >= Double.Epsilon)
                {
                    this.Speed = distance/(time); // m/s
                }
                else
                {
                    if (distance < 0)
                        this.Speed = Double.NegativeInfinity;
                    else
                        this.Speed = Double.PositiveInfinity;
                }
                this.SpeedKph = this.Speed * 3.6;

                _prevSpeedUpdateTime = t1;
                _prevSpeedUpdateDist = p1;
            }
            catch (Exception)
            {
                //Log.Instance.LogError("Calculating speed of car " + this.Driver.Id, ex);
                this.Speed = 0;
            }
        }
        public int GetCurrentSector()
        {
            int sector = 3;
            if(this.LapDistance >= 0f && this.LapDistance < 0.333f)
            {
                sector = 1;
            }
            if(this.LapDistance >=  0.333f && this.LapDistance < 0.666f)
            {
                sector = 2;
            }
            return sector;
        }
    }
}
