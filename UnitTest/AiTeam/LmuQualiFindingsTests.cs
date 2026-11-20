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
                        StringAssert.Contains("persönlich", line, group + " " + i);
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
            foreach (var p in new[] { (0.0, 66.6f), (1.4, 53.2f), (2.0, 47.0f), (3.4, 37.5f), (4.0, 31.5f) })
                history.Add(new System.Collections.Generic.KeyValuePair<DateTime, float>(t0.AddSeconds(p.Item1), p.Item2));
            Assert.IsTrue(RaceTrafficWatch.SuddenSlowdown(history, 26.2f, t0.AddSeconds(4.6), 44f, 46.3f), "66 → 26 m/s, wo Rob 44 fährt: Dreher");
            Assert.IsFalse(RaceTrafficWatch.SuddenSlowdown(history, 31.5f, t0.AddSeconds(4.0), 44f, 47.6f), "noch im Rahmen einer Bremsung");

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
                FuelCarry.Remember(quali, 1.8f, 1);
                Assert.AreEqual(-1, FuelCarry.For(quali), "eine Runde reicht nicht");
                FuelCarry.Remember(quali, 1.8f, 3);
                var race = Quali(SessionPhase.Green, 0, 10); race.SessionData.SessionType = SessionType.Race; race.carName = quali.carName;
                race.FuelData.FuelUseActive = true; race.FuelData.FuelLeft = 42f;
                Assert.AreEqual(1.8f, FuelCarry.For(race), 1e-6);
                FuelCarry.Remember(race, 2.5f, 3);
                Assert.AreEqual(1.8f, FuelCarry.For(race), 1e-6, "Rennen überschreibt die Trainingswerte nicht");
                string answer = LocalAnswers.FuelConsumption("Wie viel Sprit verbrauche ich pro Runde?", race, new AiRaceMemory());
                StringAssert.Contains("Aus Training und Quali", answer);
                StringAssert.Contains("1,8 Liter pro Runde", answer);
                StringAssert.Contains("23 Runden", answer);
                var other = Quali(SessionPhase.Green, 0, 10); other.SessionData.TrackDefinition = new TrackDefinition("Monza", 5793); other.carName = quali.carName;
                Assert.AreEqual(-1, FuelCarry.For(other), "andere Strecke: nichts übernehmen");
            }
            finally { FuelCarry.Reset(); }
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
