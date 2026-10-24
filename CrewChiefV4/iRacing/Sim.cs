using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using iRSDKSharp;

namespace CrewChiefV4.iRacing
{
    public class Sim
    {
        public Sim()
        {
            _drivers = new List<Driver>();
            _sessionData = new SessionData();
            _sessionId = -1;
            _driver = null;
            _paceCar = null;
            _raceSessionInProgress = false;
            _leaderFinished = null;
            _gameTimeWhenWhiteFlagTriggered = -1.0;
           _paceCarPresent = false;
            _sessionTimeRemain = Double.MinValue;
            _sessionRunningTime = Double.MaxValue;
        }

        enum RaceEndState {NONE, WAITING_TO_CROSS_LINE, FINISHED}
        private RaceEndState raceEndState = RaceEndState.NONE;

        private iRacingData _telemetry;

        private int _sessionId;
        public int SessionId { get { return _sessionId; } }

        private double _sessionTimeRemain;
        public double SessionTimeRemain { get { return _sessionTimeRemain; } }

        private double _sessionRunningTime;
        public double SessionRunningTime { get { return _sessionRunningTime; } }

        private int _subSessionId;
        public int SubSessionId { get { return _subSessionId; } }

        private int? _currentSessionNumber;
        public int? CurrentSessionNumber { get { return _currentSessionNumber; } }

        public iRacingData Telemetry { get { return _telemetry; } }

        private SessionData _sessionData;
        public SessionData SessionData { get { return _sessionData; } }

        private int _DriverId;
        public int DriverId { get { return _DriverId; } }

        private Driver _driver;
        public Driver Driver { get { return _driver; } }

        private Driver _paceCar;
        public Driver PaceCar { get { return _paceCar; } }

        private Boolean _paceCarPresent;
        public Boolean PaceCarPresent { get { return _paceCarPresent; } }

        private readonly List<Driver> _drivers;
        public List<Driver> Drivers { get { return _drivers; } }

        private Dictionary<int, double> _carIdxToGameTimeOffTrack = new Dictionary<int, double>();
        private bool _raceSessionInProgress = false;
        private Driver _leaderFinished = null;
        private double _gameTimeWhenWhiteFlagTriggered = -1.0;
        private const double SECONDS_OFF_WORLD_TILL_RETIRED = 20.0;
        private DateTime _lastLogTime = default(DateTime);
        //private int _numberOfStarters = -1;
        //private int _numberOfDriversOut = 0;
        private void UpdateDriverList(string sessionInfo, bool reloadDrivers)
        {
            this.GetDrivers(sessionInfo, reloadDrivers);
            this.GetResults(sessionInfo);
        }

        private void GetDrivers(string sessionInfo, bool reloadDrivers)
        {
            if (reloadDrivers)
            {
                Console.WriteLine("Reloading Drivers");
                _drivers.Clear();
                _driver = null;
                _paceCar = null;
                _paceCarPresent = false;
            }

            int max_drivers = 64;
            if (this._telemetry != null)
            {
                max_drivers = this._telemetry.CarIdxPosition.Length;
            }
            for (int id = 0; id < max_drivers; id++)
            {
                // Find existing driver in list
                var driver = _drivers.SingleOrDefault(d => d.Id == id);
                if (driver == null)
                {
                    driver = Driver.FromSessionInfo(sessionInfo, id, DriverId);

                    if (driver == null)
                    {
                        continue;
                    }

                    driver.IsCurrentDriver = false;
                    _drivers.Add(driver);
                }
                else
                {
                    driver.ParseDynamicSessionInfo(sessionInfo, DriverId == driver.Id);
                }
                
                if (DriverId == driver.Id)
                {
                    _driver = driver;
                    _driver.IsCurrentDriver = true;
                }
                if (driver.IsPaceCar)
                {
                    _paceCar = driver;
                    _paceCarPresent = true;
                }
            }
        }

        private void GetResults(string sessionInfo)
        {
            if (_currentSessionNumber == null) 
                return;
            this.GetRaceResults(sessionInfo);

            if (this.SessionData.EventType == "Race" || this.SessionData.EventType == "Open Qualify" || this.SessionData.EventType == "Lone Qualify")
            {
                this.GetQualyResults(sessionInfo);
            }
            
        }

        private void GetQualyResults(string sessionInfo)
        {
            for (int position = 0; position < _drivers.Count; position++)
            {

                string idValue = "-1";
                string classPositionString = "-1";
                if (!YamlParser.TryGetValue(sessionInfo, string.Format("QualifyResultsInfo:Results:Position:{{{0}}}CarIdx:", position ), out idValue) ||
                    !YamlParser.TryGetValue(sessionInfo, string.Format("QualifyResultsInfo:Results:Position:{{{0}}}ClassPosition:", position), out classPositionString))
                {
                    continue;
                }
                // Find driver and update results
                int id = int.Parse(idValue);
                int classPosition = -1;
                Int32.TryParse(classPositionString, out classPosition);
                var driver = _drivers.SingleOrDefault(d => d.Id == id);
                if (driver != null && !driver.IsPaceCar)
                {                   
                    driver.CurrentResults.QualifyingPosition = position + 1;
                    driver.CurrentResults.ClassQualifyingPosition = classPosition + 1;
                }
            }
        }

        /*private void GetNumberOfStarters(string sessionInfo)
        {
            var numStartersString = "-1";
            if (!YamlParser.TryGetValue(sessionInfo, "WeekendInfo:WeekendOptions:NumStarters:", out numStartersString))
            {
                return;
            }
            _numberOfStarters = int.Parse(numStartersString);
        }*/

        private void GetRaceResults(string sessionInfo)
        {
            //var numberOut = 0;
            for (int position = 1; position <= _drivers.Count; position++)
            {                
                string idValue = "0";
                string reasonOutString = "0";
                /*if(YamlParser.TryGetValue(sessionInfo, string.Format("SessionInfo:Sessions:SessionNum:{{{0}}}ResultsPositions:Position:{{{1}}}ReasonOutId:",
                    _currentSessionNumber, position), out reasonOutString))
                {
                    var reasonOut = (ReasonOutId)int.Parse(reasonOutString);
                    if(reasonOut != ReasonOutId.IDS_REASON_OUT_NOT_OUT)
                    {
                        numberOut++;
                    }
                }*/
                if (!YamlParser.TryGetValue(sessionInfo, string.Format("SessionInfo:Sessions:SessionNum:{{{0}}}ResultsPositions:Position:{{{1}}}CarIdx:",
                    _currentSessionNumber, position), out idValue))
                {
                    continue;
                }
                // Driver not found

                // Find driver and update results
                if (!Int32.TryParse(idValue, out int id))
                {  
                    Log.Error("Could not parse driver id from session info idValue: " + idValue);
                    continue; 
                }

                var driver = _drivers.SingleOrDefault(d => d.Id == id);
                if (driver != null && !driver.IsPaceCar)
                {
                    driver.UpdateResultsInfo(sessionInfo, _currentSessionNumber.Value, position);
                }
            }
            //_numberOfDriversOut = numberOut;
        }
        private void UpdateDriverTelemetry(iRacingData info)
        {
            foreach (var driver in _drivers)
            {
                driver.Live.CalculateSpeed(info, _sessionData.Track.Length);
                driver.UpdateLiveInfo(info, this.SessionData.SessionType);                
            }
            this.CalculateLivePositions(info);
        }

        private void CalculateLivePositions(iRacingData telemetry)
        {
            if (!_raceSessionInProgress)
            {
                this._carIdxToGameTimeOffTrack.Clear();
            }

            // Assume race has finished.
            this._raceSessionInProgress = false;

            // In a race that is not yet in checkered flag mode,
            // Live positions are determined from track position (total lap distance)
            // Any other conditions (race finished, P, Q, etc), positions are ordered as result positions
            SessionFlags flag = (SessionFlags)telemetry.SessionFlags;
            if (this.SessionData.SessionType == "Race" && flag.HasFlag(SessionFlags.Checkered))
            {
                // We need to check if player is the first (p1) to cross the s/f line as HasCrossedSFLine is only true for 1 tick 
                if (raceEndState == RaceEndState.WAITING_TO_CROSS_LINE || _driver.Live.IsNewLap)
                {
                    if (this._driver.Live.IsNewLap)
                    {
                        Console.WriteLine("Player just crossed line to finish race");
                        raceEndState = RaceEndState.FINISHED;

                        this._driver.FinishStatus = Driver.FinishState.Finished;
                    }
                }
                else if (raceEndState == RaceEndState.NONE)
                {
                    Console.WriteLine("Starting wait for player crossing line");
                    raceEndState = RaceEndState.WAITING_TO_CROSS_LINE;
                }
            }
            else
            {
                raceEndState = RaceEndState.NONE;
            }
            if (this.SessionData.SessionType == "Race" && raceEndState != RaceEndState.FINISHED
                && (flag.HasFlag(SessionFlags.StartGo) || flag.HasFlag(SessionFlags.StartHidden))
                && telemetry.PlayerCarPosition > 0)
            {
                this._raceSessionInProgress = true;

                // When driver disconnects (or in other cases I am not sure about yet), TotalLapDitance
                // gets ceiled to the nearest integer.  Because of that, for the reminder of a lap such car is
                // ahead of others by TotalLapDitance, which results incorrect positions announced.
                //
                // To mitigate that, try detecting such cases and using floor(TotalLapDitance) instead.
                //
                // Also, try detecting cars that finished and use YAML reported positions instead for those.

                if (this._gameTimeWhenWhiteFlagTriggered == -1.0 && flag.HasFlag(SessionFlags.White))
                {
                    this._gameTimeWhenWhiteFlagTriggered = telemetry.SessionTime;
                }

                // Correct the distances
                foreach (var driver in _drivers)
                {
                    if (driver.IsSpectator || driver.IsPaceCar || driver.CurrentResults.IsOut)
                    {
                        continue;
                    }

                    if (Math.Floor(driver.Live.TotalLapDistanceCorrected) > driver.Live.LiveLapsCompleted)
                    {
                        driver.Live.TotalLapDistanceCorrected = (float)driver.Live.LiveLapsCompleted;
                    }
                }

                // If leader has not finished yet, check if he just did.
                if (_leaderFinished == null && this._gameTimeWhenWhiteFlagTriggered != -1.0)
                {
                    // See if leading driver crossed s/f line after race time expired.
                    foreach (var driver in _drivers.OrderByDescending(d => d.Live.TotalLapDistanceCorrected))
                    {
                        if (driver.IsSpectator || driver.IsPaceCar || driver.CurrentResults.IsOut)
                        {
                            continue;
                        }

                        if (driver.IsCurrentDriver)
                        {
                            // It appears that player s/f crossing time is set before the actual s/f crossing.  So, for player "Finished" state 
                            // rely on logic that waits for new lap crossing.
                            continue;
                        }

                        if (driver.Live.GameTimeWhenLastCrossedSFLine > this._gameTimeWhenWhiteFlagTriggered)
                        {
                            _leaderFinished = driver;
                        }

                        // Only check assumed leader by lapdist (skipping lapped vehicles, spectators etc).
                        break;
                    }
                }

                // Now, detect lapped and retired finishers.
                foreach (var driver in _drivers)
                {
                    if (_leaderFinished != null
                        && driver.Live.GameTimeWhenLastCrossedSFLine >= _leaderFinished.Live.GameTimeWhenLastCrossedSFLine)
                    {
                        if (driver.IsCurrentDriver || driver.IsPaceCar)
                        {
                            // It appears that player s/f crossing time is set before the actual s/f crossing.  So, for player "Finished" state 
                            // rely on logic that waits for new lap crossing.
                            continue;
                        }

                        // Everyone, who crosses s/f after leader finished, finishes too.
                        driver.FinishStatus = Driver.FinishState.Finished;
                    }
                }

                // Determine live position from lap distance.
                // Non drivers are set to 1001
                foreach (var driver in _drivers.Where(d => d.IsSpectator || d.IsPaceCar || d.CurrentResults.IsOut))
                {
                    driver.Live.Position = 1001;
                    driver.Live.ClassPosition = 1001;
                }
                            
                
                SetPositionsByOrder(telemetry);
            }
            else  // Not Race or Finished.
            {
                // Clear out cached finished leader.  Ugly data model, ugly workarounds :(
                _leaderFinished = null;
                _gameTimeWhenWhiteFlagTriggered = -1.0;

                // In P or Q, set live position from result position (== best lap according to iRacing)
                foreach (var driver in _drivers)
                {
                    if (driver.IsSpectator || driver.IsPaceCar)
                    {
                        driver.Live.Position = 1001;  // Make it obvious those guys are not tracked.
                        continue;
                    }
                    if (telemetry.CarIdxPosition[driver.Id] > 0 && raceEndState != RaceEndState.FINISHED)
                    {
                        driver.Live.Position = telemetry.CarIdxPosition[driver.Id];
                    }
                    else
                    {
                        driver.Live.Position = driver.CurrentResults.Position;
                    }

                    if (telemetry.CarIdxClassPosition[driver.Id] > 0 && raceEndState != RaceEndState.FINISHED)
                    {
                        driver.Live.ClassPosition = telemetry.CarIdxClassPosition[driver.Id];
                    }
                    else
                    {
                        driver.Live.ClassPosition = driver.CurrentResults.ClassPosition;
                    }

                    if (driver.Live.Position == 0)
                    {
                        driver.Live.Position = 1000;  // Game sends nonsense again.  Mark those so that they don't interfere with sorting anywhere.
                    }

                    if (driver.Live.ClassPosition == 0)
                    {
                        driver.Live.ClassPosition = 1000;  // Game sends nonsense again.  Mark those so that they don't interfere with sorting anywhere.
                    }
                }
            }
        }

        private static void SetPositionsFromUserPosition(List<Driver> driversOrderedByPosition, int indexOfUser, int userPosition)
        {
            // Loop through all drivers, and calculate position from our position (which is known to be accurate.
            // We should be getting only the closest drivers to us. May have some inaccurate positions, but they
            // will likely be drivers a long way from us in the standings.
            for (var i = 0; i < driversOrderedByPosition.Count; i++)
            {
                var driver = driversOrderedByPosition[i];
                var differenceFromUser = i - indexOfUser;
                var position = userPosition + differenceFromUser;
                if (driver.FinishStatus == Driver.FinishState.Finished
                    && driver.Live.TrackSurface == TrackSurfaces.NotInWorld)
                {
                    // When finished driver disconnects, use game reported position.
                    // This should not mess up order of drivers following, because all the
                    // drivers are ordered by TotalLapDistanceCorrected.
                    driver.Live.Position = driver.CurrentResults.Position;

                    if (driver.Live.Position == 0)
                    {
                        driver.Live.Position = 1000;  // Game sends nonsense again.  Mark those so that they don't interfere with sorting anywhere.
                    }
                }
                else
                {
                    driver.Live.Position = position;
                }
            }
        }

        DateTime lastLogged = DateTime.Now;
        private void SetPositionsByOrder(iRacingData telemetry)
        {
            // We use the list of positions that drivers have and set these in order. This will mean that we
            // still correct for overtakes, but we limit ourselves to setting positions that we know about.
            // Cars that are missing from the list of drivers won't mean positions get shifted.
            // This can lead to some drivers having incorrect positions, but it's unlikely to be us, and
            // all positions are still valid.
            _drivers.Where(driver => driver.IsSpectator || driver.IsPaceCar).ToList().ForEach(d => { d.Live.Position = 1001; d.Live.ClassPosition = 1001; });
            var validDrivers = _drivers.Where(driver => !(driver.IsSpectator || driver.IsPaceCar)).ToList();

            var validPositions = validDrivers.Where(d => d.CurrentResults.Position != 0).Select(d => d.CurrentResults.Position).OrderBy(p => p).ToList();
            var classPositions = validDrivers.Where(d => telemetry.CarIdxClassPosition[d.Id] != 0).Select(d => new { d.Car.CarClassId, ClassPosition = telemetry.CarIdxClassPosition[d.Id] }).GroupBy(p => p.CarClassId, p => p.ClassPosition).ToDictionary(p => p.Key, p => p.OrderBy(i => i).ToList());

            var lastPositionSet = 0;
            var lastClassPositionSet = validDrivers.Select(d => d.Car.CarClassId).Distinct().Select(c => new { CarClassId = c, ClassPosition = 0 }).ToDictionary(p => p.CarClassId, p => p.ClassPosition);

            foreach (var driver in validDrivers.OrderByDescending(d => d.Live.TotalLapDistanceCorrected))
            {
                var currentPosition = lastPositionSet + 1;
                if (validPositions.Any())
                {
                    // we use the next valid position instead
                    currentPosition = validPositions[0];
                    validPositions.RemoveAt(0);
                }
                lastPositionSet = currentPosition;
                driver.Live.Position = currentPosition;                

                var currentClassPosition =  lastClassPositionSet[driver.Car.CarClassId] + 1;
                if(classPositions.ContainsKey(driver.Car.CarClassId) && classPositions[driver.Car.CarClassId].Any())
                {
                    // we use the next valid position instead
                    currentClassPosition = classPositions[driver.Car.CarClassId][0];
                    classPositions[driver.Car.CarClassId].RemoveAt(0);
                }
                lastClassPositionSet[driver.Car.CarClassId] = currentClassPosition;
                driver.Live.ClassPosition = currentClassPosition;                
            }
#if DEBUG
            var currentTime = DateTime.Now;
            if(lastLogged.AddMinutes(1) < currentTime)
            {
                Console.WriteLine(string.Format("available positions: {0}", string.Join("|", validPositions.OrderBy(p => p))));
                foreach (var key in classPositions.Keys)
                {
                    Console.WriteLine(string.Format("available positions for class id {0}: {1}", key, string.Join("|", classPositions[key].OrderBy(p => p))));
                }
                Console.WriteLine("positions: {0}", string.Join("|", validDrivers.OrderBy(d => d.Live.Position).Select(
                    d => string.Format("pos: {0}, classPos {1}", d.Live.Position, d.Live.ClassPosition))));
                lastLogged = currentTime;
            }
#endif
        }


        public bool SdkOnSessionInfoUpdated(string sessionInfo, int sessionNumber, int driverId, double sessionTimeRemain, double sessionRunningTime, bool isInCarDriving)
        {

            _DriverId = driverId;
            bool reloadDrivers = false;
            //also need
            int sessionId = Parser.ParseInt(YamlParser.Parse(sessionInfo, "WeekendInfo:SessionID:"));
            int subSessionId = Parser.ParseInt(YamlParser.Parse(sessionInfo, "WeekendInfo:SubSessionID:"));
            
            
            if ((_currentSessionNumber == null || (_currentSessionNumber != sessionNumber) || sessionId != _sessionId || subSessionId != _subSessionId) || // official, hosted or AI session change sessionNumber
                ((sessionTimeRemain > _sessionTimeRemain || sessionRunningTime < _sessionRunningTime) && !isInCarDriving && sessionId == 0 && sessionTimeRemain > 1 && sessionTimeRemain < 172800/* 2 days*/)) // AI session of same type restarted 
            {
                // Session changed, reset session info
                reloadDrivers = true;
                _sessionData.Update(sessionInfo, sessionNumber);
                _sessionId = sessionId;
                _subSessionId = subSessionId;

            }
            _currentSessionNumber = sessionNumber;
            // Update drivers
            this.UpdateDriverList(sessionInfo, reloadDrivers);
            _sessionTimeRemain = sessionTimeRemain;
            _sessionRunningTime = sessionRunningTime;
            //GetNumberOfStarters(sessionInfo);

            return reloadDrivers;         
        }

        public void SdkOnTelemetryUpdated(iRacingData telemetry)
        {
            // Cache info            
            _telemetry = telemetry;
            // Update drivers telemetry
            this.UpdateDriverTelemetry(telemetry);
        }
    }
}
