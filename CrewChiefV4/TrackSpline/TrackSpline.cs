using CrewChiefV4.ACE.aceData;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;

namespace CrewChiefV4.TrackSpline
{
    public class TrackSpline
    {
        public struct SplineProjection
        {
            public float spLineLength;
            public float distanceToSpline;
            public bool valid;
            public SplineProjection(float spLineLength, float distanceToSpline, bool valid)
            {
                this.spLineLength = spLineLength;
                this.distanceToSpline = distanceToSpline;
                this.valid = valid;
            }
        }
        public class Node
        {
            public aceVec3 pos;
            public float t;
        }

        public List<Node> nodes = new List<Node>();
        public float totalLength;

        // ===============================
        // BUILD
        // ===============================
        public static TrackSpline Build(List<aceVec3> samples, float simplifyTolerance)
        {
            if (simplifyTolerance <= 0f)
                simplifyTolerance = 0.5f;

            TrackSpline spline = new TrackSpline();

            if (samples == null || samples.Count < 10)
                return spline;

            var simplified = RamerDouglasPeucker(samples, 0.5f);
            var smoothed = simplified; // no smoothing

            float length = 0f;
            List<float> cumulative = new List<float>();
            cumulative.Add(0f);

            for (int i = 1; i < smoothed.Count; i++)
            {
                float d = Distance(smoothed[i - 1], smoothed[i]);
                length += d;
                cumulative.Add(length);
            }

            spline.totalLength = length;

            for (int i = 0; i < smoothed.Count; i++)
            {
                Node n = new Node();
                n.pos = smoothed[i];
                n.t = (length > 0f) ? cumulative[i] / length : 0f;
                spline.nodes.Add(n);
            }

            return spline;
        }

        // ===============================
        // PROJECTION
        // ===============================
        public SplineProjection Project(aceVec3 carPos, float previous)
        {
            if (nodes == null || nodes.Count < 2)
                return new SplineProjection(0f, 0f, false);
            float bestDistSq = float.MaxValue;
            int bestIndex = 0;
            float bestT = 0f;

            int count = nodes.Count;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1 < count) ? i + 1 : 0;

                float tSeg = ProjectToSegment(carPos, nodes[i].pos, nodes[next].pos);
                aceVec3 proj = Lerp(nodes[i].pos, nodes[next].pos, tSeg);

                float distSq = DistanceSq(carPos, proj);

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                    bestT = tSeg;
                }
            }

            int nextIndex = (bestIndex + 1 < count) ? bestIndex + 1 : 0;

            float t0 = nodes[bestIndex].t;
            float t1 = nodes[nextIndex].t;

            if (nextIndex == 0)
                t1 = 1.0f;

            float result = Lerp(t0, t1, bestT);

            // smoothing + wrap handling
            if (previous >= 0f)
            {
                float delta = result - previous;

                if (delta > 0.5f) result -= 1f;
                if (delta < -0.5f) result += 1f;

                result = Lerp(previous, result, 0.2f);

                if (result < 0f) result += 1f;
                if (result > 1f) result -= 1f;
            }

            SplineProjection projResult = new SplineProjection
            {
                spLineLength = result,
                distanceToSpline = (float)Math.Sqrt(bestDistSq),
                valid = true
            };
            return projResult;
        }

        /// <summary>
        /// Use default Track_splines file if available, fall back to the one in
        /// Documents if not, creating it from the session if it doesn't exist
        /// there either.
        /// </summary>
        private static String getSplineFilePath(GameEnum gameEnum, String trackName)
        {
            var partPath = Path.Combine(
                Utilities.FileNames.MakeValidForPathName(gameEnum.ToString()),
                Utilities.FileNames.MakeValidForPathName(trackName) + ".json");
            var defaultPath = DataFiles.getDefaultFileLocation(Path.Combine("Track_splines", partPath));
            var path = File.Exists(defaultPath) ? defaultPath : Path.Combine(DataFiles.track_splinesFolder, partPath);
            return path;
        }

        /// <summary>
        /// Saves the spline to a JSON file named {track}.json in
        /// Documents/CC/Track_splines/<Game> folder.
        /// </summary>
        public void Save(string track)
        {
            string path = getSplineFilePath(CrewChief.gameDefinition.gameEnum, track);
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Loads the spline from a JSON file named {track}.json in CC's track splines
        /// folder or the one the user created.
        /// </summary>
        public static TrackSpline Load(string track)
        {
            string path = getSplineFilePath(CrewChief.gameDefinition.gameEnum, track);

            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<TrackSpline>(json);
        }

        // ===============================
        // MATHS
        // ===============================
        static float Distance(aceVec3 a, aceVec3 b)
        {
            return (float)Math.Sqrt(DistanceSq(a, b));
        }

        static float DistanceSq(aceVec3 a, aceVec3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }

        static aceVec3 Sub(aceVec3 a, aceVec3 b)
        {
            return new aceVec3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        static float Dot(aceVec3 a, aceVec3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        static aceVec3 Lerp(aceVec3 a, aceVec3 b, float t)
        {
            return new aceVec3(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t,
                a.z + (b.z - a.z) * t
            );
        }

        static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        static float ProjectToSegment(aceVec3 p, aceVec3 a, aceVec3 b)
        {
            aceVec3 ab = Sub(b, a);
            aceVec3 ap = Sub(p, a);

            float abLenSq = Dot(ab, ab);
            if (abLenSq == 0f) return 0f;

            float t = Dot(ap, ab) / abLenSq;

            if (t < 0f) return 0f;
            if (t > 1f) return 1f;
            return t;
        }

        // ===============================
        // RDP SIMPLIFICATION
        // ===============================
        static List<aceVec3> RamerDouglasPeucker(List<aceVec3> pts, float epsilon)
        {
            if (pts.Count < 3)
                return new List<aceVec3>(pts);

            int last = pts.Count - 1;

            float distMax = 0f;
            int index = 0;

            for (int i = 1; i < last; i++)
            {
                float d = PerpendicularDistance(pts[i], pts[0], pts[last]);
                if (d > distMax)
                {
                    distMax = d;
                    index = i;
                }
            }

            if (distMax > epsilon)
            {
                List<aceVec3> left = RamerDouglasPeucker(pts.GetRange(0, index + 1), epsilon);
                List<aceVec3> right = RamerDouglasPeucker(pts.GetRange(index, pts.Count - index), epsilon);

                left.RemoveAt(left.Count - 1);
                left.AddRange(right);
                return left;
            }
            else
            {
                List<aceVec3> result = new List<aceVec3>();
                result.Add(pts[0]);
                result.Add(pts[last]);
                return result;
            }
        }

        static float PerpendicularDistance(aceVec3 p, aceVec3 a, aceVec3 b)
        {
            aceVec3 ab = Sub(b, a);
            aceVec3 ap = Sub(p, a);

            float area = Length(Cross(ab, ap));
            float baseLen = Length(ab);

            if (baseLen == 0f) return 0f;
            return area / baseLen;
        }

        static aceVec3 Cross(aceVec3 a, aceVec3 b)
        {
            return new aceVec3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x
            );
        }

        static float Length(aceVec3 v)
        {
            return (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }

        // ===============================
        // SMOOTHING
        // ===============================
        static List<aceVec3> MovingAverage(List<aceVec3> pts, int window)
        {
            List<aceVec3> result = new List<aceVec3>();

            for (int i = 0; i < pts.Count; i++)
            {
                float x = 0f, y = 0f, z = 0f;
                int count = 0;

                for (int j = -window; j <= window; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < pts.Count)
                    {
                        x += pts[idx].x;
                        y += pts[idx].y;
                        z += pts[idx].z;
                        count++;
                    }
                }

                result.Add(new aceVec3(x / count, y / count, z / count));
            }

            return result;
        }
    }
}