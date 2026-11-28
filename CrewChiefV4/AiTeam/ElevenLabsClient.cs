using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net.WebSockets;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    public class ElevenLabsVoice
    {
        public string Id;
        public string Name;
        public string Description;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Description) ? Name : Name + " – " + Description;
        }
    }

    /// <summary>
    /// ElevenLabs-Sprachausgabe: POST /v1/text-to-speech/{voice_id}?output_format=pcm_24000, Header xi-api-key.
    /// pcm_24000 ist exakt das Format, mit dem Funkfilter und Crew-Chief-Ausgabe arbeiten.
    /// </summary>
    public class ElevenLabsClient
    {
        public const string DefaultBaseUrl = "https://api.elevenlabs.io";
        private readonly HttpClient http;
        private readonly Func<string> apiKeyProvider;
        private readonly string baseUrl;
        private readonly AiUsageStats stats;

        public ElevenLabsClient(Func<string> apiKeyProvider, TimeSpan timeout, AiUsageStats stats, HttpMessageHandler handler = null, string baseUrl = DefaultBaseUrl)
        {
            this.apiKeyProvider = apiKeyProvider;
            this.stats = stats ?? new AiUsageStats();
            this.baseUrl = baseUrl.TrimEnd('/');
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            http = handler == null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = timeout;
        }

        public bool HasKey { get { return !string.IsNullOrEmpty(apiKeyProvider()); } }

        public async Task<byte[]> SpeechPcmAsync(string voiceId, string modelId, string text, TeamMember member, CancellationToken ct)
        {
            using (var diagnosticSpan = LiveDiagnostics.Begin("api", LiveDiagnostics.Data("service","elevenlabs","model",modelId,"speaker",member.ToString())))
            {
                try
                {
            if (string.Equals(modelId, "eleven_v4_turbo", StringComparison.OrdinalIgnoreCase))
                return await DialogueSpeechPcmAsync(voiceId, modelId, text, ct).ConfigureAwait(false);

            var body = new JObject
            {
                ["text"] = text,
                ["model_id"] = modelId,
                ["voice_settings"] = VoiceSettings(member),
            };
            // multilingual_v2 lehnt language_code ab, die Flash-/v3-/v4-Modelle erzwingen damit die Funksprache
            if (modelId.IndexOf("multilingual_v2", StringComparison.OrdinalIgnoreCase) < 0)
            {
                body["language_code"] = RadioLanguage.Current;
            }
            var sw = Stopwatch.StartNew();
            using (var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/v1/text-to-speech/" + Uri.EscapeDataString(voiceId) + "?output_format=pcm_24000"))
            {
                string diagnosticKey=apiKeyProvider();
                LiveDiagnostics.RememberSecret(diagnosticKey);
                request.Headers.Add("xi-api-key",diagnosticKey);
                request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                byte[] pcm = await SendForBytesAsync(request, ct).ConfigureAwait(false);
                stats.AddTts(sw.ElapsedMilliseconds);
                return pcm;
            }

                }
                catch (Exception diagnosticError) { diagnosticSpan.Fail(diagnosticError); throw; }
            }
        }

        // v4 Turbo uses the Text to Dialogue WebSocket, with one registered voice per session.
        // Keep the existing byte[] PCM contract for the radio filter and spotter pack generator.
        private async Task<byte[]> DialogueSpeechPcmAsync(string voiceId, string modelId, string text, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            using (var socket = new ClientWebSocket())
            using (var pcm = new MemoryStream())
            {
                timeout.CancelAfter(http.Timeout);
                var token = timeout.Token;
                try
                {
                    string key = apiKeyProvider();
                    LiveDiagnostics.RememberSecret(key);
                    socket.Options.SetRequestHeader("xi-api-key", key);
                    var uri = new UriBuilder(baseUrl);
                    uri.Scheme = uri.Scheme == "https" ? "wss" : "ws";
                    uri.Path = uri.Path.TrimEnd('/') + "/v1/text-to-dialogue/stream-input";
                    uri.Query = "model_id=" + Uri.EscapeDataString(modelId) + "&output_format=pcm_24000";
                    await socket.ConnectAsync(uri.Uri, token).ConfigureAwait(false);
                    await SendDialogueMessageAsync(socket, new JObject
                    {
                        ["voices"] = new JArray(voiceId)
                    }, token).ConfigureAwait(false);
                    await SendDialogueMessageAsync(socket, new JObject
                    {
                        ["inputs"] = new JArray(new JObject
                        {
                            ["text"] = text,
                            ["voice_id"] = voiceId,
                            ["new_turn"] = false
                        })
                    }, token).ConfigureAwait(false);
                    await SendDialogueMessageAsync(socket, new JObject { ["close_socket"] = true }, token).ConfigureAwait(false);

                    var buffer = new byte[8192];
                    while (true)
                    {
                        using (var message = new MemoryStream())
                        {
                            WebSocketReceiveResult received;
                            do
                            {
                                received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                                if (received.MessageType != WebSocketMessageType.Text)
                                    throw new AiApiException("ElevenLabs: WebSocket vor Abschluss der Audioausgabe geschlossen.", null);
                                message.Write(buffer, 0, received.Count);
                            } while (!received.EndOfMessage);

                            var reply = JObject.Parse(Encoding.UTF8.GetString(message.ToArray()));
                            var error = reply["error"];
                            if (error != null && error.Type != JTokenType.Null && error.Type != JTokenType.Boolean ||
                                error != null && error.Type == JTokenType.Boolean && (bool)error)
                                throw new AiApiException("ElevenLabs: " + ((string)reply["message"] ?? error.ToString()), null);
                            string audio = (string)reply["audio"];
                            if (!string.IsNullOrEmpty(audio))
                            {
                                byte[] chunk = Convert.FromBase64String(audio);
                                pcm.Write(chunk, 0, chunk.Length);
                            }
                            if ((bool?)reply["is_final"] == true)
                                break;
                        }
                    }
                    if (pcm.Length == 0 || pcm.Length % 2 != 0)
                        throw new AiApiException("ElevenLabs: Keine gültigen PCM-Audiodaten erhalten.", null);
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", token).ConfigureAwait(false);
                    stats.AddTts(sw.ElapsedMilliseconds);
                    return pcm.ToArray();
                }
                catch (OperationCanceledException e)
                {
                    stats.AddFailure();
                    throw new AiApiException(ct.IsCancellationRequested ? "abgebrochen" : "Zeitüberschreitung", null, e);
                }
                catch (Exception)
                {
                    stats.AddFailure();
                    throw;
                }
            }
        }

        private static Task SendDialogueMessageAsync(ClientWebSocket socket, JObject message, CancellationToken ct)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message.ToString(Formatting.None));
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        /// <summary>Stimmen im Konto des Nutzers (inklusive aus der Bibliothek hinzugefügter Stimmen).</summary>
        public async Task<List<ElevenLabsVoice>> ListVoicesAsync(CancellationToken ct)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/v1/voices"))
            {
                string diagnosticKey=apiKeyProvider();
                LiveDiagnostics.RememberSecret(diagnosticKey);
                request.Headers.Add("xi-api-key",diagnosticKey);
                string json = Encoding.UTF8.GetString(await SendForBytesAsync(request, ct).ConfigureAwait(false));
                return ParseVoices(json);
            }
        }

        public static List<ElevenLabsVoice> ParseVoices(string json)
        {
            var voices = JObject.Parse(json)["voices"] as JArray ?? new JArray();
            return voices.Select(v =>
            {
                var labels = v["labels"] as JObject;
                string description = labels == null ? "" : string.Join(", ",
                    new[] { (string)labels["gender"], (string)labels["accent"], (string)labels["descriptive"] ?? (string)labels["description"], (string)labels["language"] }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                return new ElevenLabsVoice { Id = (string)v["voice_id"], Name = (string)v["name"], Description = description };
            }).Where(v => !string.IsNullOrEmpty(v.Id)).OrderBy(v => v.Name).ToList();
        }

        // Sara klar und schnell, Ben ruhig, Mik dazwischen
        private static JObject VoiceSettings(TeamMember member)
        {
            double stability = member == TeamMember.Ben ? 0.6 : member == TeamMember.Mik ? 0.5 : 0.45;
            double speed = member == TeamMember.Sara ? 1.1 : member == TeamMember.Mik ? 1.05 : 1.0;
            return new JObject
            {
                ["stability"] = stability,
                ["similarity_boost"] = 0.75,
                ["style"] = 0,
                ["use_speaker_boost"] = true,
                ["speed"] = speed,
            };
        }

        private async Task<byte[]> SendForBytesAsync(HttpRequestMessage request, CancellationToken ct)
        {
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException e)
            {
                stats.AddFailure();
                throw new AiApiException(ct.IsCancellationRequested ? "abgebrochen" : "Zeitüberschreitung", null, e);
            }
            catch (HttpRequestException e)
            {
                stats.AddFailure();
                throw new AiApiException("Keine Verbindung zu ElevenLabs: " + (e.InnerException != null ? e.InnerException.Message : e.Message), null, e);
            }
            using (response)
            {
                byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    stats.AddFailure();
                    string text = Encoding.UTF8.GetString(bytes);
                    string detail = text;
                    try
                    {
                        var o = JObject.Parse(text);
                        detail = (string)o.SelectToken("detail.message") ?? (string)o.SelectToken("detail") ?? text;
                    }
                    catch (Exception) { }
                    throw new AiApiException("ElevenLabs " + (int)response.StatusCode + ": " + (detail.Length > 200 ? detail.Substring(0, 200) : detail), response.StatusCode);
                }
                return bytes;
            }
        }
    }
}
