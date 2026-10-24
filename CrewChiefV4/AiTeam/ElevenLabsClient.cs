using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            var body = new JObject
            {
                ["text"] = text,
                ["model_id"] = modelId,
                ["voice_settings"] = VoiceSettings(member),
            };
            // multilingual_v2 lehnt language_code ab, die Flash-/v3-/v4-Modelle erzwingen damit Deutsch
            if (modelId.IndexOf("multilingual_v2", StringComparison.OrdinalIgnoreCase) < 0)
            {
                body["language_code"] = "de";
            }
            var sw = Stopwatch.StartNew();
            using (var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/v1/text-to-speech/" + Uri.EscapeDataString(voiceId) + "?output_format=pcm_24000"))
            {
                request.Headers.Add("xi-api-key", apiKeyProvider());
                request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                byte[] pcm = await SendForBytesAsync(request, ct).ConfigureAwait(false);
                stats.AddTts(sw.ElapsedMilliseconds);
                return pcm;
            }
        }

        /// <summary>Stimmen im Konto des Nutzers (inklusive aus der Bibliothek hinzugefügter Stimmen).</summary>
        public async Task<List<ElevenLabsVoice>> ListVoicesAsync(CancellationToken ct)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/v1/voices"))
            {
                request.Headers.Add("xi-api-key", apiKeyProvider());
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
