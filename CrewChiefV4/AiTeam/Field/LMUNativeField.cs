using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using rF2SharedMemory.LMUData;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// Positionen aller Wagen aus LMUs nativer Telemetrie (LMU_Data). Die Wertung, aus der das Lagebild Plätze und Streckenmeter
    /// liest, schreibt LMU nur etwa fünfmal pro Sekunde; die Telemetrie aller Wagen kommt deutlich öfter. Der Fahrdynamik-Abtaster
    /// liest das Abbild ohnehin und legt hier höchstens alle 40 ms ein Bild ab. Das Lagebild schreibt damit zwischen zwei
    /// Wertungsbildern Weltposition, Tempo und Streckenmeter fort (Streckenmeter = Wertung + Tempo × Zeit seit der Wertung).
    /// </summary>
    internal sealed class LMUNativeField
    {
        internal struct Car { public int Id; public float X, Y, Z, Speed; public double Et; }

        public DateTime Utc;
        public long Sequence;
        public Car[] Cars = new Car[0];

        private static LMUNativeField current;
        private static long lastTicks, sequence;
        internal const int MinIntervalMs = 40;
        /// <summary>Längste Fortschreibung über das Wertungsbild hinaus; danach gilt der Wagen als nicht neu gemessen.</summary>
        internal const double MaxExtrapolationS = 0.6;

        private static int T(string f) => Marshal.OffsetOf(typeof(rF2VehicleTelemetry), f).ToInt32();
        private static readonly int tId = T("mID"), tEt = T("mElapsedTime"), tPos = T("mPos"), tVel = T("mLocalVel");

        internal static LMUNativeField Latest => Volatile.Read(ref current);
        internal static void Reset() { Volatile.Write(ref current, null); Interlocked.Exchange(ref lastTicks, 0); }

        /// <summary>Aus der Fahrdynamik-Schleife (bis 100 Hz): höchstens alle 40 ms ein neues Bild.</summary>
        internal static void Observe(byte[] b, DateTime nowUtc)
        {
            long last = Interlocked.Read(ref lastTicks);
            if (last != 0 && nowUtc.Ticks >= last && nowUtc.Ticks - last < TimeSpan.TicksPerMillisecond * MinIntervalMs) return;
            Interlocked.Exchange(ref lastTicks, nowUtc.Ticks);
            var s = Parse(b, nowUtc);
            if (s != null) Volatile.Write(ref current, s);
        }

        internal static LMUNativeField Parse(byte[] b, DateTime nowUtc)
        {
            if (b == null || b.Length < LMUNativeLayout.TotalSize) return null;
            int active = Math.Min((int)b[LMUNativeLayout.ActiveVehiclesOffset], LMUNativeLayout.MaxVehicles);
            if (active <= 0) return null;
            var cars = new Car[active];
            for (int i = 0; i < active; i++)
            {
                int v = LMUNativeLayout.TelemetryVehiclesOffset + i * LMUNativeLayout.VehicleTelemetrySize;
                double vx = BitConverter.ToDouble(b, v + tVel), vy = BitConverter.ToDouble(b, v + tVel + 8), vz = BitConverter.ToDouble(b, v + tVel + 16);
                double sp = Math.Sqrt(vx * vx + vy * vy + vz * vz);
                cars[i] = new Car
                {
                    Id = BitConverter.ToInt32(b, v + tId), Et = BitConverter.ToDouble(b, v + tEt),
                    X = (float)BitConverter.ToDouble(b, v + tPos), Y = (float)BitConverter.ToDouble(b, v + tPos + 8), Z = (float)BitConverter.ToDouble(b, v + tPos + 16),
                    Speed = sp >= 0 && sp < 200 ? (float)sp : float.NaN
                };
            }
            return new LMUNativeField { Utc = nowUtc, Cars = cars, Sequence = Interlocked.Increment(ref sequence) };
        }

        /// <summary>
        /// Schreibt ein Feldbild mit der nativen Telemetrie fort. <paramref name="baseDist"/> sind die Streckenmeter je Slot aus dem
        /// letzten Wertungsbild zur Sessionzeit <paramref name="scoringEt"/>. Gibt die Zahl der aktualisierten Wagen zurück.
        /// </summary>
        internal static int Apply(FieldFrame f, LMUNativeField s, double scoringEt, IDictionary<int, float> baseDist)
        {
            if (f == null || s == null || s.Cars.Length == 0 || double.IsNaN(scoringEt)) return 0;
            var byId = new Dictionary<int, Car>(s.Cars.Length);
            foreach (var c in s.Cars) byId[c.Id] = c;
            int applied = 0; double newest = double.NaN;
            foreach (var c in f.Cars)
            {
                Car t;
                if (c == null || !byId.TryGetValue(c.Slot, out t)) continue;
                double dt = t.Et - scoringEt;
                if (double.IsNaN(dt) || dt < 0 || dt > MaxExtrapolationS) continue;
                c.X = t.X; c.Y = t.Y; c.Z = t.Z;
                if (!float.IsNaN(t.Speed)) c.Speed = t.Speed;
                float d;
                if (baseDist != null && baseDist.TryGetValue(c.Slot, out d) && !float.IsNaN(d) && !float.IsNaN(c.Speed) && !c.InPit)
                {
                    float next = d + (float)(c.Speed * dt);
                    // Nicht über die Ziellinie fortschreiben: Runde und Streckenmeter bleiben zusammen, bis die Wertung sie meldet.
                    if (!float.IsNaN(f.TrackLength) && next >= f.TrackLength) next = Math.Max(d, f.TrackLength - .1f);
                    c.LapDistance = next;
                }
                c.Extrapolated = dt > 0;
                applied++;
                if (double.IsNaN(newest) || t.Et > newest) newest = t.Et;
            }
            if (applied > 0) f.SimTime = newest;
            return applied;
        }
    }
}
