using System;
using System.Linq;
using System.Text;
using CrewChiefV4.AiTeam.Telemetry;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Testkanal B: Rohdatentafel über eine nachgebaute Sim-Struktur (Hülle mit data, Räder, Wagen-Listen, Namen).</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RawSimBoardTests
    {
        public struct Wheel { public float mTemp; public double mWear; }
        public struct Scoring { public int mID; public byte mIsPlayer; public int mPlace; }
        public struct Telem { public int mID; public double mFuel; }
        public struct Shared
        {
            public uint mVersionUpdateBegin;
            public double mSpeed;
            public float mBrake;
            public float mBroken;
            public byte[] mTrackName;
            public Wheel[] mWheels;
            public Scoring[] mVehicles;
            public Telem[] mTelemetry;
        }
        public class Wrapper { public long ticksWhenRead; public Shared data; }

        public class IrData { public int PlayerCarIdx; public float[] CarIdxLapDistPct; public float FuelLevel; }
        public class IrSim { public IrData Telemetry { get; set; } }
        public class IrWrapper { public IrSim data; }

        private static readonly DateTime T = new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);

        private static Wrapper Frame(double speed, float brake, double fuel)
        {
            var d = new Shared
            {
                mSpeed = speed, mBrake = brake, mBroken = float.NaN,
                mTrackName = Encoding.ASCII.GetBytes("Monza\0\0\0\0\0\0"),
                mWheels = Enumerable.Range(0, 4).Select(i => new Wheel { mTemp = 80 + i, mWear = .9 }).ToArray(),
                mVehicles = Enumerable.Range(0, 20).Select(i => new Scoring { mID = 100 + i, mIsPlayer = (byte)(i == 7 ? 1 : 0), mPlace = i + 1 }).ToArray(),
                mTelemetry = Enumerable.Range(0, 20).Select(i => new Telem { mID = 119 - i, mFuel = i == 12 ? fuel : 1 }).ToArray()
            };
            return new Wrapper { ticksWhenRead = T.Ticks, data = d };
        }

        [Test]
        public void Finds_every_field_and_reads_only_the_own_car_from_car_lists()
        {
            var board = new RawSimBoard();
            Assert.IsTrue(board.Observe(Frame(50, .5f, 42), "Test", true, T));
            Assert.AreEqual(50, board.Number("data.mSpeed"));
            Assert.AreEqual(82, board.Number("data.mWheels[2].mTemp"));
            Assert.AreEqual(8, board.Number("data.mVehicles[ich].mPlace"), "Spieler-Kennzeichen an Index 7");
            Assert.AreEqual(42, board.Number("data.mTelemetry[ich].mFuel"), "Telemetrie-Liste über die ID des Spielers (107 an Index 12)");
            Assert.IsNaN(board.Number("data.mVersionUpdateBegin"), "Versionszähler gehören nicht in die Tafel");
            Assert.IsNaN(board.Number("ticksWhenRead"));
            var json = board.Describe("track", false, 10, T);
            Assert.AreEqual("Monza", (string)json["felder_liste"][0]["wert"]);
        }

        [Test]
        public void Status_tells_live_constant_frozen_and_invalid_apart()
        {
            var board = new RawSimBoard();
            for (int s = 0; s <= 40; s++)
                board.Observe(Frame(50 + s, s < 5 ? s * .1f : .4f, 42), "Test", true, T.AddSeconds(s));
            var now = T.AddSeconds(40);
            Assert.AreEqual("live", board.Status("data.mSpeed", now));
            Assert.AreEqual("konstant", board.Status("data.mWheels[0].mTemp", now));
            Assert.AreEqual("ungueltig", board.Status("data.mBroken", now));
            Assert.AreEqual("langsam", board.Status("data.mBrake", now));
            for (int s = 41; s <= 110; s++)
                board.Observe(Frame(50 + s, .4f, 42), "Test", true, T.AddSeconds(s));
            Assert.AreEqual("eingefroren", board.Status("data.mBrake", T.AddSeconds(110)), "hat sich bewegt, steht seit über 60 s Fahrt");
        }

        [Test]
        public void Standing_in_the_pits_is_resting_not_frozen()
        {
            var board = new RawSimBoard();
            for (int s = 0; s <= 5; s++) board.Observe(Frame(50, s * .1f, 42), "Test", true, T.AddSeconds(s));
            for (int s = 6; s <= 120; s++) board.Observe(Frame(0, .5f, 42), "Test", false, T.AddSeconds(s));
            Assert.AreEqual("ruht", board.Status("data.mBrake", T.AddSeconds(120)));
        }

        [Test]
        public void Samples_at_most_every_half_second()
        {
            var board = new RawSimBoard();
            Assert.IsTrue(board.Observe(Frame(1, 0, 1), "Test", true, T));
            Assert.IsFalse(board.Observe(Frame(2, 0, 1), "Test", true, T.AddMilliseconds(200)));
            Assert.IsTrue(board.Observe(Frame(3, 0, 1), "Test", true, T.AddMilliseconds(600)));
            Assert.AreEqual(3, board.Number("data.mSpeed"));
        }

        [Test]
        public void Search_needs_all_terms_and_can_show_only_moving_fields()
        {
            var board = new RawSimBoard();
            for (int s = 0; s <= 10; s++) board.Observe(Frame(50 + s, .5f, 42), "Test", true, T.AddSeconds(s));
            var hits = board.Describe("wheels temp", false, 50, T.AddSeconds(10));
            Assert.AreEqual(4, (int)hits["treffer"]);
            var live = board.Describe("", true, 150, T.AddSeconds(10));
            var names = ((JArray)live["felder_liste"]).Select(f => (string)f["feld"]).ToList();
            CollectionAssert.Contains(names, "data.mSpeed");
            CollectionAssert.DoesNotContain(names, "data.mWheels[0].mTemp");
            Assert.AreEqual("Test", (string)live["sim"]);
        }

        [Test]
        public void IRacing_values_behind_the_telemetry_property_are_read_with_the_player_index()
        {
            var ir = new IrWrapper { data = new IrSim { Telemetry = new IrData { PlayerCarIdx = 3, FuelLevel = 20,
                CarIdxLapDistPct = Enumerable.Range(0, 64).Select(i => i / 100f).ToArray() } } };
            var board = new RawSimBoard();
            board.Observe(ir, "IRACING", true, T);
            Assert.AreEqual(20, board.Number("FuelLevel"));
            Assert.AreEqual(.03, board.Number("CarIdxLapDistPct[ich]"), 1e-6);
        }

        [Test]
        public void Tool_reads_arguments_and_rejects_bad_json()
        {
            StringAssert.Contains("JSON", (string)RawSimBoard.Tool("{kaputt", T)["fehler"]);
            Assert.IsNotNull(RawSimBoard.Tool("{\"suche\":\"fuel\",\"nur_live\":true,\"max\":5}", T));
            Assert.AreEqual("object", (string)RawSimBoard.ToolParameters()["type"]);
        }
    }
}
