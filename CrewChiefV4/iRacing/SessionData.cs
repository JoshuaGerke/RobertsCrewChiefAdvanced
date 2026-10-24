using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using iRSDKSharp;
using Newtonsoft.Json;

namespace CrewChiefV4.iRacing
{
    public class SessionData
    {
        public SessionData()
        {
            this.SessionId = -1;
        }

        public Track Track { get; set; }
        public string EventType { get; set; }
        public string SessionType { get; set; }
        public int SessionId { get; set; }
        public int SubsessionId { get; set; }
        public string SessionTimeString { get; set; }
        public string RaceLaps { get; set; }
        public double RaceTime { get; set; }
        public bool IsHeatRacing { get; set; }
        public string IncidentLimitString { get; set; }
        public int IncidentLimit { get; set; }
        public bool IsTeamRacing { get; set; }
        public int NumCarClasses { get; set; }
        public bool StandingStart { get; set; }
        public bool StartOnLeft { get; set; }
        public bool RestartOnLeft { get; set; }
        public string StartingGrid { get; set; }
        public string Restarts { get; set; }
        public string CourseCautions { get; set; }
        public string Category { get; set; }
        public bool hasFullCourseCautions { get; set; }
        public string diskTelemetryFile { get; set; }
        public const string sessionInfoYamlPath = "SessionInfo:Sessions:SessionNum:{{{0}}}{1}:";

        public void Update(string sessionString, int sessionNumber)
        {
            try
            {
                this.Track = Track.FromSessionInfo(sessionString);
                this.SubsessionId = Parser.ParseInt(YamlParser.Parse(sessionString, "WeekendInfo:SubSessionID:"));
                this.SessionId = Parser.ParseInt(YamlParser.Parse(sessionString, "WeekendInfo:SessionID:"));
                this.IsTeamRacing = Parser.ParseInt(YamlParser.Parse(sessionString, "WeekendInfo:TeamRacing:")) == 1;
                this.NumCarClasses = Parser.ParseInt(YamlParser.Parse(sessionString, "WeekendInfo:NumCarClasses:"));
                this.EventType = YamlParser.Parse(sessionString, "WeekendInfo:EventType:");
                this.Category = YamlParser.Parse(sessionString, "WeekendInfo:Category:");
                this.SessionType = YamlParser.Parse(sessionString, string.Format(sessionInfoYamlPath, sessionNumber, "SessionType"));
                this.RaceLaps = YamlParser.Parse(sessionString, string.Format(sessionInfoYamlPath, sessionNumber, "SessionLaps"));
                this.SessionTimeString = YamlParser.Parse(sessionString, string.Format(sessionInfoYamlPath, sessionNumber, "SessionTime"));
                this.IsHeatRacing = Parser.ParseInt(YamlParser.Parse(sessionString, "WeekendInfo:HeatRacing:")) == 1;
                this.RaceTime = Parser.ParseSec(SessionTimeString);
                this.IncidentLimitString = YamlParser.Parse(sessionString, "WeekendInfo:WeekendOptions:IncidentLimit:");
                this.StandingStart = Parser.ParseInt(YamlParser.Parse(sessionString, "WeekendInfo:WeekendOptions:StandingStart:")) == 1;
                this.StartingGrid = YamlParser.Parse(sessionString, "WeekendInfo:WeekendOptions:StartingGrid:");
                this.Restarts = YamlParser.Parse(sessionString, "WeekendInfo:WeekendOptions:Restarts:");
                this.CourseCautions = YamlParser.Parse(sessionString, "WeekendInfo:WeekendOptions:CourseCautions:");

                // the telemetry is too buggy, use bundled data file
                int trackId = Parser.ParseInt(YamlParser.Parse(sessionString, "WeekendInfo:TrackID:"));
                getFormationData().TryGetValue(trackId, out FormationData formation_data);
                if (formation_data != null)
                {
                    Log.Debug(() => "iRacing grid formation workaround found for " + formation_data.track_dirpath.Replace('\\', ' '));
                    this.StartOnLeft = formation_data.start_on_left;
                    this.RestartOnLeft = formation_data.restart_on_left;
                }
                else
                {
                    Log.Warning("iRacing grid formation workaround not available for TrackID=" + trackId);
                    // we used to look at StartingGrid / Restarts but the data is basically junk
                    this.StartOnLeft = true;
                    this.RestartOnLeft = true;
                }

                if (this.Restarts == null)
                {
                    Log.Error("this.Restarts is null");
                    Log.DontSpam($"Error parsing session info for sessionNumber:{sessionNumber}\n{sessionString}", 30).Commentary();
                }
                else if (this.Restarts.Equals("double file lapped cars inside"))
                {
                    // this seems to be one of the few values that we can trust (it's used by the NASCAR '87 official series)
                    // as it seems to actually override the track setting.
                    this.RestartOnLeft = false;
                }

                this.hasFullCourseCautions = CourseCautions.Equals("full");
                this.diskTelemetryFile = YamlParser.Parse(sessionString, "WeekendInfo:TelemetryOptions:TelemetryDiskFile:");
                if (IsLimitedIncidents)
                {
                    IncidentLimit = Parser.ParseInt(IncidentLimitString);
                }
                else
                {
                    IncidentLimit = -1;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error parsing session info for sessionNumber:{sessionNumber}\n{sessionString}");
            }
        }
        public bool IsLimitedSessionLaps
        {
            get
            {
                if ((Category.ToLower() == "dirtroad" || Category.ToLower() == "dirtoval") && RaceLaps.ToLower() != "unlimited")
                    return true;

                return RaceLaps.ToLower() != "unlimited" && !IsHeatRacing;
            }
        }

        public bool IsLimitedTime
        {
            get
            {
                return SessionTimeString.ToLower() != "unlimited";
            }
        }
        public bool IsLimitedIncidents
        {
            get
            {
                return IncidentLimitString.ToLower() != "unlimited";
            }
        }

        // trackId is the key
        private static volatile Dictionary<int, FormationData> formation_data = null;
        private static Dictionary<int, FormationData> getFormationData()
        {
            if (formation_data != null)
            {
                return formation_data;
            }
            String path = DataFiles.default_iracing_formation;
            var lookup = new Dictionary<int, FormationData>();
            if (!File.Exists(path))
            {
                return lookup; // allows users to delete the file to go back to telemetry (if it's fixed)
            }
            var tracks = JsonConvert.DeserializeObject<List<FormationData>>(File.ReadAllText(path));
            foreach (var track in tracks)
            {
                lookup.Add(track.track_id, track);
            }
            formation_data = lookup;
            return formation_data;
        }
    }

    class FormationData
    {
        public String track_dirpath { get; set; }
        public int track_id { get; set; }
        public bool start_on_left { get; set; }
        public bool restart_on_left { get; set; }
    }
}