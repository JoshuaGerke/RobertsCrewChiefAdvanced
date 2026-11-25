using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// LMU Algarve 24.11.2026 (Training, Qualifying, 20-Minuten-Rennen mit 18 Autos). Rob: „Sara war fast stumm, Ben und Mik
    /// haben sie überchattet. Die Sektorbestzeiten spielen keine Rolle, und der Topspeed kam keine zwei Mal.“ Werte aus dem Log.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class LmuAlgarveTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 24, 21, 51, 54, DateTimeKind.Utc);
        private bool wasDeterministic;

        [SetUp]
        public void SetUp()
        {
            RadioLanguage.Current = RadioLanguage.German;
            wasDeterministic = RadioVariants.Deterministic;
            DriverRadioGate.Reset();
            FieldImpacts.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            RadioVariants.Deterministic = wasDeterministic;
            RadioLanguage.Current = RadioLanguage.German;
            DriverRadioGate.Reset();
            FieldImpacts.Reset();
        }

        [Test]
        public void Sara_cuts_lower_crew_radio_but_not_answers_spotter_or_the_radio_tone()
        {
            // 21:51:54.7: Saras „Vorsicht, in etwa 80 Metern ein sehr langsames Fahrzeug“ (Stufe 7) gegen Bens Zwischenstand (Stufe 2).
            Assert.IsTrue(CrewSpeechPolicy.CanSaraCut(true, false, false, 2, "race-summary", 7), "Bens 25-s-Zwischenstand weicht");
            Assert.IsTrue(CrewSpeechPolicy.CanSaraCut(true, false, false, 4, "field-fastest", 5), "Saras Feldnachricht vor Bens Bestzeit-Info");
            Assert.IsFalse(CrewSpeechPolicy.CanSaraCut(true, false, false, 5, "position", 5), "gleiche Stufe läuft zu Ende");
            Assert.IsFalse(CrewSpeechPolicy.CanSaraCut(true, false, false, 3, "race-summary", 4), "unter Stufe 5 kein Vorrang");
            Assert.IsFalse(CrewSpeechPolicy.CanSaraCut(true, false, false, 5, null, 7), "Antwort an Rob nur bei dringender Gefahr");
            Assert.IsTrue(CrewSpeechPolicy.CanSaraCut(true, false, false, 5, null, 8));
            Assert.IsFalse(CrewSpeechPolicy.CanSaraCut(true, true, false, 2, "spotter", 9), "Spotter läuft zu Ende");
            Assert.IsFalse(CrewSpeechPolicy.CanSaraCut(true, false, true, 2, "race-summary", 9), "Funkton läuft zu Ende");
            Assert.IsFalse(CrewSpeechPolicy.CanSaraCut(false, false, false, 2, "race-summary", 9), "Crew-Chief-Originalclips bleiben");
        }

        [Test]
        public void Only_a_fresh_unstarted_routine_clip_holds_the_next_one_back()
        {
            DateTime now = T;
            Assert.IsTrue(AiAudioStore.RoutineWaitingIn(new[] { new KeyValuePair<bool, DateTime>(false, now.AddSeconds(-5)) }, now));
            Assert.IsFalse(AiAudioStore.RoutineWaitingIn(new[] { new KeyValuePair<bool, DateTime>(true, now.AddSeconds(-5)) }, now),
                "läuft schon oder ist Saras Vorrang-Clip");
            Assert.IsFalse(AiAudioStore.RoutineWaitingIn(new[] { new KeyValuePair<bool, DateTime>(false, now.AddSeconds(-25)) }, now),
                "ein hängender Clip blockiert nicht ewig");
            Assert.IsFalse(AiAudioStore.RoutineWaitingIn(new KeyValuePair<bool, DateTime>[0], now));
        }

        [Test]
        public void Waiting_behind_running_radio_does_not_count_against_a_call()
        {
            // 21:52:19.6: Nach Bens 25 s langem Zwischenstand verfielen sechs Meldungen auf einmal.
            var call = new ObservedRaceCall { Topic = "sector-best", Text = "x", DetectedUtc = DateTime.UtcNow, ExpiresUtc = DateTime.UtcNow.AddSeconds(20) };
            DateTime planned = DateTime.UtcNow.AddSeconds(20);
            DriverRadioGate.OnAirStarted(DateTime.UtcNow.AddSeconds(-25));
            try
            {
                Assert.That((call.ExpiresUtc - planned).TotalSeconds, Is.InRange(24, 26.5), "25 s Funk gutgeschrieben");
                Assert.That(call.WaitCredit.TotalSeconds, Is.InRange(24, 26.5));
            }
            finally { DriverRadioGate.OnAirEnded(DateTime.UtcNow); }
            DriverRadioGate.OnAirStarted(DateTime.UtcNow.AddSeconds(-300));
            DriverRadioGate.OnAirEnded(DateTime.UtcNow);
            Assert.LessOrEqual(call.WaitCredit, DriverRadioGate.MaxCredit, "veraltet bleibt veraltet");
            var never = new ObservedRaceCall { ExpiresUtc = DateTime.MaxValue };
            Assert.AreEqual(DateTime.MaxValue, never.ExpiresUtc);
        }

        [Test]
        public void Retirements_are_news_even_under_yellow()
        {
            var caution = new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.Middle, Attention = RaceAttention.Caution };
            var call = new ObservedRaceCall { Topic = "field-retired", Text = "Noah Preston hat aufgegeben, ein Gegner weniger.", Priority = 5,
                SessionId = 5, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(45) };
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(caution, call, T, out _), "22-mal unter Gelb gehalten");
        }

        [Test]
        public void Robs_core_of_the_race_stays_battles_front_and_rear_with_class_best_in_between()
        {
            // Rob 24.11.: „Der Kern war sehr gut: die Kämpfe zum Vordermann und Hintermann, zwischendurch der Vergleich mit der Spitze.“
            var battle = new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.Middle, Attention = RaceAttention.Battle, Focus = RaceFocus.Attack };
            foreach (var topic in new[] { "story-duel-front", "story-duel-rear", "field-fastest", "sector-best" })
            {
                var call = new ObservedRaceCall { Topic = topic, Text = topic, Priority = 4, SessionId = 5, SessionType = SessionType.Race,
                    DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
                Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(battle, call, T, out string why), topic + ": " + why);
            }
        }

        [Test]
        public void Personal_track_record_is_an_own_result_not_praise()
        {
            Assert.IsTrue(RadioLoad.OwnResult("personal-best-record"), "14-mal als Lob gehalten, nie gesagt");
        }

        [Test]
        public void Start_lap_sector_is_no_baseline_and_the_class_best_is_compared()
        {
            // 21:51:57: „8,483 Sekunden Gewinn“ – die alte Marke war Sektor 1 aus der stehenden Startrunde (31,282 s).
            Assert.IsFalse(SessionLapObserver.PlausibleSectorGain(8.483f, 22.799f));
            Assert.IsTrue(SessionLapObserver.PlausibleSectorGain(.202f, 22.597f), "22:00:56: 0,202 besser als vorher");
            var gs = new GameStateData(T.Ticks);
            gs.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            var mendez = new OpponentData { DriverRawName = "Francesco Mendez", IsActive = true, CarClass = gs.carClass };
            mendez.OpponentLapData.Add(new LapData { LapNumber = 3, IsValid = true, LapTime = 106.5f, SectorTimes = new[] { 22.433f, 38.1f, 45.967f } });
            mendez.OpponentLapData.Add(new LapData { LapNumber = 4, IsValid = true, LapTime = 107.2f, SectorTimes = new[] { 22.3f, 38.5f, 46.4f } });
            gs.OpponentData["mendez"] = mendez;
            string text = SessionLapObserver.ClassBestSectorText(gs, 0, 22.597f, true);
            Assert.IsNotNull(text);
            StringAssert.Contains("Mendez", text);
            StringAssert.Contains("0,164", text, "Sektor der schnellsten Runde (1:46,500), nicht der schnellste Einzelsektor");
            Assert.IsNull(SessionLapObserver.ClassBestSectorText(gs, 0, 31.282f, true), "Startrunde ist kein Vergleich");
            StringAssert.DoesNotContain("Mendez", SessionLapObserver.ClassBestSectorText(gs, 0, 22.597f, false), "ohne Namensfreigabe");
            StringAssert.Contains("0,033", SessionLapObserver.ClassBestSectorText(gs, 0, 22.4f, true), "schneller als die Klassenbestzeit");
        }

        [Test]
        public void Class_best_sector_lines_have_four_to_six_wordings_in_every_language()
        {
            RadioVariants.Deterministic = false;
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                foreach (double delta in new[] { .164, -.033, 0 })
                {
                    var seen = Enumerable.Range(0, 80).Select(_ => SessionLapObserver.ClassBestSectorPhrase("Mendez", 22.433f, delta)).Distinct().ToList();
                    Assert.That(seen.Count, Is.InRange(4, 6), language + " " + delta);
                    Assert.IsTrue(seen.All(t => char.IsUpper(t[0]) && !char.IsDigit(t[0])), language + " " + delta);
                }
            }
        }

        [Test]
        public void Hazard_lead_says_yellow_or_crash_and_contacts_alone_stay_silent()
        {
            // 21:51:55.8: Steven Lameira schlug mit 3243,97 ein, 21:51:54.7 meldete Sara ihn als sehr langsam voraus.
            string clip = FieldImpacts.Key("Steven Lameira");
            FieldImpacts.Note("Steven Lameira", 3243.97, true, T);
            Assert.IsTrue(FieldImpacts.Recent(clip, T.AddSeconds(2)));
            Assert.IsFalse(FieldImpacts.Recent(clip, T.AddSeconds(40)), "nach 30 s kein Unfall mehr");
            FieldImpacts.Reset();
            FieldImpacts.Note("Steven Lameira", 640.35, true, T);
            Assert.IsFalse(FieldImpacts.Recent(clip, T.AddSeconds(1)), "leichter Mauerkontakt ist kein Unfall");
            FieldImpacts.Note("Ike KnoNotBrakBro", 454.88, false, T);
            Assert.IsFalse(FieldImpacts.Recent(FieldImpacts.Key("Ike KnoNotBrakBro"), T.AddSeconds(1)), "Berührung im Zweikampf bleibt stumm");
            FieldImpacts.Note("Ike KnoNotBrakBro", 1965.01, false, T);
            Assert.IsTrue(FieldImpacts.Recent(FieldImpacts.Key("Ike KnoNotBrakBro"), T.AddSeconds(1)));

            Assert.IsNull(SaraFlagPolicy.HazardLead(false, false), "ohne Gelb und ohne Unfall bleibt die Meldung wie sie ist");
            StringAssert.StartsWith("Gelb", SaraFlagPolicy.HazardLead(true, false));
            Assert.IsTrue(SaraFlagPolicy.LeadOptions(true, true).All(l => l.Contains("Gelb") && (l.Contains("Unfall") || l.Contains("gekracht"))));
            Assert.IsNull(SaraFlagPolicy.HazardLead("warning-rc-yellow-1", null, clip, T), "gewöhnliches Gelb bleibt stumm");
            RadioVariants.Deterministic = false;
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                foreach (var flags in new[] { new[] { true, false }, new[] { true, true }, new[] { false, true } })
                {
                    var options = SaraFlagPolicy.LeadOptions(flags[0], flags[1]);
                    Assert.That(options.Distinct().Count(), Is.InRange(4, 6), language + " " + string.Join(",", flags));
                    Assert.IsTrue(options.All(t => !char.IsDigit(t[0]) && (char.IsUpper(t[0]) || t[0] == '¡' && char.IsUpper(t[1]))), language.ToString());
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
            Assert.AreEqual(15, SaraFlagPolicy.HazardLeadPhrases.Distinct().Count(), "alle Auftakte werden in Saras Stimme vorbereitet");
        }

        [Test]
        public void Big_summary_comes_once_in_a_twenty_minute_race()
        {
            var s = new SessionData { SessionType = SessionType.Race, SessionHasFixedTime = true, SessionTotalRunTime = 20 * 60 };
            Assert.AreEqual(1, RaceSummaryWatch.TotalCount(s), "Algarve: 21:51:53 und 21:54:16/21:54:46 – künftig nur das Fazit zur Hälfte");
            s.SessionTotalRunTime = 45 * 60; Assert.AreEqual(2, RaceSummaryWatch.TotalCount(s));
            s.SessionTotalRunTime = 120 * 60; Assert.AreEqual(3, RaceSummaryWatch.TotalCount(s));
        }

        private static GameStateData Race(DateTime now, int lap)
        {
            var gs = new GameStateData(now.Ticks);
            var s = gs.SessionData;
            s.SessionId = 9; s.SessionType = SessionType.Race; s.SessionPhase = SessionPhase.Green;
            s.ClassPosition = 7; s.OverallPosition = 7; s.CompletedLaps = lap; s.SessionNumberOfLaps = 20;
            s.TrackDefinition = new TrackDefinition("Algarve", 4684f);
            gs.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            gs.OpponentData["ike"] = new OpponentData { DriverRawName = "Ike KnoNotBrakBro", IsActive = true, ClassPosition = 6, CarClass = gs.carClass };
            gs.OpponentData["steven"] = new OpponentData { DriverRawName = "Steven Lameira", IsActive = true, ClassPosition = 5, CarClass = gs.carClass };
            gs.Now = now; gs.SourceTelemetryUtc = now;
            return gs;
        }

        private static FieldView View()
        {
            var me = new FieldCarView { Name = "Rob", IsPlayer = true, ClassId = 1, PlaceClass = 7, TopSpeed = 266 / 3.6f };
            var cars = new[]
            {
                me,
                // 21:51:34: „Speedtrap: 269, Ike KnoNotBrakBro 268 …“ – der Schnellste ohne Namen war ein ausgeschiedener Wagen.
                new FieldCarView { Name = "Noah Preston#1844", ClassId = 1, PlaceClass = 18, TopSpeed = 269 / 3.6f, Motion = CarMotion.Retired },
                new FieldCarView { Name = "Ike KnoNotBrakBro", ClassId = 1, PlaceClass = 6, TopSpeed = 268 / 3.6f, GapS = -4.6f },
                new FieldCarView { Name = "Steven Lameira", ClassId = 1, PlaceClass = 5, TopSpeed = 267 / 3.6f, GapS = 2.5f },
                new FieldCarView { Name = "Unbekannt", ClassId = 1, PlaceClass = 9, TopSpeed = 268.5f / 3.6f, GapS = 20f },
            };
            return new FieldView { Player = me, Cars = cars };
        }

        [Test]
        public void Top_speed_names_only_running_cars_and_comes_back_after_being_discarded()
        {
            var gs = Race(T, 4);
            var watch = new TopSpeedWatch();
            watch.Evaluate(gs, View(), true, T);
            var first = watch.Take(gs, T, false, true);
            Assert.IsNotNull(first);
            Assert.AreEqual("field-topspeed", first.Topic);
            StringAssert.DoesNotContain("269", first.Text, "ausgeschiedener Wagen zählt nicht");
            StringAssert.Contains("Ike KnoNotBrakBro 268", first.Text);
            StringAssert.Contains("Steven Lameira 267", first.Text);
            StringAssert.DoesNotContain("268,5", first.Text);
            // Zweimal vor dem Abspielen verworfen (21:51:54 und 21:52:19).
            first.ReleaseForRetry(T.AddSeconds(1), "audio_discarded_before_playback");
            var t2 = T.AddSeconds(3); gs.Now = gs.SourceTelemetryUtc = t2;
            var again = watch.Take(gs, t2, false, true);
            Assert.AreSame(first, again);
            again.ReleaseForRetry(t2, "audio_discarded_before_playback");
            Assert.IsTrue(first.DeliveryFailed);
            // Eine Runde später, schon hinter dem ersten Drittel: der Termin wird mit frischen Werten neu gebildet.
            var t3 = T.AddMinutes(9); gs = Race(t3, 9);
            Assert.AreEqual(-1, TopSpeedWatch.FieldSlot(gs.SessionData));
            watch.Evaluate(gs, View(), true, t3);
            var retry = watch.Take(gs, t3, false, true);
            Assert.IsNotNull(retry, "nicht gesprochen heißt nicht erledigt");
            Assert.AreEqual("field-topspeed", retry.Topic);
            StringAssert.Contains("first_third", retry.Evidence);
        }
    }
}
