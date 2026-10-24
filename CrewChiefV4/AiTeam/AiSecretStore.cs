using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Speichert den API-Schlüssel mit Windows-DPAPI (nur für den aktuellen Windows-Benutzer lesbar).
    /// Der Schlüssel landet nie in user.config, im Log oder im Repository.
    /// Fallback: Umgebungsvariable RCCA_API_KEY bzw. OPENAI_API_KEY.
    /// </summary>
    public static class AiSecretStore
    {
        private static readonly byte[] entropy = Encoding.UTF8.GetBytes("RobertsCrewChiefAdvanced.ApiKey.v1");

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RobertsCrewChiefAdvanced", "api_key.bin");
            }
        }

        public static void Save(string apiKey, string path = null)
        {
            path = path ?? DefaultPath;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Delete(path);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey.Trim()), entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }

        public static string Load(string path = null)
        {
            path = path ?? DefaultPath;
            try
            {
                if (File.Exists(path))
                {
                    byte[] decrypted = ProtectedData.Unprotect(File.ReadAllBytes(path), entropy, DataProtectionScope.CurrentUser);
                    string key = Encoding.UTF8.GetString(decrypted);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        return key;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[KI-Team] API-Schlüssel konnte nicht gelesen werden: " + e.Message);
            }
            string env = Environment.GetEnvironmentVariable("RCCA_API_KEY");
            if (string.IsNullOrWhiteSpace(env))
            {
                env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            }
            return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
        }

        public static void Delete(string path = null)
        {
            path = path ?? DefaultPath;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>Für Anzeige und Log: nur Anfang und Ende des Schlüssels.</summary>
        public static string Mask(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return "(kein Schlüssel)";
            }
            if (apiKey.Length <= 8)
            {
                return new string('*', apiKey.Length);
            }
            return apiKey.Substring(0, 3) + "…" + apiKey.Substring(apiKey.Length - 4);
        }
    }
}
