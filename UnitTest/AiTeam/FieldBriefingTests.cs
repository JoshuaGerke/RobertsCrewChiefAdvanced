using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 21.11.2026, RaceRoom Monza: Begrüßung schon im Boxenmenü mit Strecke, Feld und Ablauf; Auswertung in der Box nach
    /// Training und Quali; Unterbrochenes wird nachgereicht.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class FieldBriefingTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 9, 5, 0, DateTimeKind.Utc);
        private bool wasDeterministic; private string wasLanguage;

        [SetUp] public void Setup() { wasDeterministic = RadioVariants.Deterministic; wasLanguage = RadioLanguage.Current; RadioVariants.Deterministic = true; RadioLanguage.Current = RadioLanguage.German; }
        [TearDown] public void TearDown() { RadioVariants.Deterministic = wasDeterministic; RadioLanguage.Current = wasLanguage; }

        private static FieldCarView Car(string name, int place, float best = float.NaN, float top = float.NaN, bool player = false) =>
            new FieldCarView { Name = name, Place = place, PlaceClass = place, BestLap = best, TopSpeed = top, IsPlayer = player, Motion = CarMotion.Normal, FastestAnyLap = best };

        private static FieldView Menu(FieldSession session, float duration = 600, params int[] raceMinutes)
        {
            var cars = new List<FieldCarView> { Car("Rob", 17, player: true) };
            for (int i = 2; i <= 31; i++) cars.Add(Car("Fahrer " + i, i == 17 ? 1 : i));
            var mins = new[] { -1, -1, -1 };
            for (int i = 0; i < raceMinutes.Length && i < 3; i++) mins[i] = raceMinutes[i];
            return new FieldView { Utc = T, Sim = "RaceRoom", Track = "Autodromo di Monza", Layout = "Grand Prix", TrackLength = 5779, Session = session,
                Phase = FieldPhase.Garage, InMenus = true, PlayerInGarage = true, TimedSession = true, TimeDuration = duration, TimeRemaining = duration,
                NumCars = 21, MaxPlace = 31, RaceMinutes = mins, RaceLaps = new[] { -1, -1, -1 }, Cars = cars, Player = cars[0] };
        }

        [Test]
        public void MenuBriefing_names_track_field_session_and_race_length()
        {
            string text = MenuBriefing.Compose(Menu(FieldSession.Practice, 600, 20), "Rob", "Ben", 0);
            StringAssert.Contains("Rob, heute sind wir in Autodromo di Monza.", text);
            StringAssert.Contains("31 Fahrer im Feld.", text, "Feldgröße aus der Sim, nicht die 21 aus der Fahrerliste");
            StringAssert.Contains("Vor uns liegen 10 Minuten Training.", text);
            StringAssert.Contains("Danach das Rennen über 20 Minuten.", text);
            StringAssert.DoesNotContain("Grad", text, "RaceRoom meldet keine Temperatur – nichts erfinden");

            string race = MenuBriefing.Compose(Menu(FieldSession.Race, 1200, 20), "Rob", "Ben", 1);
            StringAssert.Contains("Das Rennen geht über 20 Minuten.", race);
            StringAssert.DoesNotContain("Danach", race);
        }

        [Test]
        public void MenuBriefing_has_five_variants_in_three_languages_and_needs_menu_state()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var texts = Enumerable.Range(0, MenuBriefing.Variants).Select(i => MenuBriefing.Compose(Menu(FieldSession.Qualify, 600, 20, 15), "Rob", "Ben", i)).ToList();
                Assert.AreEqual(5, texts.Distinct().Count(), lang);
                Assert.IsTrue(texts.All(t => char.IsUpper(t[0])), lang);
            }
            var driving = Menu(FieldSession.Practice); driving.InMenus = false; driving.PlayerInGarage = false; driving.Phase = FieldPhase.Green;
            Assert.IsNull(MenuBriefing.Compose(driving, "Rob", "Ben", 0), "nur im Menü/in der Garage");
            var unknown = Menu(FieldSession.Unknown);
            Assert.IsNull(MenuBriefing.Compose(unknown, "Rob", "Ben", 0));
        }

        private static FieldView Done(FieldSession session, float robBest, float robFastestAny, float robTop)
        {
            var cars = new List<FieldCarView>();
            var rob = Car("Rob", 24, robBest, robTop, true); rob.FastestAnyLap = robFastestAny; rob.InPit = true; rob.Speed = 0;
            cars.Add(rob);
            float[] bests = { 105.157f, 105.3f, 105.9f, 106.1f, 106.5f, 107.0f, 108.2f };
            for (int i = 0; i < bests.Length; i++) cars.Add(Car("Fahrer " + (i + 1), i + 1, bests[i], 75f - i));
            return new FieldView { Utc = T, Sim = "RaceRoom", Track = "Monza", Session = session, Phase = FieldPhase.Checkered, TimedSession = true,
                TimeRemaining = 0, Cars = cars, Player = rob, RaceMinutes = new[] { -1, -1, -1 }, RaceLaps = new[] { -1, -1, -1 } };
        }

        [Test]
        public void Debrief_after_quali_names_deleted_lap_rank_and_gaps()
        {
            // Monza 21.11.: alle Quali-Runden gestrichen – „Deine gültige Bestzeit fehlt“ war zu wenig.
            string text = SessionDebrief.Compose(Done(FieldSession.Qualify, float.NaN, 106.3f, 80f), "Rob", 3.1f, 4, 0);
            StringAssert.Contains("leider gestrichen", text);
            StringAssert.Contains("Rang 5", text);
            StringAssert.Contains("1,1 Sekunden auf die Spitze", text);
            StringAssert.Contains("auf Platz 3", text);
            StringAssert.Contains("Verbrauch 3,1 Liter pro Runde, über 4 Runden gemessen.", text);
            StringAssert.Contains("Auf der Geraden bist du stark", text, "Rob war auf der Geraden der Schnellste");
        }

        [Test]
        public void Debrief_never_ranks_a_lap_fragment_as_deleted_best()
        {
            // RaceRoom Mantorp 22.11.: „Deine schnellste Runde war 28,274, leider gestrichen. Gültig wäre das Rang 1 gewesen.“ (3,1 km Strecke)
            RadioLanguage.Current = RadioLanguage.German;
            var v = Done(FieldSession.Practice, float.NaN, 28.274f, 80f); v.TrackLength = 3092.66f;
            string text = SessionDebrief.Compose(v, "Rob", 1.1f, 2, 0);
            StringAssert.DoesNotContain("28,274", text);
            StringAssert.DoesNotContain("leider gestrichen", text);
            StringAssert.DoesNotContain("Rang 1", text);
            // Ohne Streckenlänge greift die Feldprobe: 28 s gegen 1:45 im Feld ist keine Runde.
            var w = Done(FieldSession.Practice, float.NaN, 28.274f, 80f); w.TrackLength = float.NaN;
            StringAssert.DoesNotContain("leider gestrichen", SessionDebrief.Compose(w, "Rob", 1.1f, 2, 0));
            // Eine echte gestrichene Runde wird weiter genannt.
            var real = Done(FieldSession.Practice, float.NaN, 106.3f, 80f); real.TrackLength = 5793f;
            StringAssert.Contains("leider gestrichen", SessionDebrief.Compose(real, "Rob", 1.1f, 2, 0));
        }

        [TestCase(28.274f, 3092.66f, false)]
        [TestCase(80.5f, 3092.66f, true)]
        [TestCase(28.274f, float.NaN, true)]
        [TestCase(61f, 5793f, true)]
        public void Plausible_lap_needs_a_believable_average_speed(float seconds, float length, bool plausible)
        {
            Assert.AreEqual(plausible, FieldPicture.PlausibleLap(seconds, length));
        }

        [Test]
        public void Debrief_after_practice_in_three_languages_with_five_variants()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var texts = Enumerable.Range(0, SessionDebrief.Variants).Select(i => SessionDebrief.Compose(Done(FieldSession.Practice, 113.784f, 113.784f, 60f), "Rob", 3.3f, 1, i)).ToList();
                Assert.AreEqual(5, texts.Distinct().Count(), lang);
                Assert.IsTrue(texts.All(t => char.IsUpper(t[0])), lang);
            }
            RadioLanguage.Current = RadioLanguage.German;
            string one = SessionDebrief.Compose(Done(FieldSession.Practice, 113.784f, 113.784f, 60f), "Rob", 3.3f, 1, 0);
            StringAssert.Contains("nur grob", one, "eine Runde: als grobe Schätzung kennzeichnen");
            StringAssert.Contains("Auf der Geraden fehlt Tempo", one);
            Assert.IsNull(SessionDebrief.Compose(Done(FieldSession.Race, 1, 1, 1), "Rob", 3, 3, 0), "nach dem Rennen gibt es die Rennbilanz");
        }

        [Test]
        public void Greeting_field_line_never_says_zero_cars_out_on_track()
        {
            // Monza 21.11.: „21 Autos im Feld, 0 davon gerade draußen“.
            var gs = new GameStateData(T.Ticks); gs.SessionData.SessionType = SessionType.Practice;
            for (int i = 0; i < 4; i++) gs.OpponentData["c" + i] = new OpponentData { DriverRawName = "c" + i, InPits = true };
            string line = SessionGreetingDirector.FieldLine(gs);
            StringAssert.Contains("5 Autos im Feld, noch alle in der Box.", line);
            StringAssert.DoesNotContain("0 davon", line);
        }

        [Test]
        public void Interrupted_message_is_resumed_once_when_radio_is_free()
        {
            var r = new InterruptedRadio();
            r.Note(TeamMember.Mik, "Verbrauch steht: 3,1 Liter pro Runde.", "fuel-baseline", T);
            Assert.IsNull(r.TryTake(T.AddSeconds(1), true, out _, out _), "erst kurze Pause");
            Assert.IsNull(r.TryTake(T.AddSeconds(2), false, out _, out _), "Funk belegt");
            string again = r.TryTake(T.AddSeconds(3), true, out var who, out var topic);
            Assert.AreEqual("Noch mal von eben: Verbrauch steht: 3,1 Liter pro Runde.", again);
            Assert.AreEqual(TeamMember.Mik, who);
            Assert.IsNull(r.TryTake(T.AddSeconds(4), true, out _, out _), "nur einmal");
            r.Note(TeamMember.Mik, again, "resume", T.AddSeconds(5));
            Assert.IsNull(r.TryTake(T.AddSeconds(8), true, out _, out _), "Nachgereichtes wird nicht erneut nachgereicht");

            r.Note(TeamMember.Ben, "Achtung, Dreher vor dir!", "race-hazard-ahead", T);
            r.Note(TeamMember.Ben, "Platz 23.", "position", T);
            r.Note(TeamMember.Sara, "Gelb vorbei.", null, T);
            Assert.IsNull(r.TryTake(T.AddSeconds(3), true, out _, out _), "Zeitkritisches und Sara werden nicht nachgereicht");

            r.Note(TeamMember.Mik, "Reifen vorne 1,8 Prozent.", null, T);
            Assert.IsNull(r.TryTake(T.AddSeconds(30), true, out _, out _), "nach 25 s veraltet");
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(5, Enumerable.Range(0, 5).Select(InterruptedRadio.LeadIn).Distinct().Count(), lang);
            }
        }
    }
}
