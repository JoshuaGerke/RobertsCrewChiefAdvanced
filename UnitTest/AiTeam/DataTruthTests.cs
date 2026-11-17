using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Datenwahrheit (Rob 17.11.2026): Sektorereignisse dürfen nicht an einem einzigen Takt hängen.</summary>
    [TestFixture, Category("AiTeam")]
    public class DataTruthTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData State(int sector, float sectorOne, DateTime source, bool valid = true)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = SessionType.Practice; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = 4; gs.PositionAndMotionData.CarSpeed = 50; gs.carClass = new CarData.CarClass();
            gs.SessionData.CompletedLaps = 2; gs.SessionData.SectorNumber = sector; gs.SessionData.CurrentLapIsValid = valid;
            gs.SessionData.PlayerBestSector1Time = 30f;
            gs.SourceTelemetryUtc = source;
            gs.SessionData.PlayerLapData.Add(new LapData { LapNumber = 3, IsValid = true, SectorTimes = new[] { sectorOne, -1f, -1f } });
            return gs;
        }

        [Test]
        public void Sector_time_arriving_one_tick_late_is_still_evaluated()
        {
            var before = State(1, -1, T.AddSeconds(30));
            var edge = State(2, -1, T.AddSeconds(30.1));          // Sektorwechsel, Zeit noch nicht eingetragen
            var late = State(2, 29.4f, T.AddSeconds(30.2));       // ein Takt später ist sie da
            var watch = new SessionLapObserver();
            watch.Observe(before, null, before.SourceTelemetryUtc, false);
            watch.Observe(edge, null, edge.SourceTelemetryUtc, false, before, true);
            Assert.IsNull(watch.Take(edge, edge.SourceTelemetryUtc, false));
            watch.Observe(late, null, late.SourceTelemetryUtc, false, edge, true);
            var call = watch.Take(late, late.SourceTelemetryUtc, false);
            Assert.IsNotNull(call, "früher ging die Gelegenheit für die ganze Runde verloren");
            Assert.AreEqual("sector-best", call.Topic);
            StringAssert.Contains("29,4", call.Text);
        }

        [Test]
        public void Late_sector_time_after_the_retry_window_is_ignored()
        {
            var before = State(1, -1, T.AddSeconds(30));
            var edge = State(2, -1, T.AddSeconds(30.1));
            var late = State(2, 29.4f, T.AddSeconds(33));
            var watch = new SessionLapObserver();
            watch.Observe(before, null, before.SourceTelemetryUtc, false);
            watch.Observe(edge, null, edge.SourceTelemetryUtc, false, before, true);
            watch.Observe(late, null, late.SourceTelemetryUtc, false, edge, true);
            Assert.IsNull(watch.Take(late, late.SourceTelemetryUtc, false));
        }

        [Test]
        public void Invalid_lap_at_the_sector_change_is_not_retried()
        {
            var before = State(1, -1, T.AddSeconds(30));
            var edge = State(2, 29.4f, T.AddSeconds(30.1), valid: false);
            var later = State(2, 29.4f, T.AddSeconds(30.2));
            var watch = new SessionLapObserver();
            watch.Observe(before, null, before.SourceTelemetryUtc, false);
            watch.Observe(edge, null, edge.SourceTelemetryUtc, false, before, true);
            watch.Observe(later, null, later.SourceTelemetryUtc, false, edge, true);
            Assert.IsNull(watch.Take(later, later.SourceTelemetryUtc, false));
        }

        [Test]
        public void Blocker_names_the_reason_and_only_transient_reasons_are_retried()
        {
            var before = State(1, -1, T.AddSeconds(30));
            Assert.AreEqual("sector_time_missing", SessionLapObserver.LiveSectorBlocker(State(2, -1, T.AddSeconds(30.1)), before, T.AddSeconds(30.1), 0, out _));
            Assert.AreEqual("duplicate_sample", SessionLapObserver.LiveSectorBlocker(State(2, 29.4f, T.AddSeconds(30)), before, T.AddSeconds(30.1), 0, out _));
            Assert.AreEqual("stale_source", SessionLapObserver.LiveSectorBlocker(State(2, 29.4f, T.AddSeconds(30.1)), before, T.AddSeconds(32), 0, out _));
            Assert.AreEqual("lap_invalid", SessionLapObserver.LiveSectorBlocker(State(2, 29.4f, T.AddSeconds(30.1), false), before, T.AddSeconds(30.1), 0, out _));
            Assert.IsNull(SessionLapObserver.LiveSectorBlocker(State(2, 29.4f, T.AddSeconds(30.1)), before, T.AddSeconds(30.1), 0, out var lap));
            Assert.AreEqual(3, lap.LapNumber);
            Assert.IsTrue(SessionLapObserver.RetryableSectorReason("sector_time_missing"));
            Assert.IsTrue(SessionLapObserver.RetryableSectorReason("duplicate_sample"));
            Assert.IsFalse(SessionLapObserver.RetryableSectorReason("lap_invalid"));
            Assert.IsFalse(SessionLapObserver.RetryableSectorReason("local_yellow"));
        }

        // ---------- Datenwahrheit je Kanal ----------

        private static GameStateData Driving(float wear, float temp, bool pit = false)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 9; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.PositionAndMotionData.CarSpeed = 50; gs.PitData.InPitlane = pit;
            var t = gs.TyreData; t.TyreWearActive = true;
            t.FrontLeftPercentWear = t.FrontRightPercentWear = t.RearLeftPercentWear = t.RearRightPercentWear = wear;
            t.FrontLeft_CenterTemp = t.FrontRight_CenterTemp = t.RearLeft_CenterTemp = t.RearRight_CenterTemp = temp;
            return gs;
        }

        [Test]
        public void Wear_that_does_not_move_while_driving_is_frozen_and_not_spoken_as_current()
        {
            var truth = new DataTruth();
            for (int i = 0; i <= 200; i++) truth.Observe(Driving(5f, 80 + i % 7), T.AddSeconds(i));
            Assert.AreEqual(ChannelState.Stale, truth.State(SimChannel.TyreWear));
            Assert.IsFalse(truth.MaySpeakAsCurrent(SimChannel.TyreWear));
            Assert.AreEqual(ChannelState.Live, truth.State(SimChannel.TyreTemperature));
            StringAssert.StartsWith("eingefroren seit", truth.Describe(SimChannel.TyreWear));
        }

        [Test]
        public void Moving_wear_is_live_and_standing_in_the_pits_does_not_count_as_frozen()
        {
            var truth = new DataTruth();
            for (int i = 0; i <= 200; i++) truth.Observe(Driving(5f + i * .02f, 80), T.AddSeconds(i));
            Assert.AreEqual(ChannelState.Live, truth.State(SimChannel.TyreWear));

            truth = new DataTruth();
            truth.Observe(Driving(5f, 80), T);
            truth.Observe(Driving(5.1f, 80), T.AddSeconds(1));
            for (int i = 2; i <= 400; i++) truth.Observe(Driving(5.1f, 80, pit: true), T.AddSeconds(i));
            Assert.AreEqual(ChannelState.Live, truth.State(SimChannel.TyreWear), "Boxenstand ist keine Fahrt");
        }

        [Test]
        public void Pauses_do_not_count_as_driving_and_unknown_keeps_the_old_behaviour()
        {
            var truth = new DataTruth();
            Assert.AreEqual(ChannelState.Unknown, truth.State(SimChannel.TyreWear));
            Assert.IsTrue(truth.MaySpeakAsCurrent(SimChannel.TyreWear), "Bestandsschutz vor dem ersten Beleg");
            for (int i = 0; i < 20; i++) truth.Observe(Driving(5f, 80), T.AddSeconds(i * 30)); // 30-s-Sprünge = Pause/Menü
            Assert.AreNotEqual(ChannelState.Stale, truth.State(SimChannel.TyreWear));
        }

        [Test]
        public void Frozen_wear_silences_the_routine_tyre_report()
        {
            var truth = new DataTruth();
            try
            {
                DataTruth.Use(truth);
                var gs = Driving(12f, 85);
                for (int i = 0; i <= 200; i++) truth.Observe(gs, T.AddSeconds(i));
                Assert.IsFalse(DataTruth.Current.MaySpeakAsCurrent(SimChannel.TyreWear));
                var prefs = new AiRadioPreferences();
                foreach (var o in AiRadioPreferences.Options) prefs.Set(o.Id, 0);
                prefs.Set("tyres", 30); prefs.SetTolerance("tyres", 0, 0);
                var schedule = new AiRadioSchedule(1); schedule.Observe(gs, T, prefs);
                var call = schedule.Take(new RaceDataContext { GameState = gs, NowUtc = T.AddSeconds(31), LastTelemetryUtc = T.AddSeconds(31) }, prefs, false);
                Assert.IsTrue(call == null || call.Topic != "tyres", "eingefrorener Verschleiß wird nicht als aktuell gemeldet");
            }
            finally { DataTruth.Use(new DataTruth()); }
        }

        [Test]
        public void Static_capabilities_follow_the_official_interfaces()
        {
            Assert.IsFalse(SimCapabilities.TyreWear(GameType.ASSETTO_EVO));
            Assert.IsTrue(SimCapabilities.TyreWear(GameType.AMS2));
            Assert.IsFalse(SimCapabilities.DamageValues(GameType.IRACING));
            Assert.IsTrue(SimCapabilities.IncidentPoints(GameType.IRACING));
            Assert.IsTrue(SimCapabilities.DamageValues(GameType.LMU));
            StringAssert.Contains("Vorfallpunkte", SimCapabilities.Describe(GameType.IRACING));
            Assert.AreEqual("", SimCapabilities.Describe(GameType.RACE_ROOM));
        }

        [Test]
        public void Report_lists_every_channel_for_the_diagnostics()
        {
            var truth = new DataTruth();
            truth.Observe(Driving(5f, 80), T);
            var r = truth.Report();
            foreach (SimChannel c in Enum.GetValues(typeof(SimChannel))) Assert.IsTrue(r.ContainsKey(c.ToString()), c.ToString());
            Assert.IsTrue(r.ContainsKey("game"));
        }

        // ---------- Mehr Abwechslung (Phase D) ----------

        [Test]
        public void Position_start_and_gap_trend_texts_offer_more_variety_in_three_languages()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var s = new GameStateData(T.Ticks).SessionData; s.ClassPosition = 11;
                var up = System.Linq.Enumerable.Range(0, 9).Select(i => ActiveRaceObserver.MovedUp(s, 11, i, i % 2 == 0 ? "Rob" : null)).ToArray();
                var down = System.Linq.Enumerable.Range(0, 9).Select(i => ActiveRaceObserver.PositionLost(s, 11, i, "Rob")).ToArray();
                var now = System.Linq.Enumerable.Range(0, 6).Select(i => ActiveRaceObserver.PositionNow(s, 11, i)).ToArray();
                var start = System.Linq.Enumerable.Range(0, 5).SelectMany(v => new[] { 14, 11, 9 }.Select(p => ActiveRaceObserver.StartVerdict(s, p, "Rob", v))).ToArray();
                var ahead = System.Linq.Enumerable.Range(0, 6).SelectMany(v => new[] { "Keller", "" }.Select(n => GapTrendWatch.AheadText(n, .3f, 5, false, -1, 2.3f, v))).ToArray();
                var behind = System.Linq.Enumerable.Range(0, 6).SelectMany(v => new[] { "Wurz", "" }.Select(n => GapTrendWatch.BehindText(n, .4f, 4, false, v))).ToArray();
                Assert.AreEqual(9, up.Distinct().Count(), lang);
                Assert.AreEqual(9, down.Distinct().Count(), lang);
                Assert.AreEqual(6, now.Distinct().Count(), lang);
                Assert.AreEqual(15, start.Distinct().Count(), lang);
                Assert.AreEqual(12, ahead.Distinct().Count(), lang);
                Assert.AreEqual(12, behind.Distinct().Count(), lang);
                foreach (var t in up.Concat(down).Concat(now).Concat(start).Concat(ahead).Concat(behind))
                {
                    Assert.IsFalse(char.IsLower(t.TrimStart('¿', '¡')[0]), t);
                    StringAssert.DoesNotContain("  ", t);
                    StringAssert.DoesNotContain("Den Vordermann", t);
                    StringAssert.DoesNotContain("für der Hintermann", t);
                }
                Assert.IsTrue(start.All(t => t.Contains("Rob") && t.Contains("11")), lang);
                Assert.IsFalse(down.Any(t => t.Contains("vorgerückt")));
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        // ---------- Bathurst 16.11.: schnelle Runde, Zeitenliste, Sektorfrage ----------

        [Test]
        public void Sector_on_best_pace_marks_a_push_lap_and_slow_or_race_laps_do_not()
        {
            var before = State(1, -1, T.AddSeconds(30));
            var edge = State(2, 30.1f, T.AddSeconds(30.1));        // 0,1 s über der Bestmarke: Bestzeitkurs
            var watch = new SessionLapObserver();
            watch.Observe(before, null, before.SourceTelemetryUtc, false);
            watch.Observe(edge, null, edge.SourceTelemetryUtc, false, before, true);
            Assert.IsTrue(watch.HotLap(edge), "Pushrunde: Routine schweigt");
            edge.SessionData.CurrentLapIsValid = false;
            Assert.IsFalse(watch.HotLap(edge), "ungültig: keine Pushrunde mehr");

            var slowWatch = new SessionLapObserver();
            var slow = State(2, 31.0f, T.AddSeconds(30.1));         // eine Sekunde langsamer
            slowWatch.Observe(before, null, before.SourceTelemetryUtc, false);
            slowWatch.Observe(slow, null, slow.SourceTelemetryUtc, false, before, true);
            Assert.IsFalse(slowWatch.HotLap(slow));

            var race = State(2, 30.1f, T.AddSeconds(30.1)); race.SessionData.SessionType = SessionType.Race;
            var raceWatch = new SessionLapObserver(); raceWatch.MarkHotLapForTest(3);
            Assert.IsFalse(raceWatch.HotLap(race), "im Rennen gilt die Pushrunden-Stille nicht");
        }

        [Test]
        public void Live_sectors_compare_with_the_best_before_this_lap_and_with_the_field()
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = SessionType.Practice; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.carClass = new CarData.CarClass(); gs.SessionData.CompletedLaps = 2; gs.SessionData.SectorNumber = 3; gs.SessionData.CurrentLapIsValid = true;
            gs.SourceTelemetryUtc = T;
            gs.SessionData.PlayerLapData.Add(new LapData { LapNumber = 2, IsValid = true, LapTime = 124.8f, SectorTimes = new[] { 51.367f, 31.2f, 42.2f } });
            gs.SessionData.PlayerLapData.Add(new LapData { LapNumber = 3, IsValid = true, SectorTimes = new[] { 51.187f, 31.04f, -1f } });
            gs.OpponentData["rafa"] = new OpponentData { DriverRawName = "Rafa Rivera", CarClass = gs.carClass, bestSector1Time = 51.0f, bestSector2Time = 31.1f };
            var live = RaceDataTools.CurrentSectors(new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T });
            Assert.IsNotNull(live);
            var sectors = (Newtonsoft.Json.Linq.JArray)live["abgeschlossene_sektoren"];
            Assert.AreEqual(2, sectors.Count);
            Assert.IsTrue((bool)sectors[1]["neue_persoenliche_bestzeit"], "Sektor 2 ist neue Bestzeit – das muss Mik sehen");
            Assert.AreEqual(-.16, (double)sectors[1]["differenz_zur_bestmarke_s"], .001);
            Assert.IsTrue((bool)sectors[1]["schneller_als_alle_gegner_lila"]);
            Assert.IsFalse((bool)sectors[0]["schneller_als_alle_gegner_lila"], "Sektor 1: Rafa war schneller");
        }

        [Test]
        public void Unchanged_timesheet_is_not_repeated_outside_the_race()
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 8; gs.SessionData.SessionType = SessionType.Practice; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.carClass = new CarData.CarClass(); gs.SessionData.ClassPosition = 2; gs.SessionData.OverallPosition = 2;
            gs.SessionData.PlayerLapTimeSessionBest = 124.813f; gs.PositionAndMotionData.CarSpeed = 50;
            gs.OpponentData["rafa"] = new OpponentData { DriverRawName = "Rafa Rivera", CarClass = gs.carClass, ClassPosition = 1, OverallPosition = 1, CurrentBestLapTime = 123.178f };
            var prefs = new AiRadioPreferences();
            foreach (var o in AiRadioPreferences.Options) prefs.Set(o.Id, 0);
            prefs.Set("ahead", 30); prefs.SetTolerance("ahead", 0, 0);
            var schedule = new AiRadioSchedule(1); schedule.Observe(gs, T, prefs);
            Func<double, RaceDataContext> at = sec => new RaceDataContext { GameState = gs, NowUtc = T.AddSeconds(sec), LastTelemetryUtc = T.AddSeconds(sec), SendDriverNames = true };
            var first = schedule.Take(at(30), prefs, false);
            Assert.IsNotNull(first);
            Assert.AreEqual("ahead", first.Topic);
            first.MarkPlaybackStarted(T.AddSeconds(30)); first.MarkPlaybackFinished(T.AddSeconds(33), false);
            Assert.IsNull(schedule.Take(at(61), prefs, false), "gleiche Zeitenliste, gleicher Satz – nicht wiederholen");
            gs.OpponentData["rafa"].CurrentBestLapTime = 122.9f;
            var changed = schedule.Take(at(95), prefs, false);
            Assert.IsNotNull(changed, "neue Bestzeit des Gegners ist eine Neuigkeit");
        }

        // ---------- LMU virtuelle Energie ----------

        private static GameStateData Lap(int completed, bool pit = false)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 11; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.CompletedLaps = completed; gs.SessionData.PreviousLapWasValid = true; gs.PitData.InPitlane = pit;
            return gs;
        }

        [Test]
        public void Virtual_energy_use_is_measured_per_clean_lap_and_a_refill_restarts()
        {
            var ve = new VirtualEnergyWatch();
            ve.Observe(Lap(3), 80f); ve.Observe(Lap(3), 78f); ve.Observe(Lap(4), 74.5f); ve.Observe(Lap(5), 69f);
            Assert.AreEqual(5.5f, ve.PerLap, .01f);
            Assert.AreEqual(69f / 5.5f, ve.LapsLeft, .01f);
            ve.Observe(Lap(5, pit: true), 100f);                   // nachgeladen
            ve.Observe(Lap(6), 99f);
            Assert.AreEqual(5.5f, ve.PerLap, .01f, "Boxenrunde zählt nicht als Verbrauch");
            ve.Observe(Lap(6), -1f);
            Assert.AreEqual(-1f, ve.Level, "ohne Energie-Limit kein Stand");
        }

        [Test]
        public void Energy_report_names_the_limiting_factor_in_three_languages()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.IsNotNull(VirtualEnergyWatch.Report(60, -1, -1, 0));
                string tight = VirtualEnergyWatch.Report(60, 6, 14, 0);
                string fuel = VirtualEnergyWatch.Report(60, 6, 7, 1);
                Assert.AreNotEqual(tight, fuel);
                StringAssert.Contains(lang == RadioLanguage.German ? "10,0" : lang == RadioLanguage.English ? "10.0" : "10,0", tight);
            }
            RadioLanguage.Current = RadioLanguage.German;
            StringAssert.Contains("Energie ist knapper als der Sprit", VirtualEnergyWatch.Report(60, 6, 14, 0));
            StringAssert.Contains("Sprit knapper als die Energie", VirtualEnergyWatch.Report(60, 6, 7, 0));
            Assert.IsNull(VirtualEnergyWatch.Report(-1, 6, 7, 0));
        }
    }
}
