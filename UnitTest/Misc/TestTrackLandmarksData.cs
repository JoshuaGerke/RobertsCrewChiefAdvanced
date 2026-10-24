using CrewChiefV4;

using Newtonsoft.Json;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using static CrewChiefV4.JsonFiles;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TrackLandmarksDataTests
    {
#if DEBUG
        [Test]
        public void TrackLandmarksData_ABcheck()    // Write diffs to UnitTest\Misc\TrackLandmarksData_ABcheck.txt
        {
            var TrackLandmarksDataOld = TrackLandmarksData.getTrackLandmarksDataFromFile("..\\..\\Misc\\oldTrackLandmarksData.json");
            TrackData.SortTracks(TrackLandmarksDataOld);

            var TrackLandmarksDataNew = TrackLandmarksData.getTrackLandmarksDataFromFile(DataFiles.default_track_landmarks);
            var index = 0;
            int newIndex = 0;
            for (; index < TrackLandmarksDataOld.trackLandmarksData.Count; index++)
            {
                if (TrackLandmarksDataNew.trackLandmarksData[newIndex].approximateTrackLength == -1f && TrackLandmarksDataOld.trackLandmarksData[index].approximateTrackLength != -1f)
                {
                    TrackLandmarksDataNew.trackLandmarksData[newIndex].approximateTrackLength = 0f;
                }
                if (TrackLandmarksDataNew.trackLandmarksData[newIndex].raceroomRollingStartLapDistance == -1f && TrackLandmarksDataOld.trackLandmarksData[index].raceroomRollingStartLapDistance != -1f)
                {
                    TrackLandmarksDataNew.trackLandmarksData[newIndex].raceroomRollingStartLapDistance = 0f;
                }
                if (TrackLandmarksDataNew.trackLandmarksData[newIndex].iracingPitEntranceDistanceRoundTrack == -1f && TrackLandmarksDataOld.trackLandmarksData[index].iracingPitEntranceDistanceRoundTrack != -1f)
                {
                    TrackLandmarksDataNew.trackLandmarksData[newIndex].iracingPitEntranceDistanceRoundTrack = 0f;
                }
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new FloatWithoutTrailingZeroConverter());
                string before;
                string after;
                using (var sw = new StringWriter())
                using (var writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, TrackLandmarksDataOld.trackLandmarksData[index]);
                    writer.Flush();
                    before = sw.ToString();
                }
                using (var sw = new StringWriter())
                using (var writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, TrackLandmarksDataNew.trackLandmarksData[newIndex]);
                    writer.Flush();
                    after = sw.ToString();
                }
                if (before != after)
                {
                    Log.Warning($"Index {index}:{newIndex} differs.\nBefore: {before}\nAfter:  {after}");
                    newIndex++; // New list will be longer, skip one gives a chance of resync
                }
                newIndex++;
            }
            File.WriteAllText("..\\..\\Misc\\TrackLandmarksData_ABcheck.txt", ConsoleCapture.Contents);
        }
#endif
        [Ignore("Not a test, but a utility to reformat the JSON file if needed. Comment this line to make the trackLandmarksData.json file format consistent")]
        [Test]
        public void Reformat()
        {
            var json = TrackLandmarksData.getTrackLandmarksDataFromFile(DataFiles.default_track_landmarks);
            JsonFiles.WriteFilteredFile(DataFiles.default_track_landmarks, json);

            var jsonNew = TrackLandmarksData.getTrackLandmarksDataFromFile(DataFiles.default_track_landmarks);
            Assert.AreEqual(json.trackLandmarksData.Count, jsonNew.trackLandmarksData.Count); // Check nothing went horribly wrong.
            for (var index = 0; index < json.trackLandmarksData.Count; index++)
            {
                if (jsonNew.trackLandmarksData[index].approximateTrackLength == -1f && json.trackLandmarksData[index].approximateTrackLength != -1f)
                {
                    jsonNew.trackLandmarksData[index].approximateTrackLength = 0f;
                }
                if (jsonNew.trackLandmarksData[index].raceroomRollingStartLapDistance == -1f && json.trackLandmarksData[index].raceroomRollingStartLapDistance != -1f)
                {
                    jsonNew.trackLandmarksData[index].raceroomRollingStartLapDistance = 0f;
                }
                if (jsonNew.trackLandmarksData[index].iracingPitEntranceDistanceRoundTrack == -1f && json.trackLandmarksData[index].iracingPitEntranceDistanceRoundTrack != -1f)
                {
                    jsonNew.trackLandmarksData[index].iracingPitEntranceDistanceRoundTrack = 0f;
                }
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new FloatWithoutTrailingZeroConverter());
                string before;
                string after;
                using (var sw = new StringWriter())
                using (var writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, json.trackLandmarksData[index]);
                    writer.Flush();
                    before = sw.ToString();
                }
                using (var sw = new StringWriter())
                using (var writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, jsonNew.trackLandmarksData[index]);
                    writer.Flush();
                    after = sw.ToString();
                }
                if (before != after)
                {
                    Assert.Fail($"Index {index} differs.\nBefore: {before}\nAfter:  {after}");
                }
            }
        }
        [Ignore("Not a test, but a utility to merge user-created track landmarks into the default JSON file. Comment this line to merge")]
        [Test]
        public void MergeUserTrackLandmarksIntoDefault()
        {
            TrackData.loadTrackLandmarksData();
            JsonFiles.WriteFilteredFile(DataFiles.default_track_landmarks, TrackData.TRACK_LANDMARKS_DATA);
        }
    }
}
