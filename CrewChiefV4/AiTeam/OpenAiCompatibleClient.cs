using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    public class AiApiException : Exception
    {
        public HttpStatusCode? Status;
        public AiApiException(string message, HttpStatusCode? status = null, Exception inner = null) : base(message, inner)
        {
            Status = status;
        }
    }

    /// <summary>Zähler für Aufrufe, Tokens, Latenzen – Anzeige im KI-Team-Fenster und Budgetgrenze.</summary>
    public class AiUsageStats
    {
        private readonly object sync = new object();
        public int ChatCalls { get; private set; }
        public int SttCalls { get; private set; }
        public int TtsCalls { get; private set; }
        public int Failures { get; private set; }
        public long PromptTokens { get; private set; }
        public long CompletionTokens { get; private set; }
        public long LastChatLatencyMs { get; private set; }
        public long LastSttLatencyMs { get; private set; }
        public long LastTtsLatencyMs { get; private set; }
        public int LocalAnswers { get; private set; }

        public void AddChat(long ms, JObject usage)
        {
            lock (sync)
            {
                ChatCalls++;
                LastChatLatencyMs = ms;
                if (usage != null)
                {
                    PromptTokens += (long?)usage["prompt_tokens"] ?? 0;
                    CompletionTokens += (long?)usage["completion_tokens"] ?? 0;
                }
            }
        }

        public void AddStt(long ms) { lock (sync) { SttCalls++; LastSttLatencyMs = ms; } }
        public void AddTts(long ms) { lock (sync) { TtsCalls++; LastTtsLatencyMs = ms; } }
        public void AddFailure() { lock (sync) { Failures++; } }
        public void AddLocalAnswer() { lock (sync) { LocalAnswers++; } }

        public void Reset()
        {
            lock (sync)
            {
                ChatCalls = SttCalls = TtsCalls = Failures = LocalAnswers = 0;
                PromptTokens = CompletionTokens = 0;
            }
        }

        public override string ToString()
        {
            lock (sync)
            {
                return string.Format("KI-Aufrufe: {0} (lokal beantwortet: {1}), Spracherkennung: {2}, Sprachausgabe: {3}, Fehler: {4}\r\n" +
                    "Tokens: {5} Eingabe / {6} Ausgabe\r\nLetzte Latenz: Erkennung {7} ms, KI {8} ms, Stimme {9} ms",
                    ChatCalls, LocalAnswers, SttCalls, TtsCalls, Failures, PromptTokens, CompletionTokens,
                    LastSttLatencyMs, LastChatLatencyMs, LastTtsLatencyMs);
            }
        }
    }

    /// <summary>
    /// Schlanker Client für OpenAI-kompatible APIs (OpenAI, Azure-kompatible Proxys, LM Studio, Ollama, …):
    /// /chat/completions mit Tool Calling, /audio/transcriptions, /audio/speech.
    /// </summary>
    public class OpenAiCompatibleClient
    {
        private readonly HttpClient http;
        private readonly Func<string> apiKeyProvider;
        private readonly string baseUrl;
        public readonly AiUsageStats Stats;

        private bool IsOpenRouter
        {
            get { return baseUrl.IndexOf("openrouter.ai", StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        public OpenAiCompatibleClient(string baseUrl, Func<string> apiKeyProvider, TimeSpan timeout, AiUsageStats stats, HttpMessageHandler handler = null)
        {
            this.baseUrl = (baseUrl ?? "").TrimEnd('/');
            this.apiKeyProvider = apiKeyProvider;
            this.Stats = stats ?? new AiUsageStats();
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            http = handler == null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = timeout;
        }

        public async Task<JObject> ChatAsync(string model, JArray messages, JArray tools, string reasoningEffort,
            int maxCompletionTokens, bool jsonMode, CancellationToken ct)
        {
            using (var diagnosticSpan = LiveDiagnostics.Begin("api", LiveDiagnostics.Data("service","chat","model",model)))
            {
                try
                {
            var body = new JObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["max_completion_tokens"] = maxCompletionTokens,
            };
            if (tools != null && tools.Count > 0)
            {
                body["tools"] = tools;
                body["tool_choice"] = "auto";
            }
            if (!string.IsNullOrWhiteSpace(reasoningEffort))
            {
                // OpenRouter erwartet {"reasoning": {"effort": …}}, OpenAI das Feld reasoning_effort
                if (IsOpenRouter)
                {
                    body["reasoning"] = new JObject { ["effort"] = reasoningEffort.Trim() };
                }
                else
                {
                    body["reasoning_effort"] = reasoningEffort.Trim();
                }
            }
            if (jsonMode)
            {
                body["response_format"] = new JObject { ["type"] = "json_object" };
            }
            var sw = Stopwatch.StartNew();
            JObject response = await PostJsonWithParameterFallbackAsync("/chat/completions", body, ct).ConfigureAwait(false);
            Stats.AddChat(sw.ElapsedMilliseconds, response["usage"] as JObject);
            return response;

                }
                catch (Exception diagnosticError) { diagnosticSpan.Fail(diagnosticError); throw; }
            }
        }

        /// <summary>
        /// Manche kompatiblen Server kennen reasoning_effort oder max_completion_tokens nicht – dann
        /// einmal ohne bzw. mit max_tokens wiederholen, statt den Fahrer ohne Antwort zu lassen.
        /// </summary>
        private async Task<JObject> PostJsonWithParameterFallbackAsync(string path, JObject body, CancellationToken ct)
        {
            try
            {
                return await PostJsonAsync(path, body, ct).ConfigureAwait(false);
            }
            catch (AiApiException e) when (e.Status == HttpStatusCode.BadRequest)
            {
                string msg = e.Message ?? "";
                bool changed = false;
                if ((body["reasoning_effort"] != null || body["reasoning"] != null) && msg.IndexOf("reasoning", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    body.Remove("reasoning_effort");
                    body.Remove("reasoning");
                    changed = true;
                }
                if (body["max_completion_tokens"] != null && msg.IndexOf("max_completion_tokens", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    body["max_tokens"] = body["max_completion_tokens"];
                    body.Remove("max_completion_tokens");
                    changed = true;
                }
                if (body["response_format"] != null && msg.IndexOf("response_format", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    body.Remove("response_format");
                    changed = true;
                }
                if (!changed)
                {
                    throw;
                }
                return await PostJsonAsync(path, body, ct).ConfigureAwait(false);
            }
        }

        public async Task<string> TranscribeAsync(byte[] wav, string model, string language, string prompt, CancellationToken ct)
        {
            using (var diagnosticSpan = LiveDiagnostics.Begin("api", LiveDiagnostics.Data("service","stt","model",model,"bytes",wav.Length)))
            {
                try
                {
            var sw = Stopwatch.StartNew();
            using (var request = NewRequest(HttpMethod.Post, "/audio/transcriptions"))
            {
                if (IsOpenRouter)
                {
                    // OpenRouter: JSON mit Base64-Audio (dokumentierter Hauptweg)
                    var body = new JObject
                    {
                        ["model"] = model,
                        ["input_audio"] = new JObject { ["data"] = Convert.ToBase64String(wav), ["format"] = "wav" },
                    };
                    if (!string.IsNullOrEmpty(language))
                    {
                        body["language"] = language;
                    }
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                }
                else
                {
                    var content = new MultipartFormDataContent();
                    var file = new ByteArrayContent(wav);
                    file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                    AddFormPart(content, file, "file", "funk.wav");
                    AddFormPart(content, new StringContent(model), "model");
                    if (!string.IsNullOrEmpty(language))
                    {
                        AddFormPart(content, new StringContent(language), "language");
                    }
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        AddFormPart(content, new StringContent(prompt), "prompt");
                    }
                    AddFormPart(content, new StringContent("json"), "response_format");
                    request.Content = content;
                }
                string json = await SendAsync(request, ct).ConfigureAwait(false);
                Stats.AddStt(sw.ElapsedMilliseconds);
                var parsed = JObject.Parse(json);
                return ((string)parsed["text"] ?? "").Trim();
            }

                }
                catch (Exception diagnosticError) { diagnosticSpan.Fail(diagnosticError); throw; }
            }
        }

        // .NET schreibt name/filename sonst ohne Anführungszeichen und mit filename*, das lehnen manche Server ab
        private static void AddFormPart(MultipartFormDataContent content, HttpContent part, string name, string fileName = null)
        {
            var disposition = new ContentDispositionHeaderValue("form-data") { Name = "\"" + name + "\"" };
            if (fileName != null)
            {
                disposition.FileName = "\"" + fileName + "\"";
            }
            part.Headers.ContentDisposition = disposition;
            content.Add(part);
        }

        /// <summary>
        /// Liefert rohes PCM: 24 kHz, 16 Bit, mono. OpenAI liefert "pcm" direkt in diesem Format. Über OpenRouter
        /// hängt die Abtastrate vom Stimmenanbieter ab, dort wird MP3 geholt und selbst umgerechnet.
        /// </summary>
        public async Task<byte[]> SpeechPcmAsync(string model, string voice, string text, string instructions, CancellationToken ct)
        {
            using (var diagnosticSpan = LiveDiagnostics.Begin("api", LiveDiagnostics.Data("service","tts","model",model)))
            {
                try
                {
            var body = new JObject
            {
                ["model"] = model,
                ["voice"] = voice,
                ["input"] = text,
                ["response_format"] = IsOpenRouter ? "mp3" : "pcm",
            };
            if (!string.IsNullOrEmpty(instructions))
            {
                body["instructions"] = instructions;
            }
            var sw = Stopwatch.StartNew();
            using (var request = NewRequest(HttpMethod.Post, "/audio/speech"))
            {
                request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Stats.AddFailure();
                        throw new AiApiException(ExtractError(err, response.StatusCode), response.StatusCode);
                    }
                    byte[] audio = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    Stats.AddTts(sw.ElapsedMilliseconds);
                    return IsOpenRouter ? AiAudio.Mp3ToPcm(audio, AiAudio.OpenAiPcmSampleRate) : audio;
                }
            }

                }
                catch (Exception diagnosticError) { diagnosticSpan.Fail(diagnosticError); throw; }
            }
        }

        private async Task<JObject> PostJsonAsync(string path, JObject body, CancellationToken ct)
        {
            for (int attempt = 0; ; attempt++)
            {
                using (var request = NewRequest(HttpMethod.Post, path))
                {
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    try
                    {
                        return JObject.Parse(await SendAsync(request, ct).ConfigureAwait(false));
                    }
                    catch (AiApiException e) when (attempt == 0 && e.Status.HasValue && ((int)e.Status.Value == 429 || (int)e.Status.Value >= 500))
                    {
                        // einmal kurz neu versuchen, mehr nicht – ein später Funkspruch ist wertlos
                        await Task.Delay(400, ct).ConfigureAwait(false);
                    }
                }
            }
        }

        private HttpRequestMessage NewRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, baseUrl + path);
            if (IsOpenRouter)
            {
                // optionale App-Kennung, damit die Aufrufe im OpenRouter-Dashboard zugeordnet sind
                request.Headers.TryAddWithoutValidation("X-Title", "Robert's Crew Chief Advanced");
                request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced");
            }
            string key = apiKeyProvider == null ? null : apiKeyProvider();
            LiveDiagnostics.RememberSecret(key);
            if (!string.IsNullOrEmpty(key))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            }
            return request;
        }

        private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException e)
            {
                Stats.AddFailure();
                throw new AiApiException(ct.IsCancellationRequested ? "abgebrochen" : "Zeitüberschreitung", null, e);
            }
            catch (HttpRequestException e)
            {
                Stats.AddFailure();
                throw new AiApiException("Keine Verbindung zur KI: " + (e.InnerException != null ? e.InnerException.Message : e.Message), null, e);
            }
            using (response)
            {
                string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Stats.AddFailure();
                    throw new AiApiException(ExtractError(text, response.StatusCode), response.StatusCode);
                }
                return text;
            }
        }

        internal static string ExtractError(string body, HttpStatusCode status)
        {
            try
            {
                var o = JObject.Parse(body);
                string msg = (string)o.SelectToken("error.message");
                if (!string.IsNullOrEmpty(msg))
                {
                    return (int)status + ": " + msg;
                }
            }
            catch (Exception) { }
            return (int)status + ": " + (body != null && body.Length > 200 ? body.Substring(0, 200) : body);
        }
    }
}
