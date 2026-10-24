using CrewChiefV4.Events;
using CrewChiefV4.GameState;

using InSimDotNet.Helpers;
using InSimDotNet.Packets;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CrewChiefV4.LFS
{
    /// <summary>
    /// Maps Live for Speed telemetry (InSim packets and OutSim UDP data) into CrewChief's
    /// game-agnostic <see cref="GameStateData"/> model.
    /// Handles session phases, lap timing, sector splits, fuel percentage tracking, tyre temperatures,
    /// flags, penalties, and opponent positions.
    /// </summary>
    class LFSGameStateMapper : GameStateMapper
    {
        private static bool VersionChecked;

        private const int EXPECTED_INSIM_VERSION = 9;
        private const float AIR_TEMP = 23.0f;
        private const float TRACK_TEMP = 23.0f;

        public override GameStateData mapToGameStateData(object memoryMappedFileStruct, GameStateData previousGameState)
        {
            GameStateData pgs = previousGameState;
            var wrapper = (LFSInSimReader.LFSStructWrapper) memoryMappedFileStruct;
            var cgs = new GameStateData(wrapper.TicksWhenRead);
            LFSData lfsData = wrapper.Data;

            if (lfsData == null)
            {
                return null;
            }

            SessionData psd = previousGameState?.SessionData;
            SessionData csd = cgs.SessionData;

            cgs.rawGameData = wrapper;

            CrewChief.forceSingleClass = true;

            ////////////////////////////////////
            // Session data
            csd.SessionType = mapToSessionType(lfsData);
            csd.SessionPhase = mapToSessionPhase(lfsData, csd.SessionType);
            csd.SessionStartTime = lfsData.SessionState.StartTime;

            csd.IsNewSession = previousGameState == null
                ? csd.SessionType != SessionType.Unavailable
                : psd.SessionStartTime != csd.SessionStartTime; // TODO: Bug

            // Restore cumulative data
            if (!csd.IsNewSession && psd != null)
            {
                cgs.carClass = pgs.carClass;
                csd.DriverRawName = psd.DriverRawName;
                csd.TrackDefinition = psd.TrackDefinition;

                cgs.PitData.NumPitStops = pgs.PitData.NumPitStops;
                cgs.PenaltiesData.CutTrackWarnings = pgs.PenaltiesData.CutTrackWarnings;

                csd.DeltaTime = psd.DeltaTime;
                cgs.readLandmarksForThisLap = previousGameState.readLandmarksForThisLap;

                cgs.retriedDriverNames = pgs.retriedDriverNames;
                cgs.disqualifiedDriverNames = pgs.disqualifiedDriverNames;

                cgs.FlagData.currentLapIsFCY = pgs.FlagData.currentLapIsFCY;
                cgs.FlagData.previousLapWasFCY = pgs.FlagData.previousLapWasFCY;

                cgs.Conditions.samples = pgs.Conditions.samples;
                cgs.PitData.PitBoxPositionEstimate = pgs.PitData.PitBoxPositionEstimate;

                cgs.hardPartsOnTrackData = pgs.hardPartsOnTrackData;

                csd.formattedPlayerLapTimes = psd.formattedPlayerLapTimes;
                cgs.TimingData = pgs.TimingData;
                csd.JustGoneGreenTime = psd.JustGoneGreenTime;

                csd.IsLastLap = psd.IsLastLap;
                csd.OverallLeaderIsOnLastLap = psd.OverallLeaderIsOnLastLap;

                cgs.trackName = pgs.trackName;
                cgs.carName = pgs.carName;

                csd.GameTimeAtLastPositionFrontChange = psd.GameTimeAtLastPositionFrontChange;
                csd.GameTimeAtLastPositionBehindChange = psd.GameTimeAtLastPositionBehindChange;
                csd.OpponentKeyInFront = psd.OpponentKeyInFront;
                csd.OpponentKeyBehind = psd.OpponentKeyBehind;
            }

            // Track definition resolution: always ensure a valid TrackDefinition is present
            string trackCode = lfsData.SessionState?.Track;
            string fullTrackName = !string.IsNullOrEmpty(trackCode) ? TrackHelper.GetFullTrackName(trackCode) : (cgs.trackName ?? "Unknown");
            float trackLength = GetTrackLength(trackCode);

            if (csd.TrackDefinition == null || csd.TrackDefinition.name != fullTrackName)
            {
                csd.TrackDefinition = TrackData.getTrackDefinition(fullTrackName, trackLength);
                cgs.trackName = fullTrackName;
                CrewChief.trackName = fullTrackName;
            }

            if (lfsData.OutSimCurrentLapDist > csd.TrackDefinition.trackLength)
            {
                csd.TrackDefinition.trackLength = lfsData.OutSimCurrentLapDist;
            }

            if (csd.SessionType == SessionType.Race)
            {
                if (psd != null && (psd.SessionPhase == SessionPhase.Countdown || psd.SessionPhase == SessionPhase.Formation) && csd.SessionPhase == SessionPhase.Green)
                {
                    Console.WriteLine("Green flag!");
                    csd.JustGoneGreen = true;
                }
            }
            else if (csd.IsNewSession && csd.SessionType != SessionType.Unavailable)
            {
                csd.JustGoneGreen = true;
            }

            if (csd.IsNewSession && csd.SessionType != SessionType.Unavailable)
            {
                csd.CompletedLaps = 0;
                csd.LeaderHasFinishedRace = false;
                csd.LeaderSectorNumber = 0;
                csd.LapTimeCurrent = -1.0f;
                csd.LapTimePrevious = -1.0f;
                csd.TimeDeltaFront = -1.0f;
                csd.TimeDeltaBehind = -1.0f;
            }

            Player viewedPlayer = lfsData.SessionState.ViewPLID != 0 ? lfsData.GetPlayerByPLID(lfsData.SessionState.ViewPLID) : null;
            Player leaderPlayer = getLeaderPlayer(lfsData);

            if (viewedPlayer == null && lfsData.Players.Count > 0)
            {
                viewedPlayer = leaderPlayer ?? lfsData.Players.Values.FirstOrDefault();
            }

            bool isViewingPlayerInSession = csd.SessionType != SessionType.Unavailable && viewedPlayer != null;

            if (!isViewingPlayerInSession || leaderPlayer == null)
            {
                return cgs;
            }

            PlayerScoring playerScoring = viewedPlayer.Scoring;

            cgs.carName = viewedPlayer.CName;
            cgs.carClass = CarData.getCarClassForClassNameOrCarName(viewedPlayer.CName);
            CrewChief.carClass = cgs.carClass.carClassEnum;
            csd.DriverRawName = CleanDriverName(viewedPlayer.PName, viewedPlayer.Connection?.UName, viewedPlayer.PLID);

            if (csd.SessionType == SessionType.Qualify)
            {
                csd.SessionHasFixedTime = true;
                csd.SessionNumberOfLaps = 0;
                csd.SessionTotalRunTime = lfsData.SessionState.QualMins > 0 ? lfsData.SessionState.QualMins * 60 : 0;
            }
            else
            {
                byte lfsRaceLaps = lfsData.SessionState.RaceLaps;
                int raceLapsOrHours = getRaceLapsOrHours(lfsRaceLaps);

                csd.SessionHasFixedTime = sessionHasFixedTime(lfsRaceLaps);
                csd.SessionTotalRunTime = csd.SessionHasFixedTime ? raceLapsOrHours * 3600 : 0;
                csd.SessionNumberOfLaps = csd.SessionHasFixedTime ? 0 : raceLapsOrHours;
            }

            csd.NumCarsOverall = lfsData.Players.Count;
            csd.NumCarsOverallAtStartOfSession =
                csd.IsNewSession ? lfsData.Players.Count : psd?.NumCarsInPlayerClassAtStartOfSession ?? 0;
            csd.OverallPosition = playerScoring.Position > 0 ? playerScoring.Position : (psd != null && psd.OverallPosition > 0 ? psd.OverallPosition : 0);
            csd.ClassPosition = csd.OverallPosition;

            csd.CompletedLaps = playerScoring.LapData.Count;

            csd.SectorNumber = playerScoring.Split + 1;
            csd.IsNewSector = csd.IsNewSession || csd.SectorNumber != psd?.SectorNumber;
            csd.IsNewLap = (psd != null && csd.CompletedLaps > psd.CompletedLaps) || (csd.IsNewSector && csd.SectorNumber == 1 && csd.CompletedLaps > 0);

            csd.PositionAtStartOfCurrentLap =
                csd.IsNewLap ? csd.OverallPosition : psd?.PositionAtStartOfCurrentLap ?? 0;

            // TODO: csd.IsDisqualified
            // TODO: csd.IsDNF

            if (lfsData.SessionState.GreenTime.Ticks > 0)
            {
                csd.SessionRunningTime = (float) DateTime.Now.Subtract(lfsData.SessionState.GreenTime).TotalSeconds;
            }

            if (csd.SessionHasFixedTime)
            {
                csd.SessionTimeRemaining = Math.Max(0.0f, csd.SessionTotalRunTime - csd.SessionRunningTime);
            }
            else if (csd.SessionNumberOfLaps > 0)
            {
                int leaderLaps = Math.Max(leaderPlayer?.Scoring?.LapData?.Count ?? 0, csd.CompletedLaps);
                csd.SessionLapsRemaining = Math.Max(0, csd.SessionNumberOfLaps - leaderLaps);
            }

            // Initialize DeltaTime.
            if (csd.IsNewSession)
                csd.DeltaTime = new DeltaTime(csd.TrackDefinition.trackLength, cgs.PositionAndMotionData.DistanceRoundTrack, cgs.PositionAndMotionData.CarSpeed, cgs.Now);

            ///////////////////////////////////
            // Pit Data

            cgs.PitData.IsRefuellingAllowed = lfsData.SessionState.SessionFlags.HasFlag(RaceFlags.HOSTF_CAN_REFUEL);
            cgs.PitData.InPitlane = viewedPlayer.PitData.PitLaneFact != PitLaneFact.PITLANE_EXIT;
            cgs.PitData.HasMandatoryPitStop = csd.SessionType == SessionType.Race &&
                                              lfsData.SessionState.SessionFlags.HasFlag(RaceFlags.HOSTF_MUST_PIT);
            cgs.PitData.NumPitStops = viewedPlayer.PitData.NumStops;
            cgs.PitData.IsAtPitExit = pgs != null && pgs.PitData.InPitlane && !cgs.PitData.InPitlane;
            cgs.PitData.OnOutLap = cgs.PitData.IsAtPitExit;

            if (pgs != null
                && csd.CompletedLaps == psd.CompletedLaps
                && pgs.PitData.OnOutLap)
            {
                // If current lap is pit out lap, keep it that way till lap completes.
                cgs.PitData.OnOutLap = true;
            }

            cgs.PitData.OnInLap = cgs.PitData.InPitlane && csd.SectorNumber == 3;
            cgs.PitData.IsMakingMandatoryPitStop = cgs.PitData.HasMandatoryPitStop && cgs.PitData.OnInLap;

            if (pgs != null)
            {
                cgs.PitData.MandatoryPitStopCompleted =
                    pgs.PitData.MandatoryPitStopCompleted || cgs.PitData.IsMakingMandatoryPitStop;
            }

            // TODO: Refactor
            cgs.PitData.PitSpeedLimit = lfsData.SessionState.Track?.Substring(0, 2) == "RO" ? 50 / 3.6f : 80 / 3.6f;

            ////////////////////////////////////
            // Penalties & Incidents
            cgs.PenaltiesData.CutTrackWarnings = lfsData.CutTrackWarnings;
            if (viewedPlayer.Penalty != PenaltyValue.PENALTY_NONE)
            {
                cgs.PenaltiesData.HasDriveThrough = viewedPlayer.Penalty == PenaltyValue.PENALTY_DT;
                cgs.PenaltiesData.HasStopAndGo = viewedPlayer.Penalty == PenaltyValue.PENALTY_SG;
                cgs.PenaltiesData.HasTimeDeduction = viewedPlayer.Penalty == PenaltyValue.PENALTY_30 || viewedPlayer.Penalty == PenaltyValue.PENALTY_45;
            }

            ////////////////////////////////////
            // Fuel Data
            bool hostDisablesFuel = lfsData.SessionState.SessionFlags != 0 &&
                                    !lfsData.SessionState.SessionFlags.HasFlag(RaceFlags.HOSTF_SHOW_FUEL);

            if (!hostDisablesFuel && viewedPlayer.Fuel200 != 255)
            {
                // In LFS, fuel is handled in percentage (0..100%) across all cars and mods.
                // Fuel200 is 0..200 where 200 = 100%, so 1 unit = 0.5%.
                // 1L announced in CrewChief corresponds to 1% in the LFS F12 menu.
                cgs.FuelData.FuelCapacity = 100.0f;
                cgs.FuelData.FuelLeft = (float)viewedPlayer.Fuel200 / 2.0f;
                cgs.FuelData.FuelUseActive = true;
            }
            else if (!hostDisablesFuel && viewedPlayer.InitialFuel != 255)
            {
                // Initial fuel from setup / IS_NPL on the grid (0..100 = 0..100%)
                cgs.FuelData.FuelCapacity = 100.0f;
                cgs.FuelData.FuelLeft = (float)viewedPlayer.InitialFuel;
                cgs.FuelData.FuelUseActive = true;
            }
            else if (!hostDisablesFuel && !csd.IsNewSession && pgs != null && pgs.FuelData != null && pgs.FuelData.FuelUseActive && pgs.FuelData.FuelLeft > 0)
            {
                cgs.FuelData.FuelCapacity = pgs.FuelData.FuelCapacity;
                cgs.FuelData.FuelLeft = pgs.FuelData.FuelLeft;
                cgs.FuelData.FuelUseActive = true;
            }
            else
            {
                // Fuel telemetry is not available or disabled by host (/showfuel no)
                cgs.FuelData.FuelCapacity = 100.0f;
                cgs.FuelData.FuelLeft = -1.0f;
                cgs.FuelData.FuelUseActive = false;
            }

            ////////////////////////////////////
            // Tyre Data & Distance (from OutSim)
            if (lfsData.HasOutSimData)
            {
                // lfsData.TyreAirTemps: 0 = RL, 1 = RR, 2 = FL, 3 = FR
                float rl = lfsData.TyreAirTemps[0];
                float rr = lfsData.TyreAirTemps[1];
                float fl = lfsData.TyreAirTemps[2];
                float fr = lfsData.TyreAirTemps[3];

                cgs.TyreData.FrontLeft_CenterTemp = fl;
                cgs.TyreData.FrontLeft_LeftTemp = fl;
                cgs.TyreData.FrontLeft_RightTemp = fl;

                cgs.TyreData.FrontRight_CenterTemp = fr;
                cgs.TyreData.FrontRight_LeftTemp = fr;
                cgs.TyreData.FrontRight_RightTemp = fr;

                cgs.TyreData.RearLeft_CenterTemp = rl;
                cgs.TyreData.RearLeft_LeftTemp = rl;
                cgs.TyreData.RearLeft_RightTemp = rl;

                cgs.TyreData.RearRight_CenterTemp = rr;
                cgs.TyreData.RearRight_LeftTemp = rr;
                cgs.TyreData.RearRight_RightTemp = rr;

                // Track peak lap temperatures
                if (csd.IsNewLap || cgs.TyreData.PeakFrontLeftTemperatureForLap == 0)
                {
                    cgs.TyreData.PeakFrontLeftTemperatureForLap = fl;
                    cgs.TyreData.PeakFrontRightTemperatureForLap = fr;
                    cgs.TyreData.PeakRearLeftTemperatureForLap = rl;
                    cgs.TyreData.PeakRearRightTemperatureForLap = rr;
                }
                else if (pgs != null)
                {
                    cgs.TyreData.PeakFrontLeftTemperatureForLap = Math.Max(fl, pgs.TyreData.PeakFrontLeftTemperatureForLap);
                    cgs.TyreData.PeakFrontRightTemperatureForLap = Math.Max(fr, pgs.TyreData.PeakFrontRightTemperatureForLap);
                    cgs.TyreData.PeakRearLeftTemperatureForLap = Math.Max(rl, pgs.TyreData.PeakRearLeftTemperatureForLap);
                    cgs.TyreData.PeakRearRightTemperatureForLap = Math.Max(rr, pgs.TyreData.PeakRearRightTemperatureForLap);
                }

                var tyreTempThresholds = CarData.getTyreTempThresholds(cgs.carClass);
                cgs.TyreData.TyreTempStatus = CornerData.getCornerData(tyreTempThresholds,
                    cgs.TyreData.PeakFrontLeftTemperatureForLap, cgs.TyreData.PeakFrontRightTemperatureForLap,
                    cgs.TyreData.PeakRearLeftTemperatureForLap, cgs.TyreData.PeakRearRightTemperatureForLap);
            }
            else if (pgs != null)
            {
                cgs.TyreData = pgs.TyreData;
            }

            if (lfsData.HasOutSimData && lfsData.OutSimCurrentLapDist > 0)
            {
                cgs.PositionAndMotionData.DistanceRoundTrack = lfsData.OutSimCurrentLapDist;
            }
            else if (lfsData.SessionState.NumNodes > 0 && csd.TrackDefinition != null && csd.TrackDefinition.trackLength > 0)
            {
                cgs.PositionAndMotionData.DistanceRoundTrack = ((float)viewedPlayer.Scoring.Node / lfsData.SessionState.NumNodes) * csd.TrackDefinition.trackLength;
            }
            else if (pgs != null)
            {
                cgs.PositionAndMotionData.DistanceRoundTrack = pgs.PositionAndMotionData.DistanceRoundTrack;
            }
            CrewChief.distanceRoundTrack = cgs.PositionAndMotionData.DistanceRoundTrack;

            cgs.PositionAndMotionData.CarSpeed = (float)MathHelper.SpeedToMps(viewedPlayer.Scoring.Speed);

            if (string.IsNullOrEmpty(cgs.TyreData.TyreTypeName))
            {
                cgs.TyreData.TyreTypeName = "Road";
            }

            ////////////////////////////////////
            // Timings
            if (psd != null && !csd.IsNewSession)
            {
                // Preserve current values.
                // Those values change on sector/lap change, otherwise stay the same between updates.
                psd.RestorePlayerTimings(csd);
            }

            processPlayerTimingData(cgs, pgs, lfsData, viewedPlayer);

            csd.SessionTimesAtEndOfSectors =
                pgs != null ? psd.SessionTimesAtEndOfSectors : new SessionData().SessionTimesAtEndOfSectors;

            if (csd.IsNewSector && !csd.IsNewSession)
            {
                switch (csd.SectorNumber)
                {
                    case 1:
                        csd.SessionTimesAtEndOfSectors[3] = csd.SessionRunningTime;
                        break;
                    case 2:
                        csd.SessionTimesAtEndOfSectors[1] = csd.SessionRunningTime;
                        break;
                    case 3:
                        csd.SessionTimesAtEndOfSectors[2] = csd.SessionRunningTime;
                        break;
                }
            }

            csd.LeaderHasFinishedRace = lfsData.SessionState.LeaderFinished;
            csd.LeaderSectorNumber = leaderPlayer.Scoring.Split + 1;

            csd.TimeDeltaFront = calculateTimeDeltaFront(lfsData, viewedPlayer, psd?.TimeDeltaFront);
            csd.TimeDeltaBehind = calculateTimeDeltaBehind(lfsData, viewedPlayer, psd?.TimeDeltaBehind);

            csd.OverallSessionBestLapTime = csd.PlayerLapTimeSessionBest > 0.0f ? csd.PlayerLapTimeSessionBest : -1.0f;
            csd.PlayerClassSessionBestLapTime =
                csd.PlayerLapTimeSessionBest > 0.0f ? csd.PlayerLapTimeSessionBest : -1.0f;


            if (csd.IsNewLap
                && psd != null && !psd.IsNewLap
                && csd.LapTimePrevious > 0.0f
                && csd.PreviousLapWasValid)
            {
                // See if this looks like the last lap of a timed race.
                if (cgs.SessionData.SessionType == SessionType.Race
                    && cgs.SessionData.SessionPhase != SessionPhase.Finished
                    && cgs.SessionData.SessionPhase != SessionPhase.Checkered
                    && csd.SessionHasFixedTime)
                {
                    if (csd.OverallLeaderIsOnLastLap)
                        csd.IsLastLap = true;

                    if (playerScoring.Position == 1
                        && csd.PlayerLapTimeSessionBest > 0.0f
                        && csd.SessionTimeRemaining < csd.PlayerLapTimeSessionBest * 0.90f)
                        csd.IsLastLap = csd.OverallLeaderIsOnLastLap = true;
                }
            }

            try
            { // "Collection was modified; enumeration operation may not execute."
              // --------------------------------
              // Opponent data
                foreach (Player player in lfsData.Players.Values.Where(p => p.PLID != viewedPlayer.PLID))
                {
                    string driverName = CleanDriverName(player.PName, player.Connection?.UName, player.PLID);
                    string opponentKey = player.PLID.ToString();

                    OpponentData opponentPrevious;
                    opponentPrevious =
                        pgs == null ||
                        !pgs.OpponentData.TryGetValue(opponentKey, out opponentPrevious)
                            ? null
                            : opponentPrevious;
                    var opponent = new OpponentData();

                    opponent.DriverRawName = driverName;
                    opponent.DriverNameSet = opponent.DriverRawName.Length > 0;
                    opponent.OverallPosition = player.Scoring.Position > 0 ? player.Scoring.Position : (opponentPrevious != null && opponentPrevious.OverallPosition > 0 ? opponentPrevious.OverallPosition : 0);
                    opponent.ClassPosition = opponent.OverallPosition;
                    opponent.CarClass = CarData.getCarClassForClassNameOrCarName(player.CName);

                    if (opponent.DriverNameSet && opponentPrevious == null && CrewChief.enableDriverNames)
                    {
                        if (!csd.IsNewSession)
                            speechRecogniser?.addNewOpponentName(opponent.DriverRawName, "-1");

                        Console.WriteLine("New driver \"" + driverName +
                                          "\" is using car class " + opponent.CarClass.getClassIdentifier() +
                                          " at position " + opponent.OverallPosition);
                    }

                    // Carry over state
                    if (opponentPrevious != null)
                    {
                        // Copy so that we can safely use previous state.
                        foreach (LapData old in opponentPrevious.OpponentLapData)
                            opponent.OpponentLapData.Add(old);

                        foreach (var old in opponentPrevious.TyreChangesByLap)
                            opponent.TyreChangesByLap.Add(old.Key, old.Value);

                        opponent.NumPitStops = opponentPrevious.NumPitStops;
                        opponent.OverallPositionAtPreviousTick = opponentPrevious.OverallPosition;
                        opponent.ClassPositionAtPreviousTick = opponentPrevious.ClassPosition;
                        opponent.IsLastLap = opponentPrevious.IsLastLap;
                    }

                    if (player.PitData != null)
                    {
                        opponent.NumPitStops = player.PitData.NumStops;
                    }

                    opponent.SessionTimeAtLastPositionChange
                        = opponentPrevious != null && opponentPrevious.OverallPosition != opponent.OverallPosition
                            ? csd.SessionRunningTime
                            : -1.0f;

                    opponent.CompletedLaps = player.Scoring.LapData.Count;
                    opponent.CurrentSectorNumber = player.Scoring.Split + 1;
                    bool isNewSector = csd.IsNewSession || opponentPrevious != null &&
                        opponentPrevious.CurrentSectorNumber != opponent.CurrentSectorNumber;
                    opponent.IsNewLap = (opponentPrevious != null && opponent.CompletedLaps > opponentPrevious.CompletedLaps) ||
                                        (isNewSector && opponent.CurrentSectorNumber == 1 && opponent.CompletedLaps > 0);

                    opponent.Speed = (float)MathHelper.SpeedToMps(player.Scoring.Speed);
                    opponent.WorldPosition = new float[]
                    {
                    (float) MathHelper.LengthToMeters(player.Scoring.X),
                    (float) MathHelper.LengthToMeters(player.Scoring.Y),
                    };

                    opponent.InPits = player.PitData != null && player.PitData.PitLaneFact != PitLaneFact.PITLANE_EXIT;

                    if (lfsData.SessionState.NumNodes > 0 && csd.TrackDefinition != null && csd.TrackDefinition.trackLength > 0)
                    {
                        opponent.DistanceRoundTrack = ((float)player.Scoring.Node / lfsData.SessionState.NumNodes) * csd.TrackDefinition.trackLength;
                    }
                    else if (opponentPrevious != null)
                    {
                        opponent.DistanceRoundTrack = opponentPrevious.DistanceRoundTrack;
                    }

                    if (csd.TrackDefinition != null)
                    {
                        if (opponentPrevious != null && opponentPrevious.DeltaTime != null)
                        {
                            opponent.DeltaTime = opponentPrevious.DeltaTime;
                        }
                        else
                        {
                            opponent.DeltaTime = new DeltaTime(csd.TrackDefinition.trackLength, opponent.DistanceRoundTrack,
                                opponent.Speed, DateTime.UtcNow);
                        }

                        opponent.DeltaTime.SetNextDeltaPoint(opponent.DistanceRoundTrack, opponent.CompletedLaps,
                            opponent.Speed, cgs.Now, !opponent.InPits);
                    }

                    Lap oppLastLap = player.Scoring.GetLastLap();
                    float oppLastLapTime = oppLastLap?.LTime.Ticks > 0 ? (float)oppLastLap.LTime.TotalSeconds : -1.0f;
                    opponent.LastLapTime = oppLastLapTime;

                    if (player.Scoring.LapData.Count > 0)
                    {
                        float best = -1.0f;
                        foreach (var l in player.Scoring.LapData)
                        {
                            if (l.LTime.Ticks > 0)
                            {
                                float t = (float)l.LTime.TotalSeconds;
                                if (best < 0 || t < best)
                                    best = t;
                            }
                        }
                        opponent.CurrentBestLapTime = best;
                    }
                    else if (opponentPrevious != null)
                    {
                        opponent.CurrentBestLapTime = opponentPrevious.CurrentBestLapTime;
                    }

                    if (opponent.IsNewLap)
                    {
                        if (opponent.CompletedLaps > 0 && oppLastLapTime > 0.0f)
                        {
                            opponent.CompleteLapWithProvidedLapTime(
                                opponent.OverallPosition,
                                csd.SessionRunningTime,
                                oppLastLapTime,
                                true,
                                opponent.InPits,
                                false,
                                TRACK_TEMP,
                                AIR_TEMP,
                                csd.SessionHasFixedTime,
                                csd.SessionTimeRemaining,
                                3, cgs.TimingData, CarData.IsCarClassEqual(opponent.CarClass, cgs.carClass));
                        }
                        opponent.StartNewLap(
                            opponent.CompletedLaps + 1,
                            opponent.OverallPosition,
                            opponent.InPits,
                            csd.SessionRunningTime,
                            false,
                            TRACK_TEMP,
                            AIR_TEMP);
                    }
                    else if (opponent.OpponentLapData.Count == 0)
                    {
                        opponent.StartNewLap(
                            opponent.CompletedLaps + 1,
                            opponent.OverallPosition,
                            opponent.InPits,
                            csd.SessionRunningTime,
                            false,
                            TRACK_TEMP,
                            AIR_TEMP);
                    }

                    cgs.OpponentData.Add(opponentKey, opponent);
                }
            }
            catch (Exception ex)
            {
                // Collection was modified during enumeration
                // Ignore and continue
            }

            float overallBest = csd.PlayerLapTimeSessionBest > 0.0f ? csd.PlayerLapTimeSessionBest : -1.0f;
            foreach (var opp in cgs.OpponentData.Values)
            {
                if (opp.CurrentBestLapTime > 0.0f && (overallBest < 0.0f || opp.CurrentBestLapTime < overallBest))
                {
                    overallBest = opp.CurrentBestLapTime;
                }
            }
            csd.OverallSessionBestLapTime = overallBest;

            cgs.sortClassPositions();
            cgs.setPracOrQualiDeltas();

            if (pgs != null)
            {
                csd.SessionStartClassPosition = pgs.SessionData.SessionStartClassPosition;
                csd.ClassPositionAtStartOfCurrentLap = pgs.SessionData.ClassPositionAtStartOfCurrentLap;
                csd.NumCarsInPlayerClassAtStartOfSession = pgs.SessionData.NumCarsInPlayerClassAtStartOfSession;
            }

            ////////////////////////////////////
            // Flags
            if (lfsData.WavedFlag.IsOn)
            {
                switch (lfsData.WavedFlag.Flag)
                {
                    case FlagType.Yellow:
                        csd.Flag = FlagEnum.YELLOW;
                        cgs.FlagData.isLocalYellow = true;
                        if (csd.SectorNumber >= 1 && csd.SectorNumber <= 3)
                        {
                            cgs.FlagData.sectorFlags[csd.SectorNumber - 1] = FlagEnum.YELLOW;
                        }
                        break;
                    case FlagType.Blue:
                        csd.Flag = FlagEnum.BLUE;
                        break;
                    default:
                        csd.Flag = FlagEnum.GREEN;
                        break;
                }
            }
            else
            {
                csd.Flag = FlagEnum.GREEN;
            }

            return cgs;
        }

        public override void versionCheck(object memoryMappedFileStruct)
        {
            LFSData shared = ((LFSInSimReader.LFSStructWrapper) memoryMappedFileStruct).Data;
            int currentVersion = shared.InSimVersion;

            if (currentVersion == 0 || VersionChecked)
            {
                return;
            }

            if (currentVersion < 8)
            {
                throw new GameDataReadException("Live for Speed InSim version " + currentVersion +
                                                " is not supported. Please use InSim version 8, 9, or 10.");
            }

            VersionChecked = true;
        }

        private static SessionType mapToSessionType(LFSData lfsData)
        {
            if (lfsData.SessionState.StateFlags.HasFlag(StateFlags.ISS_REPLAY))
            {
                return lfsData.SessionState.RaceLaps == 0 ? SessionType.Practice : SessionType.Race;
            }

            switch (lfsData.SessionState.RaceInProg)
            {
                case LFSSessionType.RaceOrPractice:
                    return lfsData.SessionState.RaceLaps == 0 ? SessionType.Practice : SessionType.Race;
                case LFSSessionType.Qualifying:
                    return SessionType.Qualify;
                case LFSSessionType.NoRace:
                    if (lfsData.SessionState.StateFlags.HasFlag(StateFlags.ISS_GAME) && lfsData.Players.Count > 0)
                    {
                        return lfsData.SessionState.RaceLaps == 0 ? SessionType.Practice : SessionType.Race;
                    }
                    return SessionType.Unavailable;
                default:
                    return SessionType.Unavailable;
            }
        }

        private static SessionPhase mapToSessionPhase(LFSData lfsData, SessionType sessionType)
        {
            if (sessionType == SessionType.Race)
            {
                if (lfsData.SessionState.PlayerFinished)
                {
                    return SessionPhase.Finished;
                }
                if (lfsData.SessionState.LeaderFinished)
                {
                    return SessionPhase.Checkered;
                }
            }

            if (lfsData.SessionState.StateFlags.HasFlag(StateFlags.ISS_REPLAY))
            {
                return SessionPhase.Green;
            }

            switch (sessionType)
            {
                case SessionType.Race:
                    if (lfsData.SessionState.GreenTime == DateTime.MinValue || lfsData.SessionState.GreenTime.Ticks == 0)
                    {
                        // If we joined mid-race or lights just went green, check if cars are moving or past split
                        bool carsMoving = false;
                        foreach (var player in lfsData.Players.Values)
                        {
                            if (player.Scoring != null && (player.Scoring.Speed > 910 || player.Scoring.LapData.Count > 0 || player.Scoring.Split > 0))
                            {
                                carsMoving = true;
                                break;
                            }
                        }
                        if (carsMoving)
                        {
                            lfsData.SessionState.GreenTime = DateTime.Now;
                            return SessionPhase.Green;
                        }
                        return SessionPhase.Countdown;
                    }
                    return SessionPhase.Green;
                case SessionType.Practice:
                case SessionType.Qualify:
                case SessionType.HotLap:
                    return SessionPhase.Green;
                default:
                    return SessionPhase.Unavailable;
            }
        }

        private static bool sessionHasFixedTime(byte raceLaps)
        {
            return raceLaps >= 191;
        }

        private static int getRaceLapsOrHours(byte raceLaps)
        {
            // 1 to 48 hours
            if (sessionHasFixedTime(raceLaps))
            {
                return raceLaps - 190;
            }

            // Number of laps
            if (raceLaps <= 99)
            {
                return raceLaps;
            }

            // 100 to 1000 laps
            return (raceLaps - 100) * 10 + 100;
        }

        private void processPlayerTimingData(
            GameStateData currentGameState,
            GameStateData previousGameState, LFSData lfsData, Player viewedPlayer)
        {
            GameStateData cgs = currentGameState;
            SessionData csd = cgs.SessionData;
            SessionData psd = previousGameState?.SessionData;

            // Clear all timings on new session and start lap 1.
            if (csd.IsNewSession || csd.PlayerLapData.Count == 0)
            {
                Console.WriteLine("Start initial lap");
                csd.playerStartNewLap(csd.CompletedLaps + 1, csd.OverallPosition, cgs.PitData.InPitlane, csd.SessionRunningTime);
                return;
            }

            PlayerScoring playerScoring = viewedPlayer.Scoring;

            Debug.Assert(psd != null);

            /////////////////////////////////////
            // Current lap timings

            if (viewedPlayer.Scoring.GetLastLap()?.ETime.Ticks > 0 && lfsData.SessionState.GreenTime.Ticks > 0)
            {
                csd.LapTimeCurrent = (float) DateTime.Now.Subtract(lfsData.SessionState.GreenTime)
                    .Subtract(viewedPlayer.Scoring.GetLastLap().ETime).TotalSeconds;
            }
            else if (lfsData.SessionState.GreenTime.Ticks > 0)
            {
                csd.LapTimeCurrent = (float) DateTime.Now.Subtract(lfsData.SessionState.GreenTime).TotalSeconds;
            }

            Lap previousLap = playerScoring.GetLastLap();

            csd.LapTimePrevious = previousLap?.LTime.Ticks > 0 ? (float) previousLap.LTime.TotalSeconds : -1.0f;

            // Last (most current) per-sector times:
            csd.LastSector1Time =
                playerScoring.Split1.STime.Ticks > 0 ? (float) playerScoring.Split1.STime.TotalSeconds : -1.0f;
            csd.LastSector2Time = playerScoring.Split1.STime.Ticks > 0 && playerScoring.Split2.STime.Ticks > 0
                ? (float) playerScoring.Split2.STime.TotalSeconds - (float) playerScoring.Split1.STime.TotalSeconds
                : -1.0f;
            csd.LastSector3Time = playerScoring.Split2.STime.Ticks > 0 && playerScoring.Split3.STime.Ticks > 0
                ? (float) playerScoring.Split3.STime.TotalSeconds -
                  (float) playerScoring.Split2.STime.TotalSeconds
                : -1.0f;

            // TODO: Verify that the lap is valid?
            csd.CurrentLapIsValid = true;

            // Check if timing update is needed.
            if (!csd.IsNewLap && !csd.IsNewSector)
                return;

            /////////////////////////////////////////
            // Update Sector/Lap timings.
            float lastSectorTime = getLastSectorTime(playerScoring, csd.SectorNumber);

            if (csd.IsNewLap)
            {
                if (csd.CompletedLaps > 0 && csd.LapTimePrevious > 0.0f)
                {
                    int numSectors = lfsData.SessionState.NumberOfSectors;
                    float? overrideS1 = null;
                    float? overrideS2 = null;
                    if (numSectors == 1)
                    {
                        overrideS1 = csd.LapTimePrevious / 3.0f;
                        overrideS2 = csd.LapTimePrevious * 2.0f / 3.0f;
                    }
                    else if (numSectors == 2)
                    {
                        float s1 = playerScoring.Split1.STime.Ticks > 0 ? (float)playerScoring.Split1.STime.TotalSeconds : csd.LapTimePrevious / 2.0f;
                        overrideS2 = (csd.LapTimePrevious + s1) / 2.0f;
                    }

                    csd.playerCompleteLapWithProvidedLapTime(
                        csd.OverallPosition,
                        csd.SessionRunningTime,
                        csd.LapTimePrevious,
                        csd.CurrentLapIsValid,
                        cgs.PitData.InPitlane,
                        false,
                        TRACK_TEMP,
                        AIR_TEMP,
                        csd.SessionHasFixedTime,
                        csd.SessionTimeRemaining,
                        3, cgs.TimingData,
                        overrideS1, overrideS2);
                }

                csd.playerStartNewLap(
                    csd.CompletedLaps + 1,
                    csd.OverallPosition,
                    cgs.PitData.InPitlane, // TODO: InPitLane is delayed
                    csd.SessionRunningTime);
            }
            else if (csd.IsNewSector && lastSectorTime > 0.0f)
            {
                int sectorJustCompleted = csd.SectorNumber - 1;
                if (sectorJustCompleted >= 1 && sectorJustCompleted <= 3)
                {
                    csd.playerAddCumulativeSectorData(
                        sectorJustCompleted,
                        csd.OverallPosition,
                        lastSectorTime,
                        csd.SessionRunningTime,
                        csd.CurrentLapIsValid,
                        false,
                        TRACK_TEMP,
                        AIR_TEMP);
                }
            }
        }

        private float getLastSectorTime(PlayerScoring scoring, int currentSector)
        {
            if (scoring == null)
            {
                return -1.0f;
            }

            float lastSectorTime = -1.0f;

            if (currentSector == 1)
            {
                lastSectorTime = scoring.GetLastLap()?.LTime.Ticks > 0
                    ? (float) scoring.GetLastLap().LTime.TotalSeconds
                    : -1.0f;
            }
            else if (currentSector == 2)
            {
                lastSectorTime = scoring.Split1.STime.Ticks > 0
                    ? (float) scoring.Split1.STime.TotalSeconds
                    : -1.0f;
            }
            else if (currentSector == 3)
            {
                lastSectorTime = scoring.Split2.STime.Ticks > 0
                    ? (float) scoring.Split2.STime.TotalSeconds
                    : -1.0f;
            }

            return lastSectorTime;
        }

        private Player getLeaderPlayer(LFSData lfsData)
        {
            if (lfsData == null || lfsData.Players.Count == 0)
                return null;

            // snapshot Values to avoid an exception being thrown because the
            // code enumerates lfsData.Players.Values while that underlying
            // Dictionary is being modified (InvalidOperationException).
            // Likely root cause: Players is shared and updated concurrently
            // (no synchronization) by the reader/writer that populates LFSData. 
            var players = lfsData.Players.Values.ToList();
            Player leader = players.FirstOrDefault(p => p.Scoring != null && p.Scoring.Position == 1);
            if (leader == null)
            {
                leader = lfsData.Players.Values.FirstOrDefault(p => p.Scoring != null);
            }
            return leader;
        }

        private float calculateTimeDeltaFront(LFSData lfsData, Player viewedPlayer, float? previousTimeDeltaFront)
        {
            if (lfsData == null || viewedPlayer?.Scoring == null)
                return -1.0f;

            Player playerAhead =
                lfsData.Players.Values.FirstOrDefault(p => p.Scoring != null && p.Scoring.Position == viewedPlayer.Scoring.Position - 1);

            if (playerAhead == null || playerAhead.Scoring == null)
                return -1.0f;

            if (playerAhead.Scoring.LapData.Count == viewedPlayer.Scoring.LapData.Count &&
                playerAhead.Scoring.Split == viewedPlayer.Scoring.Split &&
                playerAhead.Scoring.Split > 0)
            {
                return (float) viewedPlayer.Scoring.GetCurrentSplit().ETime
                    .Subtract(playerAhead.Scoring.GetCurrentSplit().ETime).TotalSeconds;
            }

            if (playerAhead.Scoring.LapData.Count == viewedPlayer.Scoring.LapData.Count &&
                playerAhead.Scoring.Split == viewedPlayer.Scoring.Split + 1)
            {
                return previousTimeDeltaFront ?? -1.0f;
            }

            return -1.0f;
        }

        private float calculateTimeDeltaBehind(LFSData lfsData, Player viewedPlayer, float? previousTimeDeltaBehind)
        {
            if (lfsData == null || viewedPlayer?.Scoring == null)
                return -1.0f;

            Player playerBehind =
                lfsData.Players.Values.FirstOrDefault(p => p.Scoring != null && p.Scoring.Position == viewedPlayer.Scoring.Position + 1);

            if (playerBehind == null || playerBehind.Scoring == null)
                return -1.0f;

            if (playerBehind.Scoring.LapData.Count == viewedPlayer.Scoring.LapData.Count &&
                playerBehind.Scoring.Split == viewedPlayer.Scoring.Split &&
                playerBehind.Scoring.Split > 0)
            {
                return (float) playerBehind.Scoring.GetCurrentSplit().ETime
                    .Subtract(viewedPlayer.Scoring.GetCurrentSplit().ETime).TotalSeconds;
            }

            if (playerBehind.Scoring.LapData.Count == viewedPlayer.Scoring.LapData.Count &&
                viewedPlayer.Scoring.Split == playerBehind.Scoring.Split + 1)
            {
                return previousTimeDeltaBehind ?? -1.0f;
            }

            return -1.0f;
        }

        public static string CleanDriverName(string rawName, string fallbackUserName = null, byte plid = 0)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                if (!string.IsNullOrWhiteSpace(fallbackUserName))
                {
                    return CleanDriverName(fallbackUserName, null, plid);
                }
                return plid > 0 ? $"Car {plid}" : "Driver";
            }

            // 1. Strip LFS formatting/color codes (^0-9, ^a-z, ^A-Z, etc.)
            string cleaned = Regex.Replace(rawName, @"\^[0-9a-zA-Z#^]", "");

            // 2. Filter out non-ASCII characters (e.g. Japanese Katakana ﾏ, unicode ellipsis …, emoji, etc.)
            var sb = new StringBuilder();
            foreach (char c in cleaned)
            {
                if (c >= 32 && c <= 126)
                {
                    if (c == '|' || c == '~' || c == '`' || c == '<' || c == '>' || c == '[' || c == ']' || c == '{' || c == '}')
                    {
                        sb.Append(' ');
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    sb.Append(' ');
                }
            }
            cleaned = sb.ToString();

            // 3. Replace multiple spaces/underscores with single space
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            // 4. If result has at least 2 alphanumeric characters, use it
            int alphaNumericCount = cleaned.Count(char.IsLetterOrDigit);
            if (alphaNumericCount >= 2)
            {
                return cleaned;
            }

            // Otherwise try fallback UName (LFS username)
            if (!string.IsNullOrWhiteSpace(fallbackUserName))
            {
                string fallbackClean = CleanDriverName(fallbackUserName, null, plid);
                if (fallbackClean.Count(char.IsLetterOrDigit) >= 2)
                {
                    return fallbackClean;
                }
            }

            return plid > 0 ? $"Car {plid}" : "Driver";
        }

        private static readonly Dictionary<string, float> TrackLengths =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "BL1", 3300f }, { "BL1R", 3300f },
            { "BL2", 3307f }, { "BL2R", 3307f },
            { "BL3", 1888f }, { "BL3R", 1888f },
            { "BL4", 1000f },
            { "SO1", 2007f }, { "SO1R", 2007f },
            { "SO2", 2060f }, { "SO2R", 2060f },
            { "SO3", 4049f }, { "SO3R", 4049f },
            { "SO4", 3200f }, { "SO4R", 3200f },
            { "SO5", 2780f }, { "SO5R", 2780f },
            { "SO6", 2380f }, { "SO6R", 2380f },
            { "FE1", 1612f }, { "FE1R", 1612f },
            { "FE2", 3150f }, { "FE2R", 3150f },
            { "FE3", 3550f }, { "FE3R", 3550f },
            { "FE4", 6555f }, { "FE4R", 6555f },
            { "FE5", 2073f }, { "FE5R", 2073f },
            { "FE6", 768f },  { "FE6R", 768f },
            { "AU1", 1000f }, { "AU2", 1000f }, { "AU3", 1000f }, { "AU4", 1000f },
            { "KY1", 3000f }, { "KY1R", 3000f },
            { "KY2", 5144f }, { "KY2R", 5144f },
            { "KY3", 7360f }, { "KY3R", 7360f },
            { "WE1", 5700f }, { "WE1R", 5700f },
            { "WE2", 5727f }, { "WE2R", 5727f },
            { "WE3", 1000f },
            { "AS1", 1948f }, { "AS1R", 1948f },
            { "AS2", 3089f }, { "AS2R", 3089f },
            { "AS3", 5560f }, { "AS3R", 5560f },
            { "AS4", 8170f }, { "AS4R", 8170f },
            { "AS5", 8806f }, { "AS5R", 8806f },
            { "AS6", 7990f }, { "AS6R", 7990f },
            { "AS7", 5218f }, { "AS7R", 5218f },
            { "RO1", 3880f }, { "RO2", 3620f }, { "RO3", 3260f }, { "RO4", 2530f },
            { "RO5", 2380f }, { "RO6", 2010f }, { "RO7", 1970f }, { "RO8", 1950f },
            { "RO9", 2010f }, { "RO10", 3560f }, { "RO11", 3970f }
        };

        public static float GetTrackLength(string trackCode)
        {
            if (string.IsNullOrEmpty(trackCode))
                return 3000f;
            string code = trackCode.Length > 4 ? trackCode.Substring(0, 4) : trackCode;
            if (TrackLengths.TryGetValue(code, out float len))
                return len;
            if (trackCode.Length >= 3 && TrackLengths.TryGetValue(trackCode.Substring(0, 3), out len))
                return len;
            return 3000f;
        }
    }
}