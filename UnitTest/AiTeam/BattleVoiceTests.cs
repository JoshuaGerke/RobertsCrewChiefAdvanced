using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrewChiefV4.AiTeam;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// LMU Sebring 23.11.: „Gute Zweikämpfe wurden nicht menschlich kommentiert, nur Daten und Fakten.“
    /// Zweikämpfe und Manöver werden vom schnellen Sprachmodell mit Vorgeschichte und Gefühl neu formuliert; der feste Satz
    /// bleibt als Rückfall. Diese Tests sichern das Sicherheitsnetz: keine neuen Zahlen oder Namen, Gegnername bleibt,
    /// keine Wiederholung, keine Maschinensätze, Frist eingehalten.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class BattleVoiceTests
    {
        private string wasLanguage;
        private static readonly string[] Field = { "Valgo", "Becker", "Garcia", "Berger" };

        [SetUp]
        public void SetUp() { wasLanguage = RadioLanguage.Current; RadioLanguage.Current = RadioLanguage.German; BattleVoice.Reset(); }

        [TearDown]
        public void TearDown() { RadioLanguage.Current = wasLanguage; BattleVoice.Reset(); }

        [Test]
        public void Fights_and_manoeuvres_get_a_human_voice_routine_does_not()
        {
            foreach (var t in new[] { "position", "position-loss", "judge-overtake", "story-duel-front", "story-duel-rear", "story-free",
                "story-escaped", "battle-sector", "gap-trend", "rear-pressure", "rival-topspeed" })
                Assert.IsTrue(BattleVoice.Covers(t), t);
            foreach (var t in new[] { "story-followup", "fuel", "tyres", "greeting", "field-retired", "race-hazard-ahead", "ahead", null })
                Assert.IsFalse(BattleVoice.Covers(t), t ?? "null");
        }

        [Test]
        public void A_lively_line_with_the_same_facts_is_accepted()
        {
            string template = "Seit 4 Runden klebst du an Valgo. Geduld, die Lücke kommt.";
            string line = BattleVoice.Validate("Vier Runden schon an Valgo dran, das ist ein echtes Duell! Bleib geduldig, der Moment kommt.",
                template, new List<string> { "Valgo" }, Field, new List<string>(), out string why);
            Assert.IsNotNull(line, why);
        }

        [TestCase("Seit 5 Runden an Valgo dran, bleib cool.", "new_number:5")]
        [TestCase("Valgo ist zwei Zehntel schneller im ersten Sektor, bleib dran.", "new_number:zwei")]
        [TestCase("Das Duell gegen Becker ist eröffnet, bleib geduldig.", "rival_name_missing")]
        [TestCase("Valgo und Becker kämpfen beide, nutz das aus!", "other_name:Becker")]
        [TestCase("Das kann ich nicht bewerten, Valgo ist vorn.", "sounds_like_machine")]
        public void Invented_facts_or_machine_talk_fall_back_to_the_fixed_sentence(string candidate, string expected)
        {
            string template = "Seit 4 Runden klebst du an Valgo. Geduld, die Lücke kommt.";
            Assert.IsNull(BattleVoice.Validate(candidate, template, new List<string> { "Valgo" }, Field, new List<string>(), out string why));
            Assert.AreEqual(expected, why);
        }

        [Test]
        public void A_line_close_to_a_recent_one_is_not_repeated()
        {
            var recent = new List<string> { "Stark gemacht, Valgo ist geschluckt! Jetzt sauber wegfahren." };
            Assert.IsNull(BattleVoice.Validate("Stark gemacht, Valgo ist geschluckt! Jetzt sauber wegfahren, Rob.", "Platz 6. Vorbei an Valgo.",
                new List<string> { "Valgo" }, Field, recent, out string why));
            Assert.AreEqual("repeats_recent_line", why);
        }

        [Test]
        public void The_tone_changes_from_call_to_call()
        {
            var tones = Enumerable.Range(0, 6).Select(_ => BattleVoice.Tone("position", "Platz 6. Vorbei an Valgo.", DriverMoodState.Neutral)).ToList();
            Assert.GreaterOrEqual(tones.Distinct().Count(), 5);
            string loss = BattleVoice.Tone("position", "Valgo hat dich überholt. Platz 7.", DriverMoodState.Neutral);
            StringAssert.DoesNotContain("jubel", loss);
        }

        [Test]
        public void The_model_sees_the_history_of_the_duel()
        {
            var chronicle = new List<ChronicleEntry>
            {
                new ChronicleEntry { Lap = 3, Kind = ChronicleKind.PositionLost, Subject = "Valgo", Detail = ChronicleRecorder.OnTrack },
                new ChronicleEntry { Lap = 7, Kind = ChronicleKind.PositionGained, Subject = "Valgo", Detail = ChronicleRecorder.OnTrack },
                new ChronicleEntry { Lap = 8, Kind = ChronicleKind.PositionGained, Subject = "Becker", Detail = ChronicleRecorder.OnTrack },
            };
            var facts = BattleVoice.Facts("position", "Platz 6. Vorbei an Valgo.", "begeistert", null, null, chronicle,
                new List<string> { "Valgo" }, new List<string> { "Durchatmen." });
            Assert.AreEqual(2, (int)facts["platztausche_mit_gegner"]);
            Assert.AreEqual(2, ((JArray)facts["vorgeschichte_mit_gegner"]).Count);
            Assert.AreEqual("Der Fahrer hat gerade einen Platz gewonnen.", (string)facts["anlass"]);
            Assert.AreEqual("begeistert", (string)facts["ton"]);
        }

        [Test]
        public async Task A_slow_model_never_holds_the_call()
        {
            var started = DateTime.UtcNow;
            var r = await BattleVoice.ColorAsync(async (m, ct) => { await Task.Delay(5000, ct); return "{\"text\":\"zu spät\"}"; },
                "system", new JObject(), "Platz 6. Vorbei an Valgo.", new List<string> { "Valgo" }, Field, TimeSpan.FromMilliseconds(300));
            Assert.IsNull(r.Item1);
            Assert.AreEqual("deadline", r.Item2);
            Assert.Less((DateTime.UtcNow - started).TotalSeconds, 2);
        }

        [Test]
        public async Task A_broken_model_answer_keeps_the_fixed_sentence()
        {
            var r = await BattleVoice.ColorAsync((m, ct) => throw new InvalidOperationException("http 500"),
                "system", new JObject(), "Platz 6. Vorbei an Valgo.", new List<string> { "Valgo" }, Field, TimeSpan.FromSeconds(1));
            Assert.IsNull(r.Item1);
            StringAssert.StartsWith("model_error", r.Item2);
        }

        /// <summary>
        /// Ablauf eines Duells wie in Sebring: fünf Kampfmeldungen nacheinander. Ein Platzhalter-Modell liefert Sätze, wie sie das
        /// Sprachmodell nach der Anweisung bildet; geprüft wird, dass jede Meldung mit Namen, ohne neue Zahlen und jedes Mal anders
        /// gesprochen wird, und dass eine fehlerhafte Antwort auf den festen Satz zurückfällt.
        /// </summary>
        [Test]
        public async Task A_duel_sounds_human_and_different_every_time()
        {
            var scenes = new[]
            {
                Tuple.Create("story-duel-front", "Seit 3 Runden klebst du an Valgo. Geduld, die Lücke kommt.",
                    "Drei Runden schon an Valgos Heck, der spürt dich! Ruhig bleiben, die Lücke kommt."),
                Tuple.Create("battle-sector", "Valgo ist im ersten Sektor schneller, du im dritten.",
                    "Im dritten Sektor bist du der Stärkere, Valgo weiß das. Genau da baust du den Angriff auf."),
                Tuple.Create("position", "Platz 6. Vorbei an Valgo.",
                    "Jaaa, Valgo ist fällig! Platz 6, das war ein richtig starkes Manöver."),
                Tuple.Create("story-duel-rear", "Valgo hängt seit 3 Runden hinter dir. Du machst das sauber, weiter so.",
                    "Valgo will die Revanche, schon 3 Runden. Bleib ruhig, du hast das im Griff."),
                Tuple.Create("story-free", "Valgo ist abgeschüttelt. Gut gemacht, durchatmen.",
                    "Valgo ist abgeschüttelt, das Duell gehört dir! Durchatmen, jetzt deinen Rhythmus fahren."),
            };
            var spoken = new List<string>();
            foreach (var scene in scenes)
            {
                string tone = BattleVoice.Tone(scene.Item1, scene.Item2, DriverMoodState.Neutral);
                var facts = BattleVoice.Facts(scene.Item1, scene.Item2, tone, null, null, new List<ChronicleEntry>(), new List<string> { "Valgo" }, BattleVoice.Recent());
                string answer = scene.Item3;
                var r = await BattleVoice.ColorAsync((m, ct) => Task.FromResult("{\"text\":\"" + answer + "\"}"), "system", facts, scene.Item2,
                    new List<string> { "Valgo" }, Field, TimeSpan.FromSeconds(1));
                Assert.IsNotNull(r.Item1, scene.Item1 + ": " + r.Item2);
                BattleVoice.Remember(r.Item1);
                spoken.Add(r.Item1);
                TestContext.WriteLine(scene.Item1 + " | fest: " + scene.Item2 + " | Ton: " + tone + " | gesprochen: " + r.Item1);
            }
            Assert.AreEqual(spoken.Count, spoken.Distinct().Count());
            // Das Modell erfindet einen Abstand: zurück auf den festen Satz.
            var wrong = await BattleVoice.ColorAsync((m, ct) => Task.FromResult("{\"text\":\"Nur noch 0,4 auf Valgo, hol ihn dir!\"}"), "system", new JObject(),
                "Seit 3 Runden klebst du an Valgo. Geduld, die Lücke kommt.", new List<string> { "Valgo" }, Field, TimeSpan.FromSeconds(1));
            Assert.IsNull(wrong.Item1);
        }

        [Test]
        public void The_prompt_follows_the_radio_language()
        {
            StringAssert.Contains("Deutsch", BattleVoice.Prompt("Rob", TeamMember.Ben));
            RadioLanguage.Current = RadioLanguage.English;
            string en = BattleVoice.Prompt("Rob", TeamMember.Ben);
            StringAssert.DoesNotContain("durchgehend Deutsch", en);
            StringAssert.Contains("englische", en);
        }
    }
}
