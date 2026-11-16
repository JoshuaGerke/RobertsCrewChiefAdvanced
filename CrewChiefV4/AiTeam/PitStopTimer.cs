using System;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Misst jeden eigenen Boxenbesuch in allen Sitzungsarten (Robs Wunsch 16.11.2026):
    /// Einfahrt (Boxengasse bis Stillstand), Standzeit und Ausfahrt (Losfahren bis Boxengassenende).
    /// Nach der Ausfahrt meldet Ben die gemessenen Zeiten einmal; die letzte Messung steht auch für Fragen bereit.
    /// Zeitbasis ist die Sitzungsuhr des Spiels, damit Pausen nicht mitzählen. Durchfahrt ohne Halt wird als solche gemeldet.
    /// </summary>
    internal sealed class PitStopTimer
    {
        internal sealed class Measurement
        {
            internal float EntrySeconds = -1, StationarySeconds = -1, ExitSeconds = -1, TotalSeconds = -1, FuelAdded = -1;
            internal bool Stopped;
            internal DateTime FinishedUtc;
            internal int SessionId;

            internal JObject Describe()
            {
                return new JObject
                {
                    ["gemessen"] = true,
                    ["halt"] = Stopped,
                    ["einfahrt_bis_halt_s"] = Stopped ? (JToken)Math.Round(EntrySeconds, 1) : null,
                    ["standzeit_s"] = Stopped ? (JToken)Math.Round(StationarySeconds, 1) : null,
                    ["ausfahrt_ab_losfahren_s"] = Stopped ? (JToken)Math.Round(ExitSeconds, 1) : null,
                    ["boxengasse_gesamt_s"] = Math.Round(TotalSeconds, 1),
                    ["nachgetankt_l"] = FuelAdded > 0 ? (JToken)Math.Round(FuelAdded, 1) : null,
                    ["hinweis"] = "Gemessen mit der Sitzungsuhr von Einfahrt in die Boxengasse bis Ausfahrt. Stillstand = unter 1 m/s für mindestens 1 s."
                };
            }
        }

        private const float StoppedSpeed = 1f;
        private int session = int.MinValue;
        private bool inLane, stopped, wasStopped;
        private float laneStart, stopStart, stopEnd, fuelAtEntry = -1;
        private float stillSince = -1, onTrackSince = -1;
        private bool cameFromTrack;
        private int variant;
        private ObservedRaceCall call;

        internal Measurement Last { get; private set; }

        internal void Reset()
        {
            session = int.MinValue; inLane = stopped = wasStopped = cameFromTrack = false; stillSince = onTrackSince = -1; call = null; Last = null;
        }

        internal void Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            float t = s.SessionRunningTime;
            if (float.IsNaN(t) || float.IsInfinity(t) || t < 0) return;
            bool lane = gs.PitData.InPitlane && !gs.PitData.IsInGarage && !gs.PitData.JumpedToPits;
            float speed = gs.PositionAndMotionData.CarSpeed;
            // Rob 16.11. (Mosport): Die Ausfahrt aus der Garage zu Sitzungsbeginn ist kein Boxenstopp. Gemessen wird nur,
            // wenn wir vorher mindestens 5 s auf der Strecke gefahren sind.
            if (!lane && speed > 15) { if (onTrackSince < 0) onTrackSince = t; }
            else if (!lane && speed < 3) onTrackSince = -1;
            if (lane && !inLane)
            {
                cameFromTrack = onTrackSince >= 0 && t - onTrackSince >= 5;
                onTrackSince = -1;
                inLane = true; stopped = wasStopped = false; stillSince = -1;
                laneStart = t;
                fuelAtEntry = FuelOf(gs);
                return;
            }
            if (!lane && inLane)
            {
                inLane = false;
                if (gs.PitData.IsInGarage || gs.PitData.JumpedToPits || !cameFromTrack) return; // Garage/Zurücksetzen/Sitzungsbeginn ist kein Boxenstopp
                var m = new Measurement { SessionId = s.SessionId, FinishedUtc = now, Stopped = stopped, TotalSeconds = t - laneStart };
                float fuelNow = FuelOf(gs);
                // Nur ein deutlicher Anstieg ist Nachtanken; der Verbrauch in der Boxengasse ist dagegen klein.
                if (fuelAtEntry >= 0 && fuelNow >= 0 && fuelNow - fuelAtEntry >= .3f) m.FuelAdded = fuelNow - fuelAtEntry;
                if (stopped)
                {
                    m.EntrySeconds = stopStart - laneStart;
                    m.StationarySeconds = stopEnd - stopStart;
                    m.ExitSeconds = t - stopEnd;
                }
                if (m.TotalSeconds < 3 || m.TotalSeconds > 600 || stopped && (m.EntrySeconds < 0 || m.StationarySeconds < 0 || m.ExitSeconds < 0)) return;
                Last = m;
                call = Announce(gs, now, m);
                return;
            }
            if (!inLane) return;
            bool still = speed >= 0 && speed < StoppedSpeed;
            if (still)
            {
                if (stillSince < 0) stillSince = t;
                if (!wasStopped && t - stillSince >= 1f)
                {
                    wasStopped = true;
                    if (!stopped) { stopped = true; stopStart = stillSince; }
                }
                if (wasStopped) stopEnd = t;
            }
            else
            {
                if (wasStopped) stopEnd = t;
                wasStopped = false;
                stillSince = -1;
            }
        }

        private static float FuelOf(GameStateData gs)
        {
            float f = gs.FuelData.FuelLeft;
            return gs.FuelData.FuelUseActive && !float.IsNaN(f) && !float.IsInfinity(f) && f >= 0 && f < 10000 ? f : -1;
        }

        private ObservedRaceCall Announce(GameStateData gs, DateTime now, Measurement m)
        {
            string text = Text(m, variant++);
            if (m.FuelAdded > 0)
                text += RadioLanguage.T(" Nachgetankt: rund ", " Refuelled: about ", " Repostado: unos ") + RadioLanguage.Num(m.FuelAdded, "0") +
                    RadioLanguage.T(" Liter.", " litres.", " litros.");
            var c = new ObservedRaceCall
            {
                Topic = "pit-stop-time", Text = text, Evidence = "measured_own_pit_lane_visit", Member = TeamMember.Ben, Priority = 4,
                Position = -1, SessionId = gs.SessionData.SessionId, SessionType = gs.SessionData.SessionType,
                DetectedUtc = now, ExpiresUtc = now.AddSeconds(25),
                AdditionalValidity = live => !live.PitData.InPitlane
            };
            LiveDiagnostics.Decision(c.Topic, "detected", c.Evidence, text, c.EventId);
            return c;
        }

        internal static string Text(Measurement m, int variant)
        {
            string total = N(m.TotalSeconds);
            if (!m.Stopped)
                return RadioLanguage.T("Durch die Boxengasse in " + total + " Sekunden, ohne Halt.",
                    "Through the pit lane in " + total + " seconds, no stop.",
                    "Por el pit lane en " + total + " segundos, sin parar.");
            string a = N(m.EntrySeconds), b = N(m.StationarySeconds), c = N(m.ExitSeconds);
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T("Boxenstopp gemessen: Einfahrt " + a + ", Stand " + b + ", Ausfahrt " + c + " Sekunden. Gesamt " + total + ".",
                    "Pit stop timed: in " + a + ", stationary " + b + ", out " + c + " seconds. Total " + total + ".",
                    "Parada medida: entrada " + a + ", parado " + b + ", salida " + c + " segundos. Total " + total + ".");
                case 1: return RadioLanguage.T(b + " Sekunden gestanden, insgesamt " + total + " in der Boxengasse. Rein " + a + ", raus " + c + ".",
                    b + " seconds stationary, " + total + " in the pit lane overall. In " + a + ", out " + c + ".",
                    b + " segundos parado, " + total + " en el pit lane en total. Entrada " + a + ", salida " + c + ".");
                case 2: return RadioLanguage.T("Box erledigt. Stand " + b + " Sekunden, Boxengasse gesamt " + total + ".",
                    "Stop done. Stationary " + b + " seconds, pit lane total " + total + ".",
                    "Parada hecha. Parado " + b + " segundos, pit lane total " + total + ".");
                default: return RadioLanguage.T("Deine Boxenzeiten: rein " + a + ", stehen " + b + ", raus " + c + " Sekunden.",
                    "Your pit times: in " + a + ", stationary " + b + ", out " + c + " seconds.",
                    "Tus tiempos de boxes: entrada " + a + ", parado " + b + ", salida " + c + " segundos.");
            }
        }

        private static string N(float seconds) { return RadioLanguage.Num(seconds, "0.0"); }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
