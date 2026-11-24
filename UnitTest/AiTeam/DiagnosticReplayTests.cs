using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>C8 Aufnehmen und Abspielen: Aufnahme der Live-Diagnose einlesen, abspielen und den Funk prüfen.</summary>
    [TestFixture, Category("AiTeam")]
    public class DiagnosticReplayTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 17, 20, 0, 0, DateTimeKind.Utc);

        private static string Line(double seconds, string kind, string data) =>
            "{\"schema_version\":1,\"utc\":\"" + T.AddSeconds(seconds).ToString("o", CultureInfo.InvariantCulture) + "\",\"kind\":\"" + kind + "\",\"data\":" + data + "}";

        private static string Radio(double s, string topic, string speaker, string text) =>
            Line(s, "radio.enqueue", "{\"topic\":\"" + topic + "\",\"speaker\":\"" + speaker + "\",\"text\":\"" + text + "\",\"priority\":3}");

        [Test]
        public void Recording_is_read_back_and_frames_replay_as_game_state()
        {
            var lines = new List<string>
            {
                Line(0, "session.start", "{\"session_type\":\"Practice\",\"game\":\"Automobilista 2\",\"track\":\"Bathurst\"}"),
                Line(1, "telemetry", "{\"session_type\":\"Practice\",\"phase\":\"Green\",\"lap\":3,\"position\":4,\"in_pit\":true,\"speed_mps\":16.5,\"distance_m\":120.5,\"fuel_l\":30.2,\"gap_front_s\":1.5,\"gap_behind_s\":null,\"flag\":\"GREEN\",\"current_lap_valid\":false}"),
                "{\"kind\":\"telemetry\",\"data\":{\"lap\":", // abgeschnittene letzte Zeile
            };
            var session = DiagnosticReplay.Load(lines);
            Assert.AreEqual("Bathurst", session.Track);
            Assert.AreEqual(1, session.Frames.Count);
            var gs = DiagnosticReplay.ToGameState(session.Frames[0], 7);
            Assert.AreEqual(SessionType.Practice, gs.SessionData.SessionType);
            Assert.AreEqual(SessionPhase.Green, gs.SessionData.SessionPhase);
            Assert.AreEqual(3, gs.SessionData.CompletedLaps);
            Assert.AreEqual(4, gs.SessionData.ClassPosition);
            Assert.IsTrue(gs.PitData.InPitlane);
            Assert.IsFalse(gs.SessionData.CurrentLapIsValid);
            Assert.AreEqual(16.5f, gs.PositionAndMotionData.CarSpeed, .01f);
            Assert.AreEqual(30.2f, gs.FuelData.FuelLeft, .01f);
            Assert.AreEqual(-1f, gs.SessionData.TimeDeltaBehind, "nicht aufgezeichnet bleibt unbekannt");
        }

        [Test]
        public void Audit_finds_what_Rob_heard_in_Bathurst()
        {
            var session = DiagnosticReplay.Load(new[]
            {
                Radio(0, "pit-limit", "Mik", "Boxenlimit 60."),
                Radio(40, "pit-limit", "Mik", "Boxenlimit 60."),
                Radio(100, "pit-limit-approach", "Mik", "Limiter rein, 60."),
                Radio(200, "warning-tracklimit", "Mik", "Tracklimit."),
                Radio(210, "warning-invalid-lap", "Mik", "Runde ungültig."),
                Radio(300, "warning-flag-3", "Sara", "Gelb."),
                Radio(320, "warning-flag-3", "Sara", "Gelb."),
            });
            var rules = RadioAudit.Check(session).Select(f => f.Rule).ToList();
            CollectionAssert.Contains(rules, "gleicher_satz");
            CollectionAssert.Contains(rules, "thema_zu_oft");
            CollectionAssert.Contains(rules, "tracklimit_doppelt");
            Assert.AreEqual(1, rules.Count(r => r == "gleicher_satz"), "Flaggen dürfen sich wiederholen");
            StringAssert.Contains("Auffälligkeiten", RadioAudit.Report(session));
        }

        [Test]
        public void Latency_chain_is_measured_with_millisecond_resolution()
        {
            var session = DiagnosticReplay.Load(new[]
            {
                Line(10.000, "decision", "{\"topic\":\"position\",\"status\":\"detected\",\"event_id\":\"e1\"}"),
                Line(10.250, "radio.enqueue", "{\"topic\":\"position\",\"speaker\":\"Ben\",\"text\":\"P5\",\"priority\":4,\"event_id\":\"e1\"}"),
                Line(10.900, "audio.driver_submit", "{\"event_id\":\"e1\"}"),
                Line(11.000, "tts.end", "{\"duration_ms\":420,\"status\":\"completed\"}"),
                Line(11.000, "api.end", "{\"duration_ms\":640,\"status\":\"ok\"}"),
                Line(20.000, "ptt.released", "{\"utterance_sequence\":1}"),
                Line(26.400, "radio.enqueue", "{\"clip_id\":\"c-answer\",\"speaker\":\"Mik\",\"text\":\"Reifen gut\",\"priority\":5,\"utterance_sequence\":1}"),
                Line(26.500, "audio.driver_submit", "{\"clip_id\":\"c-answer\",\"event_id\":null}"),
            });
            var l = RadioAudit.Latencies(session);
            Assert.AreEqual(250, l["erkannt_bis_eingereiht"].Single(), 1);
            Assert.AreEqual(650, l["eingereiht_bis_audio"].Single(), 1);
            Assert.AreEqual(900, l["erkannt_bis_audio"].Single(), 1);
            Assert.AreEqual(420, l["tts_erzeugung"].Single(), 1);
            Assert.AreEqual(6500, l["frage_bis_antwort_audio"].Single(), 1);
            Assert.AreEqual(5, RadioAudit.Percentile(new List<double> { 1, 3, 5, 7, 9 }, 50), 1e-9);
            Assert.AreEqual(8.6, RadioAudit.Percentile(new List<double> { 1, 3, 5, 7, 9 }, 95), 1e-9);
            Assert.IsTrue(double.IsNaN(RadioAudit.Percentile(new List<double>(), 50)));
            StringAssert.Contains("p95", RadioAudit.LatencyReport(session));
        }

        /// <summary>
        /// Review 24.11. (Sebring): Nach 2,01 s lief ein Sektorhinweis, nach 2,07 s ein Überholurteil – beide zählten als Antwort.
        /// Rob fragt nach den Reifen, ein Sektorhinweis läuft dazwischen, Mik antwortet danach: nur Miks Clip stoppt die Uhr.
        /// </summary>
        [Test]
        public void Only_the_answer_to_this_question_stops_the_answer_clock()
        {
            var session = DiagnosticReplay.Load(new[]
            {
                Line(10.000, "ptt.released", "{\"utterance_sequence\":4}"),
                Line(11.900, "radio.enqueue", "{\"clip_id\":\"c-sector\",\"topic\":\"quali-sector\",\"speaker\":\"Ben\",\"text\":\"Sektor 2 schneller\",\"priority\":3,\"event_id\":\"e7\"}"),
                Line(12.010, "audio.driver_submit", "{\"clip_id\":\"c-sector\",\"event_id\":\"e7\"}"),
                Line(12.070, "audio.driver_submit", "{\"clip_id\":\"c-old\",\"event_id\":\"e8\"}"),
                Line(13.000, "radio.enqueue", "{\"clip_id\":\"c-earlier\",\"speaker\":\"Ben\",\"text\":\"Platz 5\",\"priority\":5,\"utterance_sequence\":3}"),
                Line(13.100, "audio.driver_submit", "{\"clip_id\":\"c-earlier\"}"),
                Line(15.400, "radio.enqueue", "{\"clip_id\":\"c-tyres\",\"speaker\":\"Mik\",\"text\":\"Reifen im Fenster\",\"priority\":5,\"utterance_sequence\":4}"),
                Line(15.500, "audio.driver_submit", "{\"clip_id\":\"c-tyres\"}"),
                Line(40.000, "ptt.released", "{\"utterance_sequence\":5}"),
                Line(41.000, "audio.driver_submit", "{\"clip_id\":\"c-other\"}"),
            });
            var answers = RadioAudit.Latencies(session)["frage_bis_antwort_audio"];
            Assert.AreEqual(1, answers.Count, "Frage 5 blieb ohne Antwort und bekommt keinen fremden Clip");
            Assert.AreEqual(5500, answers.Single(), 1, "Miks Antwort, nicht der Sektorhinweis nach 2,01 s");
        }

        [Test]
        public void Crowded_radio_counts_only_the_engineers()
        {
            var crowded = DiagnosticReplay.Load(new[]
            {
                Radio(0, "status", "Ben", "a"), Radio(5, "tyre-warm", "Mik", "b"), Radio(9, "field-pit", "Ben", "c"), Radio(14, "near-best", "Mik", "d"),
            });
            CollectionAssert.Contains(RadioAudit.Check(crowded).Select(f => f.Rule).ToList(), "funk_zu_voll");
            var spotter = DiagnosticReplay.Load(new[]
            {
                Radio(0, "status", "Ben", "a"), Radio(5, "warning-flag-1", "Sara", "b"), Radio(9, "warning-flag-2", "Sara", "c"), Radio(14, "near-best", "Mik", "d"),
            });
            Assert.IsEmpty(RadioAudit.Check(spotter));
            Assert.AreEqual("warning-native-incident", RadioAudit.Family("warning-native-incident-12"));
        }
    }
}
