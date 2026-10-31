using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Erzeugt Saras deutsches Spotterpack im Crew-Chief-Format (voice/spotter_Sara/&lt;ansage&gt;/1.wav … + subtitles.csv).
    /// Crew Chief bietet es danach als Spotterstimme "Sara" an und spielt die Ansagen wie jedes Soundpack:
    /// lokal, sofort, ohne Internet. Erzeugt wird nur einmal (bzw. nach Stimmwechsel) mit der Sara-Stimme.
    /// </summary>
    public static class SaraSpotterPack
    {
        public const string SpotterName = "Sara";
        public const string MarkerFile = "rcca_sara_pack.txt";
        public const string ManifestFile = "rcca_sara_manifest.json";

        /// <summary>Ordner relativ zu voice/ und die Varianten pro Ansage. Kurz, damit die Warnung sofort verständlich ist.</summary>
        public static readonly Dictionary<string, string[]> Phrases = new Dictionary<string, string[]>
        {
            { "spotter_Sara/car_left", new[] { "Links!", "Auto links.", "Links neben dir.", "Links." } },
            { "spotter_Sara/car_right", new[] { "Rechts!", "Auto rechts.", "Rechts neben dir.", "Rechts." } },
            { "spotter_Sara/still_there", new[] { "Noch da.", "Ist noch da.", "Linie halten.", "Noch neben dir." } },
            { "spotter_Sara/clear_left", new[] { "Links frei.", "Frei links." } },
            { "spotter_Sara/clear_right", new[] { "Rechts frei.", "Frei rechts." } },
            { "spotter_Sara/clear_all_round", new[] { "Frei.", "Alles frei.", "Frei rundum." } },
            { "spotter_Sara/in_the_middle", new[] { "Du bist in der Mitte.", "Drei breit, du in der Mitte." } },
            { "spotter_Sara/three_wide_on_left", new[] { "Drei breit, du bist links.", "Drei nebeneinander, du ganz links." } },
            { "spotter_Sara/three_wide_on_right", new[] { "Drei breit, du bist rechts.", "Drei nebeneinander, du ganz rechts." } },
            { "spotter_Sara/car_inside", new[] { "Auto innen.", "Innen!" } },
            { "spotter_Sara/car_outside", new[] { "Auto außen.", "Außen!" } },
            { "spotter_Sara/clear_inside", new[] { "Innen frei." } },
            { "spotter_Sara/clear_outside", new[] { "Außen frei." } },
            { "spotter_Sara/three_wide_on_inside", new[] { "Drei breit, du innen." } },
            { "spotter_Sara/three_wide_on_outside", new[] { "Drei breit, du außen." } },
            { "radio_check_Sara/test", new[] { "Funkcheck, hier ist Sara.", "Sara hier, ich spotte heute für dich.", "Funkcheck." } },
            { "acknowledge/spotterEnabled_Sara", new[] { "Spotter ist an.", "Ich bin wieder da." } },
            { "acknowledge/spotterDisabled_Sara", new[] { "Spotter ist aus.", "Okay, ich bin ruhig." } },
        };

        public static string VoiceFolder
        {
            get { return Path.Combine(CrewChiefV4.Audio.AudioPlayer.soundFilesPathNoChiefOverride ?? DataFiles.SoundFilesFolder, "voice"); }
        }

        public static string ReadVoiceKey(string voiceFolder)
        {
            try
            {
                string marker = Path.Combine(voiceFolder, "spotter_Sara", MarkerFile);
                return File.Exists(marker) && new FileInfo(marker).Length <= 4096 ? File.ReadAllText(marker).Trim() : "";
            }
            catch (Exception) { return ""; }
        }

        /// <summary>Identity, every required clip/subtitle and (for new packs) hashes must match. No network or writes.</summary>
        public static bool IsGenerated(string voiceFolder, string voiceKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(voiceFolder) || string.IsNullOrWhiteSpace(voiceKey) || ReadVoiceKey(voiceFolder) != voiceKey) return false;
                string manifestPath = Path.Combine(voiceFolder, "spotter_Sara", ManifestFile);
                JObject manifest = null;
                if (File.Exists(manifestPath))
                {
                    if (new FileInfo(manifestPath).Length > 128 * 1024) return false;
                    manifest = JObject.Parse(File.ReadAllText(manifestPath));
                    if ((int?)manifest["schema"] != 1 || (string)manifest["voice_key"] != voiceKey || !(manifest["files"] is JObject)) return false;
                    if (((JObject)manifest["files"]).Count != Phrases.Sum(p => p.Value.Length + 1)) return false;
                }
                // Older correctly generated packs have the identity marker but no manifest. Validate their audio too.
                foreach (var phrase in Phrases)
                {
                    for (int i = 1; i <= phrase.Value.Length; i++)
                        if (!ValidateFile(voiceFolder, phrase.Key + "/" + i + ".wav", manifest, true)) return false;
                    if (!ValidateFile(voiceFolder, phrase.Key + "/subtitles.csv", manifest, false)) return false;
                }
                return true;
            }
            catch (Exception) { return false; }
        }

        private static bool ValidateFile(string voiceFolder, string relativePath, JObject manifest, bool audio)
        {
            string file = Path.Combine(voiceFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file)) return false;
            long size = new FileInfo(file).Length;
            if (size < (audio ? 524 : 1) || size > 4 * 1024 * 1024) return false;
            byte[] bytes = File.ReadAllBytes(file);
            if (audio && !ValidWave(bytes)) return false;
            if (manifest == null) return true;
            var entry = manifest["files"][relativePath];
            return entry != null && (long?)entry["bytes"] == bytes.LongLength && (string)entry["sha256"] == Hash(bytes);
        }

        private static bool ValidWave(byte[] bytes)
        {
            // All generated clips use the same canonical PCM header; reject truncated, empty or silent data.
            if (bytes.Length < 524 || Encoding.ASCII.GetString(bytes,0,4) != "RIFF" || Encoding.ASCII.GetString(bytes,8,4) != "WAVE" ||
                Encoding.ASCII.GetString(bytes,12,4) != "fmt " || BitConverter.ToInt32(bytes,16) != 16 ||
                BitConverter.ToInt16(bytes,20) != 1 || BitConverter.ToInt16(bytes,22) != 1 || BitConverter.ToInt32(bytes,24) != AiAudio.OpenAiPcmSampleRate ||
                BitConverter.ToInt32(bytes,28) != AiAudio.OpenAiPcmSampleRate * 2 || BitConverter.ToInt16(bytes,32) != 2 || BitConverter.ToInt16(bytes,34) != 16 || Encoding.ASCII.GetString(bytes,36,4) != "data" ||
                BitConverter.ToInt32(bytes,4) != bytes.Length-8 || BitConverter.ToInt32(bytes,40) != bytes.Length-44 || (bytes.Length-44)%2 != 0) return false;
            for (int i=44; i<bytes.Length; i++) if (bytes[i] != 0) return true;
            return false;
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void WriteManifest(string voiceFolder, string voiceKey)
        {
            var files = new JObject();
            foreach (var phrase in Phrases)
                foreach (var name in Enumerable.Range(1,phrase.Value.Length).Select(i => i + ".wav").Concat(new[] { "subtitles.csv" }))
                {
                    string relative = phrase.Key + "/" + name;
                    byte[] bytes = File.ReadAllBytes(Path.Combine(voiceFolder, relative.Replace('/',Path.DirectorySeparatorChar)));
                    files[relative] = new JObject { ["bytes"] = bytes.LongLength, ["sha256"] = Hash(bytes) };
                }
            var manifest = new JObject { ["schema"] = 1, ["voice_key"] = voiceKey, ["files"] = files };
            File.WriteAllText(Path.Combine(voiceFolder,"spotter_Sara",ManifestFile),manifest.ToString(),new UTF8Encoding(false));
        }

        /// <summary>
        /// Erzeugt alle Ansagen. renderPcm liefert PCM 24 kHz/16 Bit/mono (Stimme + Funkfilter), Stille am Rand wird entfernt,
        /// damit die Warnung ohne Verzögerung beginnt. Schreibt erst in einen Zwischenordner und tauscht dann aus,
        /// damit ein Abbruch kein halbes Pack hinterlässt.
        /// </summary>
        public static async Task<int> GenerateAsync(string voiceFolder, string voiceKey, Func<string, Task<byte[]>> renderPcm, Action<string> progress)
        {
            string staging = Path.Combine(Path.GetTempPath(), "rcca_sara_" + Guid.NewGuid().ToString("N"));
            int files = 0;
            try
            {
                foreach (var entry in Phrases)
                {
                    string folder = Path.Combine(staging, entry.Key.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(folder);
                    var subtitles = new StringBuilder();
                    for (int i = 0; i < entry.Value.Length; i++)
                    {
                        string text = entry.Value[i];
                        byte[] pcm = AiAudio.TrimSilence(await renderPcm(text).ConfigureAwait(false), AiAudio.OpenAiPcmSampleRate);
                        string name = (i + 1) + ".wav";
                        File.WriteAllBytes(Path.Combine(folder, name), AiAudio.PcmToWav(pcm, AiAudio.OpenAiPcmSampleRate));
                        subtitles.Append(name).Append(",\"").Append(text.Replace("\"", "'")).Append("\"\r\n");
                        files++;
                    }
                    File.WriteAllText(Path.Combine(folder, "subtitles.csv"), subtitles.ToString(), new UTF8Encoding(false));
                    if (progress != null)
                    {
                        progress(entry.Key + " fertig");
                    }
                }
                File.WriteAllText(Path.Combine(staging, "spotter_Sara", MarkerFile), voiceKey);
                WriteManifest(staging, voiceKey);
                if (!IsGenerated(staging, voiceKey)) throw new InvalidDataException("Sara-Pack enthält unvollständige oder ungültige Originalclips. Der bisherige Stimmenbestand bleibt erhalten.");

                foreach (var top in new[] { "spotter_Sara", "radio_check_Sara", Path.Combine("acknowledge", "spotterEnabled_Sara"), Path.Combine("acknowledge", "spotterDisabled_Sara") })
                {
                    string target = Path.Combine(voiceFolder, top);
                    if (Directory.Exists(target))
                    {
                        Directory.Move(target, target + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0,6));
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    CopyDirectory(Path.Combine(staging, top), target);
                }
                return files;
            }
            finally
            {
                // Keep staging for recovery/audit; the user has not authorized file deletion.
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
            }
        }
    }
}
