using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Befunde aus der LMU-Quali vom 20.11.2026 (Sebring School Circuit). Werte aus dem Log: Uhr bei 1222 von rund 3000 m
    /// abgelaufen, Ziel 42 s später, Platz 8 beim Ablauf, Platz 10 nach der Runde.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class LmuQualiFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 20, 20, 41, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { wasDeterministic = RadioVariants.Deterministic; RadioLanguage.Current = RadioLanguage.German; RadioVariants.Deterministic = true; }

        private bool wasDeterministic;

        [TearDown]
        public void Restore() { RadioLanguage.Current = RadioLanguage.German; RadioVariants.Deterministic = wasDeterministic; }

        private static GameStateData Quali(SessionPhase phase, int laps, int position, float speed = 37)
        {
            var gs = new GameStateData(T.Ticks);
            var s = gs.SessionData;
            s.SessionId = 4; s.SessionType = SessionType.Qualify; s.SessionPhase = phase; s.CompletedLaps = laps;
            s.ClassPosition = position; s.OverallPosition = position; s.CurrentLapIsValid = true; s.PlayerLapTimeSessionBest = 65.32f;
            s.TrackDefinition = new TrackDefinition("Sebring School Circuit", 3000);
            gs.PositionAndMotionData.CarSpeed = speed;
            return gs;
        }

        [Test]
        public void Quali_result_waits_for_the_running_lap_when_the_clock_expires()
        {
            var watch = new QualifyingResultWatch();
            var green = Quali(SessionPhase.Green, 6, 8);
            watch.Observe(green, T, "Rob");
            Assert.IsNull(watch.Take(green, T, false, true));

            // Uhr abgelaufen, Rob mitten in der Runde: noch kein Ergebnis.
            var expired = Quali(SessionPhase.Finished, 6, 8);
            watch.Observe(expired, T.AddSeconds(6), "Rob", green);
            Assert.IsNull(watch.Take(expired, T.AddSeconds(6), false, true), "kein „Quali beendet“ auf der laufenden Runde");
            var later = Quali(SessionPhase.Finished, 6, 10);
            watch.Observe(later, T.AddSeconds(30), "Rob", expired);
            Assert.IsNull(watch.Take(later, T.AddSeconds(30), false, true));

            // Ziellinie: nach kurzer Wertungsruhe kommt das echte Ergebnis.
            var crossed = Quali(SessionPhase.Finished, 7, 10);
            crossed.SessionData.IsNewLap = true;
            watch.Observe(crossed, T.AddSeconds(48), "Rob", later);
            Assert.IsNull(watch.Take(crossed, T.AddSeconds(48), false, true), "Wertung rechnet die Runde noch ein");
            var settled = Quali(SessionPhase.Finished, 7, 10);
            watch.Observe(settled, T.AddSeconds(51), "Rob", crossed);
            var call = watch.Take(settled, T.AddSeconds(51), false, true);
            Assert.IsNotNull(call);
            StringAssert.Contains("Platz 10", call.Text);
            StringAssert.DoesNotContain("Platz 8", call.Text);
        }

        [Test]
        public void Quali_result_comes_at_once_when_the_driver_is_in_the_pits_or_just_crossed()
        {
            var watch = new QualifyingResultWatch();
            var green = Quali(SessionPhase.Green, 6, 8);
            green.PitData.InPitlane = true;
            watch.Observe(green, T, "Rob");
            var over = Quali(SessionPhase.Finished, 6, 8, 0);
            over.PitData.InPitlane = true;
            watch.Observe(over, T.AddSeconds(5), "Rob", green);
            Assert.IsNotNull(watch.Take(over, T.AddSeconds(5), false, true), "in der Box sofort");

            var other = new QualifyingResultWatch();
            var before = Quali(SessionPhase.Green, 6, 3);
            other.Observe(before, T, "Rob");
            var finishedOnLine = Quali(SessionPhase.Finished, 7, 3);
            other.Observe(finishedOnLine, T.AddSeconds(1), "Rob", before);
            Assert.IsNotNull(other.Take(finishedOnLine, T.AddSeconds(1), false, true), "Uhr endet mit der Ziellinie (andere Sims): sofort");
        }

        [TestCase("Split Sektor 1: 23,473 Sekunden, 0,505 langsamer als Kraja.", "Split Sektor 1: dreiundzwanzig Komma vier sieben drei Sekunden, fünf Zehntel langsamer als Kraja.")]
        [TestCase("Sektor 1: plus 1,700 auf die Bestzeit.", "Sektor 1: plus eins Komma sieben auf die Bestzeit.")]
        [TestCase("Sektor 3 mit 0,989 Rückstand.", "Sektor 3 mit eins Komma null Rückstand.")]
        [TestCase("Analyse: 1,038 Verlust in Sektor 1.", "Analyse: eins Komma null Verlust in Sektor 1.")]
        [TestCase("Nur 0,045 Sekunden dahinter.", "Nur fünf Hundertstel dahinter.")]
        [TestCase("Damit 1,237 schneller als die alte Zeit.", "Damit eins Komma zwei schneller als die alte Zeit.")]
        public void Pit_wall_speaks_thousandth_gaps_in_tenths(string input, string expected)
        {
            Assert.AreEqual(expected, GermanRadioText.Prepare(input, pitWall: true));
            string exact = GermanRadioText.Prepare(input);
            StringAssert.DoesNotContain("Zehntel", exact, "ohne Kommandostand bleibt die volle Genauigkeit");
            StringAssert.DoesNotContain("Hundertstel", exact);
        }

        private static CarDamageData Crash()
        {
            // Quali 20.11., 20:35:10: Aero MAJOR (Teil abgerissen), sonst nur Stufen ohne Prozent.
            return new CarDamageData
            {
                DamageEnabled = true, OverallAeroDamage = DamageLevel.MAJOR, PartsDetached = true,
                DentSeverity = new[] { 2, 1, 0, 0, 0, 0, 1, 2 }, LastImpactMagnitude = 5200,
                WheelDetached = new bool[4], WheelFlat = new[] { false, true, false, false },
            };
        }

        [Test]
        public void Damage_report_reads_dents_parts_and_wheels_from_the_game()
        {
            string text = IncidentWatch.DamageReport(Crash());
            StringAssert.Contains("Reifen vorne rechts platt", text);
            StringAssert.Contains("Aero stark beschädigt", text);
            StringAssert.Contains("Teile sind abgerissen", text);
            StringAssert.Contains("4 von 8 Stellen verbeult, 2 davon stark", text);
            StringAssert.Contains("komm an die Box", text);
            foreach (var lang in new[] { RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                string other = IncidentWatch.DamageReport(Crash());
                Assert.IsFalse(other.Contains("verbeult") || other.Contains("platt"), lang + ": übersetzt");
                StringAssert.Contains("4 ", other, lang + ": Anzahl der Stellen");
                Assert.IsTrue(char.IsUpper(other[0]), lang + ": Satzanfang groß");
            }
            RadioLanguage.Current = RadioLanguage.German;

            var lost = Crash(); lost.WheelDetached = new[] { false, false, true, false };
            var thresholds = new System.Collections.Generic.List<CornerData.EnumWithThresholds> {
                new CornerData.EnumWithThresholds(DamageLevel.NONE, 0.0f, 1.0f), new CornerData.EnumWithThresholds(DamageLevel.DESTROYED, 1.0f, 2.0f) };
            lost.SuspensionDamageStatus = CornerData.getCornerData(thresholds, 0, 0, 1, 0);
            string wheel = IncidentWatch.DamageReport(lost);
            StringAssert.Contains("Rad hinten links ist ab", wheel);
            StringAssert.DoesNotContain("Aufhängung", wheel, "kein doppelter Hinweis aus dem abgeleiteten Aufhängungswert");
            StringAssert.Contains("Komm sofort an die Box", wheel);

            var plain = new CarDamageData { DamageEnabled = true, OverallAeroDamage = DamageLevel.MINOR };
            Assert.AreEqual("Aero leicht beschädigt.", IncidentWatch.DamageReport(plain), "andere Sims ohne Karosseriewerte unverändert");
        }

        [Test]
        public void New_dents_or_a_lost_part_count_as_more_damage()
        {
            var before = Crash(); before.PartsDetached = false; before.DentSeverity = new[] { 1, 0, 0, 0, 0, 0, 0, 0 }; before.WheelFlat = new bool[4];
            var same = Crash(); same.PartsDetached = false; same.DentSeverity = new[] { 1, 0, 0, 0, 0, 0, 0, 0 }; same.WheelFlat = new bool[4];
            Assert.IsFalse(IncidentWatch.BodyDamageIncreased(before, same));
            same.DentSeverity = new[] { 2, 1, 0, 0, 0, 0, 0, 0 };
            Assert.IsTrue(IncidentWatch.BodyDamageIncreased(before, same), "zwei Stufen mehr Dellen");
            Assert.IsTrue(IncidentWatch.BodyDamageIncreased(before, Crash()), "Teil abgerissen und Reifen platt");
            Assert.IsFalse(IncidentWatch.BodyDamageIncreased(new CarDamageData(), new CarDamageData()), "ohne Werte nichts erfinden");
        }

        [Test]
        public void Damage_tool_hands_the_model_the_body_values()
        {
            var body = RaceDataTools.Body(Crash());
            Assert.AreEqual(4, (int)body["dellen_stellen_verbeult"]);
            Assert.AreEqual(2, (int)body["dellen_stellen_stark"]);
            Assert.IsTrue((bool)body["teile_abgerissen"]);
            Assert.AreEqual("VR", (string)body["reifen_platt"][0]);
            Assert.AreEqual("schwer", (string)body["letzter_einschlag"]);
            StringAssert.Contains("nicht bestätigt", (string)body["dellen_skala"]);
            Assert.IsNull(RaceDataTools.Body(new CarDamageData()), "Sims ohne Karosseriewerte bekommen keinen leeren Block");
        }

        [Test]
        public void Own_lap_time_replaces_a_running_routine_call_but_not_an_answer()
        {
            Assert.IsTrue(CrewSpeechPolicy.CanInterruptForResult(true, false, false, 4, "quali-sector", "own-lap"));
            Assert.IsTrue(CrewSpeechPolicy.CanInterruptForResult(true, false, false, 4, "rival-best", "own-lap"));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForResult(true, false, false, 4, null, "own-lap"), "Antwort auf Robs Frage läuft zu Ende");
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForResult(true, false, false, 4, "quali-sector", "position"));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForResult(true, true, false, 4, "quali-sector", "own-lap"), "Spotter nie");
        }

        [Test]
        public void Sector_best_names_the_gain_on_the_previous_best()
        {
            // Rennen 20.11.: „Rob, in Sektor 1 hast du dich verbessert. 23,451 Sekunden.“ – um wie viel, fehlte.
            StringAssert.Contains("0,234 Sekunden schneller", SessionLapObserver.SectorGainText(.234f));
            Assert.AreEqual("zwei Zehntel schneller als dein bisher bester Sektor.",
                GermanRadioText.Prepare(SessionLapObserver.SectorGainText(.234f), pitWall: true), "gesprochen in Zehnteln");
            Assert.IsNull(SessionLapObserver.SectorGainText(0), "ohne belegten Gewinn kein Satz");
            Assert.IsNull(SessionLapObserver.SectorGainText(float.NaN));
            RadioVariants.Deterministic = false;
            try
            {
                foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
                {
                    RadioLanguage.Current = lang;
                    var lines = new System.Collections.Generic.HashSet<string>();
                    for (int i = 0; i < 60; i++) lines.Add(SessionLapObserver.SectorGainText(.5f));
                    Assert.That(lines.Count, Is.GreaterThanOrEqualTo(4), lang + ": mindestens vier Formulierungen");
                }
            }
            finally { RadioVariants.Deterministic = true; RadioLanguage.Current = RadioLanguage.German; }
        }

        [Test]
        public void Own_best_is_called_personal_best()
        {
            // Rob 20.11.: „Bestzeit habe ich nur, wenn ich die Bestzeit des ganzen Feldes gefahren bin.“
            var catalog = RadioPhraseCatalog.Current;
            foreach (var group in new[] { "lap-best", "lap-near-best", "own-best-pace", "own-sector-best", "own-sector-best-push" })
                for (int i = 0; i < catalog.Count(group); i++)
                {
                    string line = catalog.Template(group, i);
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, "Bestzeit|Bestmarke"))
                        Assert.IsTrue(line.IndexOf("persönlich", StringComparison.OrdinalIgnoreCase) >= 0, group + " " + i + ": " + line);
                }
            StringAssert.Contains("persönliche Bestzeit", SessionLapObserver.FlyingLapText("Rob", false, true, -1, 0));
            StringAssert.Contains("lila", SessionLapObserver.FlyingLapText("Rob", true, true, -1, 0));
        }

        [Test]
        public void Spin_ahead_is_recognised_from_the_sudden_slowdown()
        {
            // Rennen 20.11., Wagen 24 vor Rob (Sebring, Kurve bei 530 m; Rob fuhr dort 42–46 m/s).
            var t0 = T;
            var history = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<DateTime, float>>();
            foreach (var p in new[] { (0.0, 66.6f), (1.0, 60f), (2.0, 53.2f), (2.6, 47.0f), (3.4, 37.5f), (4.0, 31.5f), (4.6, 26.2f) })
                history.Add(new System.Collections.Generic.KeyValuePair<DateTime, float>(t0.AddSeconds(p.Item1), p.Item2));
            Assert.IsTrue(RaceTrafficWatch.SuddenSlowdown(history, 19.5f, t0.AddSeconds(5.6), 44f, 44f), "66 → 19,5 m/s, wo Rob 44 fährt: Dreher");
            Assert.IsFalse(RaceTrafficWatch.SuddenSlowdown(history, 26.2f, t0.AddSeconds(4.6), 44f, 46.3f), "noch im Rahmen einer harten Bremsung");

            var hairpin = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<DateTime, float>> {
                new System.Collections.Generic.KeyValuePair<DateTime, float>(t0, 60f), new System.Collections.Generic.KeyValuePair<DateTime, float>(t0.AddSeconds(2), 35f) };
            Assert.IsFalse(RaceTrafficWatch.SuddenSlowdown(hairpin, 24f, t0.AddSeconds(2.8), 26f, 55f), "normale Haarnadel: Rob ist dort genauso langsam");
            Assert.IsFalse(RaceTrafficWatch.SuddenSlowdown(null, 5f, t0, null, 50f));
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(5, RaceTrafficWatch.SpinPhrases.Distinct().Count(), lang);
                CollectionAssert.IsSubsetOf(RaceTrafficWatch.SpinPhrases, RaceTrafficWatch.PreparedPhrases.ToArray(), lang + ": vorbereitet, sofort abspielbar");
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Served_penalty_is_announced_and_unknown_type_is_said_honestly()
        {
            // Rennen 20.11.: 20:45:09 eine offene Strafe ohne Art, 20:46:27 in der Box auf 0 – Rob: „erkennt, wenn ich sie abgesessen habe“.
            var watch = new IncidentWatch();
            var before = Quali(SessionPhase.Green, 1, 18); before.SessionData.SessionType = SessionType.Race;
            var got = Quali(SessionPhase.Green, 1, 18); got.SessionData.SessionType = SessionType.Race; got.PenaltiesData.NumOutstandingPenalties = 1;
            var said = watch.Evaluate(before, got, T, false);
            Assert.IsTrue(said.Any(m => m.Key == TeamMember.Ben && m.Value.Contains("Strafe") && m.Value.Contains("Display")), string.Join(" | ", said.Select(m => m.Value)));

            var served = Quali(SessionPhase.Green, 2, 19); served.SessionData.SessionType = SessionType.Race; served.PitData.InPitlane = true;
            var inLane = Quali(SessionPhase.Green, 2, 19); inLane.SessionData.SessionType = SessionType.Race; inLane.PitData.InPitlane = true; inLane.PenaltiesData.NumOutstandingPenalties = 1;
            var done = watch.Evaluate(inLane, served, T.AddSeconds(78), false);
            Assert.IsTrue(done.Any(m => m.Key == TeamMember.Ben && m.Value.Contains("Strafe abgesessen")), string.Join(" | ", done.Select(m => m.Value)));
            StringAssert.Contains("Eine ist noch offen", IncidentWatch.ServedText(1));
            foreach (var lang in new[] { RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.IsFalse(IncidentWatch.ServedText(2).Contains("Strafe"), lang + ": übersetzt");
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Fuel_use_from_practice_and_quali_is_carried_into_the_race()
        {
            // Rennen 20.11.: „Für eine Schätzung brauche ich noch ein paar hundert Meter“ – obwohl Quali 1,8 l pro Runde gemessen hatte.
            try
            {
                FuelCarry.Reset();
                var quali = Quali(SessionPhase.Green, 5, 8);
                quali.carName = "Porsche 911 GT3 R";
                FuelCarry.Remember(quali, 1.6f, 1);
                // Rob 21.11.: schon eine Runde gibt eine (grobe) Schätzung – „liefer uns noch ein paar ordentliche Runden“.
                Assert.AreEqual(1.6f, FuelCarry.For(quali), 1e-6, "eine Runde gibt eine grobe Schätzung");
                FuelCarry.Remember(quali, 1.8f, 3);
                Assert.AreEqual(1.8f, FuelCarry.For(quali), 1e-6, "dieselbe Session wird ersetzt, nicht doppelt gezählt");
                var race = Quali(SessionPhase.Green, 0, 10); race.SessionData.SessionType = SessionType.Race; race.carName = quali.carName;
                race.FuelData.FuelUseActive = true; race.FuelData.FuelLeft = 42f;
                Assert.AreEqual(1.8f, FuelCarry.For(race), 1e-6);
                FuelCarry.Remember(race, 2.5f, 3);
                Assert.AreEqual(1.8f, FuelCarry.For(race), 1e-6, "Rennen überschreibt die Trainingswerte nicht");
                string answer = LocalAnswers.FuelConsumption("Wie viel Sprit verbrauche ich pro Runde?", race, new AiRaceMemory());
                StringAssert.Contains("Belastbare Rennwerte haben wir noch nicht", answer);
                StringAssert.Contains("1,80 Liter pro Runde", answer);
                StringAssert.Contains("Verlass dich aber nicht zu sehr darauf, Rob", answer);
                StringAssert.Contains("23 Runden", answer);
                race.FuelData.FuelUseActive = false;
                StringAssert.Contains("1,80 Liter", LocalAnswers.FuelConsumption("Was sind die ca. Verbräuche aus Practice und Quali?", race, new AiRaceMemory()));
                StringAssert.Contains("1,80 Liter", LocalAnswers.FuelConsumption("Wie viel Sprit verbrauche ich pro Runde?", race, new AiRaceMemory()), "auch in der Box vor dem Start ohne Tankwert");
                RadioVariants.Deterministic = false;
                var lines = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < 60; i++) lines.Add(LocalAnswers.CarriedFuelAnswer(1.8f, 3, null));
                RadioVariants.Deterministic = true;
                Assert.That(lines.Count, Is.GreaterThanOrEqualTo(5), "fünf Formulierungen");
                var other = Quali(SessionPhase.Green, 0, 10); other.SessionData.TrackDefinition = new TrackDefinition("Monza", 5793); other.carName = quali.carName;
                Assert.AreEqual(-1, FuelCarry.For(other), "andere Strecke: nichts übernehmen");
            }
            finally { FuelCarry.Reset(); }
        }

        /// <summary>
        /// Review 24.11.: „Wie viel brauche ich für 10 Runden?“ ohne Literangabe – die Crew nannte ihre eigene Schätzung aus
        /// Training und Quali „dein Wert“. Die Herkunft muss stimmen, Robs eigene Zahl bleibt „dein Wert“.
        /// </summary>
        [Test]
        public void Fuel_plan_names_the_true_source_of_the_consumption()
        {
            try
            {
                FuelCarry.Reset();
                var quali = Quali(SessionPhase.Green, 5, 8); quali.carName = "Porsche 911 GT3 R";
                FuelCarry.Remember(quali, 1.8f, 3);
                var race = Quali(SessionPhase.Green, 0, 10); race.SessionData.SessionType = SessionType.Race; race.carName = quali.carName;
                race.FuelData.FuelUseActive = true; race.FuelData.FuelLeft = 42f;
                string answer = RaceFuelPlan.Answer("Wie viel Sprit brauche ich für 10 Runden?", race, new AiRaceMemory());
                StringAssert.Contains("1,8 Litern (geschätzt aus Training und Quali)", answer);
                StringAssert.DoesNotContain("dein Wert", answer);
                answer = RaceFuelPlan.Answer("Wie viel Sprit brauche ich für 10 Runden mit 2,5 Litern?", race, new AiRaceMemory());
                StringAssert.Contains("2,5 Litern (dein Wert)", answer);
                Assert.AreEqual("gemessen", RaceFuelPlan.BasisText(FuelEstimate.Basis.Measured));
                StringAssert.Contains("hochgerechnet", RaceFuelPlan.BasisText(FuelEstimate.Basis.Projected));
                foreach (var lang in new[] { RadioLanguage.English, RadioLanguage.Spanish })
                {
                    RadioLanguage.Current = lang;
                    foreach (var b in new[] { FuelEstimate.Basis.Measured, FuelEstimate.Basis.Projected, FuelEstimate.Basis.Carried })
                        Assert.IsFalse(RaceFuelPlan.BasisText(b).Contains("Runde") || RaceFuelPlan.BasisText(b).Contains("geschätzt"), lang + ": übersetzt");
                }
            }
            finally { FuelCarry.Reset(); RadioLanguage.Current = RadioLanguage.German; }
        }

        [Test]
        public void Fuel_use_from_practice_and_quali_is_combined_weighted_by_laps()
        {
            // Rob 21.11.: „wie hoch der Durchschnittsverbrauch war in der Practice und Quali zusammengerechnet“
            try
            {
                FuelCarry.Reset();
                var practice = Quali(SessionPhase.Green, 4, 8); practice.SessionData.SessionType = SessionType.Practice; practice.SessionData.SessionId = 1;
                practice.carName = "BMW M4 GT3";
                FuelCarry.Remember(practice, 3.0f, 4);
                var quali = Quali(SessionPhase.Green, 2, 8); quali.SessionData.SessionId = 2; quali.carName = practice.carName;
                FuelCarry.Remember(quali, 3.6f, 2);
                Assert.AreEqual(3.2f, FuelCarry.For(quali), 1e-5, "(3,0×4 + 3,6×2) / 6");
                Assert.AreEqual(6, FuelCarry.Laps(quali));
                var race = Quali(SessionPhase.Green, 0, 10); race.SessionData.SessionType = SessionType.Race; race.SessionData.SessionId = 3; race.carName = practice.carName;
                race.FuelData.FuelUseActive = false;
                string answer = LocalAnswers.FuelConsumption("Wie hoch war der Verbrauch in Practice und Quali zusammen?", race, new AiRaceMemory());
                StringAssert.Contains("Training 3,00 über 4 Runden, Quali 3,60 über 2 Runden.", answer);
                StringAssert.Contains("3,20 Liter", answer);
                foreach (var lang in new[] { RadioLanguage.English, RadioLanguage.Spanish })
                {
                    RadioLanguage.Current = lang;
                    Assert.IsTrue(char.IsUpper(LocalAnswers.SessionSplit(race)[0]), lang);
                }
            }
            finally { FuelCarry.Reset(); RadioLanguage.Current = RadioLanguage.German; }
        }

        [Test]
        public void Crew_chief_race_control_detections_become_crew_calls()
        {
            // Rennen 20.11.: 16× „sectorFlagChange“, „penalty_two_laps_left“, „you_have_a_penalty“ – alles still verworfen.
            Assert.AreEqual("yellow:2", NativeRaceControl.Classify(new[] { "flags/yellow_flag_sector_2" }));
            Assert.AreEqual("dyellow:1", NativeRaceControl.Classify(new[] { "flags\\double_yellow_flag_sector_1" }));
            Assert.AreEqual("green:3", NativeRaceControl.Classify(new[] { "flags/green_flag_sector_3" }));
            Assert.AreEqual("pen-laps:2", NativeRaceControl.Classify(new[] { "penalties/penalty_two_laps_left" }));
            Assert.AreEqual("pen-laps:1", NativeRaceControl.Classify(new[] { "penalties/penalty_one_lap_left_drivethrough" }));
            Assert.AreEqual("pen-type:dt:cut", NativeRaceControl.Classify(new[] { "penalties/drive_through_cutting_track" }));
            Assert.AreEqual("pen-type:sg:speeding", NativeRaceControl.Classify(new[] { "penalties/stop_go_penalty_speeding_in_pit_lane" }));
            Assert.IsNull(NativeRaceControl.Classify(new[] { "lap_times/personal_best" }), "Rundenzeiten bleiben bei der eigenen Logik");
            Assert.IsNull(NativeRaceControl.Classify(new[] { "penalties/you_have_a_penalty" }), "Anzahl offener Strafen meldet IncidentWatch");

            StringAssert.Contains("Sektor 2", NativeRaceControl.Text("yellow:2"));
            StringAssert.Contains("Durchfahrtsstrafe wegen Abkürzen", NativeRaceControl.Text("pen-type:dt:cut"));
            StringAssert.Contains("zwei Runden", NativeRaceControl.Text("pen-laps:2"));
            StringAssert.Contains("Diese Runde rein", NativeRaceControl.Text("pen-laps:1"));
            Assert.AreEqual(TeamMember.Sara, NativeRaceControl.Member("yellow:2"));
            Assert.AreEqual(TeamMember.Ben, NativeRaceControl.Member("pen-laps:2"));
            Assert.AreNotEqual(NativeRaceControl.Topic("yellow:1"), NativeRaceControl.Topic("yellow:2"), "Gelb in zwei Sektoren sind zwei Meldungen");
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                foreach (var kind in new[] { "yellow:0", "yellow:2", "dyellow:2", "green:2", "pen-laps:3", "pen-pitnow", "pen-type:dt:cut", "pen-blue" })
                    Assert.That(NativeRaceControl.Options(kind).Distinct().Count(), Is.GreaterThanOrEqualTo(4), lang + " " + kind);
            }
            RadioLanguage.Current = RadioLanguage.German;

            var gs = Quali(SessionPhase.Green, 3, 12); gs.SessionData.SessionType = SessionType.Race;
            gs.FlagData.sectorFlags[1] = FlagEnum.YELLOW;
            Assert.IsTrue(NativeRaceControl.StillCurrent("yellow:2", gs));
            Assert.IsFalse(NativeRaceControl.StillCurrent("green:2", gs), "Grün-Meldung veraltet, Sektor wieder gelb");
            gs.FlagData.isLocalYellow = true;
            Assert.IsFalse(NativeRaceControl.StillCurrent("yellow:0", gs), "lokales Gelb sagt Sara schon");
            gs.PenaltiesData.NumOutstandingPenalties = 0;
            Assert.IsFalse(NativeRaceControl.StillCurrent("pen-laps:2", gs), "Strafe erledigt: keine Erinnerung mehr");
        }

        [Test]
        public void Lmu_race_control_stream_is_read_and_own_penalties_are_classified()
        {
            Assert.AreEqual(2184, LMUNativeLayout.StreamSizeOffset);
            Assert.AreEqual(62928, LMUNativeLayout.StreamOffset);
            Assert.AreEqual(LMUNativeLayout.TelemetryOffset, LMUNativeLayout.Align(LMUNativeLayout.StreamOffset + LMUNativeLayout.StreamSize, 8));
            var b = new byte[LMUNativeLayout.TotalSize];
            int vs = LMUNativeLayout.ScoringVehiclesOffset;
            BitConverter.GetBytes(2).CopyTo(b, LMUNativeLayout.ScoringCountOffset);
            BitConverter.GetBytes(7).CopyTo(b, vs + System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2VehicleScoring), "mID").ToInt32());
            System.Text.Encoding.UTF8.GetBytes("Rob Gerke").CopyTo(b, vs + System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2VehicleScoring), "mDriverName").ToInt32());
            b[vs + System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2VehicleScoring), "mIsPlayer").ToInt32()] = 1;
            string stream = "<Penalty et=\"1205.3\">Rob Gerke received a Drive Thru penalty for Track Limits</Penalty>\n<Incident et=\"1210.0\">Val Bogataj(4) reported contact (0.41) with another vehicle Sammy Caban(9)</Incident>\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(stream);
            bytes.CopyTo(b, LMUNativeLayout.StreamOffset);
            BitConverter.GetBytes((long)bytes.Length).CopyTo(b, LMUNativeLayout.StreamSizeOffset);
            BitConverter.GetBytes(1210.2).CopyTo(b, LMUNativeLayout.ScoringOffset + System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2ScoringInfo), "mCurrentET").ToInt32());
            try
            {
                LMURaceControlStream.Reset();
                Assert.AreEqual(2, LMURaceControlStream.Observe(b, T));
                Assert.AreEqual(0, LMURaceControlStream.Observe(b, T), "gleiches Wertungsbild: nicht doppelt");
                Assert.IsTrue(LMURaceControlStream.TryTake(out var penalty));
                Assert.AreEqual("Penalty", penalty.Tag);
                Assert.AreEqual("pen-type:dt:cut", LMURaceControlStream.Classify(penalty));
                StringAssert.Contains("Durchfahrtsstrafe wegen Abkürzen", NativeRaceControl.Text(LMURaceControlStream.Classify(penalty)));
                Assert.IsTrue(LMURaceControlStream.TryTake(out var other));
                Assert.IsNull(LMURaceControlStream.Classify(other), "Vorfall zweier anderer Fahrer: nur Protokoll");
                StringAssert.DoesNotContain("<", LMURaceControlStream.Plain(other.Text));
                Assert.AreEqual("pen-served", LMURaceControlStream.Classify(new LMURaceControlStream.Line { Text = "Rob Gerke served penalty", Player = "Rob Gerke" }));
                Assert.AreEqual("investigation", LMURaceControlStream.Classify(new LMURaceControlStream.Line { Text = "Incident under investigation: Rob Gerke", Player = "Rob Gerke" }));
                Assert.AreEqual("limits", LMURaceControlStream.Classify(new LMURaceControlStream.Line { Text = "<TrackLimits et=\"1.0\" id=\"7\" points=\"1.0\"/>", Tag = "TrackLimits", PlayerId = 7 }));
            }
            finally { LMURaceControlStream.Reset(); }
        }

        private static int TelOff(string f) => System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2VehicleTelemetry), f).ToInt32();
        private static int InfoOff(string f) => System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2ScoringInfo), f).ToInt32();
        private static int ScoOff(string f) => System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2VehicleScoring), f).ToInt32();

        [Test]
        public void Lmu_native_snapshot_reads_lap_invalidation_track_limits_and_field()
        {
            var b = new byte[LMUNativeLayout.TotalSize];
            b[LMUNativeLayout.PlayerHasVehicleOffset] = 1; b[LMUNativeLayout.ActiveVehiclesOffset] = 2; b[LMUNativeLayout.PlayerIndexOffset] = 1;
            int v = LMUNativeLayout.TelemetryVehiclesOffset + LMUNativeLayout.VehicleTelemetrySize;
            b[v + TelOff("mLapInvalidated")] = 0; b[v + TelOff("mTrackLimitsSteps")] = 6; b[v + TelOff("mTC")] = 3; b[v + TelOff("mTCMax")] = 9;
            BitConverter.GetBytes(1.5f).CopyTo(b, v + TelOff("mTimeGapCarAhead"));
            b[LMUNativeLayout.ScoringOffset + InfoOff("mTrackLimitsStepsPerPoint")] = 2; b[LMUNativeLayout.ScoringOffset + InfoOff("mTrackLimitsStepsPerPenalty")] = 8;
            BitConverter.GetBytes(2).CopyTo(b, LMUNativeLayout.ScoringCountOffset);
            for (int i = 0; i < 2; i++)
            {
                int o = LMUNativeLayout.ScoringVehiclesOffset + i * LMUNativeLayout.VehicleScoringSize;
                b[o + ScoOff("mPlace")] = (byte)(i == 0 ? 12 : 13);
                System.Text.Encoding.UTF8.GetBytes(i == 0 ? "Sean Talania" : "Rob Gerke").CopyTo(b, o + ScoOff("mDriverName"));
                b[o + ScoOff("mIsPlayer")] = (byte)(i == 1 ? 1 : 0);
                BitConverter.GetBytes((short)(i == 0 ? 14 : 13)).CopyTo(b, o + ScoOff("mTotalLaps"));
                BitConverter.GetBytes((short)(i == 1 ? 1 : 0)).CopyTo(b, o + ScoOff("mNumPenalties"));
            }
            var s = LMUNativeSnapshot.Parse(b, T);
            Assert.IsFalse(s.LapInvalidated, "Rennen 20.11.: Runde nicht gestrichen, obwohl Crew Chief „ungültig“ meldete");
            Assert.AreEqual(3, s.LimitPoints, 1e-9);
            Assert.AreEqual(4, s.PointsPerPenalty, 1e-9);
            Assert.AreEqual("Rob Gerke", s.PlayerCar.Name);
            var json = s.ToJson(T, new[] { "Rob Gerke received a Drive Thru penalty" });
            Assert.AreEqual(1, (int)json["offene_strafen"]);
            Assert.AreEqual(1.5, (double)json["abstand_auto_vorn_s"], 1e-6);
            Assert.AreEqual("Sean Talania", (string)json["feld"][0]["fahrer"]);
            Assert.AreEqual(14, (int)json["feld"][0]["runden"]);
            Assert.AreEqual(1, json["rennleitung_zuletzt"].Count());

            string text = LMUTrackLimitPoints.Text(s.LimitPoints, s.PointsPerPenalty, s.StepsPerPoint, s.StepsPerPenalty, s.TrackLimitsSteps);
            StringAssert.Contains("3 von 4", text);
            StringAssert.Contains("Beim nächsten gibt es die Strafe", text);
            var watch = new LMUTrackLimitPoints();
            var gs = Quali(SessionPhase.Green, 3, 13);
            Assert.IsNull(watch.Observe(s, gs), "erstes Bild: nur merken");
            b[v + TelOff("mTrackLimitsSteps")] = 6;
            Assert.IsNull(watch.Observe(LMUNativeSnapshot.Parse(b, T.AddSeconds(1)), gs), "unverändert");
            b[v + TelOff("mTrackLimitsSteps")] = 7;
            StringAssert.Contains("3,5 von 4", watch.Observe(LMUNativeSnapshot.Parse(b, T.AddSeconds(2)), gs));
        }

        [Test]
        public void Own_laps_are_graded_from_very_good_to_bad_with_varied_words()
        {
            System.Collections.Generic.List<LapRecord> Laps(params float[] t) => t.Select((x, k) => new LapRecord { LapNumber = k + 1, LapTime = x, Valid = true }).ToList();
            float d;
            Assert.AreEqual(LapGrade.Grade.VeryGood, LapGrade.Of(Laps(66.8f, 65.2f, 65.3f), out d));
            Assert.AreEqual(LapGrade.Grade.Good, LapGrade.Of(Laps(66.8f, 65.2f, 65.6f), out d));
            Assert.AreEqual(LapGrade.Grade.Medium, LapGrade.Of(Laps(66.8f, 65.2f, 66.1f), out d));
            Assert.AreEqual(LapGrade.Grade.Weak, LapGrade.Of(Laps(66.8f, 65.2f, 67.4f), out d));
            Assert.AreEqual(LapGrade.Grade.Bad, LapGrade.Of(Laps(66.8f, 65.2f, 70.0f), out d));
            Assert.AreEqual(4.8f, d, .01f);
            Assert.IsNull(LapGrade.Of(Laps(66.8f, 65.2f, 65.0f), out d), "neue persönliche Bestzeit sagt die Rundenzeit-Ansage");
            var pit = Laps(66.8f, 65.2f, 90f); pit[2].InPitLap = true;
            Assert.IsNull(LapGrade.Of(pit, out d), "Boxenrunde wird nicht benotet");
            var invalid = Laps(66.8f, 65.2f, 66f); invalid[2].Valid = false;
            Assert.IsNull(LapGrade.Of(invalid, out d));
            RadioVariants.Deterministic = false;
            try
            {
                foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
                {
                    RadioLanguage.Current = lang;
                    foreach (LapGrade.Grade g in Enum.GetValues(typeof(LapGrade.Grade)))
                    {
                        var lines = new System.Collections.Generic.HashSet<string>();
                        for (int i = 0; i < 80; i++) lines.Add(LapGrade.Text(g, .8f, null));
                        Assert.AreEqual(5, lines.Count, lang + " " + g);
                    }
                }
            }
            finally { RadioVariants.Deterministic = true; RadioLanguage.Current = RadioLanguage.German; }
            StringAssert.Contains("acht Zehntel", GermanRadioText.Prepare(LapGrade.Text(LapGrade.Grade.Medium, .8f, null), true));
        }

        [Test]
        public void Incident_ahead_names_the_slow_car_in_front()
        {
            // RaceRoom 20.11.: „Unfall voraus!“ – „Warum habt ihr mir nicht gesagt, wer da steht?“
            var gs = Quali(SessionPhase.Green, 3, 10); gs.SessionData.SessionType = SessionType.Race;
            gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            gs.OpponentData["stopped"] = new OpponentData { DriverRawName = "AC Correa", IsActive = true, Speed = 0.5f, DistanceRoundTrack = 1180, CanUseName = true };
            gs.OpponentData["behind"] = new OpponentData { DriverRawName = "Andre Antunes", IsActive = true, Speed = 0f, DistanceRoundTrack = 900, CanUseName = true };
            gs.OpponentData["fast"] = new OpponentData { DriverRawName = "Morgan Henderson", IsActive = true, Speed = 50f, DistanceRoundTrack = 1100, CanUseName = true };
            StringAssert.Contains("Correa", FlagWatch.IncidentDriver(gs));
            gs.OpponentData["stopped"].DistanceRoundTrack = 1500;
            Assert.IsNull(FlagWatch.IncidentDriver(gs), "weiter als 300 m: nicht raten");
        }

        [Test]
        public void Address_keeps_the_sentence_after_the_name_lower_case()
        {
            // Quali 20.11.: „Rob, Letzte Chance. Sauber durchziehen.“
            Assert.AreEqual("Rob, letzte Chance. Sauber durchziehen.", RadioPhraseCatalog.Address("Letzte Chance. Sauber durchziehen.", "Rob"));
            Assert.AreEqual("Rob, last chance. Make it clean.", RadioPhraseCatalog.Address("Last chance. Make it clean.", "Rob"));
            Assert.AreEqual("Rob, última oportunidad. Hazla limpia.", RadioPhraseCatalog.Address("Última oportunidad. Hazla limpia.", "Rob"));
            Assert.AreEqual("Rob, Bremspunkte treffen.", RadioPhraseCatalog.Address("Bremspunkte treffen.", "Rob"), "Substantive bleiben groß");
        }

        [Test]
        public void Quali_result_hold_gives_up_after_pit_entry_or_long_wait()
        {
            var watch = new QualifyingResultWatch();
            var green = Quali(SessionPhase.Green, 6, 8);
            watch.Observe(green, T, "Rob");
            var expired = Quali(SessionPhase.Finished, 6, 8);
            watch.Observe(expired, T.AddSeconds(1), "Rob", green);
            Assert.IsNull(watch.Take(expired, T.AddSeconds(1), false, true));
            var limit = QualifyingResultWatch.FinishHoldLimit(expired.SessionData);
            Assert.That(limit.TotalSeconds, Is.EqualTo(65.32 * 1.5).Within(0.1));
            var stillOut = Quali(SessionPhase.Finished, 6, 8);
            watch.Observe(stillOut, T.AddSeconds(1) + limit + TimeSpan.FromSeconds(1), "Rob", expired);
            Assert.IsNotNull(watch.Take(stillOut, T.AddSeconds(1) + limit + TimeSpan.FromSeconds(1), false, true), "Schutz gegen ewiges Warten");

            var pit = new QualifyingResultWatch();
            pit.Observe(green, T, "Rob");
            pit.Observe(expired, T.AddSeconds(1), "Rob", green);
            var inLane = Quali(SessionPhase.Finished, 6, 8, 15);
            inLane.PitData.InPitlane = true;
            pit.Observe(inLane, T.AddSeconds(20), "Rob", expired);
            Assert.IsNotNull(pit.Take(inLane, T.AddSeconds(20), false, true), "Abbruch in die Box: Ergebnis sofort");
        }
    }
}
