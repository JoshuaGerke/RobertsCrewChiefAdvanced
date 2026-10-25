using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Automatisierte Abnahmetests für das KI-Team (Ben, Mik, Sara) nach den Abnahmekriterien,
    /// soweit sie ohne laufende Simulation prüfbar sind. Kategorie "AiTeam" für die CI.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class AiTeamTests
    {
        private static readonly DateTime now = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        [OneTimeSetUp]
        public void Init()
        {
            // wie in Program.cs – sonst schlagen die Crew-Chief-Statics (DataFiles, UserSettings) im Einzeltestlauf fehl
            if (CrewChiefV4.CrewChief.Debug == null)
            {
                CrewChiefV4.CrewChief.Debug = new CrewChiefV4.Debugging();
            }
            CrewChiefV4.DataFiles.MakeFolders();
        }

        // ---------- Routing (Kapitel 6.1) ----------

        [TestCase("Ben, warum habe ich so wenig Traktion?", TeamMember.Ben, "warum habe ich so wenig Traktion?")]
        [TestCase("Mik, wann müssen wir stoppen?", TeamMember.Mik, "wann müssen wir stoppen?")]
        [TestCase("Sara, ist rechts frei?", TeamMember.Sara, "ist rechts frei?")]
        [TestCase("Hey Mike wie sieht's mit dem Sprit aus", TeamMember.Mik, "wie sieht's mit dem Sprit aus")]
        [TestCase("Sarah habe ich noch einen neben mir", TeamMember.Sara, "habe ich noch einen neben mir")]
        public void Router_erkennt_Namensansprache(string spoken, TeamMember expected, string question)
        {
            var routed = TeamRouter.Route(spoken, null);
            Assert.AreEqual(expected, routed.Member);
            Assert.IsTrue(routed.AddressedByName);
            Assert.AreEqual(question, routed.Question);
        }

        [TestCase("Wie viel Sprit haben wir noch?", TeamMember.Mik)]
        [TestCase("Irgendwie schiebt der Wagen vorne", TeamMember.Ben)]
        [TestCase("Ist links frei?", TeamMember.Sara)]
        public void Router_ohne_Namen_nach_Thema(string spoken, TeamMember expected)
        {
            Assert.AreEqual(expected, TeamRouter.Route(spoken, null).Member);
        }

        [Test]
        public void Router_Anschlussfrage_geht_an_letzten_Gesprächspartner()
        {
            Assert.AreEqual(TeamMember.Mik, TeamRouter.Route("Und wenn wir eine Runde später kommen?", TeamMember.Mik).Member);
            Assert.AreEqual(TeamMember.Ben, TeamRouter.Route("Und dann?", null).Member);
        }

        // ---------- Sara: lokal, deterministisch, keine unbelegte Freigabe (Test 3, Kapitel 5) ----------

        [Test]
        public void Sara_meldet_Auto_rechts_und_gibt_keine_Freigabe()
        {
            var spotter = new SpotterSnapshot { SpotterRunning = true, CarsLeft = 0, CarsRight = 1, EvaluatedUtc = now.AddMilliseconds(-50) };
            Assert.AreEqual("Noch einer rechts.", SaraLocal.TryAnswer("ist rechts frei?", spotter, null, now));
            Assert.AreEqual("Links frei.", SaraLocal.TryAnswer("ist links frei?", spotter, null, now));
            Assert.AreEqual("Noch einer rechts, links frei.", SaraLocal.TryAnswer("habe ich noch einen neben mir?", spotter, null, now));
        }

        [Test]
        public void Sara_ohne_frische_Spotterdaten_niemals_frei()
        {
            var stale = new SpotterSnapshot { SpotterRunning = true, CarsLeft = 0, CarsRight = 0, EvaluatedUtc = now.AddSeconds(-2) };
            var notRunning = new SpotterSnapshot { SpotterRunning = false, EvaluatedUtc = now };
            foreach (var snapshot in new[] { stale, notRunning, SpotterSnapshot.None })
            {
                string answer = SaraLocal.TryAnswer("ist rechts frei?", snapshot, null, now);
                Assert.AreEqual(SaraLocal.NoDataAnswer, answer);
                StringAssert.DoesNotContain("Rechts frei", answer);
            }
        }

        [Test]
        public void Alte_Ueberlappung_blockiert_den_Funk_nicht_dauerhaft()
        {
            var fresh = new SpotterSnapshot { SpotterRunning = true, CarsLeft = 1, EvaluatedUtc = DateTime.UtcNow };
            var stale = new SpotterSnapshot { SpotterRunning = true, CarsLeft = 1, EvaluatedUtc = DateTime.UtcNow.AddSeconds(-5) };
            Assert.IsTrue(fresh.HasOverlap);
            Assert.IsFalse(stale.HasOverlap);
        }

        [Test]
        public void Modellantwort_als_Liste_von_Textteilen()
        {
            Assert.AreEqual("Reifen ok. Weiter so.", AiTeamService.ContentText(JArray.Parse("[{\"type\":\"text\",\"text\":\"Reifen ok.\"},{\"type\":\"text\",\"text\":\"Weiter so.\"}]")));
            Assert.AreEqual("Funk steht.", AiTeamService.ContentText(new JValue("Funk steht.")));
            Assert.IsNull(AiTeamService.ContentText(JValue.CreateNull()));
        }

        [Test]
        public void Sara_Abstand_nach_hinten_aus_Telemetrie()
        {
            var gs = new GameStateData(1);
            gs.SessionData.TimeDeltaBehind = 0.84f;
            Assert.AreEqual("Hinter dir 0,8 Sekunden.", SaraLocal.TryAnswer("wie weit ist der hinter mir weg", SpotterSnapshot.None, gs, now));
            gs.SessionData.TimeDeltaBehind = -1;
            StringAssert.Contains("liefert die Simulation gerade nicht", SaraLocal.TryAnswer("wer ist hinter mir", SpotterSnapshot.None, gs, now));
        }

        // ---------- Datenwahrheit (Test 6, Kapitel 7.5) ----------

        [Test]
        public void Ohne_Reifenverschleiss_werden_keine_Werte_erfunden()
        {
            var gs = new GameStateData(1);
            gs.TyreData.TyreWearActive = false;
            gs.TyreData.FrontLeftPercentWear = 42; // Müll, den die Simulation nicht freigegeben hat
            var result = RaceDataTools.Execute("get_tyre_status", Ctx(gs));
            Assert.IsNull(result["verschleiss_prozent"]);
            CollectionAssert.Contains(((JArray)result["nicht_verfuegbar"]).Select(t => (string)t).ToList(), "reifenverschleiss");
            CollectionAssert.Contains(((JArray)result["nicht_verfuegbar"]).Select(t => (string)t).ToList(), "reifentemperatur");
        }

        [Test]
        public void Reifenwerte_werden_mit_Einheit_geliefert_wenn_vorhanden()
        {
            var gs = new GameStateData(1);
            var t = gs.TyreData;
            t.TyreWearActive = true;
            t.FrontLeftPercentWear = 12.34f; t.FrontRightPercentWear = 11; t.RearLeftPercentWear = 8; t.RearRightPercentWear = 9;
            t.FrontLeft_LeftTemp = 85; t.FrontLeft_CenterTemp = 88; t.FrontLeft_RightTemp = 92;
            t.FrontRight_LeftTemp = 90; t.FrontRight_CenterTemp = 87; t.FrontRight_RightTemp = 84;
            t.RearLeft_LeftTemp = 80; t.RearLeft_CenterTemp = 81; t.RearLeft_RightTemp = 82;
            t.RearRight_LeftTemp = 82; t.RearRight_CenterTemp = 81; t.RearRight_RightTemp = 80;
            t.FrontLeftPressure = 180; t.FrontRightPressure = 181; t.RearLeftPressure = 175; t.RearRightPressure = 176;
            var result = RaceDataTools.Execute("get_tyre_status", Ctx(gs));
            Assert.AreEqual(12.3, (double)result["verschleiss_prozent"][0], 0.001);
            Assert.AreEqual(92, (double)result["temperatur_c"]["VL"][2], 0.001);
            Assert.AreEqual(26.11, (double)result["druck_psi"][0], 0.01);
            Assert.IsFalse((bool)result["veraltet"]);
        }

        [Test]
        public void Ohne_Simulation_sagt_jedes_Werkzeug_ehrlich_nicht_verfuegbar()
        {
            foreach (var tool in RaceDataTools.AllToolNames)
            {
                var result = RaceDataTools.Execute(tool, new RaceDataContext { GameState = null, NowUtc = now });
                Assert.IsFalse((bool)result["verfuegbar"], tool);
            }
        }

        [Test]
        public void Lagebild_enthaelt_Reifen_Sprit_Gegner_Box_fuer_Antwort_ohne_Werkzeug()
        {
            var gs = new GameStateData(1);
            gs.FuelData.FuelUseActive = true;
            gs.FuelData.FuelLeft = 42;
            gs.TyreData.FrontLeftPressure = 160; gs.TyreData.FrontRightPressure = 161; gs.TyreData.RearLeftPressure = 158; gs.TyreData.RearRightPressure = 159;
            var briefing = RaceDataTools.BuildBriefing(Ctx(gs));
            Assert.AreEqual(42, (double)briefing["sprit"]["tankinhalt_l"], 0.01);
            Assert.AreEqual(160, (double)briefing["reifen"]["druck_kpa"][0], 0.01);
            Assert.IsNotNull(briefing["gegner"]);
            Assert.IsNotNull(briefing["box"]);
            Assert.IsNull(briefing["reifen"]["quelle"]);
            Assert.IsNotNull(briefing["quelle"]);
        }

        [Test]
        public void Alte_Telemetrie_wird_als_veraltet_markiert()
        {
            var ctx = Ctx(new GameStateData(1));
            ctx.LastTelemetryUtc = now.AddSeconds(-10);
            Assert.IsTrue((bool)RaceDataTools.Execute("get_race_state", ctx)["veraltet"]);
        }

        [Test]
        public void Alle_Werkzeuge_laufen_auf_leerem_GameState_ohne_Ausnahme()
        {
            var ctx = Ctx(new GameStateData(1));
            foreach (var tool in RaceDataTools.AllToolNames)
            {
                var result = RaceDataTools.Execute(tool, ctx);
                Assert.IsNull(result["fehler"], tool + ": " + result);
            }
            Assert.AreEqual(RaceDataTools.AllToolNames.Length, RaceDataTools.BuildToolDefinitions().Count);
        }

        // ---------- Renngedächtnis / Strategiegrundlage (Test 2) ----------

        [Test]
        public void Verbrauch_pro_Runde_ohne_Boxenrunden()
        {
            var memory = new AiRaceMemory();
            float fuel = 60;
            GameStateData prev = null;
            for (int lap = 0; lap <= 4; lap++)
            {
                var gs = new GameStateData(lap + 1);
                gs.SessionData.SessionId = 7;
                gs.FuelData.FuelUseActive = true;
                gs.FuelData.FuelLeft = fuel;
                gs.SessionData.CompletedLaps = lap;
                gs.SessionData.IsNewLap = lap > 0;
                gs.SessionData.LapTimePrevious = 90 + lap;
                memory.Update(prev, gs, now.AddMinutes(lap));
                prev = gs;
                fuel -= 2.5f;
            }
            Assert.AreEqual(4, memory.GetLaps().Count);
            Assert.AreEqual(2.5, memory.AverageFuelPerLap(), 0.001);

            // Nachtanken in der Box darf den Verbrauch nicht verfälschen
            var pit = new GameStateData(10);
            pit.SessionData.SessionId = 7;
            pit.PitData.InPitlane = true;
            pit.FuelData.FuelUseActive = true;
            pit.FuelData.FuelLeft = 80;
            memory.Update(prev, pit, now.AddMinutes(5));
            var after = new GameStateData(11);
            after.SessionData.SessionId = 7;
            after.FuelData.FuelUseActive = true;
            after.FuelData.FuelLeft = 79;
            after.SessionData.CompletedLaps = 5;
            after.SessionData.IsNewLap = true;
            memory.Update(pit, after, now.AddMinutes(6));
            Assert.AreEqual(2.5, memory.AverageFuelPerLap(), 0.001);

            var ctx = Ctx(after);
            ctx.Memory = memory;
            var fuelTool = RaceDataTools.Execute("get_fuel_status", ctx);
            Assert.AreEqual(79, (double)fuelTool["tankinhalt_l"], 0.01);
            Assert.AreEqual(31.6, (double)fuelTool["reichweite_runden"], 0.01);
        }

        [Test]
        public void Sessionwechsel_leert_das_Gedächtnis()
        {
            var memory = new AiRaceMemory();
            var a = new GameStateData(1);
            a.SessionData.SessionId = 1;
            a.SessionData.CompletedLaps = 1;
            a.SessionData.IsNewLap = true;
            memory.Update(null, a, now);
            Assert.AreEqual(1, memory.GetLaps().Count);
            var b = new GameStateData(2);
            b.SessionData.SessionId = 2;
            memory.Update(a, b, now);
            Assert.AreEqual(0, memory.GetLaps().Count);
        }

        // ---------- lokale Schnellantworten (Kapitel 8.1, Test 10) ----------

        [Test]
        public void Einfache_Spritfrage_ohne_KI()
        {
            var gs = new GameStateData(1);
            gs.FuelData.FuelUseActive = true;
            gs.FuelData.FuelLeft = 34.2f;
            Assert.AreEqual("Noch 34 Liter im Tank. Den Verbrauch hab ich noch nicht sauber gemessen.", LocalAnswers.TryAnswer("wie viel Sprit haben wir noch?", gs, null));
            Assert.IsNull(LocalAnswers.TryAnswer("analysier unsere Stints und entscheide über den Undercut", gs, null));
            Assert.IsNull(LocalAnswers.TryAnswer("reicht der Sprit bis zum Ende?", gs, null));
        }

        [Test]
        public void Position_und_Rundenzeit_lokal()
        {
            var gs = new GameStateData(1);
            gs.SessionData.ClassPosition = 6;
            gs.SessionData.NumCarsInPlayerClass = 20;
            gs.SessionData.LapTimePrevious = 92.456f;
            gs.SessionData.PreviousLapWasValid = true;
            Assert.AreEqual("P6 von 20.", LocalAnswers.TryAnswer("auf welchem Platz bin ich?", gs, null));
            Assert.AreEqual("Letzte Runde 1:32,456.", LocalAnswers.TryAnswer("wie war die letzte Runde?", gs, null));
        }

        [Test]
        public void Komplexe_Fragen_nutzen_das_Analysemodell()
        {
            Assert.IsTrue(AiTeamService.NeedsDeepModel("analysiere unsere bisherigen Stints und entscheide, ob ein Undercut besser ist"));
            Assert.IsFalse(AiTeamService.NeedsDeepModel("wie sind die Reifen?"));
        }

        // ---------- Funktext-Hygiene ----------

        [Test]
        public void KI_Text_ohne_Markdown_ohne_Freigaben_und_kurz()
        {
            string raw = "**Rechts ist frei.** Deine Vorderreifen sind mit 95 Grad zu heiß. Fahr zwei Runden etwas ruhiger. Danach sehen wir weiter und reden über Setup.";
            string clean = AiPersonas.CleanForRadio(raw, 90, true);
            StringAssert.DoesNotContain("frei", clean);
            StringAssert.DoesNotContain("*", clean);
            Assert.LessOrEqual(clean.Length, 90);
            StringAssert.StartsWith("Deine Vorderreifen", clean);
        }

        [Test]
        public void Sprechername_am_Anfang_wird_entfernt()
        {
            Assert.AreEqual("Rob, Status unverändert.", AiPersonas.CleanForRadio("Ben: Ben: Rob, Status unverändert.", 200, true));
            Assert.AreEqual("Ben hat recht.", AiPersonas.CleanForRadio("Ben hat recht.", 200, true));
        }

        [Test]
        public void Begruessung_mit_zwei_Funkspruechen_wird_gelesen()
        {
            var list = AiTeamService.ParseRadioList("{\"funksprueche\":[{\"speaker\":\"ben\",\"text\":\"Servus Rob, Algarve bei 24 Grad Strecke.\"}," +
                "{\"speaker\":\"sara\",\"text\":\"Links frei\"},{\"speaker\":\"mik\",\"text\":\"Zwanzig Minuten, Sprit reicht.\"}]}", 2);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(TeamMember.Ben, list[0].Key);
            Assert.AreEqual(TeamMember.Mik, list[1].Key);
            Assert.AreEqual(0, AiTeamService.ParseRadioList("kein json", 2).Count);
        }

        [Test]
        public void Proaktive_Antwort_wird_streng_geprüft()
        {
            TeamMember m;
            string text;
            Assert.IsTrue(AiTeamService.TryParseProactive("{\"speaker\":\"mik\",\"text\":\"Der vor dir verliert seit drei Runden Zeit im letzten Sektor.\"}", out m, out text));
            Assert.AreEqual(TeamMember.Mik, m);
            Assert.IsFalse(AiTeamService.TryParseProactive("{\"speaker\":\"none\",\"text\":\"\"}", out m, out text));
            Assert.IsFalse(AiTeamService.TryParseProactive("{\"speaker\":\"sara\",\"text\":\"Links frei\"}", out m, out text));
            Assert.IsFalse(AiTeamService.TryParseProactive("kein json", out m, out text));
        }

        // ---------- Audio ----------

        [Test]
        public void Wav_Header_und_Funkfilter()
        {
            var pcm = new byte[24000 * 2];
            for (int i = 0; i < 24000; i++)
            {
                short s = (short)(Math.Sin(2 * Math.PI * 1000 * i / 24000.0) * 20000);
                pcm[i * 2] = (byte)(s & 0xFF);
                pcm[i * 2 + 1] = (byte)(s >> 8);
            }
            byte[] filtered = AiAudio.ApplyRadioEffect(pcm, 24000);
            Assert.AreEqual(pcm.Length, filtered.Length);
            byte[] wav = AiAudio.PcmToWav(filtered, 24000);
            Assert.AreEqual(44 + pcm.Length, wav.Length);
            Assert.AreEqual("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
            Assert.AreEqual(24000, BitConverter.ToInt32(wav, 24));
            Assert.AreEqual("data", Encoding.ASCII.GetString(wav, 36, 4));
            Assert.AreEqual(pcm.Length, BitConverter.ToInt32(wav, 40));
            short peak = 0;
            for (int i = 0; i < filtered.Length / 2; i++)
            {
                peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(filtered, i * 2)));
            }
            Assert.Greater(peak, 20000);  // auf ca. -1 dBFS normalisiert, aber nicht übersteuert
            Assert.Less(peak, 32000);
        }

        [Test]
        public void KI_Clip_wird_als_abspielbare_QueuedMessage_akzeptiert()
        {
            string id = AiAudioStore.Put(AiAudio.PcmToWav(new byte[4800], 24000), "Ben: Test");
            CrewChiefV4.QueuedMessage msg;
            try
            {
                msg = new CrewChiefV4.QueuedMessage("ai_team_test_" + id, 30,
                    messageFragments: new List<CrewChiefV4.MessageFragment> { CrewChiefV4.MessageFragment.Text(CrewChiefV4.Audio.SoundCache.AI_AUDIO_IDENTIFIER + id) },
                    type: CrewChiefV4.Audio.SoundType.VOICE_COMMAND_RESPONSE);
            }
            catch (TypeInitializationException e) when (e.ToString().Contains("AudioPlayer.GetDefaultOutputDeviceID"))
            {
                // GitHub-Runner ohne Soundkarte: Crew Chiefs AudioPlayer lässt sich dort nicht initialisieren
                Assert.Ignore("Kein Audioausgabegerät vorhanden: " + e.GetBaseException().Message);
                return;
            }
            Assert.IsTrue(msg.canBePlayed);
            CollectionAssert.Contains(msg.messageFolders, CrewChiefV4.Audio.SoundCache.AI_AUDIO_IDENTIFIER + id);
            byte[] wav;
            string subtitle;
            Assert.IsTrue(AiAudioStore.TryGet(id, out wav, out subtitle));
            AiAudioStore.Remove(id);
            Assert.IsFalse(AiAudioStore.TryGet(id, out wav, out subtitle));
        }

        // ---------- API-Schlüssel ----------

        [Test]
        public void Api_Schluessel_verschluesselt_speichern_und_lesen()
        {
            string path = Path.Combine(Path.GetTempPath(), "rcca_test_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                AiSecretStore.Save("sk-test-1234567890", path);
                StringAssert.DoesNotContain("sk-test", Encoding.UTF8.GetString(File.ReadAllBytes(path)));
                Assert.AreEqual("sk-test-1234567890", AiSecretStore.Load(path));
                Assert.AreEqual("sk-…7890", AiSecretStore.Mask("sk-test-1234567890"));
            }
            finally
            {
                AiSecretStore.Delete(path);
            }
        }

        // ---------- API-Client und Tool-Schleife mit simuliertem Server (Test 1, 2, 5) ----------

        [Test]
        public async Task Tool_Schleife_liefert_echte_Telemetrie_an_das_Modell()
        {
            var server = new FakeServer();
            server.Responses.Enqueue(Json(200, "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"c1\",\"type\":\"function\",\"function\":{\"name\":\"get_fuel_status\",\"arguments\":\"{}\"}}]}}],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":10}}"));
            server.Responses.Enqueue(Json(200, "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Noch 34,5 Liter. Stopp in etwa fünf Runden.\"}}],\"usage\":{\"prompt_tokens\":150,\"completion_tokens\":12}}"));
            var stats = new AiUsageStats();
            var client = new OpenAiCompatibleClient("https://example.invalid/v1", () => "sk-test", TimeSpan.FromSeconds(5), stats, server);

            var gs = new GameStateData(1);
            gs.FuelData.FuelUseActive = true;
            gs.FuelData.FuelLeft = 34.5f;
            var messages = new JArray { new JObject { ["role"] = "user", ["content"] = "Mik, wann müssen wir stoppen?" } };
            string answer = await AiTeamService.RunToolLoopAsync(client, "test-model", "none", messages, 250, 260, () => Ctx(gs), null, null, CancellationToken.None);

            Assert.AreEqual("Noch 34,5 Liter. Stopp in etwa fünf Runden.", answer);
            Assert.AreEqual(2, server.Requests.Count);
            var second = JObject.Parse(server.Requests[1]);
            var toolMessage = second["messages"].First(m => (string)m["role"] == "tool");
            Assert.AreEqual("c1", (string)toolMessage["tool_call_id"]);
            Assert.AreEqual(34.5, (double)JObject.Parse((string)toolMessage["content"])["tankinhalt_l"], 0.01);
            Assert.AreEqual("none", (string)second["reasoning_effort"]);
            Assert.AreEqual(250, stats.PromptTokens);
            Assert.AreEqual(2, stats.ChatCalls);
            StringAssert.Contains("Bearer sk-test", server.AuthHeaders[0]);
        }

        [Test]
        public async Task Unbekannter_Parameter_wird_einmal_ohne_ihn_wiederholt()
        {
            var server = new FakeServer();
            server.Responses.Enqueue(Json(400, "{\"error\":{\"message\":\"Unrecognized request argument supplied: reasoning_effort\"}}"));
            server.Responses.Enqueue(Json(200, "{\"choices\":[{\"message\":{\"content\":\"Funk steht.\"}}]}"));
            var client = new OpenAiCompatibleClient("http://localhost:1234/v1", () => null, TimeSpan.FromSeconds(5), null, server);
            var response = await client.ChatAsync("lokal", new JArray(), null, "none", 20, false, CancellationToken.None);
            Assert.AreEqual("Funk steht.", (string)response.SelectToken("choices[0].message.content"));
            Assert.IsNull(JObject.Parse(server.Requests[1])["reasoning_effort"]);
        }

        [Test]
        public void Verbindungsabbruch_liefert_saubere_Fehlermeldung()
        {
            var server = new FakeServer { Throw = new HttpRequestException("Host nicht erreichbar") };
            var client = new OpenAiCompatibleClient("https://example.invalid/v1", () => "k", TimeSpan.FromSeconds(5), null, server);
            var e = Assert.ThrowsAsync<AiApiException>(() => client.ChatAsync("m", new JArray(), null, null, 20, false, CancellationToken.None));
            StringAssert.Contains("Keine Verbindung", e.Message);
            Assert.AreEqual(1, client.Stats.Failures);
        }

        [Test]
        public async Task Sprachausgabe_und_Erkennung_Anfrageformat()
        {
            var server = new FakeServer();
            server.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 }) });
            server.Responses.Enqueue(Json(200, "{\"text\":\" Ben, wie sind die Reifen? \"}"));
            var client = new OpenAiCompatibleClient("https://example.invalid/v1", () => "k", TimeSpan.FromSeconds(5), null, server);
            byte[] pcm = await client.SpeechPcmAsync("tts-model", "cedar", "Hallo", "ruhig", CancellationToken.None);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, pcm);
            var speech = JObject.Parse(server.Requests[0]);
            Assert.AreEqual("pcm", (string)speech["response_format"]);
            Assert.AreEqual("cedar", (string)speech["voice"]);
            string text = await client.TranscribeAsync(AiAudio.PcmToWav(new byte[3200], 16000), "stt-model", "de", null, CancellationToken.None);
            Assert.AreEqual("Ben, wie sind die Reifen?", text);
            StringAssert.Contains("name=\"model\"", server.Requests[1]);
            StringAssert.Contains("filename=\"funk.wav\"", server.Requests[1]);
            StringAssert.DoesNotContain("filename*", server.Requests[1]);
            StringAssert.EndsWith("/audio/transcriptions", server.Urls[1]);
        }

        // ---------- ElevenLabs und OpenRouter ----------

        [Test]
        public async Task ElevenLabs_Anfrageformat_pcm_24000()
        {
            var server = new FakeServer();
            server.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 9, 8, 7, 6 }) });
            var stats = new AiUsageStats();
            var client = new ElevenLabsClient(() => "el-key", TimeSpan.FromSeconds(5), stats, server);
            byte[] pcm = await client.SpeechPcmAsync("voice123", "eleven_flash_v2_5", "Links frei.", TeamMember.Sara, CancellationToken.None);
            CollectionAssert.AreEqual(new byte[] { 9, 8, 7, 6 }, pcm);
            Assert.AreEqual("https://api.elevenlabs.io/v1/text-to-speech/voice123?output_format=pcm_24000", server.Urls[0]);
            Assert.AreEqual("el-key", server.XiKeys[0]);
            var body = JObject.Parse(server.Requests[0]);
            Assert.AreEqual("eleven_flash_v2_5", (string)body["model_id"]);
            Assert.AreEqual("de", (string)body["language_code"]);
            Assert.AreEqual(1, stats.TtsCalls);
        }

        [Test]
        public async Task ElevenLabs_multilingual_v2_ohne_language_code()
        {
            var server = new FakeServer();
            server.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[2]) });
            var client = new ElevenLabsClient(() => "k", TimeSpan.FromSeconds(5), null, server);
            await client.SpeechPcmAsync("v", "eleven_multilingual_v2", "Test", TeamMember.Ben, CancellationToken.None);
            Assert.IsNull(JObject.Parse(server.Requests[0])["language_code"]);
        }

        [Test]
        public void ElevenLabs_Fehler_wird_lesbar_gemeldet()
        {
            var server = new FakeServer();
            server.Responses.Enqueue(Json(401, "{\"detail\":{\"status\":\"invalid_api_key\",\"message\":\"Invalid API key\"}}"));
            var client = new ElevenLabsClient(() => "falsch", TimeSpan.FromSeconds(5), null, server);
            var e = Assert.ThrowsAsync<AiApiException>(() => client.SpeechPcmAsync("v", "eleven_flash_v2_5", "Test", TeamMember.Ben, CancellationToken.None));
            StringAssert.Contains("Invalid API key", e.Message);
        }

        [Test]
        public void ElevenLabs_Stimmenliste()
        {
            var voices = ElevenLabsClient.ParseVoices("{\"voices\":[{\"voice_id\":\"b\",\"name\":\"Zoe\",\"labels\":{\"gender\":\"female\",\"accent\":\"german\"}},{\"voice_id\":\"a\",\"name\":\"Anton\"}]}");
            Assert.AreEqual(2, voices.Count);
            Assert.AreEqual("Anton", voices[0].Name);
            Assert.AreEqual("Zoe – female, german", voices[1].ToString());
        }

        [Test]
        public async Task OpenRouter_bekommt_reasoning_Objekt_und_App_Kennung()
        {
            var server = new FakeServer();
            server.Responses.Enqueue(Json(200, "{\"choices\":[{\"message\":{\"content\":\"Funk steht.\"}}]}"));
            var client = new OpenAiCompatibleClient("https://openrouter.ai/api/v1", () => "or-key", TimeSpan.FromSeconds(5), null, server);
            await client.ChatAsync("openai/gpt-6-luna", new JArray(), null, "none", 20, false, CancellationToken.None);
            var body = JObject.Parse(server.Requests[0]);
            Assert.AreEqual("none", (string)body.SelectToken("reasoning.effort"));
            Assert.IsNull(body["reasoning_effort"]);
            Assert.AreEqual("https://openrouter.ai/api/v1/chat/completions", server.Urls[0]);
            Assert.AreEqual("Robert's Crew Chief Advanced", server.Titles[0]);
        }

        [Test]
        public async Task OpenRouter_Spracherkennung_als_JSON_mit_Base64()
        {
            var server = new FakeServer();
            server.Responses.Enqueue(Json(200, "{\"text\":\"Mik, wie viel Sprit?\"}"));
            var client = new OpenAiCompatibleClient("https://openrouter.ai/api/v1", () => "k", TimeSpan.FromSeconds(5), null, server);
            byte[] wav = AiAudio.PcmToWav(new byte[320], 16000);
            string text = await client.TranscribeAsync(wav, "openai/gpt-transcribe", "de", "Hinweis", CancellationToken.None);
            Assert.AreEqual("Mik, wie viel Sprit?", text);
            var body = JObject.Parse(server.Requests[0]);
            Assert.AreEqual("wav", (string)body.SelectToken("input_audio.format"));
            CollectionAssert.AreEqual(wav, Convert.FromBase64String((string)body.SelectToken("input_audio.data")));
            Assert.AreEqual("de", (string)body["language"]);
        }

        [Test]
        public void Zwei_Schluessel_getrennt_gespeichert()
        {
            string a = Path.Combine(Path.GetTempPath(), "rcca_a_" + Guid.NewGuid().ToString("N") + ".bin");
            string b = Path.Combine(Path.GetTempPath(), "rcca_b_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                AiSecretStore.Save("sk-or-1", a);
                AiSecretStore.Save("el-2", b);
                Assert.AreEqual("sk-or-1", AiSecretStore.Load(a));
                Assert.AreEqual("el-2", AiSecretStore.Load(b));
            }
            finally
            {
                AiSecretStore.Delete(a);
                AiSecretStore.Delete(b);
            }
        }

        // ---------- Kurven-Coaching und Sprit-Warnung ----------

        // Synthetische Runde auf einer 1000-m-Strecke mit einer Kurve von 400 bis 500 m.
        private static void DriveLap(CornerCoach coach, ref float time, int lap, float brakeAt, float apexKmh, float exitKmh, float throttleAt, float lapTime)
        {
            for (float d = 0; d < 1000; d += 5)
            {
                float kmh = 200;
                if (d >= brakeAt && d < 400) kmh = 200 - (200 - apexKmh) * (d - brakeAt) / (400 - brakeAt);
                else if (d >= 400 && d < 450) kmh = apexKmh;
                else if (d >= 450 && d <= 500) kmh = apexKmh + (exitKmh - apexKmh) * (d - 450) / 50;
                else if (d > 500 && d < 600) kmh = exitKmh + (200 - exitKmh) * (d - 500) / 100;
                float brake = d >= brakeAt && d < 400 ? 0.9f : 0f;
                float throttle = d >= throttleAt || d < brakeAt ? 1f : 0.3f;
                time += 5 / (kmh / 3.6f);
                coach.Tick(d, kmh / 3.6f, throttle, brake, time, false);
            }
            coach.CompleteLap(lap, lapTime, true);
        }

        private static CornerCoach OneCornerTrack()
        {
            var coach = new CornerCoach();
            coach.SetTrack(new[] { new CornerDefinition { Name = "Kurve 1", Start = 400, End = 500 } }, 1000);
            return coach;
        }

        [Test]
        public void Kurvenanalyse_erkennt_frueheren_Bremspunkt_und_langsameren_Scheitel()
        {
            var coach = OneCornerTrack();
            float t = 0;
            DriveLap(coach, ref t, 1, 330, 90, 120, 460, 60.0f);   // Bestrunde
            DriveLap(coach, ref t, 2, 310, 82, 112, 480, 60.6f);   // 20 m früher gebremst, langsamer
            var result = coach.Analyse();
            Assert.IsNull(result["verfuegbar"]);
            var corner = result["kurven"][0];
            Assert.AreEqual("Kurve 1", (string)corner["kurve"]);
            Assert.AreEqual(20, (double)corner["bremspunkt_m"], 6);
            Assert.AreEqual(-8, (double)corner["scheitel_kmh"], 3);
            Assert.Greater((double)corner["zeit_s"], 0.1);
            Assert.AreEqual(20, (double)corner["vollgas_spaeter_m"], 6);
        }

        [Test]
        public void Kurvenanalyse_ehrlich_ohne_Daten()
        {
            var noTrack = new CornerCoach();
            StringAssert.Contains("keine Kurvendaten", (string)noTrack.Analyse()["grund"]);
            var coach = OneCornerTrack();
            float t = 0;
            DriveLap(coach, ref t, 1, 330, 90, 120, 460, 60.0f);
            Assert.IsFalse((bool)coach.Analyse()["verfuegbar"]);
        }

        [Test]
        public void Proaktiver_Kurvenhinweis_erst_bei_wiederholtem_Zeitverlust()
        {
            var coach = OneCornerTrack();
            float t = 0;
            DriveLap(coach, ref t, 1, 330, 90, 120, 460, 60.0f);
            DriveLap(coach, ref t, 2, 300, 80, 110, 490, 60.9f);
            Assert.IsNull(coach.FindConsistentLoss(), "nach einer schlechten Runde noch kein Hinweis");
            DriveLap(coach, ref t, 3, 300, 80, 110, 490, 60.9f);
            string hint = coach.FindConsistentLoss();
            StringAssert.StartsWith("Kurve 1: in den letzten zwei Runden", hint);
            StringAssert.Contains("früher", hint);
            DriveLap(coach, ref t, 4, 300, 80, 110, 490, 60.9f);
            Assert.IsNull(coach.FindConsistentLoss(), "derselbe Hinweis höchstens alle 5 Runden");
        }

        [Test]
        public void Kurvennamen_funktauglich()
        {
            Assert.AreEqual("Kurve 6", CornerCoach.FriendlyName("turn6", 0));
            Assert.AreEqual("Kurve 12", CornerCoach.FriendlyName("turn_12", 0));
            Assert.AreEqual("La Source", CornerCoach.FriendlyName("la_source", 0));
            Assert.AreEqual("Kurve 3", CornerCoach.FriendlyName(null, 2));
        }

        [Test]
        public void Sprit_Warnung_mit_Fehlmenge_und_Sparziel()
        {
            var watch = new FuelWatch();
            // 10 Runden, 2,5 l pro Runde, 25,3 l noch da: nötig 25,75 l (inkl. 0,3 Runden Reserve)
            string text = watch.Evaluate(5, 25.3f, 2.5f, 10, true, false);
            StringAssert.StartsWith("So kommen wir bis ins Ziel nicht durch", text);
            StringAssert.Contains("Spar pro Runde", text);
            Assert.IsNull(watch.Evaluate(6, 22.8f, 2.5f, 9, true, false), "keine Wiederholung ohne Verschlechterung");
            StringAssert.Contains("passt jetzt wieder", watch.Evaluate(7, 30f, 2.5f, 8, true, false));
            Assert.IsNull(watch.Evaluate(8, 30f, 2.5f, 7, true, false));
        }

        [Test]
        public void Sprit_Warnung_empfiehlt_Stopp_bei_grosser_Luecke()
        {
            var watch = new FuelWatch();
            string text = watch.Evaluate(5, 10f, 2.5f, 10, true, true);
            StringAssert.Contains("nach aktueller Pace", text);
            StringAssert.Contains("planen einen Stopp", text);
            Assert.IsNull(new FuelWatch().Evaluate(5, 40f, 2.5f, 10, true, false), "reicht – keine Meldung");
        }

        // ---------- Hilfen ----------

        private static RaceDataContext Ctx(GameStateData gs)
        {
            return new RaceDataContext { GameState = gs, Memory = new AiRaceMemory(), NowUtc = now, LastTelemetryUtc = now.AddMilliseconds(-100), GameName = "Test" };
        }

        private static HttpResponseMessage Json(int status, string body)
        {
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        private class FakeServer : HttpMessageHandler
        {
            public readonly Queue<HttpResponseMessage> Responses = new Queue<HttpResponseMessage>();
            public readonly List<string> Requests = new List<string>();
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> AuthHeaders = new List<string>();
            public readonly List<string> XiKeys = new List<string>();
            public readonly List<string> Titles = new List<string>();
            public Exception Throw;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (Throw != null)
                {
                    throw Throw;
                }
                Urls.Add(request.RequestUri.ToString());
                AuthHeaders.Add(request.Headers.Authorization == null ? "" : request.Headers.Authorization.ToString());
                IEnumerable<string> values;
                XiKeys.Add(request.Headers.TryGetValues("xi-api-key", out values) ? values.First() : "");
                Titles.Add(request.Headers.TryGetValues("X-Title", out values) ? values.First() : "");
                Requests.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync());
                return Responses.Dequeue();
            }
        }
    }
}
