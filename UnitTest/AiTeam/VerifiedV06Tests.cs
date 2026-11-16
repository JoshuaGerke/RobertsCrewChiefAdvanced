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
        public void Spa_Verbrauch_und_Warnung_auch_ohne_gueltige_Zeiten()
        {
            var memory = new AiRaceMemory();
            float[] tanks = { 21.80567f, 18.11577f, 14.42947f, 10.68381f, 7.01713f };
            GameStateData previous = null;
            for (int i = 0; i < tanks.Length; i++)
            {
                var frame = State(i + 1, false, tanks[i]);
                frame.SessionData.SessionType = SessionType.Race;
                frame.SessionData.PreviousLapWasValid = false;
                frame.SessionData.SessionLapsRemaining = 2;
                memory.Update(previous, frame, T.AddSeconds(i * 140));
                previous = frame;
            }

            Assert.IsTrue(memory.GetLaps().All(lap => !lap.Valid));
            Assert.AreEqual(3.697135f, memory.AverageFuelPerLap(), 0.0001);
            var estimate = FuelEstimate.From(new RaceDataContext { GameState = previous, Memory = memory });
            Assert.IsNotNull(estimate);
            Assert.AreEqual(0.37714f, estimate.AdditionalWithoutReserve, 0.0001);
            Assert.IsNotNull(new FuelWatch().Evaluate(5, previous.FuelData.FuelLeft, estimate.PerLap,
                estimate.LapsRemaining, true, estimate.EstimatedFromTime, estimate.AdditionalWithoutReserve));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Messluecke_oder_Nachtanken_zwischen_Ziellinien_verwirft_Verbrauch(bool refuel)
        {
            var memory = new AiRaceMemory();
            var start = State(1, false, 50);
            var interruption = State(1, false, refuel ? 51 : 48);
            interruption.SessionData.IsNewLap = false;
            interruption.FuelData.FuelUseActive = refuel;
            var finish = State(2, false, 46);
            memory.Update(null, start, T);
            memory.Update(start, interruption, T.AddSeconds(40));
            memory.Update(interruption, finish, T.AddSeconds(90));

            Assert.AreEqual(-1, memory.AverageFuelPerLap());
        }

        [Test]
        public void Frische_Tankmessung_und_Strecke_erlauben_vorlaeufige_Prognose_vor_Rundenschluss()
        {
            var memory = new AiRaceMemory();
            var track = new TrackDefinition("Fuel measurement", 4000f);
            GameStateData frame = null;
            for (int tick = 0; tick <= 50; tick++)
            {
                var next = State(0, false, 7.0f - tick * 0.02f);
                next.Now = T.AddSeconds(tick * 0.5);
                next.SourceTelemetryUtc = next.Now;
                next.SessionData.SessionType = SessionType.Race;
                next.SessionData.SessionPhase = SessionPhase.Green;
                next.SessionData.SessionLapsRemaining = 2;
                next.SessionData.TrackDefinition = track;
                next.PositionAndMotionData.DistanceRoundTrack = tick * 20;
                next.PositionAndMotionData.CarSpeed = 40;
                memory.Update(frame, next, next.Now);
                frame = next;
                // Rob 16.11.: Schätzung schon ab rund 300 m (hier 20 m je Bild), nicht erst nach einem Viertel der Runde.
                if (tick == 14) Assert.IsNull(FuelEstimate.From(new RaceDataContext { GameState = frame, Memory = memory }));
                if (tick == 15) Assert.IsNotNull(FuelEstimate.From(new RaceDataContext { GameState = frame, Memory = memory }));
            }

            var estimate = FuelEstimate.From(new RaceDataContext { GameState = frame, Memory = memory });
            Assert.IsNotNull(estimate);
            Assert.IsTrue(estimate.ProvisionalConsumption);
            Assert.AreEqual(4, estimate.PerLap, 0.001);
            StringAssert.Contains("Erste Schätzung", LocalAnswers.TryAnswer("wie viel Sprit haben wir noch?", frame, memory));
            var jump = State(0, false, 5.5f);
            jump.Now = frame.Now.AddSeconds(0.5);
            jump.SourceTelemetryUtc = jump.Now;
            jump.SessionData.SessionType = SessionType.Race;
            jump.SessionData.SessionPhase = SessionPhase.Green;
            jump.SessionData.TrackDefinition = track;
            jump.SessionData.SessionLapsRemaining = 2;
            jump.PositionAndMotionData.DistanceRoundTrack = 3200;
            jump.PositionAndMotionData.CarSpeed = 40;
            memory.Update(frame, jump, jump.Now);
            Assert.IsNull(FuelEstimate.From(new RaceDataContext { GameState = jump, Memory = memory }));
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
