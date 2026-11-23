using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Haupt-Renningenieur liest das Feld (Rob 17.11.2026): Zweikampf vor dir, Schnellster der Klasse, freie Fahrt.</summary>
    [TestFixture, Category("AiTeam")]
    public class RaceInsightTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 17, 20, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Race(int position, int lap, float myLap)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 21; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = position; gs.SessionData.OverallPosition = position; gs.SessionData.CompletedLaps = lap;
            gs.SessionData.LapTimePrevious = myLap; gs.SessionData.PreviousLapWasValid = true;
            gs.carClass = new CarData.CarClass();
            return gs;
        }

        private static OpponentData Rival(GameStateData gs, string name, int position, float lastLap)
        {
            var o = new OpponentData { DriverRawName = name, ClassPosition = position, OverallPosition = position, CarClass = gs.carClass, LastLapTime = lastLap, LastLapValid = true };
            gs.OpponentData[name] = o;
            return o;
        }

        [Test]
        public void Two_cars_fighting_ahead_are_called_as_an_opportunity()
        {
            var gs = Race(5, 6, 90.5f); gs.SessionData.TimeDeltaFront = 1.6f; gs.SessionData.TimeDeltaBehind = 3f;
            Rival(gs, "Keller", 4, 90.8f); Rival(gs, "Wurz", 3, 90.9f); Rival(gs, "Mayer", 6, 91f);
            var watch = new RaceInsightWatch(1) { Gap = (front, back) => .6f };
            watch.Observe(gs, null, "Rob", true, T, true);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-battle-ahead", call.Topic);
            StringAssert.Contains("Wurz", call.Text); StringAssert.Contains("Keller", call.Text);
            var facts = RaceInsightWatch.Latest(21);
            Assert.IsNotNull(facts, "Sprachfragen bekommen die Feld-Erkenntnisse");
            Assert.IsTrue((bool)facts["zweikampf_vor_dir"]["echter_zweikampf"]);
            Assert.IsNull(RaceInsightWatch.Latest(999), "andere Sitzung: nichts");

            var far = Race(5, 6, 90.5f); far.SessionData.TimeDeltaFront = 4.5f;
            Rival(far, "Keller", 4, 90.8f); Rival(far, "Wurz", 3, 90.9f); Rival(far, "Mayer", 6, 91f);
            var quiet = new RaceInsightWatch(1) { Gap = (front, back) => .6f };
            quiet.Observe(far, new RaceSituation { FrontRate = 0 }, "Rob", true, T, true);
            Assert.IsNull(quiet.Take(far, T, false, true), "weit weg und nicht näherkommend: keine Chance zu melden");
        }

        [Test]
        public void Two_fastest_laps_in_class_in_a_row_are_praised()
        {
            var watch = new RaceInsightWatch(1) { Gap = (a, b) => -1 };
            var gs = Race(1, 2, 89f); Rival(gs, "A", 2, 90f); Rival(gs, "B", 3, 90.5f); Rival(gs, "C", 4, 91f);
            watch.Observe(gs, null, "Rob", true, T, true);
            Assert.IsNull(watch.Take(gs, T, false, true), "eine Runde ist noch kein Trend");
            gs = Race(1, 3, 89.1f); Rival(gs, "A", 2, 90f); Rival(gs, "B", 3, 90.5f); Rival(gs, "C", 4, 91f);
            watch.Observe(gs, null, "Rob", true, T.AddSeconds(90), true);
            var call = watch.Take(gs, T.AddSeconds(90), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-fastest", call.Topic);
            StringAssert.Contains("2", call.Text);
        }

        [Test]
        public void Free_air_both_ways_suggests_saving_tyres_and_fuel()
        {
            var gs = Race(4, 6, 92f); gs.SessionData.TimeDeltaFront = 8f; gs.SessionData.TimeDeltaBehind = 7f;
            Rival(gs, "A", 3, 90f); Rival(gs, "B", 2, 90.5f); Rival(gs, "C", 5, 91f);
            var watch = new RaceInsightWatch(1) { Gap = (a, b) => 8f };
            watch.Observe(gs, new RaceSituation { Phase = RacePhase.Middle, RearRate = -.1f }, "Rob", true, T, true);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-free-air", call.Topic);
            StringAssert.Contains("8,0 Sekunden", call.Text);
        }

        [Test]
        public void Insight_texts_vary_and_exist_in_three_languages()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var all = Enumerable.Range(0, 4).SelectMany(v => new[] {
                    RaceInsightWatch.BattleAhead(v % 2 == 0 ? "Rob" : null, "Wurz", "Keller", .6f, 1.6f, v % 2 == 0 ? .3f : 0, v),
                    RaceInsightWatch.BattleAhead("Rob", null, null, .6f, 1.6f, 0, v),
                    RaceInsightWatch.Fastest(v % 2 == 0 ? "Rob" : null, 3, v),
                    RaceInsightWatch.FreeAirText(v % 2 == 0 ? "Rob" : null, v == 1 ? -1 : 8f, 7f, v) }).ToArray();
                Assert.GreaterOrEqual(all.Distinct().Count(), 14, lang);
                foreach (var t in all) { Assert.IsFalse(char.IsLower(t[0]), t); StringAssert.DoesNotContain("  ", t); }
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Race_mind_keeps_insights_out_of_battles_and_the_last_laps()
        {
            var call = new ObservedRaceCall { Topic = "insight-battle-ahead", Text = "x", Priority = 4, SessionId = 5, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
            // RaceRoom Hockenheim 23.11. (Rob: „knappe Kämpfe um mich herum“): der Zweikampf voraus kommt auch im eigenen Zweikampf.
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.Middle, Attention = RaceAttention.Battle }, call, T, out _));
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.Middle, Attention = RaceAttention.Caution }, call, T, out _));
            Assert.AreEqual(MindVerdict.Drop, RaceMind.Judge(new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.LastLap, Attention = RaceAttention.Calm }, call, T, out _));
        }

        // ---------- Wetter nur bei Änderung ----------

        private static WeatherReading W(float air, float track, bool rain = false) => new WeatherReading { Air = air, Track = track, Rain = rain };

        [Test]
        public void Weather_is_repeated_only_after_a_noticeable_change_and_names_the_direction()
        {
            Assert.IsFalse(W(21.2f, 25.4f).ChangedFrom(W(21.2f, 25.4f)), "Bathurst: gleiche Werte, keine zweite Meldung");
            Assert.IsFalse(W(21.6f, 26.4f).ChangedFrom(W(21.2f, 25.4f)));
            Assert.IsTrue(W(21.2f, 27.0f).ChangedFrom(W(21.2f, 25.4f)));
            Assert.IsTrue(W(22.3f, 25.4f).ChangedFrom(W(21.2f, 25.4f)));
            Assert.IsTrue(W(21.2f, 25.4f, true).ChangedFrom(W(21.2f, 25.4f)));
            StringAssert.Contains("wärmer", W(21.2f, 27.4f).ChangeText(W(21.2f, 25.4f), 0));
            StringAssert.Contains("kühler", W(21.2f, 23.4f).ChangeText(W(21.2f, 25.4f), 0));
            StringAssert.Contains("Regen", W(21.2f, 25.4f, true).ChangeText(W(21.2f, 25.4f), 0));
            Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => W(21.2f, 27.4f).ChangeText(W(21.2f, 25.4f), v)).Distinct().Count());
        }

        [Test]
        public void Podium_within_reach_is_called_with_the_measured_gap()
        {
            var gs = Race(4, 8, 91f); gs.SessionData.TimeDeltaFront = 2.1f; gs.SessionData.TimeDeltaBehind = 4f;
            Rival(gs, "Keller", 3, 90.8f); Rival(gs, "Wurz", 2, 90.9f); Rival(gs, "Mayer", 5, 91f);
            var watch = new RaceInsightWatch(1) { Gap = (a, b) => 5f };
            watch.Observe(gs, new RaceSituation { Phase = RacePhase.Middle, FrontRate = .3f }, "Rob", true, T, true);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-target", call.Topic);
            StringAssert.Contains("2,1 Sekunden", call.Text);
            StringAssert.Contains("Podium", call.Text);
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var texts = Enumerable.Range(0, 3).SelectMany(v => new[] { RaceInsightWatch.TargetText(v == 1 ? null : "Rob", 3, 2.1f, .3f, v), RaceInsightWatch.TargetText("Rob", 1, 1.2f, 0, v) }).ToArray();
                Assert.AreEqual(6, texts.Distinct().Count(), lang);
                foreach (var t in texts) Assert.IsFalse(char.IsLower(t.TrimStart('¡', '¿')[0]), t);
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Neighbour_pit_stop_comes_with_the_tactical_consequence_in_races_only()
        {
            StringAssert.Contains("freie Bahn", NativeFieldEvents.PitConsequence("car_in_front_is_pitting", SessionType.Race, 0));
            StringAssert.Contains("Undercut", NativeFieldEvents.PitConsequence("car_behind_is_pitting", SessionType.Race, 0));
            Assert.AreEqual("", NativeFieldEvents.PitConsequence("car_in_front_is_pitting", SessionType.Practice, 0));
            Assert.AreEqual("", NativeFieldEvents.PitConsequence("leader_is_pitting", SessionType.Race, 0));
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => NativeFieldEvents.PitConsequence("car_behind_is_pitting", SessionType.Race, v)).Distinct().Count());
                Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => NativeFieldEvents.PitConsequence("car_in_front_is_pitting", SessionType.Race, v)).Distinct().Count());
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        // ---------- Technik-Check ohne Zahlenflut ----------

        private static GameStateData Car(float water, float oil, float aero = 0)
        {
            var gs = new GameStateData(T.Ticks);
            gs.carClass = new CarData.CarClass(); // Grenzen 115 °C Wasser, 130 °C Öl
            gs.CarDamageData.DamageEnabled = true; gs.CarDamageData.AeroDamagePercent = aero;
            gs.EngineData.EngineWaterTemp = water; gs.EngineData.EngineOilTemp = oil;
            return gs;
        }

        [Test]
        public void Healthy_car_gets_one_sentence_without_numbers()
        {
            string text = AiRadioSchedule.VehicleCheck(Car(91, 110), 0);
            StringAssert.Contains("grünen Bereich", text);
            Assert.IsFalse(text.Any(char.IsDigit), text);
            Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => AiRadioSchedule.VehicleCheck(Car(91, 110), v)).Distinct().Count());
        }

        [Test]
        public void Only_what_stands_out_is_named_with_its_value()
        {
            string hot = AiRadioSchedule.VehicleCheck(Car(108, 110), 0);
            StringAssert.Contains("Wasser 108,0 Grad", hot);
            StringAssert.DoesNotContain("Öl", hot);
            string damaged = AiRadioSchedule.VehicleCheck(Car(91, 110, aero: 6), 0);
            StringAssert.Contains("6,0", damaged);
            var unknown = Car(91, 110); unknown.CarDamageData.DamageEnabled = false;
            Assert.IsNull(AiRadioSchedule.VehicleCheck(unknown, 0), "ohne Schadensdaten kein „alles in Ordnung“");
        }

        // ---------- Stopp-Bilanz und Boxenverlust ----------

        private static LapRecord Lap(int n, float time, bool pit = false) =>
            new LapRecord { LapNumber = n, LapTime = time, Valid = !pit, Green = true, InPitLap = pit };

        [Test]
        public void Pit_loss_is_measured_from_in_and_out_lap_against_clean_laps()
        {
            var meter = new PitLossMeter();
            var laps = new List<LapRecord> { Lap(1, 95), Lap(2, 90.2f), Lap(3, 90f), Lap(4, 90.4f), Lap(5, 101f, true), Lap(6, 104f, true) };
            Assert.AreEqual(-1, meter.Observe(21, laps), "Ausfahrt noch nicht abgeschlossen");
            laps.Add(Lap(7, 90.3f));
            Assert.AreEqual(24.4f, meter.Observe(21, laps), .01f, "101 + 104 − 2 × 90,3 (Median aus 90,0 / 90,2 / 90,4 / 95,0)");
            Assert.AreEqual(-1, meter.Observe(21, laps), "derselbe Stopp zählt nur einmal");
            Assert.AreEqual(24.4f, meter.Seconds, .01f);
            Assert.AreEqual(-1, meter.Observe(22, laps.Take(4).ToList()), "neue Sitzung");
            Assert.AreEqual(-1, meter.Seconds, "neue Sitzung beginnt ohne Messung");

            var yellow = new List<LapRecord> { Lap(1, 95), Lap(2, 90f), Lap(3, 90f), Lap(4, 101f, true), Lap(5, 104f, true), Lap(6, 90f) };
            yellow[3].Green = false;
            Assert.AreEqual(-1, new PitLossMeter().Observe(21, yellow), "Stopp unter Gelb/Safety-Car ist kein Maßstab");
        }

        [Test]
        public void Rivals_ahead_with_fewer_stops_are_put_into_context_with_measured_pit_loss()
        {
            var gs = Race(6, 20, 90.5f); gs.SessionData.TimeDeltaFront = 3f; gs.SessionData.TimeDeltaBehind = 4f;
            gs.PitData.NumPitStops = 1;
            var keller = Rival(gs, "Keller", 5, 90.8f); keller.NumPitStops = 0;
            var wurz = Rival(gs, "Wurz", 4, 90.9f); wurz.NumPitStops = 0;
            var mayer = Rival(gs, "Mayer", 3, 90.9f); mayer.NumPitStops = 1;
            var gaps = new Dictionary<string, float> { ["Keller"] = 3f, ["Wurz"] = 31f, ["Mayer"] = 8f };
            var watch = new RaceInsightWatch(2) { Gap = (a, b) => -1, GapToRob = (g, o) => gaps[o.DriverRawName] };
            watch.Observe(gs, null, "Rob", true, T, true, 24.6f);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-stops-ahead", call.Topic);
            StringAssert.Contains("Keller", call.Text); StringAssert.Contains("Wurz", call.Text); StringAssert.DoesNotContain("Mayer", call.Text);
            StringAssert.Contains("rechnerisch vor Keller.", call.Text, "nur wer innerhalb des Boxenverlusts liegt, dreht sich rechnerisch");
            StringAssert.Contains("24,6", call.Text);
            var facts = RaceInsightWatch.Latest(21);
            Assert.AreEqual(2, ((Newtonsoft.Json.Linq.JArray)facts["vor_dir_mit_weniger_stopps"]).Count);
            Assert.AreEqual(24.6, (double)facts["gemessener_boxenverlust_s"], .01);

            // Gleiche Lage wird nicht jede Runde wiederholt.
            call.MarkPlaybackStarted(T);
            gs.SessionData.CompletedLaps = 22; gs.SessionData.LapTimePrevious = 92f; // keine „Schnellster“-Meldung dazwischen
            watch.Observe(gs, null, "Rob", true, T.AddMinutes(3), true, 24.6f);
            Assert.IsNull(watch.Take(gs, T.AddMinutes(3), false, true));
        }

        [Test]
        public void Rival_behind_who_already_stopped_is_flagged_without_inventing_a_pit_loss()
        {
            var gs = Race(4, 18, 90.5f); gs.SessionData.TimeDeltaFront = 9f; gs.SessionData.TimeDeltaBehind = 12f;
            var mayer = Rival(gs, "Mayer", 5, 90.4f); mayer.NumPitStops = 1;
            var watch = new RaceInsightWatch(3) { Gap = (a, b) => -1, GapToRob = (g, o) => 12f };
            watch.Observe(gs, null, "Rob", true, T, true);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-stops-behind", call.Topic);
            StringAssert.Contains("Mayer", call.Text);
            StringAssert.DoesNotContain("rechnerisch", call.Text, "ohne gemessenen Boxenverlust keine Rechnung");
        }

        [Test]
        public void Stop_balance_texts_vary_and_exist_in_three_languages()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var one = new List<string> { "Keller" }; var two = new List<string> { "Keller", "Wurz" };
                var ahead = Enumerable.Range(0, 3).Select(v => RaceInsightWatch.StopsAheadText("Rob", two, one, 24.6f, v)).ToArray();
                var behind = Enumerable.Range(0, 3).Select(v => RaceInsightWatch.StopsBehindText(null, one, new List<string>(), 24.6f, v)).ToArray();
                Assert.AreEqual(3, ahead.Distinct().Count(), lang); Assert.AreEqual(3, behind.Distinct().Count(), lang);
                foreach (string text in ahead.Concat(behind)) { StringAssert.Contains("Keller", text); StringAssert.DoesNotContain("  ", text); }
                Assert.AreEqual(lang == RadioLanguage.German ? "A, B und C" : lang == RadioLanguage.English ? "A, B and C" : "A, B y C",
                    RaceInsightWatch.List(new[] { "A", "B", "C" }));
            }
            RadioLanguage.Current = RadioLanguage.German;
        }
    }
}
