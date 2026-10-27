using System;
using System.Collections.Generic;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class LiveCrewV06Tests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 26, 18, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Lokal_20_Hz_ohne_KI_und_ohne_Doppelupdates()
        {
            var d = new RaceRadioDirector();
            Assert.IsTrue(d.Observe(T, -1, -1));
            Assert.IsFalse(d.Observe(T.AddMilliseconds(49), -1, -1));
            Assert.IsTrue(d.Observe(T.AddMilliseconds(50), -1, -1));
            Assert.IsFalse(d.Observe(T.AddMilliseconds(50), -1, -1));
            Assert.IsTrue(d.Observe(T.AddMilliseconds(100), -1, -1));
            Assert.AreEqual(3, d.SampleCount);
        }

        [Test]
        public void Alle_Zwoelf_Begruessungen_sind_unterschiedlich_und_wetterfrei()
        {
            var g = new SessionGreetingDirector();
            var seen = new HashSet<string>();
            for (int i = 0; i < SessionGreetingDirector.VariantCount; i++)
            {
                string cue = g.NextCue(SessionType.Practice, SessionPhase.Garage, false, 0);
                Assert.IsTrue(seen.Add(cue), "Variante darf sich nicht wiederholen");
                StringAssert.Contains("Garage", cue);
                StringAssert.Contains("Ben begrüßt zuerst", cue);
            }
            Assert.AreEqual(12, seen.Count);
            string last = null;
            for (int i = 0; i < 30; i++)
            {
                string current = g.NextCue(SessionType.Practice, SessionPhase.Garage, false, 0);
                Assert.AreNotEqual(last, current, "direkte Wiederholung");
                last = current;
            }
        }

        [Test]
        public void Begruessung_ist_nicht_mitten_im_Rennen_oder_unter_FCY_erlaubt()
        {
            Assert.IsTrue(SessionGreetingDirector.SafeToGreet(SessionType.Race, SessionPhase.Countdown, false, 0));
            Assert.IsTrue(SessionGreetingDirector.SafeToGreet(SessionType.Practice, SessionPhase.Garage, false, 0));
            Assert.IsTrue(SessionGreetingDirector.SafeToGreet(SessionType.Race, SessionPhase.Green, true, 0));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(SessionType.Race, SessionPhase.Green, true, 5));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(SessionType.Race, SessionPhase.Formation, false, 12));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(SessionType.Race, SessionPhase.Green, false, 48));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(SessionType.Practice, SessionPhase.Green, false, 35));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(SessionType.Race, SessionPhase.FullCourseYellow, false, 0));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(SessionType.Practice, SessionPhase.Garage, false, float.NaN));
        }

        [Test]
        public void Kraftstofftrend_nur_mit_sechs_sauberen_zusammenhaengenden_Messrunden()
        {
            var laps = new List<LapRecord>();
            float[] used = { 2.10f, 2.12f, 2.11f, 2.40f, 2.45f, 2.43f };
            for (int i = 0; i < used.Length; i++)
                laps.Add(new LapRecord { LapNumber = i + 1, LapTime = 90, Valid = true, FuelUsed = used[i] });
            var d = new RaceRadioDirector();
            StringAssert.Contains("Liter mehr", d.MeasuredFuelIncrease(laps));
            Assert.IsNull(d.MeasuredFuelIncrease(laps));
            laps[3].InPitLap = true;
            Assert.IsNull(new RaceRadioDirector().MeasuredFuelIncrease(laps));
            laps[3].InPitLap = false;
            laps[2].FuelUsed = -1;
            Assert.IsNull(new RaceRadioDirector().MeasuredFuelIncrease(laps));
        }

        [Test]
        public void Uhrzeit_Rueckwaerts_darf_Cooldown_nicht_umgehen()
        {
            var d = new RaceRadioDirector();
            Assert.IsTrue(d.ReserveInsight("pace", T, TimeSpan.FromMinutes(5)));
            Assert.IsFalse(d.ReserveInsight("pace", T.AddMinutes(-1), TimeSpan.FromMinutes(5)));
            Assert.IsFalse(d.ReserveInsight("pace", T.AddMinutes(2), TimeSpan.FromMinutes(5)));
        }
    }
}
