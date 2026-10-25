using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        /// <summary>true, wenn das Pack für genau diese Stimme schon vollständig erzeugt wurde.</summary>
        public static bool IsGenerated(string voiceFolder, string voiceKey)
        {
            string marker = Path.Combine(voiceFolder, "spotter_Sara", MarkerFile);
            return File.Exists(marker) && File.ReadAllText(marker).Trim() == voiceKey;
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

                foreach (var top in new[] { "spotter_Sara", "radio_check_Sara", Path.Combine("acknowledge", "spotterEnabled_Sara"), Path.Combine("acknowledge", "spotterDisabled_Sara") })
                {
                    string target = Path.Combine(voiceFolder, top);
                    if (Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    CopyDirectory(Path.Combine(staging, top), target);
                }
                return files;
            }
            finally
            {
                try { Directory.Delete(staging, true); } catch (Exception) { }
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
