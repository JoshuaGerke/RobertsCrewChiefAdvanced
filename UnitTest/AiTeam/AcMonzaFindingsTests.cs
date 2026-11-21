using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Assetto Corsa 21.11. (Monza, Training – Qualifying – Rennen über 5 Runden, 26 Autos): Funk im Startgrid,
    /// nachgereichte Kurzsätze, doppelte Begrüßung, schwarze Flagge mit Versetzen in die Garage und flackerndes Sektor-Gelb.
    /// Die Zeitpunkte stammen aus der Aufnahme dieses Tages.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class AcMonzaFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 14, 55, 30, DateTimeKind.Utc);

        [SetUp]
        public void Setup() { RaceExclusion.Reset(); }

        [TearDown]
        public void TearDown() { RaceExclusion.Reset(); }

        private static GameStateData Race(int session = 4, int laps = 1)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = session; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.CompletedLaps = laps; s.SessionData.Flag = FlagEnum.GREEN;
            return s;
        }

        private static FieldView Garage(FieldSession session, FieldPhase phase, int playerLaps)
        {
            return new FieldView
            {
                Utc = T, Sim = "AC", Track = "monza", Layout = "monza_rare", Session = session, Phase = phase,
                PlayerInGarage = true, NumCars = 26, MaxPlace = 26,
                Player = new FieldCarView { IsPlayer = true, CompletedLaps = playerLaps, Motion = CarMotion.Stopped },
            };
        }

        // ---------- Startgrid ----------

        [Test]
        public void No_hazard_calls_on_the_grid_before_the_launch()
        {
            Assert.IsTrue(RaceTrafficWatch.StartGridQuiet(0, DateTime.MinValue, T), "steht noch im Grid");
            Assert.IsTrue(RaceTrafficWatch.StartGridQuiet(0, T, T.AddSeconds(3)), "erste Meter nach dem Losfahren");
            Assert.IsFalse(RaceTrafficWatch.StartGridQuiet(0, T, T.AddSeconds(9)), "nach der Schonzeit wieder wach");
            Assert.IsFalse(RaceTrafficWatch.StartGridQuiet(1, DateTime.MinValue, T), "ab der ersten Runde nie stumm");
        }

        // ---------- Nachreichen unterbrochener Meldungen ----------

        [Test]
        public void Short_acknowledgements_and_time_critical_calls_are_not_resumed()
        {
            Assert.IsFalse(InterruptedRadio.Resumable(null, TeamMember.Ben, "Ben: Alles klar, Rob."), "Kurzbestätigung");
            Assert.IsTrue(InterruptedRadio.Resumable(null, TeamMember.Ben, "Dein Verbrauch liegt bei 3,1 Litern pro Runde."), "Antwort an Rob");
            Assert.IsFalse(InterruptedRadio.Resumable("pit-exit", TeamMember.Mik, "Achte auf den Verkehr, wenn du jetzt aus der Box kommst."));
            Assert.IsFalse(InterruptedRadio.Resumable("race-hazard-ahead", TeamMember.Ben, "Achtung, vor dir steht ein Auto quer auf der Strecke."));
            Assert.IsTrue(InterruptedRadio.Resumable("fuel-baseline", TeamMember.Mik, "Verbrauch steht: 3,1 Liter pro Runde."));
        }

        [Test]
        public void Interrupted_message_is_dropped_when_the_session_phase_changed()
        {
            var r = new InterruptedRadio();
            r.Note(TeamMember.Mik, "Verbrauch steht: 3,1 Liter pro Runde.", "fuel-baseline", T, "4|Race|Countdown");
            Assert.IsNull(r.TryTake(T.AddSeconds(3), true, "4|Race|Green", out _, out _), "Countdown vorbei, Rennen läuft");
            r.Note(TeamMember.Mik, "Verbrauch steht: 3,1 Liter pro Runde.", "fuel-baseline", T, "4|Race|Green");
            Assert.IsNotNull(r.TryTake(T.AddSeconds(3), true, "4|Race|Green", out _, out _), "gleiche Phase: nachreichen");
        }

        // ---------- Begrüßung im Menü ----------

        [Test]
        public void Menu_greeting_is_skipped_when_the_session_greeting_already_ran()
        {
            var practice = Race(); practice.SessionData.SessionType = SessionType.Practice;
            var view = Garage(FieldSession.Practice, FieldPhase.Green, 0);
            Assert.IsNull(AiTeamService.MenuGreetingSkip(view, practice, null, false), "noch nicht begrüßt");
            var greeting = new ObservedRaceCall { Topic = "greeting", Text = "Alles klar, Rob.", DetectedUtc = T, ExpiresUtc = T.AddSeconds(45) };
            Assert.IsTrue(greeting.TryReserve(T));
            Assert.AreEqual("session_greeting_already_given", AiTeamService.MenuGreetingSkip(view, practice, greeting, false));
            Assert.AreEqual("track_welcome_already_given", AiTeamService.MenuGreetingSkip(view, practice, null, true));
            var quali = Garage(FieldSession.Qualify, FieldPhase.Green, 0);
            Assert.IsNull(AiTeamService.MenuGreetingSkip(quali, practice, greeting, false), "neue Session darf begrüßt werden");
        }

        [Test]
        public void No_race_greeting_after_the_black_flag_put_Rob_in_the_garage()
        {
            // 14:55:37: Rennen, Phase Green, Rob in der Garage, noch 4 Runden – Ben begrüßte zum Rennen.
            var view = Garage(FieldSession.Race, FieldPhase.Green, 1);
            Assert.AreEqual("race_underway_or_over", AiTeamService.MenuGreetingSkip(view, Race(), null, false));
            var black = Race(laps: 0); black.SessionData.Flag = FlagEnum.BLACK;
            Assert.AreEqual("player_out_of_session", AiTeamService.MenuGreetingSkip(Garage(FieldSession.Race, FieldPhase.Garage, 0), black, null, false));
            Assert.IsNull(AiTeamService.MenuGreetingSkip(Garage(FieldSession.Race, FieldPhase.Garage, 0), Race(laps: 0), null, false), "vor dem Start: begrüßen");
        }

        [Test]
        public void Same_game_session_is_greeted_once_even_when_the_session_type_flips()
        {
            // RaceRoom Mantorp 21.11.: 15:15:08 „Hotlap“, 15:16:45 „Qualifying“ – laut Spiel durchgehend dasselbe Qualifying.
            string key = "RaceRoom|Mantorp Park||Qualify";
            var at = new DateTime(2026, 11, 21, 15, 15, 8, DateTimeKind.Utc);
            Assert.IsTrue(AiTeamService.FieldAlreadyGreeted(key, at, key, at.AddSeconds(97)));
            Assert.IsFalse(AiTeamService.FieldAlreadyGreeted(key, at, "RaceRoom|Mantorp Park||Practice", at.AddMinutes(11)), "neue Session");
            Assert.IsFalse(AiTeamService.FieldAlreadyGreeted(key, DateTime.MinValue, key, at), "Begrüßung noch nicht gespielt");
            Assert.IsFalse(AiTeamService.FieldAlreadyGreeted(null, at, null, at.AddSeconds(5)), "ohne Lagebild keine Sperre");
        }

        // ---------- Schwarze Flagge ----------

        [Test]
        public void Black_flag_stays_mandatory_and_blocks_routine_calls()
        {
            var black = Race(); black.SessionData.Flag = FlagEnum.BLACK;
            Assert.IsTrue(RaceExclusion.Observe(black));
            Assert.IsFalse(RaceExclusion.Observe(black), "nur einmal melden");
            var garage = Race(); garage.PitData.InPitlane = true; // AC hat Rob versetzt, Flagge liegt nicht mehr an
            Assert.IsTrue(RaceExclusion.Active(garage));
            Assert.IsTrue(FlagWatch.StillRelevant(FlagWatch.Phrases[5], garage), "Ansage der schwarzen Flagge fällt nicht weg");
            Assert.IsFalse(RaceExclusion.Allowed("pit-limit"));
            Assert.IsFalse(RaceExclusion.Allowed("focus-brief"));
            Assert.IsFalse(RaceExclusion.Allowed(null));
            Assert.IsTrue(RaceExclusion.Allowed("warning-flag-5"));
            Assert.IsTrue(RaceExclusion.Allowed("race-disqualified"));
            Assert.IsFalse(RaceExclusion.Active(Race(session: 5)), "nächste Session wieder frei");
        }

        [Test]
        public void Black_flag_call_is_valid_in_the_pit_lane_only_when_Rob_is_out()
        {
            var black = Race(); black.SessionData.Flag = FlagEnum.BLACK;
            var call = new ObservedRaceCall { Topic = "warning-flag-5", Text = FlagWatch.Phrases[5], SessionId = 4, SessionType = SessionType.Race,
                Position = -1, CompletedLaps = -1, DetectedUtc = T, ExpiresUtc = T.AddSeconds(8) };
            var garage = Race(); garage.PitData.InPitlane = true;
            Assert.IsFalse(call.IsValid(garage, T.AddSeconds(1)), "ohne Sperre gilt die alte Regel");
            RaceExclusion.Observe(black);
            Assert.IsTrue(call.IsValid(garage, T.AddSeconds(1)));
        }

        // ---------- Sektor-Gelb ----------

        private static GameStateData Flag(FlagEnum f) { var g = Race(); g.SessionData.Flag = f; return g; }

        [Test]
        public void Flickering_sector_yellow_is_not_announced()
        {
            var watch = new FlagWatch();
            // 14:38:11,9: 0,6 s Gelb
            CollectionAssert.IsEmpty(watch.Evaluate(Flag(FlagEnum.GREEN), Flag(FlagEnum.YELLOW), T));
            CollectionAssert.IsEmpty(watch.Evaluate(Flag(FlagEnum.YELLOW), Flag(FlagEnum.YELLOW), T.AddSeconds(0.5)));
            CollectionAssert.IsEmpty(watch.Evaluate(Flag(FlagEnum.YELLOW), Flag(FlagEnum.GREEN), T.AddSeconds(0.6)));
            // 14:50:36,3: 25 s Gelb
            var start = T.AddSeconds(10);
            CollectionAssert.IsEmpty(watch.Evaluate(Flag(FlagEnum.GREEN), Flag(FlagEnum.YELLOW), start));
            CollectionAssert.IsEmpty(watch.Evaluate(Flag(FlagEnum.YELLOW), Flag(FlagEnum.YELLOW), start.AddSeconds(1)));
            CollectionAssert.AreEqual(new[] { FlagWatch.Phrases[2] }, watch.Evaluate(Flag(FlagEnum.YELLOW), Flag(FlagEnum.YELLOW), start.AddSeconds(1.6)));
            CollectionAssert.IsEmpty(watch.Evaluate(Flag(FlagEnum.YELLOW), Flag(FlagEnum.YELLOW), start.AddSeconds(5)), "nur einmal");
        }

        // ---------- Keine Telemetrie ----------

        [Test]
        public void Missing_telemetry_says_whether_Crew_Chief_was_started()
        {
            StringAssert.Contains("nicht gestartet", RaceDataTools.NoTelemetryReason(false));
            StringAssert.Contains("Spiel liefert", RaceDataTools.NoTelemetryReason(true));
        }
    }
}
