using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// LMU Sebring 24.11.2026 (Quali und 10-Minuten-Rennen): 17 fertige Meldungen kamen nie an, einige galten danach als
    /// „schon gesagt“; Reifen und Fahrzeugzustand galten als mit dem Wetter gebündelt, das nie abgespielt wurde; die Startrunde
    /// war Maßstab für „deine Beste heute“; Mik behauptete, er könne nichts aufschreiben.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class SebringDriveFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 24, 7, 5, 0, DateTimeKind.Utc);

        private static ObservedRaceCall Call(string topic, string text) => new ObservedRaceCall
        {
            Topic = topic, Text = text, Member = TeamMember.Ben, Priority = 3, Evidence = "test", SessionId = 1,
            SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30)
        };

        [Test]
        public void A_sentence_that_was_never_played_is_not_said()
        {
            var memory = new CrewMemory();
            var call = Call("battle-sector", "Sektor 1 sechs Zehntel schneller als Agostini.");
            memory.Remember(call, T);
            var retry = Call("battle-sector", "Sektor 1 sechs Zehntel schneller als Agostini.");
            Assert.IsTrue(memory.SaidRecently(retry, T.AddSeconds(5)), "vorgemerkt: kein zweiter Wächter mit demselben Satz");
            memory.Forget(call, T);
            Assert.IsFalse(memory.SaidRecently(retry, T.AddSeconds(5)), "nie abgespielt: der nächste Anlauf darf sprechen");

            // Wurde derselbe Satz inzwischen wirklich gesprochen, bleibt er gesperrt.
            memory.Remember(call, T);
            memory.Remember(retry, T.AddSeconds(3));
            memory.Forget(call, T);
            Assert.IsTrue(memory.SaidRecently(Call("battle-sector", "Sektor 1 sechs Zehntel schneller als Agostini."), T.AddSeconds(5)));
        }

        [Test]
        public void A_forgotten_position_call_restores_the_last_reported_place()
        {
            var memory = new CrewMemory();
            var spoken = Call("position", "Platz 14."); spoken.StatedPosition = 14;
            memory.Remember(spoken, T);
            var unheard = Call("position", "Starkes Manöver, Platz 13."); unheard.StatedPosition = 13;
            memory.Remember(unheard, T.AddSeconds(30));
            memory.Forget(unheard, T.AddSeconds(30));
            var again = Call("position", "Platz 13, stark gemacht."); again.StatedPosition = 13;
            Assert.IsFalse(memory.NothingNew(again, T.AddSeconds(35), out string why), why);
        }

        [Test]
        public void A_dropped_report_does_not_take_its_bundled_topics_with_it()
        {
            var prefs = new AiRadioPreferences();
            foreach (var o in AiRadioPreferences.Options) prefs.Set(o.Id, 0);
            prefs.Set("status", 120); prefs.Set("weather", 120); prefs.Set("progress", 120);
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 5; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = 3; gs.SessionData.SessionNumberOfLaps = 30; gs.SessionData.SessionLapsRemaining = 20; gs.SessionData.CompletedLaps = 10;
            gs.carClass = new CarData.CarClass(); gs.PositionAndMotionData.CarSpeed = 60;
            gs.Conditions.UpdateLiveSample(T, 10, 1, 18, 24, 0, 0, 0, 0, 0, false, CrewChiefV4.Events.ConditionsMonitor.TrackStatus.UNKNOWN);
            var schedule = new AiRadioSchedule(3);
            string[] dropped = null;
            var heardAfter = new HashSet<string>();
            for (int t = 0; t <= 900; t++)
            {
                var now = T.AddSeconds(t);
                schedule.Observe(gs, now, prefs);
                var call = schedule.Take(new RaceDataContext { GameState = gs, NowUtc = now, LastTelemetryUtc = now }, prefs, false, null, false);
                if (call == null) continue;
                var bundled = schedule.BundledTopics.ToArray();
                if (dropped == null && bundled.Length > 0)
                {
                    // Der Gastgeber wird verworfen, ohne das Bündel freizugeben (Funkregie, Kontingent, Stimme ausgefallen).
                    dropped = bundled;
                    call.MarkConsumed(now);
                    continue;
                }
                call.MarkPlaybackStarted(now); call.MarkPlaybackFinished(now.AddSeconds(3), false);
                if (dropped != null) foreach (var topic in new[] { call.Topic }.Concat(bundled)) heardAfter.Add(topic);
                if (dropped != null && dropped.All(heardAfter.Contains)) break;
            }
            Assert.IsNotNull(dropped, "Es gab ein Bündel");
            CollectionAssert.IsSubsetOf(dropped, heardAfter, "Gebündelte Themen kommen nach dem verworfenen Bericht noch");
        }

        [Test]
        public void The_start_lap_is_no_benchmark_for_the_fastest_lap_in_the_field()
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionType = SessionType.Race;
            gs.SessionData.PlayerLapTimeSessionBest = 135.9f;
            gs.SessionData.CompletedLaps = 1;
            Assert.Less(SessionLapObserver.ComparableOwnBest(gs), 0, "nur die Startrunde gefahren");
            StringAssert.DoesNotContain("dahinter", SessionLapObserver.GapAndPush(123.387f, SessionLapObserver.ComparableOwnBest(gs), 1));
            gs.SessionData.PlayerLapTimeSessionBest = 125.796f;
            gs.SessionData.CompletedLaps = 2;
            Assert.AreEqual(125.796f, SessionLapObserver.ComparableOwnBest(gs), .001f);
            gs.SessionData.SessionType = SessionType.Qualify;
            gs.SessionData.CompletedLaps = 1;
            Assert.AreEqual(125.796f, SessionLapObserver.ComparableOwnBest(gs), .001f, "in Training und Quali zählt jede gezeitete Runde");
        }

        [TestCase("Aufschreiben und melden."), TestCase("Schreib das auf, die Reifen kommen in Echtzeit."), TestCase("Write that down please.")]
        public void Asking_to_write_something_down_uses_the_notes_tool(string said)
        {
            Assert.IsTrue(AiTeamService.NeedsTools(said, null), said);
        }

        [Test]
        public void The_crew_knows_it_can_take_notes()
        {
            string was = RadioLanguage.Current;
            try
            {
                RadioLanguage.Current = RadioLanguage.German;
                string prompt = AiPersonas.SystemPrompt(TeamMember.Mik, "Rob", false);
                StringAssert.Contains("Du kannst dauerhaft notieren", prompt);
            }
            finally { RadioLanguage.Current = was; }
        }
    }
}
