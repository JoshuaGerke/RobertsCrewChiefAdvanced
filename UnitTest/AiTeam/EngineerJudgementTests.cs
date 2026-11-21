using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Robs Wünsche vom 16.11.2026 abends: Funkstille beim Funken, weniger Technik-Routine, Ingenieur-Urteil, lila Sektoren.</summary>
    [TestFixture, Category("AiTeam")]
    public class EngineerJudgementTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 16, 20, 30, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Race(int position = 5)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 5; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = position; gs.SessionData.OverallPosition = position; gs.SessionData.CompletedLaps = 4;
            gs.SessionData.TrackDefinition = new TrackDefinition("Test", 4000);
            gs.carClass = new CarData.CarClass(); gs.PositionAndMotionData.CarSpeed = 40; gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            return gs;
        }

        private static OpponentData Rival(GameStateData gs, string key, string name, int position, float best)
        {
            var o = new OpponentData { DriverRawName = name, ClassPosition = position, OverallPosition = position, CarClass = gs.carClass,
                CurrentBestLapTime = best, CompletedLaps = 4, Speed = 40, DistanceRoundTrack = 900 };
            gs.OpponentData[key] = o;
            return o;
        }

        private static List<LapRecord> Laps(params float[] times)
        {
            var laps = new List<LapRecord>();
            for (int i = 0; i < times.Length; i++)
                laps.Add(new LapRecord { LapNumber = i + 1, LapTime = times[i], Valid = true, Position = 5, RecordedUtc = T });
            return laps;
        }

        // ---------- Funktaste: Funkstille ----------

        [Test]
        public void Push_to_talk_holds_every_call_until_shortly_after_release()
        {
            try
            {
                DriverRadioGate.Reset();
                Assert.IsFalse(DriverRadioGate.Holding(T));
                DriverRadioGate.Pressed(T);
                Assert.IsTrue(DriverRadioGate.Holding(T.AddSeconds(5)), "solange gedrückt: Funkstille");
                Assert.IsFalse(DriverRadioGate.MayStart(false, T.AddSeconds(5)), "Crew-Chief- und KI-Ansagen warten");
                Assert.IsTrue(DriverRadioGate.MayStart(true, T.AddSeconds(5)), "Seitenwarnung des Spotters bleibt erlaubt");
                DriverRadioGate.Released(T.AddSeconds(6));
                Assert.IsTrue(DriverRadioGate.Holding(T.AddSeconds(7)), "kurze Ruhe nach dem Loslassen");
                Assert.IsFalse(DriverRadioGate.Holding(T.AddSeconds(8)), "danach läuft die Warteschlange weiter");
            }
            finally { DriverRadioGate.Reset(); }
        }

        [Test]
        public void Stuck_push_to_talk_key_does_not_silence_the_crew_forever()
        {
            try
            {
                DriverRadioGate.Pressed(T);
                // Monza 21.11.: Robs Funksprüche dauern bis zu 60 s – die Funkstille gilt so lange wie die längste Aufnahme.
                Assert.IsTrue(DriverRadioGate.Holding(T.AddSeconds(59)));
                Assert.IsFalse(DriverRadioGate.Holding(T + DriverRadioGate.MaxHold + TimeSpan.FromSeconds(1)));
                Assert.That(DriverRadioGate.MaxHold, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(70)), "hängende Taste blockiert nicht ewig");
            }
            finally { DriverRadioGate.Reset(); }
        }

        [Test]
        public void Clip_already_selected_waits_when_the_driver_presses_before_it_starts()
        {
            try
            {
                // Quali 20.11.: Mik ausgewählt 31,818 s, Funktaste 31,902 s, Start 32,291 s – er redete über Rob.
                DriverRadioGate.Reset();
                DriverRadioGate.Pressed(T);
                var clock = T;
                int waited = DriverRadioGate.WaitForSilence(false, () => clock, ms =>
                {
                    clock = clock.AddMilliseconds(ms);
                    if (clock >= T.AddSeconds(2) && clock < T.AddSeconds(2).AddMilliseconds(ms)) DriverRadioGate.Released(clock);
                });
                Assert.That(clock, Is.GreaterThanOrEqualTo(T.AddSeconds(2) + DriverRadioGate.AfterRelease), "startet erst nach dem Loslassen und der kurzen Ruhe");
                Assert.That(waited, Is.GreaterThan(3000).And.LessThan(3700));

                DriverRadioGate.Pressed(T);
                Assert.AreEqual(0, DriverRadioGate.WaitForSilence(true, () => T, ms => Assert.Fail("Seitenwarnung wartet nie")));
                DriverRadioGate.Reset();
                Assert.AreEqual(0, DriverRadioGate.WaitForSilence(false, () => T, ms => Assert.Fail("ohne Funkspruch keine Wartezeit")));

                DriverRadioGate.Pressed(T);
                var stuck = T;
                int capped = DriverRadioGate.WaitForSilence(false, () => stuck, ms => stuck = stuck.AddMilliseconds(ms));
                Assert.That(capped, Is.LessThanOrEqualTo((int)(DriverRadioGate.MaxHold + DriverRadioGate.AfterRelease).TotalMilliseconds), "hängende Taste blockiert nicht ewig");
            }
            finally { DriverRadioGate.Reset(); }
        }

        // ---------- Technik-Routine höchstens alle 3–4 Minuten ----------

        [Test]
        public void Technical_routines_share_one_budget_of_three_to_four_minutes()
        {
            var gs = Race(); gs.FuelData.FuelUseActive = true; gs.FuelData.FuelLeft = 40; gs.EngineData.EngineWaterTemp = 94;
            var prefs = new AiRadioPreferences();
            foreach (var o in AiRadioPreferences.Options) prefs.Set(o.Id, 0);
            prefs.Set("fuel", 30); prefs.SetTolerance("fuel", 0, 0); prefs.Set("vehicle", 30); prefs.SetTolerance("vehicle", 0, 0);
            var schedule = new AiRadioSchedule(1); schedule.Observe(gs, T, prefs);
            Func<double, RaceDataContext> at = sec => new RaceDataContext { GameState = gs, NowUtc = T.AddSeconds(sec), LastTelemetryUtc = T.AddSeconds(sec) };
            var first = schedule.Take(at(30), prefs, false);
            Assert.IsNotNull(first);
            Assert.IsTrue(AiRadioSchedule.IsTechnical(first.Topic));
            first.MarkPlaybackStarted(T.AddSeconds(30)); first.MarkPlaybackFinished(T.AddSeconds(33), false);
            schedule.Take(at(34), prefs, false); // verbucht die Zustellung
            string other = first.Topic == "fuel" ? "vehicle" : "fuel";
            Assert.IsTrue(schedule.TechnicalBudgetBlocks(other, T.AddSeconds(100)), "zweites Technikthema wartet");
            Assert.IsTrue(schedule.TechnicalBudgetBlocks(other, T.AddSeconds(30 + 179)), "mindestens drei Minuten");
            Assert.IsFalse(schedule.TechnicalBudgetBlocks(other, T.AddSeconds(30 + 241)), "spätestens nach vier Minuten frei");
            Assert.IsFalse(schedule.TechnicalBudgetBlocks(first.Topic, T.AddSeconds(100)), "eigener Takt des Themas bleibt");
            Assert.IsFalse(schedule.TechnicalBudgetBlocks("status", T.AddSeconds(100)), "Rennstatus ist keine Technik");
            Assert.IsFalse(schedule.TechnicalBudgetBlocks("ahead", T.AddSeconds(100)));
        }

        [Test]
        public void Technical_budget_can_be_switched_off_and_fuel_default_is_four_minutes()
        {
            Assert.AreEqual(240, new AiRadioPreferences()["fuel"]);
            Assert.IsTrue(AiRadioSchedule.IsTechnical("weather") && AiRadioSchedule.IsTechnical("tyres") && AiRadioSchedule.IsTechnical("stint"));
            Assert.IsFalse(AiRadioSchedule.IsTechnical("pace") || AiRadioSchedule.IsTechnical("progress") || AiRadioSchedule.IsTechnical("behind"));
            var schedule = new AiRadioSchedule(1) { TechnicalSpacingSeconds = 0 };
            Assert.IsFalse(schedule.TechnicalBudgetBlocks("fuel", T));
        }

        // ---------- Ingenieur-Urteil: Fakten ----------

        [Test]
        public void Consistency_needs_five_clean_consecutive_laps()
        {
            var laps = Laps(91f, 90.1f, 90.3f, 90.0f, 90.4f, 90.2f);
            Assert.AreEqual(.4f, DriverJudgement.Spread(laps, 5, out float avg), .001f);
            Assert.AreEqual(90.2f, avg, .01f);
            laps[3].InPitLap = true;
            Assert.AreEqual(-1f, DriverJudgement.Spread(laps, 5, out _), "Boxenrunde unterbricht die Serie");
            laps[3].InPitLap = false; laps[4].Valid = false;
            Assert.AreEqual(-1f, DriverJudgement.Spread(laps, 5, out _), "ungültige Runde zählt nicht");
            Assert.AreEqual(-1f, DriverJudgement.Spread(Laps(90, 90, 90), 5, out _), "zu wenige Runden");
        }

        [Test]
        public void Defence_counts_laps_with_the_same_car_close_behind_and_position_kept()
        {
            var laps = Laps(90, 90, 90, 90);
            foreach (var l in laps) { l.BehindIdentity = "x"; l.GapBehind = .7f; }
            laps[0].BehindIdentity = "y";
            Assert.AreEqual(3, DriverJudgement.DefendedLaps(laps, out string who));
            Assert.AreEqual("x", who);
            laps[3].GapBehind = 1.4f;
            Assert.AreEqual(0, DriverJudgement.DefendedLaps(laps, out _), "über einer Sekunde ist kein Druck");
            laps[3].GapBehind = .5f; laps[2].Position = 6;
            Assert.AreEqual(1, DriverJudgement.DefendedLaps(laps, out _), "Platzwechsel beendet die Serie");
        }

        [Test]
        public void Positions_lost_ignore_laps_with_an_own_pit_stop()
        {
            var laps = Laps(90, 90, 90, 90);
            laps[1].Position = 5; laps[2].Position = 6; laps[3].Position = 8;
            Assert.AreEqual(3, DriverJudgement.PositionsLost(laps, 2));
            laps[3].InPitLap = true;
            Assert.AreEqual(0, DriverJudgement.PositionsLost(laps, 2));
        }

        // ---------- Ingenieur-Urteil: Sprache ----------

        [Test]
        public void Every_judgement_phrase_exists_in_three_languages_with_and_without_name()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var texts = new List<string>();
                foreach (string name in new[] { "Rob", null })
                    for (int v = 0; v < 10; v++)
                    {
                        texts.Add(DriverJudgement.Overtake(name, "Timo", v % 2 == 0, v % 3 == 0, v % 2 == 0 ? 1.8f : -1, v));
                        texts.Add(DriverJudgement.Overtake(name, null, false, false, -1, v));
                        texts.Add(DriverJudgement.Defence(name, v % 2 == 0 ? "Timo" : null, 3, v));
                        texts.Add(DriverJudgement.Consistency(name, 5, v % 2 == 0 ? .3f : .02f, v));
                        texts.Add(DriverJudgement.Slide(name, 3, 2, v));
                        texts.Add(DriverJudgement.Impact(name, v % 2 == 0, v));
                    }
                foreach (var t in texts)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(t));
                    StringAssert.DoesNotContain("{n}", t);
                    StringAssert.DoesNotContain("  ", t);
                    Assert.IsFalse(char.IsLower(t.TrimStart('¿', '¡')[0]), t);
                }
                Assert.GreaterOrEqual(texts.Distinct().Count(), 60, "viel Auswahl statt starrer Sätze");
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Without_a_name_the_question_still_reads_naturally()
        {
            Assert.AreEqual("Was ist los? Du hast in den letzten 2 Runden 3 Plätze verloren. Komm wieder in die Spur und konzentrier dich.",
                DriverJudgement.Slide(null, 3, 2, 0));
            StringAssert.StartsWith("Rob, was ist los?", DriverJudgement.Slide("Rob", 3, 2, 0));
            RadioLanguage.Current = RadioLanguage.Spanish;
            StringAssert.StartsWith("¿Qué pasa?", DriverJudgement.Slide(null, 3, 2, 0));
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Overtake_praise_pushes_towards_the_next_car_when_the_gap_is_known()
        {
            string text = DriverJudgement.Overtake("Rob", "Timo", false, true, 1.8f, 0);
            StringAssert.Contains("Timo", text);
            StringAssert.Contains("1,8 Sekunden", text);
        }

        // ---------- Ingenieur-Urteil: Ablauf ----------

        [Test]
        public void Clean_held_overtake_on_a_faster_car_is_praised()
        {
            var gs = Race(5); gs.SessionData.PlayerLapTimeSessionBest = 89f;
            var timo = Rival(gs, "timo", "Timo Glock", 4, 88f);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, Laps(90, 90, 90, 90), false, "Rob", true, T);
            gs.SessionData.ClassPosition = 4; timo.ClassPosition = 5;
            watch.Observe(gs, Laps(90, 90, 90, 90), false, "Rob", true, T.AddSeconds(.5));
            watch.Observe(gs, Laps(90, 90, 90, 90), false, "Rob", true, T.AddSeconds(10));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(10), false, true), "erst halten, dann loben");
            watch.Observe(gs, Laps(90, 90, 90, 90), false, "Rob", true, T.AddSeconds(16));
            var call = watch.Take(gs, T.AddSeconds(16), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-overtake", call.Topic);
            Assert.AreEqual(TeamMember.Ben, call.Member);
            StringAssert.Contains("Timo", call.Text);
        }

        [Test]
        public void Place_gained_through_a_rival_pit_stop_or_lost_again_is_not_praised()
        {
            var gs = Race(5); gs.SessionData.PlayerLapTimeSessionBest = 89f;
            var timo = Rival(gs, "timo", "Timo Glock", 4, 88f);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.SessionData.ClassPosition = 4; timo.ClassPosition = 5; timo.InPits = true;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(.5));
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(16));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(16), false, true), "Boxenstopp des Gegners");

            gs = Race(5); gs.SessionData.PlayerLapTimeSessionBest = 89f; timo = Rival(gs, "timo", "Timo Glock", 4, 88f);
            watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.SessionData.ClassPosition = 4; timo.ClassPosition = 5;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(.5));
            gs.SessionData.ClassPosition = 5; timo.ClassPosition = 4;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(5));
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(16));
            // Rob 21.11.: Kein Lob, aber eine Reaktion auf den Konter desselben Autos.
            var again = watch.Take(gs, T.AddSeconds(16), false, true);
            Assert.AreEqual("judge-overtake-countered", again?.Topic, "Platz gleich wieder verloren");
        }

        [Test]
        public void Impact_brings_empathy_once_the_car_is_moving_again()
        {
            var gs = Race(); gs.CarDamageData.DamageEnabled = true; gs.CarDamageData.AeroDamagePercent = 0;
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.CarDamageData.AeroDamagePercent = 12; gs.PositionAndMotionData.CarSpeed = 5;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(1));
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(9));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(9), false, true), "steht noch – nicht im Moment selbst");
            gs.PositionAndMotionData.CarSpeed = 40;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(12));
            var call = watch.Take(gs, T.AddSeconds(12), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-impact", call.Topic);
        }

        [Test]
        public void Slow_damage_creep_is_not_an_impact()
        {
            var gs = Race(); gs.CarDamageData.DamageEnabled = true;
            var watch = new DriverJudgementWatch(1);
            for (int i = 0; i <= 40; i++)
            {
                gs.CarDamageData.AeroDamagePercent = i * .2f;
                watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(i));
            }
            Assert.IsNull(watch.Take(gs, T.AddSeconds(40), false, true));
        }

        [Test]
        public void Losing_places_brings_a_question_and_a_push()
        {
            var gs = Race(8); gs.SessionData.CompletedLaps = 6;
            var laps = Laps(90, 90, 90, 90, 91, 92);
            laps[3].Position = 5; laps[4].Position = 6; laps[5].Position = 8;
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, laps, true, "Rob", true, T);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-slide", call.Topic);
        }

        [Test]
        public void Consistent_laps_are_praised_with_the_measured_spread()
        {
            var gs = Race(); gs.SessionData.CompletedLaps = 6;
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, Laps(91, 90.1f, 90.3f, 90.0f, 90.4f, 90.2f), true, "Rob", true, T);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-consistency", call.Topic);
            StringAssert.Contains("0,4", call.Text);
        }

        [Test]
        public void Defending_against_a_faster_car_is_praised()
        {
            var gs = Race(5); gs.SessionData.PlayerLapTimeSessionBest = 90f; gs.SessionData.CompletedLaps = 6;
            var timo = Rival(gs, "timo", "Timo Glock", 6, 89f);
            string id = RaceReader.Identity("timo", timo);
            var laps = Laps(91, 91, 90.5f, 91.5f, 90.2f, 91.8f);
            foreach (var l in laps.Skip(3)) { l.BehindIdentity = id; l.GapBehind = .6f; }
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, laps, true, "Rob", true, T);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-defence", call.Topic);
        }

        [Test]
        public void Race_mind_keeps_judgements_out_of_battles_and_final_laps()
        {
            var call = new ObservedRaceCall { Topic = "judge-overtake", Text = "x", Priority = 4, SessionId = 5, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
            Func<RacePhase, RaceAttention, MindVerdict> judge = (p, a) =>
                RaceMind.Judge(new RaceSituation { Utc = T, SessionId = 5, Phase = p, Attention = a }, call, T, out _);
            Assert.AreEqual(MindVerdict.Hold, judge(RacePhase.Middle, RaceAttention.Battle));
            Assert.AreEqual(MindVerdict.Admit, judge(RacePhase.Middle, RaceAttention.Calm));
            Assert.AreEqual(MindVerdict.Drop, judge(RacePhase.LastLap, RaceAttention.Calm));
        }

        [Test]
        public void Sim_impact_time_counts_only_with_a_noticeable_loss_of_speed()
        {
            var gs = Race(); gs.CarDamageData.LastImpactTime = 0; gs.PositionAndMotionData.CarSpeed = 50;
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.CarDamageData.LastImpactTime = 61.2f;                  // Einschlag gemeldet, Tempo bleibt (Kerb-Kontakt)
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(1));
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(2));
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(12));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(12), false, true), "ohne Tempoverlust kein Mitgefühl");

            gs = Race(); gs.CarDamageData.LastImpactTime = 0; gs.PositionAndMotionData.CarSpeed = 50;
            watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.CarDamageData.LastImpactTime = 61.2f;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(1));
            gs.PositionAndMotionData.CarSpeed = 30;                   // 20 m/s verloren
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(1.5));
            gs.PositionAndMotionData.CarSpeed = 40;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(11));
            var call = watch.Take(gs, T.AddSeconds(11), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-impact", call.Topic);
        }

        [Test]
        public void Impact_value_present_at_session_start_is_only_a_baseline()
        {
            var gs = Race(); gs.CarDamageData.LastImpactTime = 33f; gs.PositionAndMotionData.CarSpeed = 50;
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.PositionAndMotionData.CarSpeed = 20;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(1));
            gs.PositionAndMotionData.CarSpeed = 40;
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(11));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(11), false, true));
        }

        // ---------- Lila Sektoren ----------

        [Test]
        public void Sector_rating_distinguishes_personal_best_and_purple()
        {
            SessionLapObserver.RateSector(30.1f, 30.4f, 30.2f, out bool own, out bool purple);
            Assert.IsTrue(own); Assert.IsTrue(purple);
            SessionLapObserver.RateSector(30.3f, 30.4f, 30.2f, out own, out purple);
            Assert.IsTrue(own); Assert.IsFalse(purple, "schneller als ich, aber nicht als das Feld");
            SessionLapObserver.RateSector(30.1f, -1, 30.2f, out own, out purple);
            Assert.IsFalse(own); Assert.IsTrue(purple, "erste schnelle Runde: lila ohne frühere eigene Bestzeit");
            SessionLapObserver.RateSector(30.1f, -1, -1, out own, out purple);
            Assert.IsFalse(own || purple, "ohne Vergleich keine Behauptung");
        }

        [Test]
        public void Two_best_sectors_call_says_it_could_be_the_fastest_lap_in_six_ways()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var texts = new HashSet<string>();
                for (int v = 0; v < 6; v++)
                    foreach (bool purple in new[] { true, false })
                        foreach (bool spoken in new[] { true, false })
                            foreach (string name in new[] { "Rob", null })
                            {
                                string t = SessionLapObserver.FlyingLapText(name, purple, spoken, v % 2 == 0 ? .36 : -1, v);
                                Assert.IsFalse(string.IsNullOrWhiteSpace(t));
                                Assert.IsFalse(char.IsLower(t.TrimStart('¿', '¡')[0]), t);
                                texts.Add(t);
                            }
                Assert.GreaterOrEqual(texts.Count, 20);
            }
            RadioLanguage.Current = RadioLanguage.German;
            StringAssert.Contains("schnellste Runde", SessionLapObserver.FlyingLapText("Rob", false, false, -1, 0));
            StringAssert.Contains("lila", SessionLapObserver.FlyingLapText("Rob", true, false, -1, 1));
            StringAssert.Contains("0,4 Sekunden", SessionLapObserver.FlyingLapText(null, true, false, .36, 0));
        }

        // ---------- Rob 18.11.: schneller Platzverlust, Dreher, Vorfallpunkte, wieder im Tritt ----------

        [Test]
        public void Losing_three_places_quickly_brings_a_refocus_call()
        {
            var gs = Race(5);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.SessionData.ClassPosition = 6; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(4));
            gs.SessionData.ClassPosition = 8; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(10));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(10), false, true), "erst wenn sich die Reihenfolge beruhigt hat");
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(16));
            var call = watch.Take(gs, T.AddSeconds(16), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-dropped", call.Topic);
            StringAssert.Contains("3 Plätze", call.Text);
        }

        [Test]
        public void Own_pit_stop_is_no_position_loss()
        {
            var gs = Race(5);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.PitData.InPitlane = true; gs.SessionData.ClassPosition = 9; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(10));
            gs.PitData.InPitlane = false; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(30));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(30), false, true));
        }

        [Test]
        public void Spin_without_contact_gets_a_word_once_moving_again()
        {
            var gs = Race(5); gs.PositionAndMotionData.CarSpeed = 35;
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.PositionAndMotionData.CarSpeed = 1; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(2));
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(5));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(5), false, true), "steht noch");
            gs.PositionAndMotionData.CarSpeed = 25; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(9));
            var call = watch.Take(gs, T.AddSeconds(9), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-spin", call.Topic);
        }

        [Test]
        public void Rising_incident_points_get_a_calm_down_call()
        {
            var gs = Race(5); gs.SessionData.CurrentIncidentCount = 2;
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.SessionData.CurrentIncidentCount = 3; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(40));
            gs.SessionData.CurrentIncidentCount = 4; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(80));
            gs.SessionData.CurrentIncidentCount = 5; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(120));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(120), false, true), "drei Punkte sind noch kein Muster");
            gs.SessionData.CurrentIncidentCount = 6; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(150));
            var call = watch.Take(gs, T.AddSeconds(150), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-incidents", call.Topic);
            StringAssert.Contains("4", call.Text);
        }

        [Test]
        public void New_human_calls_vary_in_every_language()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                foreach (string name in new[] { "Rob", null })
                {
                    var groups = new[]
                    {
                        Enumerable.Range(0, 6).Select(v => DriverJudgement.Dropped(name, 3, v)).ToList(),
                        Enumerable.Range(0, 6).Select(v => DriverJudgement.Spin(name, v)).ToList(),
                        Enumerable.Range(0, 5).Select(v => DriverJudgement.IncidentsRising(name, 4, v)).ToList(),
                        Enumerable.Range(0, 5).Select(v => DriverJudgement.Recovered(name, v)).ToList(),
                        Enumerable.Range(0, 5).Select(v => DriverJudgement.Impact(name, true, v)).ToList(),
                    };
                    foreach (var g in groups)
                    {
                        Assert.GreaterOrEqual(g.Distinct().Count(), 5, lang + ": " + g[0]);
                        foreach (var t in g)
                        {
                            StringAssert.DoesNotContain("{n}", t);
                            StringAssert.DoesNotContain("  ", t);
                            Assert.IsFalse(char.IsLower(t.TrimStart('¿', '¡')[0]), t);
                        }
                    }
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Taking_the_place_back_from_the_same_rival_is_a_story()
        {
            // Synthetisches Szenario 2 (RRCA 2.0): Timo überholt Rob, Rob holt sich den Platz zurück.
            var gs = Race(4); gs.SessionData.PlayerLapTimeSessionBest = 89f;
            var timo = Rival(gs, "timo", "Timo", 5, 89.5f);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, Laps(90, 90, 90, 90), false, "Rob", true, T);
            gs.SessionData.ClassPosition = 5; gs.SessionData.OverallPosition = 5; timo.ClassPosition = 4; timo.OverallPosition = 4;
            watch.Observe(gs, Laps(90, 90, 90, 90), false, "Rob", true, T.AddSeconds(1));
            gs.SessionData.ClassPosition = 4; gs.SessionData.OverallPosition = 4; timo.ClassPosition = 5; timo.OverallPosition = 5;
            watch.Observe(gs, Laps(90, 90, 90, 90), false, "Rob", true, T.AddSeconds(40));
            watch.Observe(gs, Laps(90, 90, 90, 90), false, "Rob", true, T.AddSeconds(56));
            var call = watch.Take(gs, T.AddSeconds(56), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-retake", call.Topic);
            StringAssert.Contains("Timo", call.Text);
        }

        [Test]
        public void Retake_lines_vary_in_every_language()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                foreach (string passed in new[] { "Timo", null })
                    foreach (string name in new[] { "Rob", null })
                    {
                        var texts = Enumerable.Range(0, 6).Select(v => DriverJudgement.Retake(name, passed, v)).ToList();
                        Assert.AreEqual(6, texts.Distinct().Count(), texts[0]);
                        foreach (var t in texts) { StringAssert.DoesNotContain("{n}", t); StringAssert.DoesNotContain("  ", t); Assert.IsFalse(char.IsLower(t.TrimStart('¡', '¿')[0]), t); }
                    }
            }
            RadioLanguage.Current = RadioLanguage.German;
        }
    }
}
