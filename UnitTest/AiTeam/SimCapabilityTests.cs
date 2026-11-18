using System.Linq;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>RRCA 2.0 (2.2): Fähigkeitsmatrix der sechs Kernsimulationen – vollständig, belegt, ohne erfundene Frequenzen.</summary>
    [TestFixture, Category("AiTeam")]
    public class SimCapabilityTests
    {
        [Test]
        public void Every_core_sim_lists_every_field_once()
        {
            var fields = SimCapabilityMatrix.All.Select(c => c.Field).Distinct().ToList();
            Assert.GreaterOrEqual(fields.Count, 20);
            foreach (var sim in SimCapabilityMatrix.Sims)
            {
                var mine = SimCapabilityMatrix.For(sim).ToList();
                CollectionAssert.AreEquivalent(fields, mine.Select(c => c.Field).ToList(), sim);
            }
            CollectionAssert.AreEquivalent(new[] { "AMS2", "R3E", "LMU", "ACE", "IRACING", "ACC" }, SimCapabilityMatrix.Sims);
        }

        [Test]
        public void Mapped_means_there_is_a_code_reference_and_no_rate_is_invented()
        {
            foreach (var c in SimCapabilityMatrix.All.Where(c => c.Mapped && c.Field != "fahrdynamik_100hz"))
                StringAssert.IsMatch(@"\.cs:\d+$", c.Evidence, c.Sim + "/" + c.Field);
            Assert.IsTrue(SimCapabilityMatrix.All.All(c => c.MeasuredHz == null), "Raten erst nach Messung eintragen");
        }

        [Test]
        public void Known_limits_are_stated_honestly()
        {
            Assert.IsFalse(SimCapabilityMatrix.Get("IRACING", "weltposition_xyz").Mapped, "iRacing liefert keine 3D-Gegnerpositionen");
            Assert.IsFalse(SimCapabilityMatrix.Get("IRACING", "fahrdynamik_100hz").Mapped);
            Assert.IsTrue(SimCapabilityMatrix.Get("AMS2", "kollision").Mapped, "AMS2 mLastOpponentCollision*");
            Assert.IsTrue(SimCapabilityMatrix.Get("IRACING", "vorfallpunkte").Mapped);
            Assert.IsTrue(SimCapabilityMatrix.Get("ACE", "fahrdynamik_100hz").Mapped);
            Assert.AreEqual(DataOrigin.SimulatorReportedEvent, SimCapabilityMatrix.Get("R3E", "flaggen").Origin);
            Assert.AreEqual(DataOrigin.Measured, SimCapabilityMatrix.Get("R3E", "kraftstoff").Origin);
        }

        [Test]
        public void Matrix_is_machine_readable()
        {
            var all = SimCapabilityMatrix.Describe();
            Assert.IsNotNull(all["grundlage"]);
            foreach (var sim in SimCapabilityMatrix.Sims) Assert.Greater(((Newtonsoft.Json.Linq.JArray)all[sim]).Count, 0);
            var one = SimCapabilityMatrix.Describe("LMU");
            Assert.IsNull(one["AMS2"]);
            Assert.AreEqual(JTokenNull(), one["LMU"][0]["gemessen_hz"].Type);
            Assert.AreEqual("LMU", SimCapabilityMatrix.SimOf(CrewChiefV4.GameEnum.LMU));
            Assert.IsNull(SimCapabilityMatrix.SimOf(CrewChiefV4.GameEnum.RF2_64BIT));
        }

        private static Newtonsoft.Json.Linq.JTokenType JTokenNull() => Newtonsoft.Json.Linq.JTokenType.Null;
    }
}
