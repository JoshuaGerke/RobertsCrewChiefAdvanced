using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Probe;
using NUnit.Framework;
using rF2SharedMemory;
using rF2SharedMemory.LMUData;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Telemetrie-Messung für Le Mans Ultimate: Offsets aus dem Marshaller, eigenes Auto aus der Wertung, Wertung nach
    /// Telemetrie-Einträgen geordnet, Versionsblock gegen halb geschriebene Bilder. Synthetische Puffer, keine Spieldaten.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class LMUProbeTests
    {
        [Test]
        public void Layout_matches_the_game_constants()
        {
            var a = new LMUProbeAnalyzer(1000);
            Assert.IsNull(a.LayoutError);
            Assert.AreEqual(LMUConstants.MM_TELEMETRY_STRUCT_SIZE, LMUProbeAnalyzer.TelemetrySize);
            Assert.AreEqual(LMUConstants.MM_SCORING_STRUCT_SIZE, LMUProbeAnalyzer.ScoringSize);
            Assert.AreEqual("LMU", a.FilePrefix);
            Assert.IsNotNull(a.CreateSource(), "LMU liest zwei Abbildungen über eine eigene Quelle");
            Assert.AreEqual(a.Channels.Count, a.Channels.Select(c => c.Id).Distinct().Count(), "Kanalnamen eindeutig");
            Assert.IsTrue(a.Channels.All(c => c.Offset >= 0 && c.Offset + 8 <= a.StructSize));
        }

        [Test]
        public void Own_car_and_rivals_are_found_although_both_lists_are_sorted_differently()
        {
            byte[] b = Frame();
            var a = new LMUProbeAnalyzer(1000);
            a.Observe(b, 1, 0);
            Func<string, int, double> read = (id, i) => a.Channels.Single(c => c.Id == id).Read(b, i);
            Assert.AreEqual(2, a.OwnIndex, "eigenes Auto ist Telemetrie-Eintrag 2 (ID 9)");
            Assert.AreEqual(-55.5, read("bewegung.v_laengs", 0), 1e-9);
            Assert.AreEqual(360.15, read("reifen.temp_mitte_hr", 0), 1e-9);
            Assert.AreEqual(1.0, read("hilfe.abs_greift", 0), 1e-9);
            Assert.AreEqual(0.25, read("sprit.virtuelle_energie", 0), 1e-6);
            Assert.AreEqual(4.0, read("runde.platz", 0), 1e-9);
            Assert.AreEqual(12.0, read("runde.runden", 0), 1e-9);
            Assert.AreEqual(-1.0, read("sitzung.gelb", 0), 1e-9, "vorzeichenbehaftetes Byte");
            Assert.AreEqual(2.0, read("gegner.platz", 0), 1e-9, "Telemetrie-Eintrag 0 ist ID 7, in der Wertung an Stelle 2");
            Assert.AreEqual(1234.5, read("gegner.strecke_m", 0), 1e-9);
            Assert.AreEqual(3.0, read("gegner.platz", 1), 1e-9);
            Assert.AreEqual(2, a.MaxOpponents);
            Assert.AreEqual("Le Mans", a.TrackName);
            Assert.IsNull(a.HeaderError);
            Assert.AreEqual(1, a.ActivePolls, "Fahrt erkannt");
        }

        [Test]
        public void Monitor_pause_and_spectating_are_not_counted_as_driving()
        {
            var a = new LMUProbeAnalyzer(1000);
            byte[] b = Frame(realtime: 0);
            a.Observe(b, 1, 0);
            Assert.IsTrue(a.Inactive(b), "Monitor/Garage");
            b = Frame(phase: 9);
            a.Observe(b, 2, 0);
            Assert.IsTrue(a.Inactive(b), "Pause");
            b = Frame(player: false);
            a.Observe(b, 3, 0);
            Assert.IsTrue(a.Inactive(b), "kein eigenes Auto: Zuschauen");
            Assert.AreEqual(0, a.ActivePolls);
        }

        [Test]
        public void Rates_follow_the_telemetry_version()
        {
            var a = new LMUProbeAnalyzer(1000);
            byte[] b = Frame();
            int vel = LMUProbeAnalyzer.TelemetryVehicles + 2 * LMUProbeAnalyzer.VehicleSize + Marshal.OffsetOf(typeof(rF2VehicleTelemetry), "mLocalVel").ToInt32() + 16;
            uint version = 0;
            for (long ms = 1; ms <= 3000; ms++)
            {
                if (ms % 10 == 0)
                {
                    version++;
                    Buffer.BlockCopy(BitConverter.GetBytes(version), 0, b, 0, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(version), 0, b, 4, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(-40.0 - ms * 0.001), 0, b, vel, 8);
                }
                a.Observe(b, ms, 0);
            }
            Assert.AreEqual(100, a.SourceFramesPerSecond, 2, "100 Telemetrie-Bilder pro Sekunde");
            Assert.IsTrue(double.IsNaN(a.SimTicksPerSecond));
            var speed = a.Channels.Single(c => c.Id == "bewegung.v_laengs");
            Assert.AreEqual(10, speed.Intervals.Percentile(0.5), 0.01);
            StringAssert.Contains("LMU", ProbeReport.Summary(a, DateTime.Now, 1));
        }

        [Test]
        public void Versioned_copy_takes_only_complete_frames_and_only_the_used_entries()
        {
            const int count = 8, array = 16, stride = 10;
            var src = new byte[array + LMUProbeAnalyzer.Max * stride];
            for (int i = 0; i < src.Length; i++) src[i] = 0xAB;
            Put(src, 0, 5u); Put(src, 4, 5u); Put(src, count, 2);
            var map = new ArrayMap(src);
            var target = new byte[100 + src.Length];
            Assert.IsTrue(LMUProbeAnalyzer.CopyVersioned(map, target, 100, count, array, stride, out int torn));
            Assert.AreEqual(0, torn);
            Assert.AreEqual(0xAB, target[100 + array + 2 * stride - 1], "zweiter Eintrag kopiert");
            Assert.AreEqual(0, target[100 + array + 2 * stride], "dritter Eintrag nicht kopiert");

            Put(src, 4, 4u); // Spiel schreibt gerade: Beginn ≠ Ende
            Assert.IsFalse(LMUProbeAnalyzer.CopyVersioned(map, target, 100, count, array, stride, out torn));
            Assert.AreEqual(8, torn);

            Put(src, 4, 5u); Put(src, count, -3);
            Assert.IsTrue(LMUProbeAnalyzer.CopyVersioned(map, new byte[src.Length], 0, count, array, stride, out torn), "unsinnige Anzahl: nur der Kopf");

            Put(src, count, 1000);
            Assert.IsTrue(LMUProbeAnalyzer.CopyVersioned(map, new byte[src.Length], 0, count, array, stride, out torn), "zu große Anzahl wird begrenzt");

            var busy = new ArrayMap(src) { WriteDuringCopy = 1 };
            Put(src, count, 1);
            Assert.IsTrue(LMUProbeAnalyzer.CopyVersioned(busy, new byte[src.Length], 0, count, array, stride, out torn));
            Assert.AreEqual(1, torn, "während des Kopierens überschrieben: verworfen, nächster Versuch gilt");
        }

        [Test]
        public void Native_layout_matches_the_game_interface()
        {
            Assert.IsNull(LMUNativeLayout.LayoutProblem());
            Assert.AreEqual(1632, LMUNativeLayout.ScoringOffset);
            Assert.AreEqual(2192, LMUNativeLayout.ScoringVehiclesOffset);
            Assert.AreEqual(128464, LMUNativeLayout.TelemetryOffset);
            Assert.AreEqual(128468, LMUNativeLayout.TelemetryVehiclesOffset);
            Assert.AreEqual(324824, LMUNativeLayout.TotalSize);
            var a = new LMUProbeAnalyzer(1000, true);
            Assert.IsNull(a.LayoutError);
            Assert.AreEqual("LMU_nativ", a.FilePrefix);
            Assert.AreEqual("LMU_Data", a.SharedMemoryName);
            Assert.IsTrue(a.Channels.All(c => c.Offset >= 0 && c.Offset + 8 <= a.StructSize));
        }

        [Test]
        public void Native_frame_uses_the_player_index_from_the_game()
        {
            byte[] b = NativeFrame(player: 2, et: 12.5);
            var a = new LMUProbeAnalyzer(1000, true);
            a.Observe(b, 1, 0);
            Assert.AreEqual(2, a.OwnIndex);
            Func<string, int, double> read = (id, i) => a.Channels.Single(c => c.Id == id).Read(b, i);
            Assert.AreEqual(-55.5, read("bewegung.v_laengs", 0), 1e-9);
            Assert.AreEqual(4.0, read("runde.platz", 0), 1e-9);
            Assert.AreEqual(2.0, read("gegner.platz", 0), 1e-9);
            Assert.AreEqual(1, a.ActivePolls);
            for (int ms = 2; ms <= 1000; ms++)
            {
                if (ms % 10 == 0) Put(b, LMUNativeLayout.TelemetryVehiclesOffset + 2 * LMUNativeLayout.VehicleTelemetrySize + Marshal.OffsetOf(typeof(rF2VehicleTelemetry), "mElapsedTime").ToInt32(), 12.5 + ms / 1000.0);
                a.Observe(b, ms, 0);
            }
            Assert.AreEqual(100, a.SourceFramesPerSecond, 3, "100 Hz über die Fahrzeit des eigenen Autos");
            var f = new DynamicsFrame(); double last = double.NaN;
            Assert.IsTrue(LMUNativeDynamicsSource.Fill(b, f, ref last));
            Assert.AreEqual(55.5, f.Speed, 1e-3);
            Assert.IsTrue(f.AbsActive, "nativ meldet ABS-Eingriff");
            Assert.IsFalse(f.AbsUnknown);
            Assert.IsFalse(LMUNativeDynamicsSource.Fill(b, f, ref last), "gleiche Fahrzeit: kein neues Bild");
            b[LMUNativeLayout.PlayerHasVehicleOffset] = 0;
            Assert.IsFalse(LMUNativeLayout.Plausible(b) && LMUNativeDynamicsSource.Fill(b, f, ref last), "ohne eigenes Auto kein Bild");
        }

        private static void Put(byte[] b, int at, double v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, b, at, 8); }

        /// <summary>Natives Abbild: drei Autos, Telemetrie 7, 3, 9; Wertung 3, 9, 7; eigenes Auto Index 2 (ID 9).</summary>
        private static byte[] NativeFrame(int player, double et)
        {
            var b = new byte[new LMUProbeAnalyzer(1000, true).StructSize];
            b[LMUNativeLayout.ActiveVehiclesOffset] = 3; b[LMUNativeLayout.PlayerIndexOffset] = (byte)player; b[LMUNativeLayout.PlayerHasVehicleOffset] = 1;
            int[] ids = { 7, 3, 9 };
            for (int i = 0; i < 3; i++)
            {
                var v = Filled<rF2VehicleTelemetry>(); v.mID = ids[i]; v.mElapsedTime = et;
                if (i == player) { v.mLocalVel.z = -55.5; v.mABSActive = 1; }
                Bytes(v).CopyTo(b, LMUNativeLayout.TelemetryVehiclesOffset + i * LMUNativeLayout.VehicleTelemetrySize);
            }
            var info = Filled<rF2ScoringInfo>(); info.mNumVehicles = 3; info.mInRealtime = 1; info.mGamePhase = 5;
            Bytes(info).CopyTo(b, LMUNativeLayout.ScoringOffset);
            int[] sids = { 3, 9, 7 }; byte[] places = { 3, 4, 2 };
            for (int i = 0; i < 3; i++)
            {
                var s = Filled<rF2VehicleScoring>(); s.mID = sids[i]; s.mPlace = places[i]; s.mIsPlayer = (byte)(sids[i] == ids[player] ? 1 : 0);
                Bytes(s).CopyTo(b, LMUNativeLayout.ScoringVehiclesOffset + i * LMUNativeLayout.VehicleScoringSize);
            }
            return b;
        }

        // ---- Hilfen ----

        private sealed class ArrayMap : IProbeMap
        {
            private readonly byte[] data;
            internal int WriteDuringCopy;
            internal ArrayMap(byte[] data) { this.data = data; }
            public uint U32(int at) { return BitConverter.ToUInt32(data, at); }
            public int I32(int at) { return BitConverter.ToInt32(data, at); }
            public void Bytes(int at, byte[] target, int targetOffset, int count)
            {
                Buffer.BlockCopy(data, at, target, targetOffset, count);
                if (WriteDuringCopy-- > 0) { uint v = U32(0) + 1; Put(data, 0, v); Put(data, 4, v); }
            }
        }

        private static void Put(byte[] b, int at, uint v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, b, at, 4); }
        private static void Put(byte[] b, int at, int v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, b, at, 4); }

        /// <summary>Drei Autos. Telemetrie-Reihenfolge ID 7, 3, 9; Wertung 3, 9, 7. Das eigene Auto ist ID 9.</summary>
        private static byte[] Frame(byte realtime = 1, byte phase = 5, bool player = true)
        {
            var t = Filled<rF2Telemetry>();
            t.mVersionUpdateBegin = t.mVersionUpdateEnd = 1;
            t.mNumVehicles = 3;
            int[] telemetryIds = { 7, 3, 9 };
            for (int i = 0; i < 3; i++) t.mVehicles[i].mID = telemetryIds[i];
            t.mVehicles[2].mLocalVel.z = -55.5;
            t.mVehicles[2].mWheels[3].mTemperature[1] = 360.15;
            t.mVehicles[2].mABSActive = 1;
            t.mVehicles[2].mVirtualEnergy = 0.25f;

            var s = Filled<rF2Scoring>();
            s.mVersionUpdateBegin = s.mVersionUpdateEnd = 1;
            s.mScoringInfo.mNumVehicles = 3;
            s.mScoringInfo.mInRealtime = realtime;
            s.mScoringInfo.mGamePhase = phase;
            s.mScoringInfo.mYellowFlagState = -1;
            Encoding.UTF8.GetBytes("Le Mans").CopyTo(s.mScoringInfo.mTrackName, 0);
            int[] scoringIds = { 3, 9, 7 };
            byte[] places = { 3, 4, 2 };
            for (int i = 0; i < 3; i++) { s.mVehicles[i].mID = scoringIds[i]; s.mVehicles[i].mPlace = places[i]; }
            s.mVehicles[1].mIsPlayer = (byte)(player ? 1 : 0);
            s.mVehicles[1].mTotalLaps = 12;
            s.mVehicles[2].mLapDist = 1234.5;

            var b = new byte[new LMUProbeAnalyzer(1000).StructSize];
            Bytes(t).CopyTo(b, 0);
            Bytes(s).CopyTo(b, LMUProbeAnalyzer.TelemetrySize);
            return b;
        }

        private static byte[] Bytes<T>(T data)
        {
            int size = Marshal.SizeOf(typeof(T));
            var bytes = new byte[size];
            IntPtr p = Marshal.AllocHGlobal(size);
            try { Marshal.StructureToPtr(data, p, false); Marshal.Copy(p, bytes, 0, size); }
            finally { Marshal.FreeHGlobal(p); }
            return bytes;
        }

        private static T Filled<T>() { return (T)Fill(typeof(T)); }

        private static object Fill(Type type)
        {
            object boxed = Activator.CreateInstance(type);
            foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!f.FieldType.IsArray)
                {
                    if (f.FieldType.IsValueType && !f.FieldType.IsPrimitive && !f.FieldType.IsEnum) f.SetValue(boxed, Fill(f.FieldType));
                    continue;
                }
                var m = (MarshalAsAttribute)Attribute.GetCustomAttribute(f, typeof(MarshalAsAttribute));
                Type element = f.FieldType.GetElementType();
                Array array = Array.CreateInstance(element, m.SizeConst);
                if (element.IsValueType && !element.IsPrimitive)
                    for (int i = 0; i < array.Length; i++) array.SetValue(Fill(element), i);
                f.SetValue(boxed, array);
            }
            return boxed;
        }
    }
}
