using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;
using CrewChiefV4.AMS2;
using CrewChiefV4.AiTeam.Probe;
using CrewChiefV4.RaceRoom.RaceRoomData;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>P1 Telemetrie-Messung (RaceRoom, AMS2): Byte-Offsets, Raten, Pausen, Bericht ohne Namen.</summary>
    [TestFixture, Category("AiTeam")]
    public class TelemetryProbeTests
    {
        [Test]
        public void Own_layout_matches_the_marshaller()
        {
            Assert.IsNull(PackedLayout.Verify(typeof(RaceRoomShared)));
            Assert.AreEqual(Marshal.SizeOf(typeof(DriverData)), PackedLayout.Size(typeof(DriverData)));
            Assert.AreEqual(Marshal.SizeOf(typeof(PlayerData)), PackedLayout.Size(typeof(PlayerData)));
            int player = Marshal.OffsetOf(typeof(RaceRoomShared), "Player").ToInt32();
            Assert.AreEqual(player + Marshal.OffsetOf(typeof(PlayerData), "GameSimulationTime").ToInt32(),
                PackedLayout.Offset(typeof(RaceRoomShared), "Player.GameSimulationTime"));
            // Generisch: TireData<TireTempInformation>, FrontRight beginnt nach 24 Byte, Center nach 4 Byte.
            int tireTemp = Marshal.OffsetOf(typeof(RaceRoomShared), "TireTemp").ToInt32();
            Assert.AreEqual(tireTemp + 24 + 4, PackedLayout.Offset(typeof(RaceRoomShared), "TireTemp.FrontRight.CurrentTemp.Center"));
            int drivers = Marshal.OffsetOf(typeof(RaceRoomShared), "DriverData").ToInt32();
            Assert.AreEqual(drivers + 2 * Marshal.SizeOf(typeof(DriverData)) + Marshal.OffsetOf(typeof(DriverData), "Place").ToInt32(),
                PackedLayout.Offset(typeof(RaceRoomShared), "DriverData[2].Place"));
        }

        [Test]
        public void Channels_read_the_same_values_the_marshaller_writes()
        {
            var data = Empty();
            data.CarSpeed = 51.25f;
            data.FuelLeft = 42.5f;
            data.Player.GameSimulationTime = 123.5;
            data.Player.LocalGforce.X = -1.75;
            data.TireTemp.RearLeft.CurrentTemp.Center = 88.5f;
            data.BrakeTemp.FrontRight.CurrentTemp = 412f;
            data.AidSettings.Tc = 5;
            data.CarDamage.Aerodynamics = 0.25f;
            data.Flags.ClosestYellowDistanceIntoTrack = 950f;
            byte[] buffer = ToBytes(data);
            var a = new R3EProbeAnalyzer(1000);
            Func<string, double> read = id => a.Channels.Single(c => c.Id == id).Read(buffer, 0);
            Assert.AreEqual(51.25, read("bewegung.tempo"), 1e-6);
            Assert.AreEqual(42.5, read("sprit.liter"), 1e-6);
            Assert.AreEqual(123.5, read("quelle.sim_zeit"), 1e-9);
            Assert.AreEqual(-1.75, read("bewegung.g_quer"), 1e-9);
            Assert.AreEqual(88.5, read("reifen.temp_mitte_hl"), 1e-6);
            Assert.AreEqual(412, read("bremse.temp_vr"), 1e-6);
            Assert.AreEqual(5.0, read("hilfe.tc_status"), 1e-9);
            Assert.AreEqual(0.25, read("schaden.aero"), 1e-6);
            Assert.AreEqual(950, read("sitzung.gelb_abstand"), 1e-6);

            data.DriverData[1].Position.X = 77.5f;
            data.DriverData[1].Place = 2;
            buffer = ToBytes(data);
            var gegner = a.Channels.Single(c => c.Id == "gegner.pos_x");
            Assert.AreEqual(77.5, gegner.Read(buffer, 1), 1e-6);
        }

        [Test]
        public void Rates_follow_the_source_and_slower_opponent_updates()
        {
            var a = new R3EProbeAnalyzer(1000); // 1 Tick = 1 ms
            var data = Racing();
            int frame = 0;
            for (long ms = 1; ms <= 2000; ms++)
            {
                if (ms % 16 == 0) Step(ref data, ++frame);
                a.Observe(ToBytes(data), ms, 0);
            }
            Assert.IsNull(a.HeaderError);
            Assert.AreEqual(62.5, a.SourceFramesPerSecond, 1.5);
            Assert.AreEqual(16, a.FrameIntervals.Percentile(0.5), 1.0);
            Assert.AreEqual(6 * 62.5, a.SimTicksPerSecond, 10);

            var tempo = Ch(a, "bewegung.tempo");
            Assert.AreEqual("vorhanden", tempo.Status);
            Assert.Greater(a.FrameShare(tempo), 0.95);
            Assert.AreEqual("konstant", Ch(a, "sprit.liter").Status);
            StringAssert.StartsWith("fehlt", Ch(a, "hilfe.abs_status").Status);

            var map = Ch(a, "gegner.pos_x");
            Assert.AreEqual(3, a.MaxOpponents);
            Assert.AreEqual(48, map.Intervals.Percentile(0.5), 0.01);
            Assert.AreEqual(62.5 / 3, a.ChangesPerSecond(map), 1.0);
            Assert.Less(a.FrameShare(map), 0.4);
            Assert.AreEqual("vorhanden", Ch(a, "eigen_liste.pos_x").Status);
        }

        [Test]
        public void Pause_is_not_counted_and_does_not_stretch_intervals()
        {
            var a = new R3EProbeAnalyzer(1000);
            var data = Racing();
            int frame = 0;
            long ms = 0;
            for (; ms < 500; ms++) { if (ms % 16 == 0) Step(ref data, ++frame); a.Observe(ToBytes(data), ms + 1, 0); }
            data.GamePaused = 1;
            for (; ms < 1500; ms++) a.Observe(ToBytes(data), ms + 1, 0);
            data.GamePaused = 0;
            for (; ms < 2000; ms++) { if (ms % 16 == 0) Step(ref data, ++frame); a.Observe(ToBytes(data), ms + 1, 0); }
            Assert.AreEqual(1000, a.PausedMs, 2);
            Assert.AreEqual(1000, a.ActiveMs, 20);
            Assert.Less(Ch(a, "bewegung.tempo").Intervals.MaxMs, 40);
            Assert.Less(a.FrameIntervals.MaxMs, 40);
        }

        [Test]
        public void Opponent_leaving_and_returning_does_not_create_a_long_interval()
        {
            var a = new R3EProbeAnalyzer(1000);
            var data = Racing();
            int frame = 0;
            for (long ms = 1; ms <= 3000; ms++)
            {
                if (ms % 16 == 0) Step(ref data, ++frame);
                data.DriverData[3].Place = ms > 1000 && ms < 2000 ? 0 : 4; // ein Gegner verlässt kurz die Liste
                a.Observe(ToBytes(data), ms, 0);
            }
            Assert.Less(Ch(a, "gegner.strecke_m").Intervals.MaxMs, 100);
        }

        [Test]
        public void Histogram_percentiles_stay_in_the_measured_range()
        {
            var h = new IntervalHistogram();
            for (int i = 0; i < 10; i++) h.Add(16);
            Assert.AreEqual(16, h.Percentile(0.5), 1e-9);
            Assert.AreEqual(16, h.Percentile(0.95), 1e-9);
            h.Add(400);
            Assert.AreEqual(400, h.Percentile(1), 1e-9);
            Assert.IsTrue(double.IsNaN(new IntervalHistogram().Percentile(0.5)));
        }

        [Test]
        public void Report_is_readable_and_contains_no_driver_names()
        {
            var a = new R3EProbeAnalyzer(1000);
            var data = Racing();
            data.TrackName = Bytes("Nürburgring", 64);
            data.PlayerName = Bytes("Rob Gerke", 64);
            data.DriverData[1].DriverInfo.Name = Bytes("Max Mustermann", 64);
            int frame = 0;
            for (long ms = 1; ms <= 500; ms++) { if (ms % 16 == 0) Step(ref data, ++frame); a.Observe(ToBytes(data), ms, 0); }
            string csv = ProbeReport.Csv(a);
            string summary = ProbeReport.Summary(a, DateTime.Now, 2.5);
            string json = ProbeReport.Json(a, DateTime.Now, 2.5, "test").ToString();
            StringAssert.StartsWith("Gruppe;Kanal;", csv);
            StringAssert.Contains("bewegung.tempo", csv);
            StringAssert.Contains("Neue Datenbilder vom Spiel", summary);
            StringAssert.Contains("Nürburgring", summary);
            StringAssert.Contains("rrca-telemetry-probe/1", json);
            foreach (string text in new[] { csv, summary, json })
            {
                StringAssert.DoesNotContain("Mustermann", text);
                StringAssert.DoesNotContain("Gerke", text);
            }
        }

        // ---- AMS2 ----

        [Test]
        public void Ams2_layout_matches_the_reader()
        {
            var a = new AMS2ProbeAnalyzer(1000);
            Assert.IsNull(a.LayoutError);
            Assert.AreEqual(Marshal.SizeOf(typeof(ams2APIStruct)), a.StructSize);
            Assert.AreEqual(AMS2SharedMemoryReader.SequenceOffset, a.SequenceOffset);
            Assert.AreEqual("AMS2", a.FilePrefix);
        }

        [Test]
        public void Ams2_channels_read_the_same_values_the_marshaller_writes()
        {
            var d = Ams2Racing();
            d.mSpeed = 61.5f;
            d.mAntiLockActive = true;
            d.mTyreTemp[2] = 91.5f;
            d.mAmbientTemperature = 18.5f;
            d.mParticipantData[3].mWorldPosition[2] = -123.25f;
            d.mParticipantData[3].mRacePosition = 7;
            d.mSpeeds[3] = 44.5f;
            d.mOrientations[3 * 3 + 1] = 1.25f;
            byte[] b = Ams2Bytes(d);
            var a = new AMS2ProbeAnalyzer(1000);
            Func<string, int, double> read = (id, i) => a.Channels.Single(c => c.Id == id).Read(b, i);
            Assert.AreEqual(61.5, read("bewegung.tempo", 0), 1e-6);
            Assert.AreEqual(1.0, read("hilfe.abs_greift", 0), 1e-9);
            Assert.AreEqual(91.5, read("reifen.temp_hl", 0), 1e-6);
            Assert.AreEqual(18.5, read("wetter.luft", 0), 1e-6);
            Assert.AreEqual(-123.25, read("gegner.pos_z", 3), 1e-6);
            Assert.AreEqual(7.0, read("gegner.platz", 3), 1e-9);
            Assert.AreEqual(44.5, read("gegner.tempo", 3), 1e-6);
            Assert.AreEqual(1.25, read("gegner.gier", 3), 1e-6);
        }

        [Test]
        public void Ams2_rates_pause_and_opponents()
        {
            var a = new AMS2ProbeAnalyzer(1000);
            var d = Ams2Racing();
            int frame = 0;
            for (long ms = 1; ms <= 3000; ms++)
            {
                d.mGameState = ms > 1000 && ms <= 1500 ? 3u : 2u; // eine halbe Sekunde Pause
                if (d.mGameState == 2 && ms % 16 == 0)
                {
                    frame++;
                    d.mSequenceNumber = (uint)(frame * 2);
                    d.mSpeed = 40 + frame * 0.01f;
                    d.mParticipantData[0].mWorldPosition[0] = frame * 0.5f;
                    if (frame % 3 == 0)
                        for (int i = 1; i < 4; i++) d.mParticipantData[i].mWorldPosition[0] = frame * 0.5f + i;
                }
                a.Observe(Ams2Bytes(d), ms, 0);
            }
            Assert.IsNull(a.HeaderError);
            Assert.AreEqual(500, a.PausedMs, 2);
            Assert.AreEqual(62.5, a.SourceFramesPerSecond, 2);
            Assert.IsTrue(double.IsNaN(a.SimTicksPerSecond));
            Assert.Less(a.FrameIntervals.MaxMs, 40);
            Assert.AreEqual(3, a.MaxOpponents);
            var map = a.Channels.Single(c => c.Id == "gegner.pos_x");
            Assert.AreEqual(48, map.Intervals.Percentile(0.5), 0.01);
            Assert.AreEqual("vorhanden", a.Channels.Single(c => c.Id == "eigen_liste.pos_x").Status);
            StringAssert.Contains("AMS2", ProbeReport.Summary(a, DateTime.Now, 1));
        }

        private static ams2APIStruct Ams2Racing()
        {
            var d = Filled<ams2APIStruct>();
            d.mVersion = 14;
            d.mGameState = 2;
            d.mNumParticipants = 4;
            d.mViewedParticipantIndex = 0;
            for (int i = 0; i < 4; i++) d.mParticipantData[i].mIsActive = true;
            return d;
        }

        private static byte[] Ams2Bytes(ams2APIStruct data)
        {
            int size = Marshal.SizeOf(typeof(ams2APIStruct));
            var bytes = new byte[size];
            IntPtr p = Marshal.AllocHGlobal(size);
            try { Marshal.StructureToPtr(data, p, false); Marshal.Copy(p, bytes, 0, size); }
            finally { Marshal.FreeHGlobal(p); }
            return bytes;
        }

        /// <summary>Legt alle festen Arrays (auch in Unterstrukturen) in der Länge an, die der Marshaller erwartet.</summary>
        private static T Filled<T>() { return (T)Fill(typeof(T)); }

        private static object Fill(Type type)
        {
            object boxed = Activator.CreateInstance(type);
            foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!f.FieldType.IsArray) continue;
                var m = (MarshalAsAttribute)Attribute.GetCustomAttribute(f, typeof(MarshalAsAttribute));
                Type element = f.FieldType.GetElementType();
                Array array = Array.CreateInstance(element, m.SizeConst);
                if (element.IsValueType && !element.IsPrimitive)
                    for (int i = 0; i < array.Length; i++) array.SetValue(Fill(element), i);
                f.SetValue(boxed, array);
            }
            return boxed;
        }

        // ---- Hilfen ----

        private static ProbeChannel Ch(R3EProbeAnalyzer a, string id) { return a.Channels.Single(c => c.Id == id); }

        private static RaceRoomShared Racing()
        {
            var d = Empty();
            d.VersionMajor = 3;
            d.AllDriversOffset = PackedLayout.Offset(typeof(RaceRoomShared), "NumCars");
            d.DriverDataSize = Marshal.SizeOf(typeof(DriverData));
            d.FuelLeft = 50;
            d.AidSettings.Abs = -1;
            d.VehicleInfo.SlotId = 5;
            d.NumCars = 4;
            int[] slots = { 5, 10, 11, 12 };
            for (int i = 0; i < 4; i++)
            {
                d.DriverData[i].Place = i + 1;
                d.DriverData[i].DriverInfo.SlotId = slots[i];
            }
            return d;
        }

        /// <summary>Ein neues Quellbild: Spieler jedes Bild, Gegner-Karte nur jedes dritte.</summary>
        private static void Step(ref RaceRoomShared d, int frame)
        {
            d.Player.GameSimulationTicks = frame * 6;
            d.Player.GameSimulationTime = frame / 62.5;
            d.CarSpeed = 40 + frame * 0.01f;
            d.LapDistance = frame * 0.6f;
            d.DriverData[0].Position.X = frame * 0.6f;
            d.DriverData[0].LapDistance = frame * 0.6f;
            if (frame % 3 == 0)
                for (int i = 1; i < 4; i++)
                {
                    d.DriverData[i].Position.X = frame * 0.6f + i * 10;
                    d.DriverData[i].LapDistance = frame * 0.6f + i * 10;
                }
        }

        private static RaceRoomShared Empty()
        {
            var d = new RaceRoomShared
            {
                TrackName = new byte[64], LayoutName = new byte[64], PlayerName = new byte[64],
                DriverData = new DriverData[128]
            };
            d.VehicleInfo.Name = new byte[64];
            for (int i = 0; i < d.DriverData.Length; i++) d.DriverData[i].DriverInfo.Name = new byte[64];
            return d;
        }

        private static byte[] Bytes(string s, int length)
        {
            var b = new byte[length];
            byte[] raw = Encoding.UTF8.GetBytes(s);
            Array.Copy(raw, b, Math.Min(raw.Length, length - 1));
            return b;
        }

        private static byte[] ToBytes(RaceRoomShared data)
        {
            int size = Marshal.SizeOf(typeof(RaceRoomShared));
            var bytes = new byte[size];
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(data, p, false);
                Marshal.Copy(p, bytes, 0, size);
            }
            finally { Marshal.FreeHGlobal(p); }
            return bytes;
        }
    }
}
