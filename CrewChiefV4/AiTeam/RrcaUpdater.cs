using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Sucht beim Start nach einer neueren veröffentlichten RRCA-Version (GitHub-Release mit Versionsnummer)
    /// und installiert sie auf Wunsch: herunterladen, Prüfsumme prüfen, entpacken, nach dem Beenden die
    /// Programmdateien ersetzen und neu starten. Benutzerdaten liegen außerhalb des Programmordners.
    /// </summary>
    public static class RrcaUpdater
    {
        public const string Repository = "JoshuaGerke/RobertsCrewChiefAdvanced";
        public const string AssetName = "RobertsCrewChiefAdvanced.zip";
        public static string ReleasesPage => "https://github.com/" + Repository + "/releases/latest";
        private const string DownloadPrefix = "https://github.com/" + Repository + "/releases/download/";
        private static readonly Regex VersionPattern = new Regex(@"^v?(\d+)\.(\d+)(?:\.(\d+))?(.*)$", RegexOptions.CultureInvariant);

        public sealed class Release
        {
            public string Tag, ZipUrl, HashUrl, Notes;
        }

        /// <summary>Version des laufenden Pakets aus VERSION.txt, null bei Entwicklungsbuilds.</summary>
        public static string InstalledVersion()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VERSION.txt");
                if (!File.Exists(path)) return null;
                string line = File.ReadAllLines(path).FirstOrDefault(l => l.StartsWith("Version:", StringComparison.OrdinalIgnoreCase));
                return line?.Substring(8).Trim();
            }
            catch (Exception) { return null; }
        }

        /// <summary>True, wenn remote neuer ist. Vorabversionen (Zusatz wie -alpha) gelten als älter als dieselbe Nummer ohne Zusatz.</summary>
        public static bool IsNewer(string remote, string installed)
        {
            var r = VersionPattern.Match(remote ?? ""); var i = VersionPattern.Match(installed ?? "");
            if (!r.Success) return false;
            if (!i.Success) return true;
            for (int g = 1; g <= 3; g++)
            {
                int a = r.Groups[g].Success ? int.Parse(r.Groups[g].Value) : 0, b = i.Groups[g].Success ? int.Parse(i.Groups[g].Value) : 0;
                if (a != b) return a > b;
            }
            bool remotePre = r.Groups[4].Value.Trim().Length > 0, installedPre = i.Groups[4].Value.Trim().Length > 0;
            return installedPre && !remotePre;
        }

        private static HttpClient Client()
        {
            // GitHub verlangt TLS 1.2; ältere Laufzeiteinstellungen nicht dem Zufall überlassen
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RRCA-Updater");
            return http;
        }

        /// <summary>Neuere Version oder null. Ohne Internet oder bei Fehlern einfach null.</summary>
        public static async Task<Release> CheckAsync()
        {
            string installed = InstalledVersion();
            // Entwicklungsstände ("dev-…") und Eigenbauten ohne Versionsnummer melden kein Update
            if (installed == null || !VersionPattern.IsMatch(installed)) return null;
            try
            {
                using (var http = Client())
                {
                    http.Timeout = TimeSpan.FromSeconds(10);
                    var json = JObject.Parse(await http.GetStringAsync("https://api.github.com/repos/" + Repository + "/releases/latest").ConfigureAwait(false));
                    string tag = (string)json["tag_name"];
                    if (string.IsNullOrEmpty(tag) || !IsNewer(tag, installed)) return null;
                    var assets = json["assets"] as JArray;
                    string Url(string name) => assets?.FirstOrDefault(a => (string)a["name"] == name)?["browser_download_url"]?.ToString();
                    string zip = Url(AssetName), hash = Url(AssetName + ".sha256");
                    if (zip == null || !zip.StartsWith(DownloadPrefix, StringComparison.Ordinal)) return null;
                    if (hash != null && !hash.StartsWith(DownloadPrefix, StringComparison.Ordinal)) hash = null;
                    return new Release { Tag = tag, ZipUrl = zip, HashUrl = hash, Notes = (string)json["body"] ?? "" };
                }
            }
            catch (Exception e) { Console.WriteLine("[RRCA] Update-Prüfung nicht möglich: " + e.GetBaseException().Message); return null; }
        }

        /// <summary>Darf in den Programmordner geschrieben werden? Sonst nur die Download-Seite öffnen.</summary>
        public static bool CanWriteProgramFolder()
        {
            try
            {
                string probe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".rrca_write_test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Lädt und prüft das Update und startet den Austausch, der nach dem Beenden dieser Instanz läuft.
        /// Gibt eine Fehlermeldung zurück oder null, wenn die App jetzt beendet werden soll.
        /// </summary>
        public static async Task<string> PrepareAsync(Release release, Action<string> progress)
        {
            string work = Path.Combine(Path.GetTempPath(), "rrca_update_" + Regex.Replace(release.Tag, @"[^\w.-]", "_"));
            string zipPath = Path.Combine(work, AssetName), staging = Path.Combine(work, "files");
            try
            {
                if (Directory.Exists(work)) Directory.Delete(work, true);
                Directory.CreateDirectory(work);
                using (var http = Client())
                {
                    progress?.Invoke("download");
                    using (var stream = await http.GetStreamAsync(release.ZipUrl).ConfigureAwait(false))
                    using (var file = File.Create(zipPath)) await stream.CopyToAsync(file).ConfigureAwait(false);
                    if (release.HashUrl != null)
                    {
                        string expected = (await http.GetStringAsync(release.HashUrl).ConfigureAwait(false)).Trim().Split(' ', '\t')[0].ToLowerInvariant();
                        if (!string.Equals(expected, Sha256(zipPath), StringComparison.Ordinal))
                            return "checksum";
                    }
                }
                progress?.Invoke("extract");
                ZipFile.ExtractToDirectory(zipPath, staging);
                if (!File.Exists(Path.Combine(staging, "CrewChiefV4.exe"))) return "invalid";
                StartSwap(staging, AppDomain.CurrentDomain.BaseDirectory, work);
                return null;
            }
            catch (Exception e)
            {
                return "error: " + e.Message;
            }
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }

        // Kleines Skript wartet, bis RRCA beendet ist, kopiert die neuen Dateien über die alten und startet neu
        private static void StartSwap(string staging, string target, string work)
        {
            string exe = Path.Combine(target, "CrewChiefV4.exe");
            string script = Path.Combine(work, "update.ps1");
            string Quote(string s) => "'" + s.Replace("'", "''") + "'";
            var ps = new StringBuilder();
            ps.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            ps.AppendLine("Wait-Process -Id " + Process.GetCurrentProcess().Id + " -Timeout 120");
            ps.AppendLine("Start-Sleep -Seconds 1");
            ps.AppendLine("robocopy " + Quote(staging.TrimEnd('\\')) + " " + Quote(target.TrimEnd('\\')) + " /E /R:5 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null");
            ps.AppendLine("if ($LASTEXITCODE -ge 8) { Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('RRCA update failed. Please download the new version manually: " + ReleasesPage + "', 'RRCA') | Out-Null }");
            ps.AppendLine("Start-Process -FilePath " + Quote(exe) + " -WorkingDirectory " + Quote(target.TrimEnd('\\')));
            File.WriteAllText(script, ps.ToString(), new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
    }
}
