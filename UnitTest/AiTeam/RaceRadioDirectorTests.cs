using System;
using System.Collections.Generic;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Lokaler 13-Hz-Filter, belastbare Pace-Trends und Funkdisziplin.</summary>
    [TestFixture, Category("AiTeam")]
    public class RaceRadioDirectorTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Telemetrie_Hoechstfrequenz_ist_begrenzt_und_Cloud_wird_nicht_benoetigt()
        {
            var d = new RaceRadioDirector();
            Assert.IsTrue(d.Observe(Start, -1, -1));
            Assert.IsFalse(d.Observe(Start.AddMilliseconds(30), -1, -1));
            Assert.IsTrue(d.Observe(Start.AddMilliseconds(75), -1, -1));
            Assert.AreEqual(2, d.SampleCount);
        }

        [Test]
        public void Naher_Verkehr_unterdrueckt_nicht_dringliche_Meldungen()
        {
            var d = new RaceRadioDirector();
            d.Observe(Start, 1.3f, 10f);
            Assert.IsTrue(d.TrafficQuiet(Start.AddSeconds(5)));
            Assert.IsFalse(d.ReserveInsight("corner", Start.AddSeconds(5), TimeSpan.FromMinutes(5)));
            Assert.IsFalse(d.TrafficQuiet(Start.AddSeconds(10)));
            Assert.IsTrue(d.ReserveInsight("corner", Start.AddSeconds(10), TimeSpan.FromMinutes(5)));
            Assert.IsFalse(d.ReserveInsight("corner", Start.AddSeconds(90), TimeSpan.FromMinutes(5)));
            Assert.IsTrue(d.ReserveInsight("corner", Start.AddMinutes(6), TimeSpan.FromMinutes(5)));
        }

        [Test]
        public void Sechs_aufeinanderfolgende_gueltige_Runden_ergeben_belegten_Paceverlust()
        {
            var d = new RaceRadioDirector();
            var laps = Laps(90f, 90.1f, 89.9f, 91.9f, 91.6f, 92f);
            string text = d.ConsistentPaceLoss(laps);
            Assert.IsNotNull(text);
            StringAssert.Contains("langsamer", text);
            StringAssert.Contains("Ursache noch nicht", text);
            Assert.IsNull(d.ConsistentPaceLoss(laps)); // keine Wiederholung desselben Rundenfensters
        }

        [Test]
        public void Ein_ausreisser_oder_boxenrunde_ist_kein_belegter_Trend()
        {
            var d = new RaceRadioDirector();
            Assert.IsNull(d.ConsistentPaceLoss(Laps(90f,90f,90f,90f,90.1f,93f)));
            var b = Laps(90f,90f,90f,92f,92f,92f);
            b[2].InPitLap = true;
            Assert.IsNull(new RaceRadioDirector().ConsistentPaceLoss(b));
            var c = Laps(90f,90f,90f,92f,92f,92f);
            c[3].LapNumber = 8;
            Assert.IsNull(new RaceRadioDirector().ConsistentPaceLoss(c));
        }

        [Test]
        public void Mik_verspricht_kein_unrealistisches_sparen_bei_Tankverbot()
        {
            var watch = new FuelWatch();
            string radio = watch.Evaluate(5, 10f, 2f, 8f, false, false);
            Assert.IsNotNull(radio);
            StringAssert.Contains("Nachtanken ist nicht erlaubt", radio);
            StringAssert.Contains("nicht abgesichert", radio);
            StringAssert.DoesNotContain("Spar pro Runde", radio);
        }

        [Test]
        public void Mik_meldet_nur_messbaren_Trend_zum_selben_Gegner()
        {
            var d = new RaceRadioDirector();
            Assert.IsNull(d.MeasuredOpponentClosing("auto-1", 10, 9.0f));
            Assert.IsNull(d.MeasuredOpponentClosing("auto-1", 11, 8.5f));
            StringAssert.Contains("aufgeholt", d.MeasuredOpponentClosing("auto-1", 12, 7.8f));
            // Fahrerwechsel/Positionswechsel: keine Fortsetzung der alten Trendlinie.
            Assert.IsNull(d.MeasuredOpponentClosing("auto-2", 13, 7.0f));
            Assert.IsNull(d.MeasuredOpponentClosing("auto-2", 14, 6.5f));
            Assert.IsNull(d.MeasuredOpponentClosing("auto-2", 15, -1));
        }

        [Test]
        public void Mik_ignoriert_Spruenge_und_ungesicherte_Zeitabstaende()
        {
            var d = new RaceRadioDirector();
            Assert.IsNull(d.MeasuredOpponentClosing("op", 1, 15f));
            Assert.IsNull(d.MeasuredOpponentClosing("op", 3, 13f)); // eine Runde fehlt
            Assert.IsNull(d.MeasuredOpponentClosing("op", 4, 13.1f));
            Assert.IsNull(d.MeasuredOpponentClosing("", 5, 12f)); // kein Gegner-Schlüssel
        }

        [Test]
        public void Boxenbriefing_und_Proaktivprompt_sind_datengestuetzt()
        {
            StringAssert.Contains("Session", AiPersonas.SessionBriefingPrompt);
            StringAssert.Contains("Funkregie", AiPersonas.ProactivePrompt);
            StringAssert.Contains("speaker none", AiPersonas.ProactivePrompt);
        }

        private static List<LapRecord> Laps(params float[] times)
        {
            var l = new List<LapRecord>();
            for (int i = 0; i < times.Length; i++)
                l.Add(new LapRecord { LapNumber = i + 1, LapTime = times[i], Valid = true });
            return l;
        }
    }
}
