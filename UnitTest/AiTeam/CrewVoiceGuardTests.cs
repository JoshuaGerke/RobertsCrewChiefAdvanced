using System;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// RaceRoom Mantorp 22.11. (v0.22.0): Ab 20:40 schwiegen Ben und Mik, nur Sara sprach noch. Belegt: ElevenLabs 401 „exceeds your
    /// quota of 30004 … 0 credits remaining“. Vorher wurde Bens Zwischenfazit 19:40:04, :09, :28, :42 (UTC) neu erzeugt (je ~470 Zeichen),
    /// verworfen und nie gespielt; danach folgte jede Sekunde ein neuer Versuch.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class CrewVoiceGuardTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 22, 19, 40, 4, DateTimeKind.Utc);
        private const string Summary = "Rob, kurz zur Lage: Platz 13, seit dem Start 4 Plätze gutgemacht. Deine persönliche Bestzeit 1:20,874, die Klassenbestzeit ist 1,4 Sekunden schneller. Viel los: 13 Überholmanöver, 10-mal überholt worden. Das Duell mit Aristide Fusecchia zählt 3 Wechsel. Julio Zimoch ist 2,8 Sekunden vor dir.";
        private const string Gap = "Vorne Julio Zimoch, Abstand 3,4 Sekunden.";

        private static bool Try(PaidRenderGuard g, string topic, string id, string text, DateTime now, out string reason)
        {
            if (!g.MayRender(topic, id, text, now, out reason)) return false;
            g.Rendering(topic, id, text, now);
            g.Rendered(id);
            return true;
        }

        [Test]
        public void Mantorp_summary_sequence_is_rendered_at_most_twice_instead_of_five_times()
        {
            var g = new PaidRenderGuard();
            int paid = 0;
            string reason;
            // 19:40:04 erstes Zwischenfazit (151c) wird erzeugt
            Assert.IsTrue(Try(g, "race-summary", "151c", Summary, T0, out reason)); paid++;
            // 19:40:09 zweites Zwischenfazit (9319), während das erste noch auf den Funk wartet
            Assert.IsFalse(Try(g, "race-summary", "9319", Summary, T0.AddSeconds(5), out reason));
            Assert.AreEqual("same_topic_already_rendered", reason);
            // 19:40:18 151c vor dem Abspielen verworfen
            g.Discarded("race-summary", "151c", Summary, T0.AddSeconds(14));
            // 19:40:28 und 19:40:42 neue Zwischenfazits: Abkühlzeit
            Assert.IsFalse(Try(g, "race-summary", "36fd", Summary, T0.AddSeconds(24), out reason));
            Assert.AreEqual("paid_render_recently_discarded", reason);
            Assert.IsFalse(Try(g, "race-summary", "ef58", Summary, T0.AddSeconds(38), out reason));
            // Die verworfene Meldung selbst darf einmal mit frischen Zahlen nachkommen …
            Assert.IsTrue(Try(g, "race-summary", "151c", Summary, T0.AddSeconds(20), out reason)); paid++;
            g.Discarded("race-summary", "151c", Summary, T0.AddSeconds(30));
            // … aber nicht ein drittes Mal.
            Assert.IsFalse(Try(g, "race-summary", "151c", Summary, T0.AddSeconds(31), out reason));
            Assert.AreEqual("paid_render_limit_per_event", reason);
            Assert.AreEqual(2, paid, "Mantorp: 5 bezahlte Erzeugungen, jetzt höchstens 2");
        }

        [Test]
        public void Summary_comes_again_after_the_cooldown_and_after_a_played_summary()
        {
            var g = new PaidRenderGuard();
            string reason;
            Assert.IsTrue(Try(g, "race-summary", "a", Summary, T0, out reason));
            g.Discarded("race-summary", "a", Summary, T0.AddSeconds(10));
            Assert.IsFalse(g.MayRender("race-summary", "b", Summary, T0.AddSeconds(69), out reason));
            Assert.IsTrue(g.MayRender("race-summary", "b", Summary, T0.AddSeconds(71), out reason), "nach 60 s Abkühlzeit wieder frei");

            Assert.IsTrue(Try(g, "race-summary", "c", Summary, T0.AddSeconds(80), out reason));
            g.Finished("race-summary", "c");
            Assert.IsTrue(g.MayRender("race-summary", "d", Summary, T0.AddSeconds(81), out reason), "gespielt: Thema sofort wieder frei");
        }

        [Test]
        public void Short_gap_calls_are_not_held_back_by_other_gap_calls()
        {
            var g = new PaidRenderGuard();
            string reason;
            Assert.IsTrue(Try(g, "ahead", "g1", Gap, T0, out reason));
            g.Discarded("ahead", "g1", Gap, T0.AddSeconds(2));
            Assert.IsTrue(Try(g, "ahead", "g2", Gap, T0.AddSeconds(3), out reason), "Abstände bleiben lebendig");
            Assert.IsTrue(Try(g, "ahead", "g1", Gap, T0.AddSeconds(4), out reason));
            Assert.IsFalse(g.MayRender("ahead", "g1", Gap, T0.AddSeconds(5), out reason));
            Assert.AreEqual("paid_render_limit_per_event", reason);
        }

        [Test]
        public void Waiting_for_a_free_channel_does_not_count_as_a_paid_render()
        {
            var g = new PaidRenderGuard();
            string reason;
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(g.MayRender("ahead", "w", Gap, T0.AddSeconds(i), out reason));
                g.Rendering("ahead", "w", Gap, T0.AddSeconds(i));
                g.Finished("ahead", "w");
            }
        }

        [Test]
        public void Same_text_again_reuses_the_paid_audio_for_two_minutes()
        {
            var g = new PaidRenderGuard();
            var pcm = new byte[] { 1, 2, 3 };
            g.Remember("ben|text", pcm, T0);
            Assert.AreSame(pcm, g.Reuse("ben|text", T0.AddSeconds(30)));
            Assert.IsNull(g.Reuse("ben|text", T0.AddSeconds(121)));
            Assert.IsNull(g.Reuse("mik|text", T0.AddSeconds(1)));
            for (int i = 0; i < PaidRenderGuard.ReuseEntries; i++) g.Remember("k" + i, new byte[] { 9 }, T0);
            Assert.IsNull(g.Reuse("ben|text", T0.AddSeconds(1)), "nur die letzten Erzeugungen bleiben im Speicher");
        }

        [Test]
        public void Elevenlabs_quota_message_from_Mantorp_is_recognised()
        {
            const string msg = "ElevenLabs 401: This request exceeds your quota of 30004. You have 0 credits remaining, while 212 credits are required for this request.";
            Assert.IsTrue(CrewVoiceOutage.QuotaExhausted(msg));
            Assert.AreEqual("quota_exhausted", CrewVoiceOutage.Reason(401, msg));
            Assert.AreEqual("payment_incomplete", CrewVoiceOutage.Reason(401, "ElevenLabs 401: Your subscription has a failed or incomplete payment."));
            Assert.AreEqual("rate_limited", CrewVoiceOutage.Reason(429, "ElevenLabs 429: too_many_concurrent_requests"));
            StringAssert.Contains("Originalstimme", CrewVoiceOutage.StatusText("quota_exhausted"));
        }

        [TestCase("Timings/gap_in_front", "timings/gap_in_front_increasing")]
        [TestCase("Timings/gap_behind", "timings/gap_behind_decreasing")]
        [TestCase("new_fastest_lap", "opponents/new_fastest_lap")]
        [TestCase("COMPOUND_laptime", "lap_times/lap_time")]
        [TestCase("position", "position/p")]
        [TestCase("fuel", "fuel/half_distance_low_fuel")]
        public void During_an_outage_Crew_Chief_speaks_gaps_times_positions_again(string name, string folder)
        {
            Assert.IsTrue(CrewVoiceOutage.NativeFallbackAllows(name, new[] { folder }, true));
        }

        [TestCase("COMPOUND_sectorFlagChange_Sector2", "flags/sector_2_yellow")]
        [TestCase("COMPOUND_localFlagChange_yellow", "flags/yellow_flag")]
        [TestCase("COMPOUND_incident_drivers", "flags/incident_drivers")]
        public void Flags_stay_with_Sara_so_they_do_not_come_twice(string name, string folder)
        {
            Assert.IsFalse(CrewVoiceOutage.NativeFallbackAllows(name, new[] { folder }, true));
            Assert.IsTrue(CrewVoiceOutage.NativeFallbackAllows(name, new[] { folder }, false), "ohne Sara übernimmt Crew Chief auch Flaggen");
        }

        [Test]
        public void Crew_knows_which_announcements_already_run_automatically()
        {
            RadioLanguage.Current = RadioLanguage.German;
            string prompt = AiPersonas.SystemPrompt(TeamMember.Mik, "Rob", false);
            StringAssert.Contains("Diese Ansagen kommen schon automatisch", prompt);
            StringAssert.Contains("schnellste Runde eines Gegners", prompt);
            StringAssert.Contains("persönliche Bestzeit", prompt);
            StringAssert.Contains("wirklich neue automatische Meldung", prompt);
        }
    
        [TestCase("Die Ansagen bezüglich Öl und Wasser, also Motor und Öl, Wasserüberhitzung, streichen für die ganze Session, nicht mehr nennen.", 1)]
        [TestCase("Keine Motorwarnungen mehr", 1)]
        [TestCase("No more oil and water calls", 1)]
        [TestCase("Öl und Wasser bitte wieder melden", -1)]
        [TestCase("Motor wieder einschalten", -1)]
        [TestCase("Wie ist die Öltemperatur?", 0)]
        [TestCase("Wasser auf der Strecke, keine Gefahr mehr?", 0)]
        [TestCase("Der Motor ist stumm", 0)]
        [TestCase("Is the engine overheating again?", 0)]
        public void Driver_can_silence_engine_temperature_calls_for_the_session(string question, int expected)
        {
            // RaceRoom Norisring 22.11.: Mik antwortete „automatische Meldungen kann ich nicht abschalten“ und warnte weiter.
            Assert.AreEqual(expected, EngineMuteCommand.Parse(question));
        }

        [Test]
        public void Muted_engine_warnings_stay_quiet_until_it_gets_much_hotter_and_end_with_the_session()
        {
            var w = new LiveWarningDispatcher();
            Assert.IsTrue(w.EngineWarningDue("both", 120, 112, T0));
            w.EngineWarningSpoken("both", 120, 112, T0);
            w.MuteEngineWarnings(121, 112);
            Assert.IsFalse(w.EngineWarningDue("both", 124, 114, T0.AddMinutes(4)), "abgeschaltet: auch nach der 3-Minuten-Sperre still");
            Assert.IsFalse(w.EngineWarningDue("water", 121, 121, T0.AddMinutes(5)));
            Assert.IsTrue(w.EngineWarningDue("water", 121, 122, T0.AddMinutes(5)), "echte Eskalation (+10 °C) kommt trotzdem");
            w.EngineWarningSpoken("water", 121, 122, T0.AddMinutes(5));
            Assert.IsFalse(w.EngineWarningDue("water", 121, 125, T0.AddMinutes(9)), "die nächste Ausnahme braucht wieder +10 °C");
            w.Reset();
            Assert.IsFalse(w.EngineMuted, "neue Session: wieder an");
            w.MuteEngineWarnings(121, 112);
            w.UnmuteEngineWarnings();
            Assert.IsTrue(w.EngineWarningDue("both", 124, 114, T0.AddMinutes(20)));
        }
    }
}
