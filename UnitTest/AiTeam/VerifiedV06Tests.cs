using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using CrewChiefV4.NumberProcessing;
using CrewChiefV4.RF2;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class VerifiedV06Tests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        private static GameStateData State(int lap, bool newSession = false, float fuel = 50)
        {
            var s = new GameStateData(lap + 1);
            s.SessionData.SessionId = 7;
            s.SessionData.IsNewSession = newSession;
            s.SessionData.IsNewLap = lap > 0;
            s.SessionData.CompletedLaps = lap;
            s.SessionData.PreviousLapWasValid = true;
            s.SessionData.LapTimePrevious = 90;
            s.FuelData.FuelUseActive = true;
            s.FuelData.FuelLeft = fuel;
            return s;
        }

        [Test]
        public void Wiederholtes_Rundensignal_zaehlt_nur_eine_Runde()
        {
            var memory = new AiRaceMemory();
            var a = State(1);
            Assert.IsTrue(memory.Update(null, a, T));
            Assert.IsFalse(memory.Update(a, a, T.AddMilliseconds(50)));
            Assert.AreEqual(1, memory.GetLaps().Count);
        }

        [Test]
        public void Sessionflag_als_Pegel_loescht_nicht_jeden_Tick()
        {
            var memory = new AiRaceMemory();
            var a = State(1, true);
            memory.Update(null, a, T);
            memory.Update(a, State(1, true), T.AddMilliseconds(50));
            Assert.AreEqual(1, memory.GetLaps().Count);
        }

        [Test]
        public void Wiederverwendete_SessionID_leert_auch_Funkhistorie()
        {
            var memory = new AiRaceMemory();
            var a = State(4);
            memory.Update(null, a, T);
            memory.AddRadio(TeamMember.Ben, "alter Befund", T);
            memory.Update(a, State(0, true), T.AddSeconds(1));
            Assert.IsEmpty(memory.GetLaps());
            Assert.IsEmpty(memory.GetRadio(TimeSpan.FromMinutes(5), T.AddSeconds(1)));
            memory.Clear();
            Assert.AreEqual(int.MinValue, memory.SessionId);
            Assert.AreEqual(DateTime.MinValue, memory.LastTickUtc);
        }

        [Test]
        public void Ungueltige_Runde_verfaelscht_Verbrauch_nicht()
        {
            var memory = new AiRaceMemory();
            var a = State(1, false, 50);
            var b = State(2, false, 47.5f);
            var c = State(3, false, 37.5f);
            c.SessionData.PreviousLapWasValid = false;
            memory.Update(null, a, T);
            memory.Update(a, b, T.AddSeconds(90));
            memory.Update(b, c, T.AddSeconds(180));
            Assert.AreEqual(2.5f, memory.AverageFuelPerLap(), 0.001);
        }

        [TestCase(float.NaN, 2.5f, 10f)]
        [TestCase(10f, float.NaN, 10f)]
        [TestCase(10f, 2.5f, float.PositiveInfinity)]
        [TestCase(float.PositiveInfinity, 2.5f, 10f)]
        [TestCase(10f, float.PositiveInfinity, 10f)]
        public void Ungueltige_Spritwerte_erzeugen_keinen_Funk(float fuel, float perLap, float laps)
        {
            Assert.IsNull(new FuelWatch().Evaluate(5, fuel, perLap, laps, true, false));
        }

        [Test]
        public void Leerer_Tank_wird_als_Defizit_gemeldet()
        {
            StringAssert.Contains("Tankstopp", new FuelWatch().Evaluate(5, 0, 2, 5, true, false));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Grosses_Defizit_beruecksichtigt_Nachtankregel(bool allowed)
        {
            string text = new FuelWatch().Evaluate(5, 10, 2, 10, allowed, false);
            if (allowed) StringAssert.Contains("Tankstopp", text);
            else
            {
                StringAssert.Contains("Nachtanken ist nicht erlaubt", text);
                StringAssert.DoesNotContain("brauchen einen Tankstopp", text);
            }
        }

        [Test]
        public void Zukuenftige_oder_negative_Spotterdaten_geben_nicht_frei()
        {
            var spotter = new SpotterSnapshot { SpotterRunning = true, EvaluatedUtc = T.AddMilliseconds(1) };
            Assert.IsFalse(spotter.IsFresh(T));
            Assert.AreEqual(SaraLocal.NoDataAnswer, SaraLocal.TryAnswer("rechts frei?", spotter, null, T));
            spotter.EvaluatedUtc = T;
            spotter.CarsRight = -1;
            Assert.IsFalse(spotter.IsFresh(T));
        }

        [Test]
        public void Eingereihter_Funk_wird_vor_Audiozugriff_neu_validiert()
        {
            bool valid = true;
            var id = AiAudioStore.Put(new byte[10], "alte Session", () => valid);
            byte[] wav;
            string text;
            Assert.IsTrue(AiAudioStore.TryGet(id, out wav, out text));
            valid = false;
            Assert.IsFalse(AiAudioStore.TryGet(id, out wav, out text));
            Assert.IsNull(wav);
            Assert.IsNull(text);
        }

        [Test]
        public void Fehler_in_Audiogueltigkeit_wird_sicher_verworfen()
        {
            var id = AiAudioStore.Put(new byte[10], "verwerfen", () => { throw new InvalidOperationException(); });
            byte[] wav;
            string text;
            Assert.IsFalse(AiAudioStore.TryGet(id, out wav, out text));
        }

        [TestCase(SessionType.Practice, SessionPhase.Garage, false, 0, true)]
        [TestCase(SessionType.Practice, SessionPhase.Green, false, 30, false)]
        [TestCase(SessionType.Qualify, SessionPhase.Green, true, 0, true)]
        [TestCase(SessionType.Race, SessionPhase.Countdown, false, 0, true)]
        [TestCase(SessionType.Race, SessionPhase.Formation, false, 20, false)]
        [TestCase(SessionType.Race, SessionPhase.Green, false, 0, false)]
        [TestCase(SessionType.Race, SessionPhase.Green, true, 0, true)]
        [TestCase(SessionType.Practice, SessionPhase.Garage, false, -1, false)]
        public void Begruessung_startet_nur_im_passenden_Zustand(SessionType type, SessionPhase phase, bool pit, float speed, bool expected)
        {
            Assert.AreEqual(expected, SessionGreetingDirector.SafeToGreet(type, phase, pit, speed));
        }

        [TestCase("de-DE")]
        [TestCase("en-GB")]
        public void Dezimalzahlen_sind_unabhaengig_von_Windows_Sprache(string culture)
        {
            var before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                new SpeechRecogniser(null);
                Assert.AreEqual(21.45f, SpokenNumberParser.Parse("box change tyre pressures twenty one point four five bar"), 0.001);
                Assert.AreEqual(37, Rf2PitMenuController.ParseFuelLevel("9.7gal/15 laps"));
            }
            finally { Thread.CurrentThread.CurrentCulture = before; }
        }

        [Test]
        public void Lange_Session_begrenzt_Gedaechtnis_und_Funkhistorie()
        {
            var memory = new AiRaceMemory();
            GameStateData previous = null;
            for (int lap = 0; lap < 1000; lap++)
            {
                var current = State(lap, false, 5000 - lap * 2);
                memory.Update(previous, current, T.AddSeconds(lap * 90));
                memory.AddRadio(TeamMember.Mik, "Befund", T.AddSeconds(lap * 90));
                previous = current;
            }
            Assert.AreEqual(AiRaceMemory.MaxLaps, memory.GetLaps().Count);
            Assert.AreEqual(AiRaceMemory.MaxRadio, memory.GetRadio(TimeSpan.FromDays(2), T.AddDays(1)).Count);
            Assert.AreEqual(2, memory.AverageFuelPerLap(), 0.001);
        }

        [Test]
        public void Hintermanntrend_braucht_drei_kontinuierliche_Werte_desselben_Gegners()
        {
            var d = new RaceRadioDirector();
            Assert.IsNull(d.MeasuredOpponentApproaching("auto-a", 1, 8));
            Assert.IsNull(d.MeasuredOpponentApproaching("auto-a", 2, 7.5f));
            StringAssert.Contains("Hintermann", d.MeasuredOpponentApproaching("auto-a", 3, 6.8f));
            Assert.IsNull(d.MeasuredOpponentApproaching("auto-a", 3, 6.8f));
            Assert.IsNull(d.MeasuredOpponentApproaching("auto-b", 4, 6));
            Assert.IsNull(d.MeasuredOpponentApproaching("auto-b", 6, 5));
            d.Reset();
            Assert.IsNull(d.MeasuredOpponentApproaching("auto-a", 7, 4));
        }

        [Test]
        public void Ein_Abstandssprung_oder_NaN_ist_kein_kontinuierlicher_Gegnertrend()
        {
            var d = new RaceRadioDirector();
            d.MeasuredOpponentApproaching("a", 1, 10);
            d.MeasuredOpponentApproaching("a", 2, 10.5f);
            Assert.IsNull(d.MeasuredOpponentApproaching("a", 3, 8));
            Assert.IsNull(d.MeasuredOpponentApproaching("a", 4, float.NaN));
        }

        [Test]
        public void Sechs_Stunden_Sampler_behalten_nur_Zaehler_und_letzten_Zeitpunkt()
        {
            var d = new RaceRadioDirector();
            const int samples = 6 * 60 * 60 * 20;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < samples; i++) d.Observe(T.AddMilliseconds(i * 50), 8, 7);
            watch.Stop();
            Assert.AreEqual(samples, d.SampleCount);
            TestContext.WriteLine("Simulierte sechs Stunden: " + samples + " lokale Samples in " + watch.ElapsedMilliseconds + " ms; keine Echtzeit-/Audiolatenzmessung.");
        }

        [Test]
        public void Zukunftstelemetrie_ist_nicht_gueltig()
        {
            var ctx = new RaceDataContext { GameState = State(1), Memory = new AiRaceMemory(), NowUtc = T, LastTelemetryUtc = T.AddSeconds(1) };
            Assert.IsTrue((bool)RaceDataTools.Execute("get_race_state", ctx)["veraltet"]);
        }
    }
}
