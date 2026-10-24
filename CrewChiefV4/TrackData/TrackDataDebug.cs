using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4
{
#if DEBUG
    public partial class TrackData
    {
        #region DuplicateTrackNamesCheck
        /// <summary>
        /// This is only called in debug builds and just reports any duplicate
        /// track names, it doesn't do anything to trackLandmarksData
        /// </summary>
        internal static void checkForDuplicates()
        {
            // Why are there multiple ...Tracks dictionaries?  Unless... it was intended to loop checking all games? Even that doesn't make much sense
            var acTracks = new Dictionary<string, float>();
            var aceTracks = new Dictionary<string, float>();
            var pcarsTracks = new Dictionary<string, float>();
            var pcars2Tracks = new Dictionary<string, float>();
            var ams2Tracks = new Dictionary<string, float>();
            var raceroomTracks = new Dictionary<int, float>();
            var rf1Tracks = new Dictionary<string, float>();
            var rf2Tracks = new Dictionary<string, float>();
            var lmuTracks = new Dictionary<string, float>();
            var gtr2Tracks = new Dictionary<string, float>();
            var irTracks = new Dictionary<string, float>();
            var accTracks = new Dictionary<string, float>();
            List<GameEnum> gameList = new List<GameEnum> {
                GameEnum.ASSETTO_64BIT,
                GameEnum.ASSETTO_EVO,
                GameEnum.PCARS_64BIT,
                GameEnum.PCARS2,
                GameEnum.AMS2,
                GameEnum.RACE_ROOM,
                GameEnum.RF1,
                GameEnum.RF2_64BIT,
                GameEnum.LMU,
                GameEnum.GTR2,
                GameEnum.IRACING,
                GameEnum.ACC
            };
            foreach (var gameEnum in gameList)
            {
                foreach (var trackLandmarks in TRACK_LANDMARKS_DATA.trackLandmarksData)
                {
                    switch (gameEnum)
                    {
                        case GameEnum.ASSETTO_64BIT:
                            checkForDuplicatesHelper(trackLandmarks.acTrackNames, trackLandmarks.approximateTrackLength, "AC", acTracks);
                            break;
                        case GameEnum.ASSETTO_EVO:
                            checkForDuplicatesHelper(trackLandmarks.aceTrackNames, trackLandmarks.approximateTrackLength, "ACE", aceTracks);
                            break;
                        case GameEnum.PCARS_64BIT:
                            checkForDuplicatesHelper(trackLandmarks.pcarsTrackName, trackLandmarks.approximateTrackLength, "pCars", pcarsTracks);
                            break;
                        case GameEnum.PCARS2:
                            checkForDuplicatesHelper(trackLandmarks.pcars2TrackName, trackLandmarks.approximateTrackLength, "pCars2", pcars2Tracks);
                            break;
                        case GameEnum.AMS2:
                            checkForDuplicatesHelper(trackLandmarks.ams2TrackName, trackLandmarks.approximateTrackLength, "ams2", ams2Tracks);
                            break;
                        case GameEnum.RACE_ROOM:
                            checkForDuplicatesHelper(trackLandmarks.raceroomLayoutId, trackLandmarks.approximateTrackLength, "RaceRoom", raceroomTracks);
                            break;
                        case GameEnum.RF1:
                            checkForDuplicatesHelper(trackLandmarks.rf1TrackNames, trackLandmarks.approximateTrackLength, "rF1", rf1Tracks);
                            break;
                        case GameEnum.RF2_64BIT:
                            checkForDuplicatesHelper(trackLandmarks.rf2TrackNames, trackLandmarks.approximateTrackLength, "rF2", rf2Tracks);
                            break;
                        case GameEnum.LMU:
                            checkForDuplicatesHelper(trackLandmarks.lmuTrackName, trackLandmarks.approximateTrackLength, "LMU", lmuTracks);
                            break;
                        case GameEnum.GTR2:
                            checkForDuplicatesHelper(trackLandmarks.gtr2TrackNames, trackLandmarks.approximateTrackLength, "GTR 2", gtr2Tracks);
                            break;
                        case GameEnum.IRACING:
                            checkForDuplicatesHelper(trackLandmarks.irTrackName, trackLandmarks.approximateTrackLength, "iRacing", irTracks);
                            break;
                        case GameEnum.ACC:
                            checkForDuplicatesHelper(trackLandmarks.accTrackName, trackLandmarks.approximateTrackLength, "ACC", accTracks);
                            break;
                    }
                }
            }
        }

        private static void checkForDuplicatesHelper(string[] trackNames, float approximateTrackLength, string gameName, Dictionary<string, float> gameTracksMappedSoFar)
        {
            if (trackNames != null)
            {
                foreach (var trackName in trackNames)
                {
                    checkForDuplicatesHelper(trackName, approximateTrackLength, gameName, gameTracksMappedSoFar);
                }
            }
        }

        private static void checkForDuplicatesHelper(string trackName, float approximateTrackLength, string gameName, Dictionary<string, float> gameTracksMappedSoFar)
        {
            if (!string.IsNullOrWhiteSpace(trackName))
            {
                if (!gameTracksMappedSoFar.ContainsKey(trackName))
                {
                    gameTracksMappedSoFar.Add(trackName, approximateTrackLength);
                }
                else
                {

                    Log.Warning("DEBUG BUILDS ONLY: Multiple Track Definitions found for " + gameName + " track: "
                        + trackName + " Approx length: " + approximateTrackLength.ToString("0.000")
                        + "  Duplicate approx length: " + gameTracksMappedSoFar[trackName].ToString("0.000"));
                    if (approximateTrackLength != gameTracksMappedSoFar[trackName])
                    {
                        Log.Commentary("Probably not a problem as approximate lengths are different");
                    }
                }
            }
        }

        private static void checkForDuplicatesHelper(int raceroomTrackId, float approximateTrackLength, string gameName, Dictionary<int, float> gameTracksMappedSoFar)
        {
            if (raceroomTrackId != -1)
            {
                if (!gameTracksMappedSoFar.ContainsKey(raceroomTrackId))
                {
                    gameTracksMappedSoFar.Add(raceroomTrackId, approximateTrackLength);
                }
                else
                {
                    Log.Warning("DEBUG BUILDS ONLY: Multiple Track Definitions found for " + gameName + " track: "
                        + raceroomTrackId + " Approx length: " + approximateTrackLength.ToString("0.000")
                        + "  Duplicate approx length: " + gameTracksMappedSoFar[raceroomTrackId].ToString("0.000"));
                }
            }
        }
        #endregion DuplicateTrackNamesCheck

        #region MergeAndSortTrackLandmarksData
        /// <summary>
        /// In debug builds get any user created trackLandmarkData files
        /// in the debug and release doc folders, merge them in, sort the
        /// result and save into the default trackLandmarksData.json file
        /// to be submitted to the repo.
        /// </summary>
        internal static void UpdateDefaultTrackLandmarks()
        {
            ProcessDirectory(DataFiles.track_landmarksFolder_release_build);

            SortTracks(TRACK_LANDMARKS_DATA);

            JsonFiles.WriteFilteredFile(DataFiles.default_track_landmarks, TRACK_LANDMARKS_DATA);
        }

        internal static void SortTracks(TrackLandmarksData TRACK_LANDMARKS_DATA)
        {
            TRACK_LANDMARKS_DATA.trackLandmarksData =
                TRACK_LANDMARKS_DATA.trackLandmarksData
                    .GroupBy(GetDuplicateKey)
                    .Select(g => g.First())
                    .ToList();

            TRACK_LANDMARKS_DATA.trackLandmarksData =
                TRACK_LANDMARKS_DATA.trackLandmarksData
                    .GroupBy(GetDuplicateKey)
                    .Select(g => g.First())
                    .ToList();
            
            TRACK_LANDMARKS_DATA.trackLandmarksData =
            TRACK_LANDMARKS_DATA.trackLandmarksData
            .Select(t => new
            {
                Track = t,
                SortKey = GetSortKey(t)
            })
            .OrderBy(x => x.SortKey == null ? 1 : 0)
            .ThenBy(x => x.SortKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Track.raceroomLayoutId)
            .Select(x => x.Track)
            .ToList();
        }

        private static string NormalizeForSort(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.TrimStart();

            // Assetto Corsa stock track prefix
            if (value.StartsWith("ks_", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(3);
            }

            // Find first letter
            int start = 0;
            while (start < value.Length && !char.IsLetter(value[start]))
            {
                start++;
            }

            // No letters anywhere (e.g. "1234", "99", "--42")
            if (start >= value.Length)
                return null;

            value = value.Substring(start);

            // Remove accents/diacritics
            string normalized = value.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString()
                .Normalize(NormalizationForm.FormC)
                .ToUpperInvariant();
        }
        private static string GetSortKey(TrackLandmarksForTrack track)
        {
            var names = new List<string>();

            void AddNames(IEnumerable<string> values)
            {
                if (values != null)
                {
                    names.AddRange(values.Where(s => !string.IsNullOrWhiteSpace(s)));
                }
            }

            AddNames(track.rf1TrackNames);
            AddNames(track.rf2TrackNames);
            AddNames(track.gtr2TrackNames);
            AddNames(track.acTrackNames);
            AddNames(track.aceTrackNames);

            if (!string.IsNullOrWhiteSpace(track.lmuTrackName))
                names.Add(track.lmuTrackName);

            if (!string.IsNullOrWhiteSpace(track.irTrackName))
                names.Add(track.irTrackName);

            if (!string.IsNullOrWhiteSpace(track.pcarsTrackName))
                names.Add(track.pcarsTrackName);

            if (!string.IsNullOrWhiteSpace(track.pcars2TrackName))
                names.Add(track.pcars2TrackName);

            if (!string.IsNullOrWhiteSpace(track.ams2TrackName))
                names.Add(track.ams2TrackName);

            if (!string.IsNullOrWhiteSpace(track.accTrackName))
                names.Add(track.accTrackName);

            if (names.Count == 0)
                return null;

            return names
                .Select(NormalizeForSort)
                .Where(s => !string.IsNullOrEmpty(s))
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static string GetDuplicateKey(TrackLandmarksForTrack t)
        {
            string Join(string[] values)
            {
                return values == null
                    ? ""
                    : string.Join("|",
                        values
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
            }

            return string.Join("~",
                Join(t.rf1TrackNames),
                Join(t.rf2TrackNames),
                t.lmuTrackName ?? "",
                Join(t.gtr2TrackNames),
                Join(t.acTrackNames),
                Join(t.aceTrackNames),
                t.irTrackName ?? "",
                t.pcarsTrackName ?? "",
                t.pcars2TrackName ?? "",
                t.ams2TrackName ?? "",
                t.accTrackName ?? "",
                t.approximateTrackLength.ToString(CultureInfo.InvariantCulture),
                t.raceroomLayoutId.ToString());
        }
        #endregion MergeAndSortTrackLandmarksData
    }
#endif
}
