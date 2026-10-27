using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Nimmt das Mikrofon auf, solange die KI-Funktaste gedrückt ist (16 kHz, mono).</summary>
    public class AiMicRecorder : IDisposable
    {
        public const int SampleRate = 16000;
        private readonly object sync = new object();
        private WaveInEvent waveIn;
        private MemoryStream buffer;
        private DateTime startedUtc;

        public bool IsRecording { get { lock (sync) { return waveIn != null; } } }

        public static string[] AvailableDevices()
        {
            var names = new string[WaveInEvent.DeviceCount];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = WaveInEvent.GetCapabilities(i).ProductName;
            }
            return names;
        }

        public static int FindDevice(string nameFragment)
        {
            if (!string.IsNullOrWhiteSpace(nameFragment))
            {
                for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                {
                    if (WaveInEvent.GetCapabilities(i).ProductName.IndexOf(nameFragment.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
                Console.WriteLine("[KI-Team] Mikrofon \"" + nameFragment + "\" nicht gefunden, nehme Standardgerät.");
            }
            return 0;
        }

        public void Start(string deviceName)
        {
            lock (sync)
            {
                if (waveIn != null)
                {
                    return;
                }
                buffer = new MemoryStream();
                waveIn = new WaveInEvent
                {
                    DeviceNumber = FindDevice(deviceName),
                    WaveFormat = new WaveFormat(SampleRate, 16, 1),
                    BufferMilliseconds = 50,
                };
                var stream = buffer;
                waveIn.DataAvailable += (s, e) =>
                {
                    lock (stream)
                    {
                        // Sicherheitsgrenze: 30 Sekunden
                        if (stream.Length < SampleRate * 2 * 30)
                        {
                            stream.Write(e.Buffer, 0, e.BytesRecorded);
                        }
                    }
                };
                startedUtc = DateTime.UtcNow;
                waveIn.StartRecording();
            }
        }

        /// <summary>Beendet die Aufnahme und liefert das WAV oder null, wenn sie zu kurz war.</summary>
        public byte[] Stop(out TimeSpan duration)
        {
            WaveInEvent toStop;
            MemoryStream recorded;
            lock (sync)
            {
                toStop = waveIn;
                recorded = buffer;
                waveIn = null;
                buffer = null;
            }
            duration = TimeSpan.Zero;
            if (toStop == null)
            {
                return null;
            }
            // Nachlauf, damit das letzte Wort nicht abgeschnitten wird
            Thread.Sleep(150);
            using (var stopped = new ManualResetEventSlim(false))
            {
                toStop.RecordingStopped += (s, e) => stopped.Set();
                toStop.StopRecording();
                // StopRecording ist asynchron: erst nach RecordingStopped sind alle Puffer geschrieben
                stopped.Wait(1000);
            }
            toStop.Dispose();
            duration = DateTime.UtcNow - startedUtc;
            byte[] pcm;
            lock (recorded)
            {
                pcm = recorded.ToArray();
            }
            if (pcm.Length < SampleRate * 2 * 0.35)
            {
                return null;
            }
            return AiAudio.PcmToWav(pcm, SampleRate);
        }

        public void Dispose()
        {
            TimeSpan ignored;
            Stop(out ignored);
        }
    }

    /// <summary>
    /// Überwacht die KI-Funktaste: Controller-Knopf (Crew-Chief-Zuordnung "talk_to_ai_team")
    /// oder eine Tastaturtaste (Einstellung ai_team_ptt_key, z. B. "F9").
    /// </summary>
    public class AiPushToTalk
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private readonly Func<bool> controllerHeld;
        private readonly Action onPressed;
        private readonly Action onReleased;
        private Thread thread;
        private volatile bool running;
        private volatile int keyCode;

        public AiPushToTalk(Func<bool> controllerHeld, Action onPressed, Action onReleased)
        {
            this.controllerHeld = controllerHeld;
            this.onPressed = onPressed;
            this.onReleased = onReleased;
        }

        public void SetKey(string keyName)
        {
            Keys key;
            keyCode = !string.IsNullOrWhiteSpace(keyName) && Enum.TryParse(keyName.Trim(), true, out key) ? (int)key : 0;
        }

        public void Start()
        {
            if (running)
            {
                return;
            }
            running = true;
            thread = new Thread(Loop) { IsBackground = true, Name = "AiTeam.PushToTalk" };
            thread.Start();
        }

        public void Stop()
        {
            running = false;
        }

        private void Loop()
        {
            bool wasHeld = false;
            while (running)
            {
                bool held = false;
                try
                {
                    held = (keyCode != 0 && (GetAsyncKeyState(keyCode) & 0x8000) != 0) || (controllerHeld != null && controllerHeld());
                }
                catch (Exception) { }
                if (held && !wasHeld)
                {
                    SafeInvoke(onPressed);
                }
                else if (!held && wasHeld)
                {
                    SafeInvoke(onReleased);
                }
                wasHeld = held;
                Thread.Sleep(20);
            }
        }

        private static void SafeInvoke(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("ptt.error",LiveDiagnostics.Data("error_type",e.GetType().Name));
                Console.WriteLine("[KI-Team] Fehler in der Funktaste: " + e.Message);
            }
        }
    }
}
