using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Speichert API-Schlüssel mit Windows-DPAPI (nur für den aktuellen Windows-Benutzer lesbar).
    /// Die Schlüssel landen nie in user.config, im Log oder im Repository.
    /// Chat/Erkennung (OpenRouter oder OpenAI): api_key.bin, Fallback RCCA_API_KEY, OPENROUTER_API_KEY, OPENAI_API_KEY.
    /// Stimmen (ElevenLabs): elevenlabs_key.bin, Fallback ELEVENLABS_API_KEY.
    /// </summary>
    public static class AiSecretStore
    {
        private static readonly byte[] entropy = Encoding.UTF8.GetBytes("RobertsCrewChiefAdvanced.ApiKey.v1");

        public static string DefaultPath
        {
            get { return Path.Combine(Folder, "api_key.bin"); }
        }

        public static string ElevenLabsPath
        {
            get { return Path.Combine(Folder, "elevenlabs_key.bin"); }
        }

        private static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobertsCrewChiefAdvanced"); }
        }

        private static readonly string[] chatEnvVars = { "RCCA_API_KEY", "OPENROUTER_API_KEY", "OPENAI_API_KEY" };

        public static string LoadElevenLabs()
        {
            return Load(ElevenLabsPath, "ELEVENLABS_API_KEY");
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

        public static string Load(string path = null, params string[] envVars)
        {
            path = path ?? DefaultPath;
            if (envVars == null || envVars.Length == 0)
            {
                envVars = path == DefaultPath ? chatEnvVars : new string[0];
            }
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
            foreach (string name in envVars)
            {
                string env = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(env))
                {
                    return env.Trim();
                }
            }
            return null;
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
