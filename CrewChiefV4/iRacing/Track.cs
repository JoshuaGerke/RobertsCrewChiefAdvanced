using System;
using System.Collections.Generic;
using System.Globalization;
using iRSDKSharp;
namespace CrewChiefV4.iRacing
{
    public class Track
    {
        public Track()
        {
            IsOval = false;
            TrackType = TrackTypes.Unknown;
            FormationLapCount = 0; // green first time round
        }
        public enum TrackTypes : int
        {
            Unknown = 0,
            RoadCourse,
            DirtRoad,
            ShortOval,
            MileOval,
            MediumOval,
            SuperSpeedWay,
            Oval,
            DirtOval, 
        }
        public int Id { get; set; }
        public string Name { get; set; }
        public string CodeName { get; set; }
        public double Length { get; set; }
        public bool NightMode { get; set; }
        public bool IsOval { get; set; }
        public string Category { get; set; }
        public string TrackTypeString { get; set; }
        public TrackTypes TrackType { get; set; }
        public float TrackPitSpeedLimit { get; set; }
        public int FormationLapCount { get; set; }
        public float TrackNorthOffset { get; set; }
        public static double ConvertToRadians(double angle)
        {
            return (Math.PI / 180) * angle;
        }
        public static Track FromSessionInfo(string sessionString)
        {
            var track = new Track();
            track.TrackNorthOffset = 1.57079632f; // + 90deg
            track.Id = Parser.ParseInt(YamlParser.Parse(sessionString, "WeekendInfo:TrackID:"));
            track.Name = YamlParser.Parse(sessionString, "WeekendInfo:TrackDisplayName:");
            track.CodeName = YamlParser.Parse(sessionString, "WeekendInfo:TrackName:");
            track.Length = Parser.ParseTrackLength(YamlParser.Parse(sessionString, "WeekendInfo:TrackLength:"));            
            track.NightMode = YamlParser.Parse(sessionString, "WeekendInfo:NightMode:") == "1";
            track.Category = YamlParser.Parse(sessionString, "WeekendInfo:Category:");
            track.TrackTypeString = YamlParser.Parse(sessionString, "WeekendInfo:TrackType:");
            track.TrackPitSpeedLimit = Parser.ParseTrackPitSpeedLimit(YamlParser.Parse(sessionString, "WeekendInfo:TrackPitSpeedLimit:")) / 3.6f;
            if (track.TrackTypeString.Equals("road course"))
            {
                track.TrackType = TrackTypes.RoadCourse;
            }
            else if (track.TrackTypeString.Equals("dirt road"))
            {
                track.TrackType = TrackTypes.DirtRoad;
            }
            else if (track.TrackTypeString.Equals("dirt oval"))
            {
                track.TrackType = TrackTypes.DirtOval;
                track.IsOval = true;
            }
            else if (track.TrackTypeString.Equals("short oval"))
            {
                track.TrackType = TrackTypes.ShortOval;
                track.FormationLapCount = 1;
                track.IsOval = true;
            }
            else if (track.TrackTypeString.Equals("mile oval"))
            {
                track.TrackType = TrackTypes.MileOval;
                track.IsOval = true;
            }
            else if (track.TrackTypeString.Equals("medium oval"))
            {
                track.TrackType = TrackTypes.MediumOval;
                track.IsOval = true;
            }
            else if (track.TrackTypeString.Equals("super speedway"))
            {
                track.TrackType = TrackTypes.SuperSpeedWay;
                track.IsOval = true;
            }
            // this is here to insure we set a type if none of the above reads true, so hopefully we can catch it this way.
            else if (track.TrackTypeString.Contains("oval"))
            {
                track.TrackType = TrackTypes.Oval;
                track.IsOval = true;
            }
            else if (track.TrackTypeString.Contains("road"))
            {
                track.TrackType = TrackTypes.RoadCourse;
            }
            else
            {
                track.TrackType = TrackTypes.Unknown;
            }
            return track;
        }
        
    }
}
