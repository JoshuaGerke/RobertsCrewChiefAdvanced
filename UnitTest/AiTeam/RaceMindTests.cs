using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Erkenntnisse aus Robs RaceRoom-Rennen 16.11.2026: Vorfallpunkte, letzte Runde im Zeitrennen, Platzmeldungen, Zwischenresümee.</summary>
    [TestFixture, Category("AiTeam")]
    public class RaceMindTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 16, 15, 40, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Race(int classPosition = 12)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 5; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = classPosition; s.SessionData.OverallPosition = classPosition; s.SessionData.CompletedLaps = 6;
            s.SessionData.TrackDefinition = new TrackDefinition("Mantorp Park", 3106f);
            s.PositionAndMotionData.CarSpeed = 45; s.PositionAndMotionData.DistanceRoundTrack = 1000;
            return s;
        }

        [Test]
        public void Incident_points_are_warned_in_steps_before_the_limit()
        {
            var watch = new IncidentPointsWatch();
            var gs = Race(); gs.SessionData.HasLimitedIncidents = true; gs.SessionData.MaxIncidentCount = 30; gs.SessionData.CurrentIncidentCount = 4;
            watch.Observe(gs, T);
            Assert.IsNull(watch.Take(gs, T, false, true), "Ausgangsstand beim Verbinden wird nicht gemeldet");
            gs.SessionData.CurrentIncidentCount = 15; watch.Observe(gs, T.AddSeconds(1));
            var half = watch.Take(gs, T.AddSeconds(1), false, true);
            Assert.IsNotNull(half); StringAssert.Contains("15 von 30", half.Text); half.MarkPlaybackStarted(T.AddSeconds(1));
            gs.SessionData.CurrentIncidentCount = 16; watch.Observe(gs, T.AddSeconds(2));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(2), false, true), "gleiche Stufe nicht erneut");
            gs.SessionData.CurrentIncidentCount = 27; watch.Observe(gs, T.AddSeconds(3));
            var tight = watch.Take(gs, T.AddSeconds(3), false, true);
            Assert.IsNotNull(tight); Assert.AreEqual(7, tight.Priority); StringAssert.Contains("3", tight.Text); tight.MarkPlaybackStarted(T.AddSeconds(3));
            gs.SessionData.CurrentIncidentCount = 29; watch.Observe(gs, T.AddSeconds(4));
            var last = watch.Take(gs, T.AddSeconds(4), false, true);
            Assert.IsNotNull(last); Assert.AreEqual(7, last.Priority); StringAssert.Contains("Punkt", last.Text);
        }

        [Test]
        public void Without_a_limit_nothing_is_claimed()
        {
            var watch = new IncidentPointsWatch();
            var gs = Race(); gs.SessionData.HasLimitedIncidents = false; gs.SessionData.MaxIncidentCount = -1; gs.SessionData.CurrentIncidentCount = 10;
            watch.Observe(gs, T); gs.SessionData.CurrentIncidentCount = 20; watch.Observe(gs, T.AddSeconds(1));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(1), false, true));
            Assert.AreEqual(5, IncidentPointsWatch.Level(29, 30)); Assert.AreEqual(4, IncidentPointsWatch.Level(27, 30));
            Assert.AreEqual(3, IncidentPointsWatch.Level(23, 30)); Assert.AreEqual(2, IncidentPointsWatch.Level(15, 30));
        }

        private static GameStateData TimedExpired(float leaderDistance, float ownDistance)
        {
            var s = Race();
            s.SessionData.SessionHasFixedTime = true; s.SessionData.SessionTotalRunTime = 900; s.SessionData.SessionRunningTime = 900; s.SessionData.SessionTimeRemaining = 0;
            s.PositionAndMotionData.DistanceRoundTrack = ownDistance;
            var leader = new OpponentData { OverallPosition = 1, ClassPosition = 1, DistanceRoundTrack = leaderDistance, IsActive = true, DriverRawName = "Leader" };
            s.OpponentData["leader"] = leader;
            return s;
        }

        [Test]
        public void Expired_clock_says_last_lap_when_the_leader_reaches_the_line_first()
        {
            LapWatch.ForgetTimedFinish();
            var before = Race(); var s = TimedExpired(2500, 900);
            string text = new LapWatch().Evaluate(before, s, -1, -1, false, true, null, null, "Rob");
            StringAssert.Contains("letzte Runde", text);
            Assert.AreEqual(1, LapWatch.LapsLeft(s.SessionData));
            LapWatch.ForgetTimedFinish();
        }

        [Test]
        public void Expired_clock_announces_one_more_lap_when_we_cross_the_line_first()
        {
            LapWatch.ForgetTimedFinish();
            var before = Race(); var s = TimedExpired(300, 2900);
            var watch = new LapWatch();
            StringAssert.Contains("Noch diese Runde", watch.Evaluate(before, s, -1, -1, false));
            Assert.AreEqual(2, LapWatch.LapsLeft(s.SessionData));
            var next = TimedExpired(800, 10); next.SessionData.CompletedLaps = 7; next.SessionData.IsNewLap = true;
            Assert.AreEqual(1, LapWatch.LapsLeft(next.SessionData));
            StringAssert.Contains("Letzte Runde", watch.Evaluate(s, next, -1, -1, false));
            LapWatch.ForgetTimedFinish();
        }

        [Test]
        public void Position_texts_vary_and_never_call_a_loss_progress()
        {
            var s = Race().SessionData;
            var up = Enumerable.Range(0, 5).Select(i => ActiveRaceObserver.MovedUp(s, 13, i, "Rob")).ToArray();
            var down = Enumerable.Range(0, 5).Select(i => ActiveRaceObserver.PositionLost(s, 17, i, "Rob")).ToArray();
            Assert.AreEqual(5, up.Distinct().Count()); Assert.AreEqual(5, down.Distinct().Count());
            Assert.IsFalse(down.Any(t => t.Contains("vorgerückt")));
            Assert.IsFalse(Enumerable.Range(0, 3).Select(i => ActiveRaceObserver.PositionNow(s, 17, i)).Any(t => t.Contains("vorgerückt")));
        }

        [Test]
        public void Calm_requires_five_seconds_of_free_road()
        {
            var gs = Race();
            Assert.IsTrue(RaceSummaryWatch.Calm(gs, 8, 9));
            Assert.IsFalse(RaceSummaryWatch.Calm(gs, 4.5f, 9));
            gs.OpponentData["near"] = new OpponentData { IsActive = true, DistanceRoundTrack = 1150 }; // 150 m = 3,3 s bei 45 m/s
            Assert.IsFalse(RaceSummaryWatch.Calm(gs, -1, -1));
            gs.OpponentData["near"].DistanceRoundTrack = 1400; // 400 m = 8,9 s
            Assert.IsTrue(RaceSummaryWatch.Calm(gs, -1, -1));
            gs.FlagData.isLocalYellow = true;
            Assert.IsFalse(RaceSummaryWatch.Calm(gs, -1, -1));
        }

        [Test]
        public void Summary_closing_line_fits_the_situation_and_varies()
        {
            var s = Race().SessionData;
            Assert.AreEqual(RaceSummaryWatch.Mood.Attack, RaceSummaryWatch.ChooseMood(s, 6f, 20f, -1f, 0f, 3, 0, false));
            Assert.AreEqual(RaceSummaryWatch.Mood.Defend, RaceSummaryWatch.ChooseMood(s, 30f, 6f, 0f, -1f, 0, 3, false));
            Assert.AreEqual(RaceSummaryWatch.Mood.Safe, RaceSummaryWatch.ChooseMood(s, 6f, 6f, -1f, -1f, 3, 3, true));
            Assert.AreEqual(RaceSummaryWatch.Mood.Steady, RaceSummaryWatch.ChooseMood(s, 40f, 30f, 0f, 0f, 3, 3, false));
            var lines = Enumerable.Range(0, 4).Select(i => RaceSummaryWatch.Closing(RaceSummaryWatch.Mood.Attack, i, "Rob")).ToArray();
            Assert.AreEqual(4, lines.Distinct().Count());
            StringAssert.Contains("Rob", lines[0]);
            StringAssert.DoesNotContain(", ,", RaceSummaryWatch.Closing(RaceSummaryWatch.Mood.Attack, 0, ""));
        }

        [Test]
        public void Planned_summaries_scale_with_race_length()
        {
            var s = Race().SessionData; s.SessionHasFixedTime = true;
            s.SessionTotalRunTime = 10 * 60; Assert.AreEqual(1, RaceSummaryWatch.PlannedCount(s));
            s.SessionTotalRunTime = 25 * 60; Assert.AreEqual(2, RaceSummaryWatch.PlannedCount(s));
            s.SessionTotalRunTime = 60 * 60; Assert.AreEqual(3, RaceSummaryWatch.PlannedCount(s));
        }

        [Test]
        public void Summary_is_composed_from_measured_values_only()
        {
            var gs = Race(12); gs.SessionData.SessionStartClassPosition = 19;
            var ctx = new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T, Memory = new AiRaceMemory(), DriverName = "Rob" };
            string text = new RaceSummaryWatch(7).Compose(ctx, -1, -1);
            StringAssert.Contains("Rob", text);
            StringAssert.Contains("Platz 12", text);
            StringAssert.Contains("7 Plätze gutgemacht", text);
            StringAssert.DoesNotContain("Sekunden", text, "ohne gemessene Abstände keine Abstandszahl");
        }

        [Test]
        public void Start_position_question_is_answered_locally()
        {
            var gs = Race(5); gs.SessionData.SessionStartClassPosition = 9;
            StringAssert.Contains("Platz 9", LocalAnswers.TryAnswer("Welche Position zum Start?", gs, null));
            StringAssert.Contains("Plus 4", LocalAnswers.TryAnswer("Welche Position zum Start?", gs, null));
            gs.SessionData.SessionPhase = SessionPhase.Formation; gs.SessionData.ClassPosition = 9;
            Assert.AreEqual("Du startest von Platz 9.", LocalAnswers.TryAnswer("Startplatz?", gs, null));
            gs.SessionData.SessionPhase = SessionPhase.Green; gs.SessionData.SessionStartClassPosition = 0;
            StringAssert.Contains("nicht sicher", LocalAnswers.TryAnswer("Von welchem Platz bin ich gestartet?", gs, null));
            Assert.IsNull(LocalAnswers.TryAnswer("Startposition vom Vordermann?", gs, null), "Gegnerfrage nicht als eigener Startplatz");
        }

        [Test]
        public void Short_position_question_without_my_means_own_position()
        {
            var gs = Race(7); gs.SessionData.NumCarsInPlayerClass = 20;
            Assert.AreEqual("Platz 7 von 20.", LocalAnswers.TryAnswer("Welche Position?", gs, null));
        }

        [Test]
        public void Opponent_stops_on_track_are_counted_once_per_standstill()
        {
            OpponentStopLog.Reset();
            var gs = Race(); gs.SessionData.SessionId = 77;
            var op = new OpponentData { DriverRawName = "Sergi Reche", CarNumber = "12", IsActive = true, Speed = 40 };
            gs.OpponentData["reche"] = op;
            float t = 100;
            void Tick(float speed, float seconds) { op.Speed = speed; for (float x = 0; x < seconds; x += .5f) { gs.SessionData.SessionRunningTime = t += .5f; OpponentStopLog.Observe(gs); } }
            Tick(40, 2); Tick(0, 5); Tick(0, 5);
            Assert.AreEqual(1, OpponentStopLog.Stops(op, out bool now)); Assert.IsTrue(now);
            Tick(30, 2); Tick(0.5f, 4);
            Assert.AreEqual(2, OpponentStopLog.Stops(op, out now));
            op.InPits = true; Tick(0, 10); op.InPits = false;
            Assert.AreEqual(2, OpponentStopLog.Stops(op, out now), "Stehen in der Box zählt nicht"); Assert.IsFalse(now);
            var never = new OpponentData { DriverRawName = "Ohne Tempo", CarNumber = "3", IsActive = true, Speed = 0 };
            gs.OpponentData["never"] = never; Tick(0, 10);
            Assert.AreEqual(0, OpponentStopLog.Stops(never, out now), "ohne gemessene Fahrt keine Behauptung");
            OpponentStopLog.Reset();
        }

        [Test]
        public void Gap_trend_projects_catch_only_for_a_consistent_trend()
        {
            Assert.AreEqual(.3f, GapTrendWatch.ClosingRate(new[] { 4.0f, 3.7f, 3.4f, 3.1f }), .001f);
            Assert.AreEqual(.3f, GapTrendWatch.ClosingRate(new[] { 4.0f, 3.6f, 3.65f, 3.1f }), .001f, "ein Ausreißer ist erlaubt");
            Assert.AreEqual(0f, GapTrendWatch.ClosingRate(new[] { 4.0f, 3.2f, 4.1f, 3.1f }), "Zickzack ist kein Trend");
            Assert.AreEqual(0f, GapTrendWatch.ClosingRate(new[] { 4.0f, 3.7f, 3.4f }), "unter drei Runden Trend keine Aussage");
            Assert.AreEqual(8, GapTrendWatch.LapsToCatch(3.1f, .3f));
            Assert.AreEqual(-1, GapTrendWatch.LapsToCatch(3.1f, .1f));
            Assert.AreEqual("3 Zehntel", GapTrendWatch.Rate(.31f));
            Assert.AreEqual("ein Zehntel", GapTrendWatch.Rate(.12f));
            string a = GapTrendWatch.AheadText("Keller", .3f, 5, false, -1, 2.3f, 0);
            Assert.AreEqual("Du holst 3 Zehntel pro Runde auf Keller. In etwa 5 Runden bist du dran.", a);
            string late = GapTrendWatch.AheadText("Keller", .2f, 12, true, 6, 3.3f, 0);
            StringAssert.Contains("reicht so knapp nicht", late); StringAssert.Contains("5 Zehntel", late);
            StringAssert.StartsWith("Der Hintermann", GapTrendWatch.BehindText("", .4f, 4, false, 0));
            StringAssert.Contains("bis zur Flagge reicht es ihm so nicht", GapTrendWatch.BehindText("Wurz", .4f, 9, true, 0));
        }

        // ---------- Renningenieur-Kopf (RaceMind) ----------

        private static RaceSituation Situation(RacePhase phase, RaceAttention attention, RaceFocus focus = RaceFocus.Rhythm) =>
            new RaceSituation { Utc = T, SessionId = 5, Phase = phase, Attention = attention, Focus = focus };

        private static ObservedRaceCall Call(string topic, int priority = 3, SessionType type = SessionType.Race) =>
            new ObservedRaceCall { Topic = topic, Text = topic, Priority = priority, SessionId = 5, SessionType = type, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };

        private static MindVerdict Judge(RaceSituation x, string topic, int priority = 3) => RaceMind.Judge(x, Call(topic, priority), T, out _);

        [Test]
        public void Mind_knows_the_race_phase()
        {
            var gs = Race(); var s = gs.SessionData;
            s.SessionNumberOfLaps = 20; s.SessionLapsRemaining = 14; s.CompletedLaps = 6;
            Assert.AreEqual(RacePhase.Middle, RaceMind.PhaseOf(s));
            s.CompletedLaps = 2; s.SessionLapsRemaining = 18; Assert.AreEqual(RacePhase.Early, RaceMind.PhaseOf(s));
            s.CompletedLaps = 16; s.SessionLapsRemaining = 4; Assert.AreEqual(RacePhase.Late, RaceMind.PhaseOf(s));
            s.CompletedLaps = 18; s.SessionLapsRemaining = 2; Assert.AreEqual(RacePhase.Final, RaceMind.PhaseOf(s));
            s.IsLastLap = true; Assert.AreEqual(RacePhase.LastLap, RaceMind.PhaseOf(s)); s.IsLastLap = false;
            s.CompletedLaps = 0; s.SessionLapsRemaining = 20; Assert.AreEqual(RacePhase.Start, RaceMind.PhaseOf(s));
            s.SessionPhase = SessionPhase.Formation; Assert.AreEqual(RacePhase.PreStart, RaceMind.PhaseOf(s));
            s.SessionType = SessionType.Practice; s.SessionPhase = SessionPhase.Green; Assert.AreEqual(RacePhase.NotRacing, RaceMind.PhaseOf(s));
        }

        [Test]
        public void Mind_reads_battle_traffic_and_calm_with_hysteresis()
        {
            var gs = Race(); var mind = new RaceMind();
            Assert.AreEqual(RaceAttention.Battle, mind.AttentionOf(gs, T, false, .6f, 9f, null));
            Assert.AreEqual(RaceAttention.Battle, mind.AttentionOf(gs, T.AddSeconds(2), false, 3f, 9f, null), "Zweikampf hält kurz nach");
            Assert.AreEqual(RaceAttention.Open, mind.AttentionOf(gs, T.AddSeconds(4), false, 3f, 9f, null));
            Assert.AreEqual(RaceAttention.Traffic, mind.AttentionOf(gs, T.AddSeconds(5), false, 2f, 9f, null));
            Assert.AreEqual(RaceAttention.Calm, mind.AttentionOf(gs, T.AddSeconds(8), false, 8f, 9f, null));
            Assert.AreEqual(RaceAttention.Battle, mind.AttentionOf(gs, T.AddSeconds(9), true, 8f, 9f, null), "Spotter-Überlappung ist Zweikampf");
            gs.FlagData.isLocalYellow = true;
            Assert.AreEqual(RaceAttention.Caution, mind.AttentionOf(gs, T.AddSeconds(20), false, 8f, 9f, null));
            gs.FlagData.isLocalYellow = false; gs.PitData.InPitlane = true;
            Assert.AreEqual(RaceAttention.Pit, mind.AttentionOf(gs, T.AddSeconds(21), false, 8f, 9f, null));
        }

        [Test]
        public void Mind_sets_the_focus_like_a_race_engineer()
        {
            var x = Situation(RacePhase.Middle, RaceAttention.Open);
            x.LapsLeft = 10;
            Assert.AreEqual(RaceFocus.Rhythm, RaceMind.FocusOf(x));
            x.GapBehind = .8f; Assert.AreEqual(RaceFocus.Defend, RaceMind.FocusOf(x));
            x.GapAhead = 1.2f; Assert.AreEqual(RaceFocus.Attack, RaceMind.FocusOf(x), "vorne in Reichweite geht vor");
            x.GapAhead = 6f; x.FrontLapsToCatch = 12; x.GapBehind = 7f; x.RearLapsToCatch = 2;
            Assert.AreEqual(RaceFocus.Defend, RaceMind.FocusOf(x), "Hintermann in zwei Runden dran");
            x.RearLapsToCatch = 12; Assert.AreEqual(RaceFocus.Rhythm, RaceMind.FocusOf(x), "Einholen erst nach dem Ende zählt nicht");
            x.Phase = RacePhase.Final; Assert.AreEqual(RaceFocus.BringHome, RaceMind.FocusOf(x));
            x.IncidentsTight = true; Assert.AreEqual(RaceFocus.Safe, RaceMind.FocusOf(x));
        }

        [Test]
        public void Mind_never_holds_back_safety_or_own_race_events()
        {
            var battleLastLap = Situation(RacePhase.LastLap, RaceAttention.Battle, RaceFocus.Defend);
            foreach (var topic in new[] { "position", "position-loss", "own-lap", "race-hazard-ahead", "race-traffic-rear", "race-disqualified", "warning-tyre", "pit-limit" })
                Assert.AreEqual(MindVerdict.Admit, Judge(battleLastLap, topic), topic);
            Assert.AreEqual(MindVerdict.Admit, Judge(battleLastLap, "incident-points", 3), "auch der erste Vorfallpunkt");
            Assert.AreEqual(MindVerdict.Admit, Judge(battleLastLap, "weather-rain", 5), "Wetterwarnung");
            Assert.AreEqual(MindVerdict.Admit, Judge(battleLastLap, "chase-target"), "unbekannte Taktikmeldungen bleiben unverändert");
            Assert.AreEqual(MindVerdict.Admit, Judge(battleLastLap, "field-anything", 6), "ab Priorität 6 immer");
        }

        [Test]
        public void Mind_decides_what_matters_now()
        {
            var calm = Situation(RacePhase.Middle, RaceAttention.Calm);
            var open = Situation(RacePhase.Middle, RaceAttention.Open);
            var battle = Situation(RacePhase.Middle, RaceAttention.Battle, RaceFocus.Attack);
            var final = Situation(RacePhase.Final, RaceAttention.Open, RaceFocus.BringHome);
            var lastLap = Situation(RacePhase.LastLap, RaceAttention.Open, RaceFocus.BringHome);
            var start = Situation(RacePhase.Start, RaceAttention.Open);

            Assert.AreEqual(MindVerdict.Admit, Judge(calm, "field-pit"));
            Assert.AreEqual(MindVerdict.Hold, Judge(Situation(RacePhase.Middle, RaceAttention.Battle), "field-pit"), "im Zweikampf warten");
            Assert.AreEqual(MindVerdict.Drop, Judge(battle, "field-pit"), "im Angriff verfallen Nachrichten über andere");
            Assert.AreEqual(MindVerdict.Hold, Judge(start, "status"));
            Assert.AreEqual(MindVerdict.Drop, Judge(final, "field-fastest"));
            Assert.AreEqual(MindVerdict.Drop, Judge(Situation(RacePhase.Middle, RaceAttention.Open, RaceFocus.Defend), "field-pit"), "Fokus auf dem eigenen Kampf");
            Assert.AreEqual(MindVerdict.Hold, Judge(Situation(RacePhase.Middle, RaceAttention.Traffic), "reader-pace-front"));

            Assert.AreEqual(MindVerdict.Hold, Judge(open, "race-summary"), "Zwischenstand nur in ruhiger Minute");
            Assert.AreEqual(MindVerdict.Admit, Judge(calm, "race-summary"));
            Assert.AreEqual(MindVerdict.Drop, Judge(final, "race-summary"));

            Assert.AreEqual(MindVerdict.Hold, Judge(battle, "gap-trend"));
            Assert.AreEqual(MindVerdict.Admit, Judge(final, "gap-trend"), "Hochrechnung zählt gerade am Ende");
            Assert.AreEqual(MindVerdict.Drop, Judge(lastLap, "gap-trend"));

            Assert.AreEqual(MindVerdict.Hold, Judge(battle, "sector-best"));
            Assert.AreEqual(MindVerdict.Admit, Judge(lastLap, "sector-best"), "Sektorbestzeit in der letzten Runde passt");
        }

        [Test]
        public void Mind_is_silent_without_fresh_race_assessment()
        {
            var battle = Situation(RacePhase.Middle, RaceAttention.Battle);
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(battle, Call("field-pit"), T.AddSeconds(3), out _), "veraltete Lage blockiert nichts");
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(battle, Call("field-pit", 3, SessionType.Practice), T, out _), "nur im Rennen");
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(null, Call("field-pit"), T, out _));
        }

        [Test]
        public void Held_call_comes_back_later_without_using_a_delivery_attempt()
        {
            var call = Call("field-pit");
            for (int i = 0; i < 5; i++)
            {
                var at = T.AddSeconds(i * 3);
                Assert.IsTrue(call.TryReserve(at), "Versuch " + i);
                call.HoldBack(at, TimeSpan.FromSeconds(2), "race_mind:test");
                Assert.IsFalse(call.CanReserve(at.AddSeconds(1)));
            }
        }

        [Test]
        public void Situation_describes_itself_for_the_analysis()
        {
            var x = Situation(RacePhase.Late, RaceAttention.Calm, RaceFocus.Attack);
            x.FrontRate = .3f; x.FrontLapsToCatch = 5; x.Opportunities = new[] { "vordermann_einholbar" };
            var o = x.Describe();
            Assert.AreEqual("spaete_phase", (string)o["rennphase"]);
            Assert.AreEqual("ruhig", (string)o["lage"]);
            Assert.AreEqual("angreifen", (string)o["fokus"]);
            Assert.AreEqual(5, (int)o["vorne_dran_in_runden"]);
            Assert.AreEqual(-.3, (double)o["vorne_abstand_aenderung_s_pro_runde"], .001);
            StringAssert.Contains("Hochrechnung", (string)o["hinweis"]);
        }

        [Test]
        public void Summary_closing_follows_the_mind_focus()
        {
            Assert.AreEqual(RaceSummaryWatch.Mood.Defend, RaceSummaryWatch.MoodOf(RaceFocus.Defend, 4));
            Assert.AreEqual(RaceSummaryWatch.Mood.Leading, RaceSummaryWatch.MoodOf(RaceFocus.Rhythm, 1));
            Assert.AreEqual(RaceSummaryWatch.Mood.Steady, RaceSummaryWatch.MoodOf(RaceFocus.BringHome, 3));
        }

        [Test]
        public void Start_verdict_and_race_debrief_vary_and_stay_grounded()
        {
            var s = Race(11).SessionData;
            var texts = Enumerable.Range(0, 3).Select(v => ActiveRaceObserver.StartVerdict(s, 14, "Rob", v)).ToArray();
            Assert.AreEqual(3, texts.Distinct().Count());
            Assert.IsTrue(texts.All(t => t.Contains("Rob") && t.Contains("11")));
            s.PlayerLapTimeSessionBest = 83.456f; s.HasLimitedIncidents = true; s.MaxIncidentCount = 30; s.CurrentIncidentCount = 0;
            StringAssert.Contains("Schnellste Runde 1:23,456.", RaceRadioLanguage.Debrief(s));
            StringAssert.Contains("Ohne einen Vorfallpunkt", RaceRadioLanguage.Debrief(s));
            s.CurrentIncidentCount = 7; StringAssert.Contains("Vorfallpunkte 7 von 30.", RaceRadioLanguage.Debrief(s));
            Assert.AreEqual("", RaceRadioLanguage.Debrief(Race().SessionData));
        }

        [Test]
        public void Tyre_brief_only_from_reported_values()
        {
            var gs = Race();
            Assert.IsNull(OwnTyreAnswer.Brief(gs));
            gs.TyreData.TyreWearActive = true;
            gs.TyreData.FrontLeftPercentWear = 3; gs.TyreData.FrontRightPercentWear = 4; gs.TyreData.RearLeftPercentWear = 2; gs.TyreData.RearRightPercentWear = 5;
            StringAssert.Contains("noch gut", OwnTyreAnswer.Brief(gs));
            gs.TyreData.FrontRightPercentWear = 23.4f;
            Assert.AreEqual("Reifen: höchster Verschleiß 23 Prozent.", OwnTyreAnswer.Brief(gs));
        }

        [Test]
        public void Simple_driving_questions_need_one_request_only()
        {
            var gs = Race();
            foreach (var q in new[] { "Wie viel Sprit habe ich noch?", "Wie groß ist der Abstand nach vorne?", "Wie war meine letzte Runde?", "Welche Position?", "Wie sind die Reifen?" })
                Assert.IsFalse(AiTeamService.NeedsTools(q, gs), q);
            foreach (var q in new[] { "Wie wird das Wetter?", "Haben wir einen Schaden?", "Wie waren die letzten Runden im Verlauf?", "Wer ist der Führende?", "Wo verliere ich in Kurve drei?" })
                Assert.IsTrue(AiTeamService.NeedsTools(q, gs), q);
            Assert.IsFalse(AiTeamService.NeedsTools("Völlig okay so?", gs), "kein Treffer mitten im Wort für Öl");
        }

        [Test]
        public void Name_is_put_in_front_without_breaking_nouns_or_rival_names()
        {
            Assert.AreEqual("Rob, wieder einen Platz erkämpft.", RadioPhraseCatalog.Address("Wieder einen Platz erkämpft.", "Rob"));
            Assert.AreEqual("Rob, Sektor 2 in 31,456 Sekunden.", RadioPhraseCatalog.Address("Sektor 2 in 31,456 Sekunden.", "Rob"), "Substantiv bleibt groß");
            Assert.AreEqual("Rob, Keller setzt die Klassenbestzeit.", RadioPhraseCatalog.Address("Keller setzt die Klassenbestzeit.", "Rob"), "Gegnername bleibt");
            Assert.AreEqual("Rob, 31,456 Sekunden.", RadioPhraseCatalog.Address("31,456 Sekunden.", "Rob"));
            Assert.AreEqual("Rob, schon da.", RadioPhraseCatalog.Address("Rob, schon da.", "Rob"), "nie doppelt");
            Assert.AreEqual("Ohne Namen.", RadioPhraseCatalog.Address("Ohne Namen.", ""));
            var repeats = Enumerable.Range(2, 6).Select(n => RaceRadioLanguage.Overtake(n, 7, "Rob")).ToArray();
            Assert.IsTrue(repeats.Count(t => t.StartsWith("Rob, ")) >= 2, "mehrfaches Überholen auch mit Namen");
            Assert.IsTrue(repeats.Count(t => !t.StartsWith("Rob, ")) >= 2, "aber nicht jedes Mal");
        }

        // ---------- Mosport-Erkenntnisse 16.11.2026 ----------

        private static void SetTemps(GameStateData gs, float fl, float fr, float rl, float rr)
        {
            var t = gs.TyreData;
            t.FrontLeft_LeftTemp = t.FrontLeft_CenterTemp = t.FrontLeft_RightTemp = fl;
            t.FrontRight_LeftTemp = t.FrontRight_CenterTemp = t.FrontRight_RightTemp = fr;
            t.RearLeft_LeftTemp = t.RearLeft_CenterTemp = t.RearLeft_RightTemp = rl;
            t.RearRight_LeftTemp = t.RearRight_CenterTemp = t.RearRight_RightTemp = rr;
        }

        [Test]
        public void Hot_tyre_is_explained_with_grip_and_cool_down_advice()
        {
            Assert.AreEqual(new[] { 2 }, TyreGripWatch.HotCorners(new[] { 80f, 81f, 96f, 82f }, -1).ToArray(), "deutlich heißer als die anderen");
            Assert.AreEqual(0, TyreGripWatch.HotCorners(new[] { 80f, 81f, 88f, 82f }, -1).Count, "kleiner Unterschied ist normal");
            Assert.AreEqual(new[] { 2, 3 }, TyreGripWatch.HotCorners(new[] { 90f, 90f, 104f, 105f }, 100).ToArray(), "über dem Fenster");
            string text = TyreGripWatch.HotText(new System.Collections.Generic.List<int> { 2 }, new[] { 80f, 81f, 96f, 82f }, -1, 0);
            StringAssert.Contains("Hinten links", text); StringAssert.Contains("Traktion und Grip", text); StringAssert.Contains("96", text);
            StringAssert.Contains("beide Hinterreifen", TyreGripWatch.HotText(new System.Collections.Generic.List<int> { 2, 3 }, new[] { 90f, 90f, 104f, 105f }, 100, 1));

            var gs = Race(); var watch = new TyreGripWatch();
            SetTemps(gs, 80, 81, 96, 82);
            watch.Observe(gs, T);
            Assert.IsNull(watch.Take(gs, T, false, true), "erst nach 4 s Dauer");
            watch.Observe(gs, T.AddSeconds(5));
            var call = watch.Take(gs, T.AddSeconds(5), false, true);
            Assert.IsNotNull(call); Assert.AreEqual("tyre-hot", call.Topic); Assert.AreEqual(TeamMember.Mik, call.Member);
        }

        [Test]
        public void Dirty_tyres_are_reported_measured_or_explicitly_as_assumption()
        {
            StringAssert.Contains("vermutlich", TyreGripWatch.DirtText(null, false, 0));
            StringAssert.DoesNotContain("vermutlich", TyreGripWatch.DirtText(new System.Collections.Generic.List<int> { 2, 3 }, true, 0));
            var gs = Race(); var watch = new TyreGripWatch();
            gs.PenaltiesData.IsOffRacingSurface = true;
            for (int i = 0; i <= 20; i++) watch.Observe(gs, T.AddMilliseconds(i * 100));
            gs.PenaltiesData.IsOffRacingSurface = false;
            for (int i = 21; i <= 60; i++) watch.Observe(gs, T.AddMilliseconds(i * 100));
            var call = watch.Take(gs, T.AddSeconds(6), false, true);
            Assert.IsNotNull(call); Assert.AreEqual("tyre-dirt", call.Topic); StringAssert.Contains("vermutlich", call.Text);
        }

        [Test]
        public void Too_much_fuel_in_qualifying_is_pointed_out_once()
        {
            string text = QualiFuelWatch.Advice(19.4f, 1.93f, 240, 78, "Rob");
            StringAssert.Contains("10 Runden", text); StringAssert.Contains("Rob", text); StringAssert.Contains("leichter", text);
            Assert.IsNull(QualiFuelWatch.Advice(9f, 1.9f, 240, 78, "Rob"), "passt ungefähr");
        }

        [Test]
        public void After_a_spin_position_losses_settle_and_status_does_not_repeat()
        {
            var mind = new RaceMind();
            var loss = Call("position", 2);
            Assert.AreEqual(MindVerdict.Hold, mind.Judge(loss, T.AddSeconds(1), out _), "kurz warten, bis die Wertung steht");
            Assert.AreEqual(MindVerdict.Admit, mind.Judge(loss, T.AddSeconds(3), out _));
            Assert.AreEqual(MindVerdict.Drop, mind.Judge(Call("status"), T.AddSeconds(10), out _), "Status direkt danach entfällt");
            Assert.AreEqual(MindVerdict.Admit, mind.Judge(Call("status"), T.AddSeconds(25), out _));
            Assert.AreEqual(MindVerdict.Admit, mind.Judge(Call("position", 5), T.AddSeconds(1), out _), "Überholen sofort");
        }

        // ---------- Bestzeiten-Datenbank ----------

        private static PersonalBest Pb(string game, float lap, float[] sectors) => new PersonalBest
        {
            Game = game, Car = "Porsche 911 GT3 R", Track = "Mosport", Layout = "Mosport/1/3936m", Compound = "Medium",
            LapSeconds = lap, FuelAtStart = 34, TrackC = 33, AirC = 25, DateUtc = DateTime.UtcNow.AddDays(-95), Sectors = sectors
        };

        [Test]
        public void Personal_bests_are_kept_per_sim_and_only_improve()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rrca-pb-" + Guid.NewGuid().ToString("N"));
            try
            {
                var ams2 = new PersonalBestStore(folder, "Automobilista 2");
                Assert.IsTrue(ams2.Offer(Pb("Automobilista 2", 78.5f, new[] { 24f, 24.5f, 30f }), out var none)); Assert.IsNull(none);
                Assert.IsFalse(ams2.Offer(Pb("RaceRoom", 70f, null), out _), "nie in die Datenbank einer anderen Simulation");
                Assert.IsFalse(ams2.Offer(Pb("Automobilista 2", 79f, new[] { 23.6f, 25f, 30.4f }), out _), "langsamer wird nicht Bestzeit");
                var best = ams2.Find("Automobilista 2", "Porsche 911 GT3 R", "Mosport", "Mosport/1/3936m");
                Assert.AreEqual(78.5f, best.LapSeconds, .001f);
                Assert.AreEqual(23.6f, best.BestSectors[0], .001f, "bester Einzelsektor aus einer langsameren Runde");
                Assert.IsTrue(ams2.Offer(Pb("Automobilista 2", 77.9f, new[] { 23.9f, 24.2f, 29.8f }), out var old));
                Assert.AreEqual(78.5f, old.LapSeconds, .001f);
                Assert.IsNull(ams2.Find("RaceRoom", "Porsche 911 GT3 R", "Mosport", "Mosport/1/3936m"));
            }
            finally { try { System.IO.Directory.Delete(folder, true); } catch (Exception) { } }
        }

        [Test]
        public void Personal_best_briefing_names_time_ago_conditions_and_sectors()
        {
            var pb = Pb("Automobilista 2", 77.271f, new[] { 23.5f, 24f, 29.771f });
            Assert.AreEqual("vor 3 Monaten", PersonalBestWatch.Ago(DateTime.UtcNow.AddDays(-95), DateTime.UtcNow));
            string brief = PersonalBestWatch.Briefing(pb, 22, 37, "Rob", 2);
            StringAssert.StartsWith("Rob, vor 3 Monaten bist du hier schon mal eine 1:17,271 gefahren", brief);
            StringAssert.Contains("34 Litern", brief); StringAssert.Contains("33 Grad Strecke", brief); StringAssert.Contains("Sektoren", brief);
            StringAssert.Contains("leichter", brief); StringAssert.Contains("4 Grad wärmer", brief);
            Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => PersonalBestWatch.SectorLoss(2, .31f, v)).Distinct().Count());
            StringAssert.Contains("Sektor 2", PersonalBestWatch.SectorLoss(2, .31f, 0));
        }

        [Test]
        public void Consumption_question_is_answered_locally_even_when_phrased_casually()
        {
            const string q = "Kannst du mir schon ein bisschen was auslesen, was ich ungefähr an Sprit verbrauche aktuell?";
            var gs = Race();
            StringAssert.Contains("kein verlässlicher Tankstand", LocalAnswers.FuelConsumption(q, gs, null));
            gs.FuelData.FuelUseActive = true; gs.FuelData.FuelLeft = 30;
            StringAssert.Contains("paar hundert Meter", LocalAnswers.FuelConsumption(q, gs, null), "ohne Messung ehrlich, aber mit Grund");
            Assert.IsNull(LocalAnswers.FuelConsumption("Wie viel Sprit verbraucht der Vordermann?", gs, null), "Gegnerfrage geht an die Auswertung");
            Assert.IsNull(LocalAnswers.FuelConsumption("Wie viel Sprit habe ich noch?", gs, null), "Tankstand beantwortet der bestehende Weg");
            Assert.IsNull(LocalAnswers.FuelConsumption("Reicht der Sprit bei meinem Verbrauch bis ins Ziel?", gs, null), "Strategiefrage");
        }

        [Test]
        public void All_time_record_is_only_called_against_an_earlier_session()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rrca-pb-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PersonalBestStore(folder, "Automobilista 2");
                var gs = Race(); gs.carName = "Porsche 992 GT3 R"; gs.SessionData.SessionType = SessionType.Practice;
                var watch = new PersonalBestWatch();
                watch.Observe(gs, store, "Automobilista 2", "Rob", T);
                watch.ObserveLap(gs, new LapRecord { LapNumber = 1, LapTime = 127.762f, Valid = true }, store, "Automobilista 2", "Rob", T.AddSeconds(130));
                watch.ObserveLap(gs, new LapRecord { LapNumber = 2, LapTime = 124.834f, Valid = true }, store, "Automobilista 2", "Rob", T.AddSeconds(255));
                Assert.IsNull(watch.Take(gs, T.AddSeconds(255), false, true), "Bathurst: keine „schnellste Runde überhaupt“ gegen eine Runde derselben Sitzung");

                gs.SessionData.SessionId = 6;
                watch.Observe(gs, store, "Automobilista 2", "Rob", T.AddHours(2));
                watch.ObserveLap(gs, new LapRecord { LapNumber = 3, LapTime = 124.100f, Valid = true }, store, "Automobilista 2", "Rob", T.AddHours(2).AddSeconds(130));
                var call = watch.Take(gs, T.AddHours(2).AddSeconds(130), false, true);
                Assert.IsNotNull(call); Assert.AreEqual("personal-best-record", call.Topic); StringAssert.Contains("2:04,100", call.Text);
            }
            finally { try { System.IO.Directory.Delete(folder, true); } catch (Exception) { } }
        }

        [Test]
        public void Slick_window_follows_Robs_guide_not_the_generic_threshold()
        {
            var gs = Race();
            Assert.IsTrue(TyreGripWatch.Window(gs, out float cold, out float hot));
            Assert.AreEqual(80f, cold); Assert.AreEqual(100f, hot);
            StringAssert.Contains("85 bis 90 sehr gut", TyreGripWatch.WarmText(cold, hot, 0));
            StringAssert.Contains("über 80 Grad", TyreGripWatch.WarmText(cold, hot, 0));
            gs.TyreData.FrontLeftTyreType = TyreType.Wet;
            Assert.IsFalse(TyreGripWatch.IsRaceSlick(gs.TyreData.FrontLeftTyreType));
        }
    }
}
