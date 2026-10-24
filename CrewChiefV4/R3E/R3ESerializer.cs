using Newtonsoft.Json;
using CrewChiefV4.RaceRoom.RaceRoomData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.RaceRoom
{
    class R3ESerializer : GameDataSerializer
    {
        // remove trailing nulls from encoded string byte arrays
        private bool trimNullBytes = true;
        private bool roundRealNumbers = false;
        private int roundToDP;
        private int majorVersion;
        private int minorVersion;

        private Dictionary<string, HashSet<string>> cachedDisabledProperties = new Dictionary<string, HashSet<string>>();

        public R3ESerializer(bool trimNullBytes, int roundToDP, int majorVersion, int minorVersion)
        {
            this.trimNullBytes = trimNullBytes;
            this.roundToDP = roundToDP;
            if (roundToDP > 0)
            {
                this.roundRealNumbers = true;
            }
            this.majorVersion = majorVersion;
            this.minorVersion = minorVersion;
        }

        public string Serialize(Object gameData, String disabledPropertyList)
        {
            HashSet<string> disabledProperties = null;
            if (disabledPropertyList != null && disabledPropertyList.Length > 0)
            {
                if (!cachedDisabledProperties.TryGetValue(disabledPropertyList, out disabledProperties))
                {
                    disabledProperties = new HashSet<string>(disabledPropertyList.Split(','));
                    cachedDisabledProperties.Add(disabledPropertyList, disabledProperties);
                }
            }
            if (gameData == null)
            {
                return "{}";
            }
            try
            {
                RaceRoomShared data = (RaceRoomShared)gameData;
                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);

                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    writer.WriteStartObject();
                    writeProperty(writer, "VersionMajor", this.majorVersion, disabledProperties);
                    writeProperty(writer, "VersionMinor", this.minorVersion, disabledProperties);
                    writeProperty(writer, "DriverDataSize", data.DriverDataSize, disabledProperties);
                    writeProperty(writer, "GameMode", data.GameMode, disabledProperties);
                    writeProperty(writer, "GamePaused", data.GamePaused, disabledProperties);
                    writeProperty(writer, "GameInMenus", data.GameInMenus, disabledProperties);
                    writeProperty(writer, "GameInReplay", data.GameInReplay, disabledProperties);
                    writeProperty(writer, "GameUsingVr", data.GameUsingVr, disabledProperties);
                    if (enabled("Player", disabledProperties))
                    {
                        writer.WritePropertyName("Player");
                        writer.WriteStartObject();
                        writeProperty(writer, "UserId", data.Player.UserId, disabledProperties);
                        writeProperty(writer, "GameSimulationTicks", data.Player.GameSimulationTicks, disabledProperties);
                        writeProperty(writer, "GameSimulationTime", roundRealNumber(data.Player.GameSimulationTime), disabledProperties);
                        writeXYZ(writer, "Position", data.Player.Position.X, data.Player.Position.Y, data.Player.Position.Z, disabledProperties);
                        writeXYZ(writer, "Velocity", data.Player.Velocity.X, data.Player.Velocity.Y, data.Player.Velocity.Z, disabledProperties);
                        writeXYZ(writer, "LocalVelocity", data.Player.LocalVelocity.X, data.Player.LocalVelocity.Y, data.Player.LocalVelocity.Z, disabledProperties);
                        writeXYZ(writer, "Acceleration", data.Player.Acceleration.X, data.Player.Acceleration.Y, data.Player.Acceleration.Z, disabledProperties);
                        writeXYZ(writer, "LocalAcceleration", data.Player.LocalAcceleration.X, data.Player.LocalAcceleration.Y, data.Player.LocalAcceleration.Z, disabledProperties);
                        writeXYZ(writer, "Orientation", data.Player.Orientation.X, data.Player.Orientation.Y, data.Player.Orientation.Z, disabledProperties);
                        writeXYZ(writer, "Rotation", data.Player.Rotation.X, data.Player.Rotation.Y, data.Player.Rotation.Z, disabledProperties);
                        writeXYZ(writer, "AngularAcceleration", data.Player.AngularAcceleration.X, data.Player.AngularAcceleration.Y, data.Player.AngularAcceleration.Z, disabledProperties);
                        writeXYZ(writer, "AngularVelocity", data.Player.AngularVelocity.X, data.Player.AngularVelocity.Y, data.Player.AngularVelocity.Z, disabledProperties);
                        writeXYZ(writer, "LocalAngularVelocity", data.Player.LocalAngularVelocity.X, data.Player.LocalAngularVelocity.Y, data.Player.LocalAngularVelocity.Z, disabledProperties);
                        writeXYZ(writer, "LocalGforce", data.Player.LocalGforce.X, data.Player.LocalGforce.Y, data.Player.LocalGforce.Z, disabledProperties);
                        writeProperty(writer, "SteeringForce", roundRealNumber(data.Player.SteeringForce), disabledProperties);
                        writeProperty(writer, "SteeringForcePercentage", roundRealNumber(data.Player.SteeringForcePercentage), disabledProperties);
                        writeProperty(writer, "EngineTorque", roundRealNumber(data.Player.EngineTorque), disabledProperties);
                        writeProperty(writer, "CurrentDownforce", roundRealNumber(data.Player.CurrentDownforce), disabledProperties);
                        writeProperty(writer, "Voltage", roundRealNumber(data.Player.Voltage), disabledProperties);
                        writeProperty(writer, "ErsLevel", roundRealNumber(data.Player.ErsLevel), disabledProperties);
                        writeProperty(writer, "PowerMguH", roundRealNumber(data.Player.PowerMguH), disabledProperties);
                        writeProperty(writer, "PowerMguK", roundRealNumber(data.Player.PowerMguK), disabledProperties);
                        writeProperty(writer, "TorqueMguK", roundRealNumber(data.Player.TorqueMguK), disabledProperties);
                        writeCorners(writer, "SuspensionDeflection", data.Player.SuspensionDeflection.FrontLeft, data.Player.SuspensionDeflection.FrontRight, data.Player.SuspensionDeflection.RearLeft, data.Player.SuspensionDeflection.RearRight, disabledProperties);
                        writeCorners(writer, "SuspensionVelocity", data.Player.SuspensionVelocity.FrontLeft, data.Player.SuspensionVelocity.FrontRight, data.Player.SuspensionVelocity.RearLeft, data.Player.SuspensionVelocity.RearRight, disabledProperties);
                        writeCorners(writer, "Camber", data.Player.Camber.FrontLeft, data.Player.Camber.FrontRight, data.Player.Camber.RearLeft, data.Player.Camber.RearRight, disabledProperties);
                        writeCorners(writer, "RideHeight", data.Player.RideHeight.FrontLeft, data.Player.RideHeight.FrontRight, data.Player.RideHeight.RearLeft, data.Player.RideHeight.RearRight, disabledProperties);
                        writeProperty(writer, "FrontWingHeight", roundRealNumber(data.Player.FrontWingHeight), disabledProperties);
                        writeProperty(writer, "FrontRollAngle", roundRealNumber(data.Player.FrontRollAngle), disabledProperties);
                        writeProperty(writer, "RearRollAngle", roundRealNumber(data.Player.RearRollAngle), disabledProperties);
                        writeProperty(writer, "ThirdSpringSuspensionDeflectionFront", roundRealNumber(data.Player.ThirdSpringSuspensionDeflectionFront), disabledProperties);
                        writeProperty(writer, "ThirdSpringSuspensionVelocityFront", roundRealNumber(data.Player.ThirdSpringSuspensionVelocityFront), disabledProperties);
                        writeProperty(writer, "ThirdSpringSuspensionDeflectionRear", roundRealNumber(data.Player.ThirdSpringSuspensionDeflectionRear), disabledProperties);
                        writeProperty(writer, "ThirdSpringSuspensionVelocityRear", roundRealNumber(data.Player.ThirdSpringSuspensionVelocityRear), disabledProperties);
                        writer.WriteEndObject();
                    }

                    writeByteArray(writer, "TrackName", data.TrackName, disabledProperties);
                    writeByteArray(writer, "LayoutName", data.LayoutName, disabledProperties);
                    writeProperty(writer, "TrackId", data.TrackId, disabledProperties);
                    writeProperty(writer, "LayoutId", data.LayoutId, disabledProperties);
                    writeProperty(writer, "LayoutLength", roundRealNumber(data.LayoutLength), disabledProperties);
                    writeSectors(writer, "SectorStartFactors", data.SectorStartFactors.Sector1, data.SectorStartFactors.Sector2, data.SectorStartFactors.Sector3, disabledProperties);
                    writeSessions(writer, "RaceSessionLaps", data.RaceSessionLaps.Race1, data.RaceSessionLaps.Race2, data.RaceSessionLaps.Race3, disabledProperties);
                    writeSessions(writer, "RaceSessionMinutes", data.RaceSessionMinutes.Race1, data.RaceSessionMinutes.Race2, data.RaceSessionMinutes.Race3, disabledProperties);
                    writeProperty(writer, "EventIndex", data.EventIndex, disabledProperties);
                    writeProperty(writer, "SessionType", data.SessionType, disabledProperties);
                    writeProperty(writer, "SessionIteration", data.SessionIteration, disabledProperties);
                    writeProperty(writer, "SessionLengthFormat", data.SessionLengthFormat, disabledProperties);
                    writeProperty(writer, "SessionPitSpeedLimit", roundRealNumber(data.SessionPitSpeedLimit), disabledProperties);
                    writeProperty(writer, "SessionPhase", data.SessionPhase, disabledProperties);
                    writeProperty(writer, "StartLights", data.StartLights, disabledProperties);
                    writeProperty(writer, "TireWearActive", data.TireWearActive, disabledProperties);
                    writeProperty(writer, "FuelUseActive", data.FuelUseActive, disabledProperties);
                    writeProperty(writer, "NumberOfLaps", data.NumberOfLaps, disabledProperties);
                    writeProperty(writer, "SessionTimeDuration", roundRealNumber(data.SessionTimeDuration), disabledProperties);
                    writeProperty(writer, "SessionTimeRemaining", roundRealNumber(data.SessionTimeRemaining), disabledProperties);
                    writeProperty(writer, "PitWindowStatus", data.PitWindowStatus, disabledProperties);
                    writeProperty(writer, "PitWindowStart", data.PitWindowStart, disabledProperties);
                    writeProperty(writer, "PitWindowEnd", data.PitWindowEnd, disabledProperties);
                    writeProperty(writer, "InPitlane", data.InPitlane, disabledProperties);
                    writeProperty(writer, "PitMenuSelection", data.PitMenuSelection, disabledProperties);
                    
                    if (enabled("PitMenuState", disabledProperties))
                    {
                        writer.WritePropertyName("PitMenuState");
                        writer.WriteStartObject();
                        writeProperty(writer, "Preset", data.PitMenuState.Preset, disabledProperties);
                        writeProperty(writer, "Penalty", data.PitMenuState.Penalty, disabledProperties);
                        writeProperty(writer, "Driverchange", data.PitMenuState.Driverchange, disabledProperties);
                        writeProperty(writer, "Fuel", data.PitMenuState.Fuel, disabledProperties);
                        writeProperty(writer, "FrontTires", data.PitMenuState.FrontTires, disabledProperties);
                        writeProperty(writer, "RearTires", data.PitMenuState.RearTires, disabledProperties);
                        writeProperty(writer, "Body", data.PitMenuState.Body, disabledProperties);
                        writeProperty(writer, "CancelPitRequest", data.PitMenuState.CancelPitReqest, disabledProperties);
                        writeProperty(writer, "FrontWing", data.PitMenuState.FrontWing, disabledProperties);
                        writeProperty(writer, "RearWing", data.PitMenuState.RearWing, disabledProperties);
                        writeProperty(writer, "Suspension", data.PitMenuState.Suspension, disabledProperties);
                        writeProperty(writer, "RequestPit", data.PitMenuState.RequestPit, disabledProperties);
                        writer.WriteEndObject();
                    }

                    writeProperty(writer, "PitState", data.PitState, disabledProperties);
                    writeProperty(writer, "PitTotalDuration", roundRealNumber(data.PitTotalDuration), disabledProperties);
                    writeProperty(writer, "PitElapsedTime", roundRealNumber(data.PitElapsedTime), disabledProperties);
                    writeProperty(writer, "PitAction", data.PitAction, disabledProperties);
                    writeProperty(writer, "NumPitstopsPerformed", data.NumPitstopsPerformed, disabledProperties);
                    writeProperty(writer, "PitMinDurationTotal", data.PitMinDurationTotal, disabledProperties);
                    writeProperty(writer, "PitMinDurationLeft", data.PitMinDurationLeft, disabledProperties);

                    if (enabled("Flags", disabledProperties))
                    {
                        writer.WritePropertyName("Flags");
                        writer.WriteStartObject();
                        writeProperty(writer, "Yellow", data.Flags.Yellow, disabledProperties);
                        writeProperty(writer, "YellowCausedIt", data.Flags.YellowCausedIt, disabledProperties);
                        writeProperty(writer, "YellowOvertake", data.Flags.YellowOvertake, disabledProperties);
                        writeProperty(writer, "YellowPositionsGained", data.Flags.YellowPositionsGained, disabledProperties);
                        writeSectors(writer, "SectorYellow", data.Flags.SectorYellow.Sector1, data.Flags.SectorYellow.Sector2, data.Flags.SectorYellow.Sector3, disabledProperties);
                        writeProperty(writer, "ClosestYellowDistanceIntoTrack", data.Flags.ClosestYellowDistanceIntoTrack, disabledProperties);
                        writeProperty(writer, "Blue", data.Flags.Blue, disabledProperties);
                        writeProperty(writer, "Black", data.Flags.Black, disabledProperties);
                        writeProperty(writer, "Green", data.Flags.Green, disabledProperties);
                        writeProperty(writer, "Checkered", data.Flags.Checkered, disabledProperties);
                        writeProperty(writer, "White", data.Flags.White, disabledProperties);
                        writeProperty(writer, "BlackAndWhite", data.Flags.BlackAndWhite, disabledProperties);
                        writer.WriteEndObject();
                    }

                    writeProperty(writer, "Position", data.Position, disabledProperties);
                    writeProperty(writer, "PositionClass", data.PositionClass, disabledProperties);
                    writeProperty(writer, "FinishStatus", data.FinishStatus, disabledProperties);
                    writeProperty(writer, "CutTrackWarnings", data.CutTrackWarnings, disabledProperties);

                    if (enabled("Penalties", disabledProperties))
                    {
                        writer.WritePropertyName("Penalties");
                        writer.WriteStartObject();
                        writeProperty(writer, "DriveThrough", data.Penalties.DriveThrough, disabledProperties);
                        writeProperty(writer, "StopAndGo", data.Penalties.StopAndGo, disabledProperties);
                        writeProperty(writer, "PitStop", data.Penalties.PitStop, disabledProperties);
                        writeProperty(writer, "TimeDeduction", data.Penalties.TimeDeduction, disabledProperties);
                        writeProperty(writer, "SlowDown", data.Penalties.SlowDown, disabledProperties);
                        writer.WriteEndObject();
                    }

                    writeProperty(writer, "NumPenalties", data.NumPenalties, disabledProperties);
                    writeProperty(writer, "CompletedLaps", data.CompletedLaps, disabledProperties);
                    writeProperty(writer, "MaxIncidentPoints", data.MaxIncidentPoints, disabledProperties);
                    writeProperty(writer, "CurrentLapValid", data.CurrentLapValid, disabledProperties);
                    writeProperty(writer, "TrackSector", data.TrackSector, disabledProperties);
                    writeProperty(writer, "LapDistance", roundRealNumber(data.LapDistance), disabledProperties);
                    writeProperty(writer, "LapDistanceFraction", roundRealNumber(data.LapDistanceFraction), disabledProperties);
                    writeProperty(writer, "LapTimeBestLeader", roundRealNumber(data.LapTimeBestLeader), disabledProperties);
                    writeProperty(writer, "LapTimeBestLeaderClass", roundRealNumber(data.LapTimeBestLeaderClass), disabledProperties);
                    writeSectors(writer, "SectorTimesSessionBestLap", data.SectorTimesSessionBestLap.Sector1, data.SectorTimesSessionBestLap.Sector2, data.SectorTimesSessionBestLap.Sector3, disabledProperties);
                    writeProperty(writer, "LapTimeBestSelf", roundRealNumber(data.LapTimeBestSelf), disabledProperties);
                    writeSectors(writer, "SectorTimesBestSelf", data.SectorTimesBestSelf.Sector1, data.SectorTimesBestSelf.Sector2, data.SectorTimesBestSelf.Sector3, disabledProperties);
                    writeProperty(writer, "LapTimePreviousSelf", roundRealNumber(data.LapTimePreviousSelf), disabledProperties);
                    writeSectors(writer, "SectorTimesPreviousSelf", data.SectorTimesPreviousSelf.Sector1, data.SectorTimesPreviousSelf.Sector2, data.SectorTimesPreviousSelf.Sector3, disabledProperties);
                    writeProperty(writer, "LapTimeCurrentSelf", roundRealNumber(data.LapTimeCurrentSelf), disabledProperties);
                    writeSectors(writer, "SectorTimesCurrentSelf", data.SectorTimesCurrentSelf.Sector1, data.SectorTimesCurrentSelf.Sector2, data.SectorTimesCurrentSelf.Sector3, disabledProperties);
                    writeProperty(writer, "LapTimeDeltaLeader", roundRealNumber(data.LapTimeDeltaLeader), disabledProperties);
                    writeProperty(writer, "LapTimeDeltaLeaderClass", roundRealNumber(data.LapTimeDeltaLeaderClass), disabledProperties);
                    writeProperty(writer, "TimeDeltaFront", roundRealNumber(data.TimeDeltaFront), disabledProperties);
                    writeProperty(writer, "TimeDeltaBehind", roundRealNumber(data.TimeDeltaBehind), disabledProperties);
                    writeProperty(writer, "TimeDeltaBestSelf", roundRealNumber(data.TimeDeltaBestSelf), disabledProperties);
                    writeSectors(writer, "BestIndividualSectorTimeSelf", data.BestIndividualSectorTimeSelf.Sector1, data.BestIndividualSectorTimeSelf.Sector2, data.BestIndividualSectorTimeSelf.Sector3, disabledProperties);
                    writeSectors(writer, "BestIndividualSectorTimeLeader", data.BestIndividualSectorTimeLeader.Sector1, data.BestIndividualSectorTimeLeader.Sector2, data.BestIndividualSectorTimeLeader.Sector3, disabledProperties);
                    writeSectors(writer, "BestIndividualSectorTimeLeaderClass", data.BestIndividualSectorTimeLeaderClass.Sector1, data.BestIndividualSectorTimeLeaderClass.Sector2, data.BestIndividualSectorTimeLeaderClass.Sector3, disabledProperties);

                    writeProperty(writer, "IncidentPoints", data.IncidentPoints, disabledProperties);
                    writeProperty(writer, "LapValidState", data.LapValidState, disabledProperties);
                    writeProperty(writer, "PrevLapValid", data.PrevLapValid, disabledProperties);

                    if (enabled("VehicleInfo", disabledProperties))
                    {
                        writer.WritePropertyName("VehicleInfo");
                        writer.WriteStartObject();
                        writeByteArray(writer, "Name", data.VehicleInfo.Name, disabledProperties);
                        writeProperty(writer, "CarNumber", data.VehicleInfo.CarNumber, disabledProperties);
                        writeProperty(writer, "ClassId", data.VehicleInfo.ClassId, disabledProperties);
                        writeProperty(writer, "ModelId", data.VehicleInfo.ModelId, disabledProperties);
                        writeProperty(writer, "TeamId", data.VehicleInfo.TeamId, disabledProperties);
                        writeProperty(writer, "LiveryId", data.VehicleInfo.LiveryId, disabledProperties);
                        writeProperty(writer, "ManufacturerId", data.VehicleInfo.ManufacturerId, disabledProperties);
                        writeProperty(writer, "UserId", data.VehicleInfo.UserId, disabledProperties);
                        writeProperty(writer, "SlotId", data.VehicleInfo.SlotId, disabledProperties);
                        writeProperty(writer, "ClassPerformanceIndex", data.VehicleInfo.ClassPerformanceIndex, disabledProperties);
                        writeProperty(writer, "EngineType", data.VehicleInfo.EngineType, disabledProperties);
                        writeProperty(writer, "CarWidth", data.VehicleInfo.CarWidth, disabledProperties);
                        writeProperty(writer, "CarLength", data.VehicleInfo.CarLength, disabledProperties);
                        writeProperty(writer, "Rating", data.VehicleInfo.Rating, disabledProperties);
                        writeProperty(writer, "Reputation", data.VehicleInfo.Reputation, disabledProperties);
                        writer.WriteEndObject();
                    }

                    writeByteArray(writer, "PlayerName", data.PlayerName, disabledProperties);
                    writeProperty(writer, "ControlType", data.ControlType, disabledProperties);
                    writeProperty(writer, "CarSpeed", roundRealNumber(data.CarSpeed), disabledProperties);
                    writeProperty(writer, "EngineRps", roundRealNumber(data.EngineRps), disabledProperties);
                    writeProperty(writer, "MaxEngineRps", roundRealNumber(data.MaxEngineRps), disabledProperties);
                    writeProperty(writer, "UpshiftRps", roundRealNumber(data.UpshiftRps), disabledProperties);
                    writeProperty(writer, "Gear", data.Gear, disabledProperties);
                    writeProperty(writer, "NumGears", data.NumGears, disabledProperties);
                    writeXYZ(writer, "CarCgLocation", data.CarCgLocation.X, data.CarCgLocation.Y, data.CarCgLocation.Z, disabledProperties);
                    write3(writer, "CarOrientation", data.CarOrientation.Pitch, data.CarOrientation.Yaw, data.CarOrientation.Roll, "Pitch", "Yaw", "Roll", disabledProperties);
                    writeXYZ(writer, "LocalAcceleration", data.LocalAcceleration.X, data.LocalAcceleration.Y, data.LocalAcceleration.Z, disabledProperties);
                    writeProperty(writer, "TotalMass", roundRealNumber(data.TotalMass), disabledProperties);
                    writeProperty(writer, "FuelLeft", roundRealNumber(data.FuelLeft), disabledProperties);
                    writeProperty(writer, "FuelCapacity", roundRealNumber(data.FuelCapacity), disabledProperties);
                    writeProperty(writer, "FuelPerLap", roundRealNumber(data.FuelPerLap), disabledProperties);
                    writeProperty(writer, "VirtualEnergyLeft", roundRealNumber(data.VirtualEnergyLeft), disabledProperties);
                    writeProperty(writer, "VirtualEnergyCapacity", roundRealNumber(data.VirtualEnergyCapacity), disabledProperties);
                    writeProperty(writer, "VirtualEnergyPerLap", roundRealNumber(data.VirtualEnergyPerLap), disabledProperties);
                    // renamed from WaterTemp to Temp in the official API:
                    writeProperty(writer, "EngineTemp", roundRealNumber(data.EngineWaterTemp), disabledProperties);
                    writeProperty(writer, "EngineWaterTemp", roundRealNumber(data.EngineWaterTemp), disabledProperties);
                    writeProperty(writer, "EngineOilTemp", roundRealNumber(data.EngineOilTemp), disabledProperties);
                    writeProperty(writer, "FuelPressure", roundRealNumber(data.FuelPressure), disabledProperties);
                    writeProperty(writer, "EngineOilPressure", roundRealNumber(data.EngineOilPressure), disabledProperties);
                    writeProperty(writer, "TurboPressure", roundRealNumber(data.TurboPressure), disabledProperties);
                    writeProperty(writer, "Throttle", roundRealNumber(data.Throttle), disabledProperties);
                    writeProperty(writer, "ThrottleRaw", roundRealNumber(data.ThrottleRaw), disabledProperties);
                    writeProperty(writer, "Brake", roundRealNumber(data.Brake), disabledProperties);
                    writeProperty(writer, "BrakeRaw", roundRealNumber(data.BrakeRaw), disabledProperties);
                    writeProperty(writer, "Clutch", roundRealNumber(data.Clutch), disabledProperties);
                    writeProperty(writer, "ClutchRaw", roundRealNumber(data.ClutchRaw), disabledProperties);
                    writeProperty(writer, "SteerInputRaw", roundRealNumber(data.SteerInputRaw), disabledProperties);
                    writeProperty(writer, "SteerLockDegrees", data.SteerLockDegrees, disabledProperties);
                    writeProperty(writer, "SteerWheelRangeDegrees", data.SteerWheelRangeDegrees, disabledProperties);

                    if (enabled("AidSettings", disabledProperties))
                    {
                        writer.WritePropertyName("AidSettings");
                        writer.WriteStartObject();
                        writeProperty(writer, "Abs", data.AidSettings.Abs, disabledProperties);
                        writeProperty(writer, "Tc", data.AidSettings.Tc, disabledProperties);
                        writeProperty(writer, "Esp", data.AidSettings.Esp, disabledProperties);
                        writeProperty(writer, "Countersteer", data.AidSettings.Countersteer, disabledProperties);
                        writeProperty(writer, "Cornering", data.AidSettings.Cornering, disabledProperties);
                        writer.WriteEndObject();
                    }
                    if (enabled("Drs", disabledProperties))
                    {
                        writer.WritePropertyName("Drs");
                        writer.WriteStartObject();
                        writeProperty(writer, "Equipped", data.Drs.Equipped, disabledProperties);
                        writeProperty(writer, "Available", data.Drs.Available, disabledProperties);
                        writeProperty(writer, "NumActivationsLeft", data.Drs.NumActivationsLeft, disabledProperties);
                        writeProperty(writer, "Engaged", data.Drs.Engaged, disabledProperties);
                        writer.WriteEndObject();
                    }

                    writeProperty(writer, "PitLimiter", data.PitLimiter, disabledProperties);
                    
                    if (enabled("PushToPass", disabledProperties))
                    {
                        writer.WritePropertyName("PushToPass");
                        writer.WriteStartObject();
                        writeProperty(writer, "Available", data.PushToPass.Available, disabledProperties);
                        writeProperty(writer, "Engaged", data.PushToPass.Engaged, disabledProperties);
                        writeProperty(writer, "AmountLeft", data.PushToPass.AmountLeft, disabledProperties);
                        writeProperty(writer, "EngagedTimeLeft", data.PushToPass.EngagedTimeLeft, disabledProperties);
                        writeProperty(writer, "WaitTimeLeft", data.PushToPass.WaitTimeLeft, disabledProperties);
                        writer.WriteEndObject();
                    }
                    writeProperty(writer, "BrakeBias", roundRealNumber(data.BrakeBias), disabledProperties);
                    writeProperty(writer, "DrsNumActivationsTotal", data.DrsNumActivationsTotal, disabledProperties);
                    writeProperty(writer, "PtPNumActivationsTotal", data.PtPNumActivationsTotal, disabledProperties);
                    writeProperty(writer, "BatterySoC", data.BatterySoC, disabledProperties);
                    writeProperty(writer, "WaterLeft", data.WaterLeft, disabledProperties);
                    writeProperty(writer, "AbsSetting", data.AbsSetting, disabledProperties);
                    writeProperty(writer, "HeadLights", data.HeadLights, disabledProperties);

                    writeProperty(writer, "TireType", data.TireType, disabledProperties);
                    writeCorners(writer, "TireRps", data.TireRps.FrontLeft, data.TireRps.FrontRight, data.TireRps.RearLeft, data.TireRps.RearRight, disabledProperties);
                    writeCorners(writer, "TireSpeed", data.TireSpeed.FrontLeft, data.TireSpeed.FrontRight, data.TireSpeed.RearLeft, data.TireSpeed.RearRight, disabledProperties);
                    writeCorners(writer, "TireGrip", data.TireGrip.FrontLeft, data.TireGrip.FrontRight, data.TireGrip.RearLeft, data.TireGrip.RearRight, disabledProperties);
                    writeCorners(writer, "TireWear", data.TireWear.FrontLeft, data.TireWear.FrontRight, data.TireWear.RearLeft, data.TireWear.RearRight, disabledProperties);
                    writeCorners(writer, "TireFlatspot", data.TireFlatspot.FrontLeft, data.TireFlatspot.FrontRight, data.TireFlatspot.RearLeft, data.TireFlatspot.RearRight, disabledProperties);
                    writeCorners(writer, "TirePressure", data.TirePressure.FrontLeft, data.TirePressure.FrontRight, data.TirePressure.RearLeft, data.TirePressure.RearRight, disabledProperties);
                    writeCorners(writer, "TireDirt", data.TireDirt.FrontLeft, data.TireDirt.FrontRight, data.TireDirt.RearLeft, data.TireDirt.RearRight, disabledProperties);
                    writeCorners(writer, "TireLoad", data.TireLoad.FrontLeft, data.TireLoad.FrontRight, data.TireLoad.RearLeft, data.TireLoad.RearRight, disabledProperties);

                    if (enabled("TireTemp", disabledProperties))
                    {
                        writer.WritePropertyName("TireTemp");
                        writer.WriteStartObject();
                        {
                            writer.WritePropertyName("FrontLeft");
                            writer.WriteStartObject();
                            {
                                writer.WritePropertyName("CurrentTemp");
                                writer.WriteStartObject();
                                {
                                    writeProperty(writer, "Left", data.TireTemp.FrontLeft.CurrentTemp.Left, disabledProperties);
                                    writeProperty(writer, "Center", data.TireTemp.FrontLeft.CurrentTemp.Center, disabledProperties);
                                    writeProperty(writer, "Right", data.TireTemp.FrontLeft.CurrentTemp.Right, disabledProperties);
                                    writer.WriteEndObject();
                                }
                                writeProperty(writer, "OptimalTemp", data.TireTemp.FrontLeft.OptimalTemp, disabledProperties);
                                writeProperty(writer, "ColdTemp", data.TireTemp.FrontLeft.ColdTemp, disabledProperties);
                                writeProperty(writer, "HotTemp", data.TireTemp.FrontLeft.HotTemp, disabledProperties);
                            }
                            writer.WriteEndObject();
                        }
                        {
                            writer.WritePropertyName("FrontRight");
                            writer.WriteStartObject();
                            {
                                writer.WritePropertyName("CurrentTemp");
                                writer.WriteStartObject();
                                {
                                    writeProperty(writer, "Left", data.TireTemp.FrontRight.CurrentTemp.Left, disabledProperties);
                                    writeProperty(writer, "Center", data.TireTemp.FrontRight.CurrentTemp.Center, disabledProperties);
                                    writeProperty(writer, "Right", data.TireTemp.FrontRight.CurrentTemp.Right, disabledProperties);
                                    writer.WriteEndObject();
                                }
                                writeProperty(writer, "OptimalTemp", data.TireTemp.FrontRight.OptimalTemp, disabledProperties);
                                writeProperty(writer, "ColdTemp", data.TireTemp.FrontRight.ColdTemp, disabledProperties);
                                writeProperty(writer, "HotTemp", data.TireTemp.FrontRight.HotTemp, disabledProperties);
                            }
                            writer.WriteEndObject();
                        }
                        {
                            writer.WritePropertyName("RearLeft");
                            writer.WriteStartObject();
                            {
                                writer.WritePropertyName("CurrentTemp");
                                writer.WriteStartObject();
                                {
                                    writeProperty(writer, "Left", data.TireTemp.RearLeft.CurrentTemp.Left, disabledProperties);
                                    writeProperty(writer, "Center", data.TireTemp.RearLeft.CurrentTemp.Center, disabledProperties);
                                    writeProperty(writer, "Right", data.TireTemp.RearLeft.CurrentTemp.Right, disabledProperties);
                                    writer.WriteEndObject();
                                }
                                writeProperty(writer, "OptimalTemp", data.TireTemp.RearLeft.OptimalTemp, disabledProperties);
                                writeProperty(writer, "ColdTemp", data.TireTemp.RearLeft.ColdTemp, disabledProperties);
                                writeProperty(writer, "HotTemp", data.TireTemp.RearLeft.HotTemp, disabledProperties);
                            }
                            writer.WriteEndObject();
                        }
                        {
                            writer.WritePropertyName("RearRight");
                            writer.WriteStartObject();
                            {
                                writer.WritePropertyName("CurrentTemp");
                                writer.WriteStartObject();
                                {
                                    writeProperty(writer, "Left", data.TireTemp.RearRight.CurrentTemp.Left, disabledProperties);
                                    writeProperty(writer, "Center", data.TireTemp.RearRight.CurrentTemp.Center, disabledProperties);
                                    writeProperty(writer, "Right", data.TireTemp.RearRight.CurrentTemp.Right, disabledProperties);
                                    writer.WriteEndObject();
                                }
                                writeProperty(writer, "OptimalTemp", data.TireTemp.RearRight.OptimalTemp, disabledProperties);
                                writeProperty(writer, "ColdTemp", data.TireTemp.RearRight.ColdTemp, disabledProperties);
                                writeProperty(writer, "HotTemp", data.TireTemp.RearRight.HotTemp, disabledProperties);
                            }
                            writer.WriteEndObject();
                        }
                        writer.WriteEndObject();
                    }

                    writeProperty(writer, "TireTypeFront", data.TireTypeFront, disabledProperties);
                    writeProperty(writer, "TireTypeRear", data.TireTypeRear, disabledProperties);
                    writeProperty(writer, "TireSubtypeFront", data.TireSubtypeFront, disabledProperties);
                    writeProperty(writer, "TireSubtypeRear", data.TireSubtypeRear, disabledProperties);

                    if (enabled("BrakeTemp", disabledProperties))
                    {
                        writer.WritePropertyName("BrakeTemp");
                        writer.WriteStartObject();
                        {
                            writer.WritePropertyName( "FrontLeft");
                            writer.WriteStartObject();
                            {
                                writeProperty(writer, "CurrentTemp", data.BrakeTemp.FrontLeft.CurrentTemp, disabledProperties);
                                writeProperty(writer, "OptimalTemp", data.BrakeTemp.FrontLeft.OptimalTemp, disabledProperties);
                                writeProperty(writer, "ColdTemp", data.BrakeTemp.FrontLeft.ColdTemp, disabledProperties);
                                writeProperty(writer, "HotTemp", data.BrakeTemp.FrontLeft.HotTemp, disabledProperties);
                                writer.WriteEndObject();
                            }
                            writer.WritePropertyName("FrontRight");
                            writer.WriteStartObject();
                            {
                                writeProperty(writer, "CurrentTemp", data.BrakeTemp.FrontRight.CurrentTemp, disabledProperties);
                                writeProperty(writer, "OptimalTemp", data.BrakeTemp.FrontRight.OptimalTemp, disabledProperties);
                                writeProperty(writer, "ColdTemp", data.BrakeTemp.FrontRight.ColdTemp, disabledProperties);
                                writeProperty(writer, "HotTemp", data.BrakeTemp.FrontRight.HotTemp, disabledProperties);
                                writer.WriteEndObject();
                            }
                            writer.WritePropertyName("RearLeft");
                            writer.WriteStartObject();
                            {
                                writeProperty(writer, "CurrentTemp", data.BrakeTemp.RearLeft.CurrentTemp, disabledProperties);
                                writeProperty(writer, "OptimalTemp", data.BrakeTemp.RearLeft.OptimalTemp, disabledProperties);
                                writeProperty(writer, "ColdTemp", data.BrakeTemp.RearLeft.ColdTemp, disabledProperties);
                                writeProperty(writer, "HotTemp", data.BrakeTemp.RearLeft.HotTemp, disabledProperties);
                                writer.WriteEndObject();
                            }
                            writer.WritePropertyName("RearRight");
                            writer.WriteStartObject();
                            {
                                writeProperty(writer, "CurrentTemp", data.BrakeTemp.RearRight.CurrentTemp, disabledProperties);
                                writeProperty(writer, "OptimalTemp", data.BrakeTemp.RearRight.OptimalTemp, disabledProperties);
                                writeProperty(writer, "ColdTemp", data.BrakeTemp.RearRight.ColdTemp, disabledProperties);
                                writeProperty(writer, "HotTemp", data.BrakeTemp.RearRight.HotTemp, disabledProperties);
                                writer.WriteEndObject();
                            }
                        }
                        writer.WriteEndObject();
                    }

                    writeCorners(writer, "BrakePressure", data.BrakePressure.FrontLeft, data.BrakePressure.FrontRight, data.BrakePressure.RearLeft, data.BrakePressure.RearRight, disabledProperties);

                    writeProperty(writer, "TractionControlSetting", data.TractionControlSetting, disabledProperties);
                    writeProperty(writer, "EngineMapSetting", data.EngineMapSetting, disabledProperties);
                    writeProperty(writer, "EngineBrakeSetting", data.EngineBrakeSetting, disabledProperties);
                    writeProperty(writer, "TractionControlPercent", data.TractionControlPercent, disabledProperties);

                    if (enabled("CarDamage", disabledProperties))
                    {
                        writer.WritePropertyName("CarDamage");
                        writer.WriteStartObject();
                        writeProperty(writer, "Engine", roundRealNumber(data.CarDamage.Engine), disabledProperties);
                        writeProperty(writer, "Transmission", roundRealNumber(data.CarDamage.Transmission), disabledProperties);
                        writeProperty(writer, "Aerodynamics", roundRealNumber(data.CarDamage.Aerodynamics), disabledProperties);
                        writeProperty(writer, "Suspension", roundRealNumber(data.CarDamage.Suspension), disabledProperties);
                        writer.WriteEndObject();
                    }

                    writeProperty(writer, "NumCars", data.NumCars, disabledProperties);
                    if (enabled("DriverData", disabledProperties))
                    {
                        writer.WritePropertyName("DriverData");
                        writer.WriteStartArray();
                        for (int i = 0; i < data.NumCars; i++)
                        {
                            if (i < data.DriverData.Length)
                            {
                                addDriverData(writer, data.DriverData[i], disabledProperties);
                            }
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                Log.Exception(e, "Unable to serialize game data: ");
                return "{}";
            }
        }

        private void addDriverData(JsonWriter writer, DriverData driverData, HashSet<string> disabledProperties)
        {
            if (enabled("DriverInfo", disabledProperties))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("DriverInfo");
                writer.WriteStartObject();
                writeByteArray(writer, "Name", driverData.DriverInfo.Name, disabledProperties);
                writeProperty(writer, "CarNumber", driverData.DriverInfo.CarNumber, disabledProperties);
                writeProperty(writer, "ClassId", driverData.DriverInfo.ClassId, disabledProperties);
                writeProperty(writer, "ModelId", driverData.DriverInfo.ModelId, disabledProperties);
                writeProperty(writer, "TeamId", driverData.DriverInfo.TeamId, disabledProperties);
                writeProperty(writer, "LiveryId", driverData.DriverInfo.LiveryId, disabledProperties);
                writeProperty(writer, "ManufacturerId", driverData.DriverInfo.ManufacturerId, disabledProperties);
                writeProperty(writer, "UserId", driverData.DriverInfo.UserId, disabledProperties);
                writeProperty(writer, "SlotId", driverData.DriverInfo.SlotId, disabledProperties);
                writeProperty(writer, "ClassPerformanceIndex", driverData.DriverInfo.ClassPerformanceIndex, disabledProperties);
                writeProperty(writer, "EngineType", driverData.DriverInfo.EngineType, disabledProperties);
                writer.WriteEndObject();
            }
            writeProperty(writer, "FinishStatus", driverData.FinishStatus, disabledProperties);
            writeProperty(writer, "Place", driverData.Place, disabledProperties);
            writeProperty(writer, "PlaceClass", driverData.PlaceClass, disabledProperties);
            writeProperty(writer, "LapDistance", roundRealNumber(driverData.LapDistance), disabledProperties);
            writeProperty(writer, "LapDistanceFraction", roundRealNumber(driverData.LapDistanceFraction), disabledProperties);
            writeXYZ(writer, "Position", driverData.Position.X, driverData.Position.Y, driverData.Position.Z, disabledProperties);
            writeProperty(writer, "TrackSector", driverData.TrackSector, disabledProperties);
            writeProperty(writer, "CompletedLaps", driverData.CompletedLaps, disabledProperties);
            writeProperty(writer, "CurrentLapValid", driverData.CurrentLapValid, disabledProperties);
            writeProperty(writer, "LapTimeCurrentSelf", roundRealNumber(driverData.LapTimeCurrentSelf), disabledProperties);
            writeSectors(writer, "SectorTimeCurrentSelf", driverData.SectorTimeCurrentSelf.Sector1, driverData.SectorTimeCurrentSelf.Sector2, driverData.SectorTimeCurrentSelf.Sector3, disabledProperties);
            writeSectors(writer, "SectorTimePreviousSelf", driverData.SectorTimePreviousSelf.Sector1, driverData.SectorTimePreviousSelf.Sector2, driverData.SectorTimePreviousSelf.Sector3, disabledProperties);
            writeSectors(writer, "SectorTimeBestSelf", driverData.SectorTimeBestSelf.Sector1, driverData.SectorTimeBestSelf.Sector2, driverData.SectorTimeBestSelf.Sector3, disabledProperties);
            writeProperty(writer, "TimeDeltaFront", roundRealNumber(driverData.TimeDeltaFront), disabledProperties);
            writeProperty(writer, "TimeDeltaBehind", roundRealNumber(driverData.TimeDeltaBehind), disabledProperties);
            writeProperty(writer, "PitStopStatus", driverData.PitStopStatus, disabledProperties);
            writeProperty(writer, "InPitlane", driverData.InPitlane, disabledProperties);
            writeProperty(writer, "NumPitstops", driverData.NumPitstops, disabledProperties);

            if (enabled("Penalties", disabledProperties))
            {
                writer.WritePropertyName("Penalties");
                writer.WriteStartObject();
                writeProperty(writer, "DriveThrough", driverData.Penalties.DriveThrough, disabledProperties);
                writeProperty(writer, "StopAndGo", driverData.Penalties.StopAndGo, disabledProperties);
                writeProperty(writer, "PitStop", driverData.Penalties.PitStop, disabledProperties);
                writeProperty(writer, "TimeDeduction", driverData.Penalties.TimeDeduction, disabledProperties);
                writeProperty(writer, "SlowDown", driverData.Penalties.SlowDown, disabledProperties);
                writer.WriteEndObject();
            }
            writeProperty(writer, "CarSpeed", roundRealNumber(driverData.CarSpeed), disabledProperties);
            writeProperty(writer, "TireTypeFront", driverData.TireTypeFront, disabledProperties);
            writeProperty(writer, "TireTypeRear", driverData.TireTypeRear, disabledProperties);
            writeProperty(writer, "TireSubtypeFront", driverData.TireSubtypeFront, disabledProperties);
            writeProperty(writer, "TireSubtypeRear", driverData.TireSubtypeRear, disabledProperties);
            writeProperty(writer, "BasePenaltyWeight", roundRealNumber(driverData.BasePenaltyWeight), disabledProperties);
            writeProperty(writer, "AidPenaltyWeight", roundRealNumber(driverData.AidPenaltyWeight), disabledProperties);
            writeProperty(writer, "DrsState", driverData.DrsState, disabledProperties);
            writeProperty(writer, "PtpState", driverData.PtpState, disabledProperties);
            writeProperty(writer, "VirtualEnergy", driverData.VirtualEnergy, disabledProperties);
            writeProperty(writer, "PenaltyType", driverData.PenaltyType, disabledProperties);
            writeProperty(writer, "PenaltyReason", driverData.PenaltyReason, disabledProperties);
            writeProperty(writer, "EngineState", driverData.EngineState, disabledProperties);
            writer.WriteEndObject();
        }

        // a few utility methods-----

        private bool enabled(String property, HashSet<string> disabledProperties)
        {
            return disabledProperties == null || !disabledProperties.Contains(property);
        }

        private void writeProperty(JsonWriter writer, String property, Object value, HashSet<string> disabledProperties)
        {
            if (enabled(property, disabledProperties))
            {
                writer.WritePropertyName(property); writer.WriteValue(value);
            }
        }

        // write a byte array, trimming redundant null chars off the end
        private void writeByteArray(JsonWriter writer, String property, byte[] rawData, HashSet<string> disabledProperties)
        {
            if (enabled(property, disabledProperties))
            {
                writer.WritePropertyName(property);
                if (rawData == null)
                {
                    writer.WriteValue(new byte[] { 0 });
                }
                else if (this.trimNullBytes)
                {
                    // count back until we find the first non-null byte
                    int i = rawData.Length - 1;
                    while (i >= 0 && rawData[i] == 0)
                    {
                        --i;
                    }
                    if (i < 0)
                    {
                        // there are no non-null bytes in the array
                        writer.WriteValue(new byte[] { 0 });
                    }
                    else
                    {
                        // now rawData[i] is the last non-zero byte
                        byte[] trimmed = new byte[i + 1];
                        Array.Copy(rawData, trimmed, i + 1);
                        writer.WriteValue(trimmed);
                    }
                }
                else
                {
                    writer.WriteValue(rawData);
                }
            }
        }

        // XYZ data, rounded and formatted real numbers
        private void writeXYZ(JsonWriter writer, String property, double x, double y, double z, HashSet<string> disabledProperties)
        {
            write3(writer, property, roundRealNumber(x), roundRealNumber(y), roundRealNumber(z), "X", "Y", "Z", disabledProperties);
        }

        // XYZ data, integers
        private void writeXYZ(JsonWriter writer, String property, int x, int y, int z, HashSet<string> disabledProperties)
        {
            write3(writer, property, x, y, z, "X", "Y", "Z", disabledProperties);
        }

        // sector data, rounded and formatted real numbers
        private void writeSectors(JsonWriter writer, String property, double x, double y, double z, HashSet<string> disabledProperties)
        {
            write3(writer, property, roundRealNumber(x), roundRealNumber(y), roundRealNumber(z), "Sector1", "Sector2", "Sector3", disabledProperties);
        }

        // sector data, integers
        private void writeSectors(JsonWriter writer, String property, int x, int y, int z, HashSet<string> disabledProperties)
        {
            write3(writer, property, x, y, z, "Sector1", "Sector2", "Sector3", disabledProperties);
        }

        private void writeSessions(JsonWriter writer, String property, object x, object y, object z, HashSet<string> disabledProperties)
        {
            write3(writer, property, x, y, z, "Race1", "Race2", "Race3", disabledProperties);
        }

        private void write3(JsonWriter writer, String property, object x, object y, object z, String prop1, String prop2, String prop3, HashSet<string> disabledProperties)
        {
            if (enabled(property, disabledProperties))
            {
                writer.WritePropertyName(property);
                writer.WriteStartObject();
                writer.WritePropertyName(prop1); writer.WriteValue(x);
                writer.WritePropertyName(prop2); writer.WriteValue(y);
                writer.WritePropertyName(prop3); writer.WriteValue(z);
                writer.WriteEndObject();
            }
        }

        // corners, formatted rounded real numbers
        private void writeCorners(JsonWriter writer, String property, double frontLeft, double frontRight, double rearLeft, double rearRight, HashSet<string> disabledProperties)
        {
            write4(writer, property, roundRealNumber(frontLeft), roundRealNumber(frontRight), roundRealNumber(rearLeft), roundRealNumber(rearRight),
                "FrontLeft", "FrontRight", "RearLeft", "RearRight", disabledProperties);
        }

        // corners, ints
        private void writeCorners(JsonWriter writer, String property, int frontLeft, int frontRight, int rearLeft, int rearRight, HashSet<string> disabledProperties)
        {
            write4(writer, property, roundRealNumber(frontLeft), roundRealNumber(frontRight), roundRealNumber(rearLeft), roundRealNumber(rearRight),
               "FrontLeft", "FrontRight", "RearLeft", "RearRight", disabledProperties);
        }

        private void write4(JsonWriter writer, String property, object a, object b, object c, object d,
            String prop1, String prop2, String prop3, String prop4, HashSet<string> disabledProperties)
        {
            if (enabled(property, disabledProperties))
            {
                writer.WritePropertyName(property);
                writer.WriteStartObject();
                writer.WritePropertyName(prop1); writer.WriteValue(a);
                writer.WritePropertyName(prop2); writer.WriteValue(b);
                writer.WritePropertyName(prop3); writer.WriteValue(c);
                writer.WritePropertyName(prop4); writer.WriteValue(d);
                writer.WriteEndObject();
            }
        }

        // rounding
        private double roundRealNumber(double input)
        {
            if (roundRealNumbers)
            {
                return Math.Round(input, roundToDP);
            }
            else
            {
                return input;
            }
        }
    }
}
