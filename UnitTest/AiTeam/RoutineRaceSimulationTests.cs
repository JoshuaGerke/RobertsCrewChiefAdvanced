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
    /// Rob 23.11.: „Wird das endlich wieder zuverlässig gehen?“ Ein ganzes Rennen mit 14 Autos, Verkehr und zwei Zweikämpfen,
    /// genau mit Robs Cockpit-Einstellungen (zehn Berichte, je −7/+8 s). Geprüft wird über Funkplan und Rennkopf, ob jeder
    /// Bericht im eingestellten Takt kommt.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RoutineRaceSimulationTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 19, 0, 0, DateTimeKind.Utc);
        private static readonly Dictionary<string, int> Rob = new Dictionary<string, int>
        {
            ["status"] = 120, ["weather"] = 300, ["fuel"] = 120, ["tyres"] = 240, ["pace"] = 180,
            ["ahead"] = 120, ["behind"] = 120, ["stint"] = 300, ["vehicle"] = 300, ["progress"] = 180,
        };
        private const double Lap = 139.0;
        private const int RaceSeconds = 2400;

        private static bool Battle(double t) => t >= 600 && t < 900 || t >= 1500 && t < 1650;

        private static GameStateData Frame(double t, CarData.CarClass cls)
        {
            int completed = 1 + (int)(t / Lap);
            var gs = new GameStateData(T.AddSeconds(t).Ticks);
            var s = gs.SessionData;
            s.SessionId = 23; s.SessionType = SessionType.Race; s.SessionPhase = SessionPhase.Green;
            s.SessionNumberOfLaps = 30; s.CompletedLaps = completed; s.SessionLapsRemaining = 30 - completed;
            s.ClassPosition = 3; s.OverallPosition = 3; s.NumCarsOverall = 14; s.NumCarsInPlayerClass = 14;
            s.LapTimePrevious = (float)(Lap + (completed % 3) * .2); s.PreviousLapWasValid = true; s.CurrentLapIsValid = true;
            s.TrackDefinition = new TrackDefinition("Spa", 7004f);
            s.IsNewLap = t > 1 && (t % Lap) < 1;
            gs.SourceTelemetryUtc = T.AddSeconds(t);
            gs.carClass = cls;
            gs.PositionAndMotionData.CarSpeed = 60; gs.PositionAndMotionData.DistanceRoundTrack = (float)((t % Lap) / Lap * 7004);
            gs.FuelData.FuelUseActive = true; gs.FuelData.FuelLeft = (float)(80 - t / Lap * 2.5);
            var w = gs.TyreData; w.TyreWearActive = true;
            w.FrontLeftPercentWear = w.FrontRightPercentWear = w.RearLeftPercentWear = w.RearRightPercentWear = (float)(t / Lap * 1.5);
            gs.CarDamageData.DamageEnabled = true;
            gs.Conditions.UpdateLiveSample(T.AddSeconds(t), completed, 1, 18, 24, 0, 0, 0, 0, 0, false, CrewChiefV4.Events.ConditionsMonitor.TrackStatus.UNKNOWN);
            for (int p = 1; p <= 14; p++)
            {
                if (p == 3) continue;
                string key = "car" + p;
                gs.OpponentData[key] = new OpponentData { DriverRawName = "Fahrer " + p, ClassPosition = p, OverallPosition = p, CarClass = cls,
                    CompletedLaps = completed, DistanceRoundTrack = (float)(((t % Lap) / Lap * 7004 + (3 - p) * 90 + 7004) % 7004), Speed = 60 };
            }
            s.OpponentKeyInFront = "car2"; s.OpponentKeyBehind = "car4";
            s.TimeDeltaFront = Battle(t) ? 0.6f : 1.8f; s.TimeDeltaBehind = 2.1f;
            return gs;
        }

        [Test]
        public void Every_configured_report_comes_in_its_window_over_a_whole_race()
        {
            var prefs = new AiRadioPreferences();
            foreach (var o in AiRadioPreferences.Options) { prefs.Set(o.Id, Rob.TryGetValue(o.Id, out int s) ? s : 0); prefs.SetTolerance(o.Id, 7, 8); }
            var cls = new CarData.CarClass();
            var schedule = new AiRadioSchedule(7);
            var memory = new AiRaceMemory();
            var spoken = Rob.Keys.ToDictionary(k => k, k => new List<double>());
            GameStateData previous = null;
            double busyUntil = -1;
            for (int i = 0; i <= RaceSeconds * 2; i++)
            {
                double t = i * .5;
                var now = T.AddSeconds(t);
                var gs = Frame(t, cls);
                memory.Update(previous, gs, now);
                previous = gs;
                schedule.Observe(gs, now, prefs);
                if (t < busyUntil) continue;
                var ctx = new RaceDataContext { GameState = gs, NowUtc = now, LastTelemetryUtc = now, Memory = memory };
                var call = schedule.Take(ctx, prefs, false, null, false);
                if (call == null) continue;
                var situation = new RaceSituation { Utc = now, SessionId = 23, Phase = RacePhase.Middle,
                    Attention = Battle(t) ? RaceAttention.Battle : RaceAttention.Traffic, Focus = RaceFocus.Rhythm };
                if (RaceMind.Judge(situation, call, now, out _) != MindVerdict.Admit) { call.ReleaseForRetry(now, "held"); continue; }
                call.RefreshBeforeSpeak(ctx);
                var bundled = schedule.BundledTopics.ToArray();
                call.MarkPlaybackStarted(now); call.MarkPlaybackFinished(now.AddSeconds(5), false);
                memory.AddRadio(call.Member, call.Text, now);
                busyUntil = t + 5;
                spoken[call.Topic].Add(t);
                foreach (var b in bundled) spoken[b].Add(t);
            }
            var report = string.Join("\n", Rob.Keys.Select(k => k + ": " + spoken[k].Count + "x, größte Lücke " +
                (spoken[k].Count > 1 ? spoken[k].Zip(spoken[k].Skip(1), (a, b) => b - a).Max().ToString("0") : "-") + " s"));
            TestContext.WriteLine(report);
            foreach (var topic in Rob.Keys)
            {
                var times = spoken[topic];
                int interval = Rob[topic];
                // Erste Meldung spätestens nach Intervall + 8 s + Zweikampf + eine Funkpause; danach nie mehr als ein Fenster Lücke.
                double allowed = interval + 8 + 8 + 25;
                Assert.IsNotEmpty(times, topic + " kam nie\n" + report);
                Assert.LessOrEqual(times[0], allowed, topic + " kam zu spät zum ersten Mal\n" + report);
                for (int k = 1; k < times.Count; k++)
                    Assert.LessOrEqual(times[k] - times[k - 1], allowed, topic + " Lücke bei " + times[k - 1] + " s\n" + report);
                Assert.GreaterOrEqual(times.Count, (int)(RaceSeconds / allowed), topic + " zu selten\n" + report);
            }
        }
    }
}
