using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// RRCA 2.0 Phase 1: Variantenbeutel (6.2), semantische Wiederholsperre (6.3) und Ereignisgedächtnis (4.2).
    /// Die Szenarien sind synthetisch und als solche gekennzeichnet; das Positionsszenario bildet die echte Mosport-Aufnahme
    /// vom 16.11. nach („guter Start … Platz 14“ und eine Sekunde später „du liegst jetzt auf Position 14“).
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class RaceEventMemoryTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 18, 21, 0, 0, DateTimeKind.Utc);

        private static ObservedRaceCall Call(string topic, int stated = 0, string key = null, int priority = 4, int session = 7) =>
            new ObservedRaceCall { Topic = topic, Text = topic + " text " + stated + " " + key, StatedPosition = stated, SemanticKey = key,
                Priority = priority, SessionId = session, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(20), Member = TeamMember.Ben };

        [Test]
        public void Variant_bag_avoids_the_last_three_and_is_reproducible()
        {
            bool was = RadioVariants.Deterministic;
            try
            {
                RadioVariants.Deterministic = false;
                var options = new[] { "a", "b", "c", "d", "e", "f" };
                RadioVariants.Seed(42);
                var first = Enumerable.Range(0, 60).Select(_ => RadioVariants.Pick("bag", options)).ToList();
                for (int i = 3; i < first.Count; i++)
                    CollectionAssert.DoesNotContain(first.Skip(i - 3).Take(3).ToList(), first[i], "Wiederholung innerhalb der letzten drei bei " + i);
                CollectionAssert.AreEquivalent(options, first.Distinct().ToList(), "alle Varianten kommen vor");
                RadioVariants.Seed(42);
                CollectionAssert.AreEqual(first, Enumerable.Range(0, 60).Select(_ => RadioVariants.Pick("bag", options)).ToList(), "gleicher Startwert, gleiche Folge");
                Assert.AreEqual(2, RadioVariants.Avoided(4), "bei vier Varianten nur die letzten zwei meiden, sonst wird es vorhersehbar");
                Assert.AreEqual(3, RadioVariants.Avoided(6));
                Assert.AreEqual(1, RadioVariants.Avoided(2));
            }
            finally { RadioVariants.Deterministic = was; RadioVariants.Seed(Environment.TickCount); }
        }

        [Test]
        public void Same_position_in_other_words_is_nothing_new()
        {
            var memory = new CrewMemory();
            memory.Remember(Call("start-verdict", stated: 14), T);
            Assert.IsTrue(memory.NothingNew(Call("position", stated: 14), T.AddSeconds(1), out string why), "Mosport 16.11.: P14 doppelt");
            StringAssert.Contains("position_already_reported", why);
            Assert.IsFalse(memory.NothingNew(Call("position", stated: 13), T.AddSeconds(2), out _), "neuer Platz ist neu");
            Assert.IsFalse(memory.NothingNew(Call("position", stated: 14), T.AddSeconds(95), out _), "nach 90 s darf der Stand wieder kommen");
            Assert.IsFalse(memory.NothingNew(Call("position", stated: 14, session: 8), T.AddSeconds(1), out _), "andere Session");
            Assert.IsFalse(memory.NothingNew(Call("gap-trend"), T.AddSeconds(1), out _), "ohne genannten Platz keine Sperre");
        }

        [Test]
        public void Position_change_back_and_forth_is_always_reported()
        {
            var memory = new CrewMemory();
            memory.Remember(Call("position", stated: 5), T);
            memory.Remember(Call("position", stated: 4), T.AddSeconds(3));
            Assert.IsFalse(memory.NothingNew(Call("position", stated: 5), T.AddSeconds(30), out _), "zurückgefallen auf 5: neu gegenüber zuletzt 4");
        }

        [Test]
        public void Same_event_from_another_watch_is_suppressed_but_safety_never()
        {
            var memory = new CrewMemory();
            memory.Remember(Call("field-fastest", key: "field|fastest|Timo|80.108"), T);
            Assert.IsTrue(memory.NothingNew(Call("rival-best", key: "field|fastest|Timo|80.108"), T.AddSeconds(20), out string why));
            StringAssert.Contains("same_event", why);
            Assert.IsFalse(memory.NothingNew(Call("warning-flag-3", key: "field|fastest|Timo|80.108", priority: 9), T.AddSeconds(20), out _), "Warnungen gehen immer");
            Assert.IsFalse(memory.NothingNew(Call("race-hazard-ahead", stated: 14, priority: 7), T.AddSeconds(1), out _), "Gefahr geht immer");
            memory.Clear();
            Assert.IsFalse(memory.NothingNew(Call("rival-best", key: "field|fastest|Timo|80.108"), T.AddSeconds(21), out _), "Sessionwechsel leert das Gedächtnis");
        }

        [Test]
        public void Event_log_tracks_status_and_stays_bounded()
        {
            var log = new RaceEventLog();
            var a = Call("position", stated: 9);
            log.Note(a, EventStatus.Detected, T);
            log.Note(a, EventStatus.Deferred, T.AddSeconds(1), "race_mind:battle");
            log.Note(a, EventStatus.Reported, T.AddSeconds(3));
            log.Note(a, EventStatus.Dropped, T.AddSeconds(4), "spaeter");
            var rec = log.Recent(7, 1).Single();
            Assert.AreEqual(EventStatus.Reported, rec.Status, "gemeldet bleibt gemeldet");
            Assert.AreEqual("eigener-platz|9", rec.SemanticKey);
            Assert.AreEqual(T.AddSeconds(3), rec.ReportedAt);
            var b = Call("gap-trend");
            log.Note(b, EventStatus.Dropped, T.AddSeconds(5), "crew_memory_same_event");
            Assert.AreEqual(1, log.Recent(7, 10, EventStatus.Dropped).Length);
            var reported = log.DescribeReported(7, T.AddSeconds(13));
            Assert.AreEqual(1, reported.Count);
            Assert.AreEqual("position", (string)reported[0]["thema"]);
            Assert.AreEqual(10, (int)reported[0]["vor_s"]);
            for (int i = 0; i < RaceEventLog.Capacity + 50; i++) log.Note(Call("status"), EventStatus.Detected, T);
            Assert.AreEqual(RaceEventLog.Capacity, log.Count, "Ringpuffer, kein unbegrenztes Wachstum");
            Assert.AreEqual(0, log.Recent(8, 10).Length, "nur die eigene Session");
        }

        [Test]
        public void Overlap_on_track_without_damage_is_no_contact_call()
        {
            // Szenario 17 (synthetisch): Gegner auf gleicher Höhe, kein Schaden, kein Vorfallpunkt, kein Tempoverlust → keine Kontaktmeldung.
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green; gs.SessionData.SessionId = 7;
            gs.SessionData.ClassPosition = 5; gs.SessionData.CompletedLaps = 4; gs.SessionData.CurrentLapIsValid = true;
            gs.PositionAndMotionData.CarSpeed = 45; gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            gs.OpponentData["x"] = new OpponentData { DriverRawName = "Timo", ClassPosition = 6, DistanceRoundTrack = 1000, Speed = 45, IsActive = true };
            var watch = new DriverJudgementWatch(1);
            for (int i = 0; i < 40; i++)
            {
                watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(i * .5));
                var call = watch.Take(gs, T.AddSeconds(i * .5), false, true);
                Assert.IsTrue(call == null || !call.Topic.StartsWith("judge-impact"), call?.Text);
            }
        }

        [Test]
        public void Question_for_a_switched_off_member_is_answered_by_someone_who_is_on()
        {
            // LMU-Test 18.11.: „Seid ihr alle da?“ ging an den ausgeschalteten Mik und blieb ohne Antwort.
            Func<TeamMember, bool> noMik = m => m != TeamMember.Mik;
            Assert.AreEqual(TeamMember.Ben, TeamRouter.Available(TeamMember.Ben, noMik, null), "eingeschaltet: bleibt");
            Assert.AreEqual(TeamMember.Sara, TeamRouter.Available(TeamMember.Mik, noMik, TeamMember.Sara), "zuerst der letzte Gesprächspartner");
            Assert.AreEqual(TeamMember.Ben, TeamRouter.Available(TeamMember.Mik, noMik, null), "sonst Ben");
            Assert.AreEqual(TeamMember.Ben, TeamRouter.Available(TeamMember.Mik, noMik, TeamMember.Mik));
            Assert.IsNull(TeamRouter.Available(TeamMember.Mik, m => false, null), "niemand eingeschaltet");
        }

        [Test]
        public void Misheard_sim_name_is_corrected_before_the_crew_repeats_it()
        {
            Assert.AreEqual("Hallo Sarah, wir legen gleich los mit Le Mans Ultimate.", TeamRouter.FixSimNames("Hallo Sarah, wir legen gleich los mit Lemon Ultimate."));
            Assert.AreEqual("Le Mans Ultimate läuft", TeamRouter.FixSimNames("Lemans Ultimate läuft"));
            Assert.AreEqual("Le Mans Ultimate", TeamRouter.FixSimNames("Le Mans Ultimate"), "richtig bleibt richtig");
            Assert.AreEqual("Zitronen sind gelb", TeamRouter.FixSimNames("Zitronen sind gelb"));
            Assert.IsNull(TeamRouter.FixSimNames(null));
        }

        [Test]
        public void Note_round_with_spoken_reply_needs_no_second_model_round()
        {
            // LMU 18.11.: zehnmal save_note und danach eine zweite Runde nur für den Funktext (je 2–3 s).
            var notes = new List<string> { "Mehr Infos beim Wiedereinsetzen" };
            StringAssert.Contains("nach dem Stopp sag ich dir, wo du rauskommst",
                AiTeamService.SaveRoundAnswer(true, notes, "Verstanden, nach dem Stopp sag ich dir, wo du rauskommst.", "Analysier in Zukunft, wo ich rauskomme", 0, 400),
                "Funktext kam mit dem Speichern: sofort sprechen");
            Assert.IsNull(AiTeamService.SaveRoundAnswer(true, notes, null, "Analysier in Zukunft, wo ich rauskomme, und wie liege ich jetzt?", 0, 400),
                "Frage ohne mitgeschickten Text braucht die zweite Runde");
            StringAssert.Contains("Mehr Infos beim Wiedereinsetzen.", AiTeamService.SaveRoundAnswer(true, notes, "", "Merk dir: mehr Infos beim Wiedereinsetzen", 0, 400),
                "reiner Merkauftrag: lokale Bestätigung");
            Assert.IsNull(AiTeamService.SaveRoundAnswer(false, notes, "Text", "Merk dir das", 0, 400), "auch Daten abgefragt: normale Runde");
            Assert.IsNull(AiTeamService.SaveRoundAnswer(true, notes, "ok", "Wie viel Sprit?", 0, 400), "zu kurzer Text ist keine Antwort");
            Assert.Greater(AiTeamService.FinalRoundGraceSeconds, 0, "letzte Antwortrunde bekommt Nachfrist");
        }

        [Test]
        public void Pure_memory_request_is_confirmed_without_a_second_model_round()
        {
            Assert.IsTrue(AiTeamService.PureMemoryRequest("[an Mik] Merk dir bitte, dass ich Sektorvergleiche im Training will."));
            Assert.IsTrue(AiTeamService.PureMemoryRequest("[an Ben] Please remember I prefer short gaps."));
            Assert.IsTrue(AiTeamService.PureMemoryRequest("[an Ben] Guarda que quiero menos radio."));
            Assert.IsFalse(AiTeamService.PureMemoryRequest("[an Ben] Merk dir das und sag mir, wie viel Sprit ich habe?"), "mit Frage braucht es die Antwort");
            Assert.IsFalse(AiTeamService.PureMemoryRequest("[an Ben] Wie ist der Abstand nach vorn"));
            Assert.IsFalse(AiTeamService.PureMemoryRequest(null));
            StringAssert.Contains("Sektorvergleiche im Training.", AiTeamService.SaveOnlyAnswer(true, new List<string> { "Sektorvergleiche im Training" }, null, 0));
            Assert.AreEqual("Mach ich.", AiTeamService.SaveOnlyAnswer(true, new List<string> { "x" }, " Mach ich. ", 0), "eigener Text des Modells hat Vorrang");
            Assert.IsNull(AiTeamService.SaveOnlyAnswer(false, new List<string> { "x" }, null, 0), "nicht gespeichert: normale Antwort");
            Assert.IsNull(AiTeamService.SaveOnlyAnswer(true, new List<string>(), null, 0));
        }
    }
}
