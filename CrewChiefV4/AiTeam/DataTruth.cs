using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Datenkanäle, deren Wahrheit die Crew vor einer Aussage kennen muss.</summary>
    internal enum SimChannel { TyreWear, TyreTemperature, TyrePressure, Fuel, Damage, Incidents, OwnSectors, OpponentLapTimes, OpponentPositions }

    /// <summary>Was über einen Kanal bekannt ist.</summary>
    internal enum ChannelState
    {
        /// <summary>Noch keine Fahrt mit Werten beobachtet – bisheriges Verhalten bleibt (Bestandsschutz).</summary>
        Unknown,
        /// <summary>Werte ändern sich während der Fahrt – aktuell.</summary>
        Live,
        /// <summary>Werte vorhanden, aber während der Fahrt eingefroren (z. B. erst nach dem Boxenstopp aktualisiert).</summary>
        Stale,
        /// <summary>Die Simulation liefert diesen Kanal nicht.</summary>
        Missing
    }

    /// <summary>
    /// Was eine Simulation laut ihrer offiziellen Schnittstelle grundsätzlich liefert (Rob 17.11.2026, Phase A2).
    /// Nur Dinge, die sich im Betrieb nicht zuverlässig selbst feststellen lassen; alles andere misst <see cref="DataTruth"/>.
    /// </summary>
    internal static class SimCapabilities
    {
        /// <summary>Verschleißwerte kommen überhaupt (AC EVO liefert im Early Access keinen Reifenverschleiß).</summary>
        internal static bool TyreWear(GameType game) => game != GameType.ASSETTO_EVO;

        /// <summary>Prozentuale Schadenswerte (iRacing liefert keine; Einschläge nur über Vorfallpunkte).</summary>
        internal static bool DamageValues(GameType game) => game != GameType.IRACING;

        /// <summary>Vorfallpunkte als Zeichen für Kontakt.</summary>
        internal static bool IncidentPoints(GameType game) => game == GameType.IRACING;

        /// <summary>Kurztext für die Diagnose und das Sprachmodell.</summary>
        internal static string Describe(GameType game)
        {
            var parts = new List<string>();
            if (!TyreWear(game)) parts.Add("kein Reifenverschleiß von der Simulation");
            if (!DamageValues(game)) parts.Add("keine Schadenswerte, Kontakt nur über Vorfallpunkte");
            return parts.Count == 0 ? "" : string.Join("; ", parts);
        }
    }

    /// <summary>
    /// Datenwahrheit im Betrieb (Rob 17.11.2026, Phase A1): Für jeden Kanal wird während der Fahrt gemessen, ob sich die Werte
    /// wirklich bewegen. Eingefrorene Werte (z. B. iRacing-Reifen, die erst in der Box aktualisieren) oder gar nicht
    /// gelieferte Kanäle (AC-EVO-Verschleiß) erkennt die Crew so selbst – unabhängig von Annahmen über die Simulation.
    /// Ein Kanal gilt erst als eingefroren, wenn er während einer echten Fahrt (grün, nicht in der Box, mit Tempo) lange
    /// genug unverändert blieb; vor dem ersten Beleg bleibt alles beim Alten.
    /// </summary>
    internal sealed class DataTruth
    {
        private static DataTruth current = new DataTruth();
        /// <summary>Die Messung der laufenden Fahrt (wird vom KI-Team je Takt gefüttert).</summary>
        internal static DataTruth Current { get { return Volatile.Read(ref current); } }
        internal static void Use(DataTruth truth) { Volatile.Write(ref current, truth ?? new DataTruth()); }

        /// <summary>So lange Fahrt ohne jede Änderung, bis ein Kanal als eingefroren gilt.</summary>
        internal static TimeSpan StaleAfter(SimChannel c)
        {
            switch (c)
            {
                case SimChannel.TyreTemperature: return TimeSpan.FromSeconds(30);
                case SimChannel.TyrePressure: return TimeSpan.FromSeconds(90);
                case SimChannel.Fuel: return TimeSpan.FromSeconds(60);
                case SimChannel.TyreWear: return TimeSpan.FromSeconds(150);
                case SimChannel.OpponentPositions: return TimeSpan.FromSeconds(20);
                default: return TimeSpan.MaxValue; // ereignisartige Kanäle (Schaden, Vorfälle, Zeiten) ändern sich nicht ständig
            }
        }

        private sealed class Track
        {
            public string Signature; public bool HasValue; public double DrivenSinceChange, DrivenTotal; public long Changes; public DateTime LastChangeUtc;
            public double ChangeIntervalSum; public int ChangeIntervals;
        }

        private readonly Dictionary<SimChannel, Track> tracks = new Dictionary<SimChannel, Track>();
        private readonly HashSet<SimChannel> noticed = new HashSet<SimChannel>();
        private readonly object gate = new object();
        private int session = int.MinValue;
        private DateTime lastObserved = DateTime.MinValue;
        private GameType game = GameType.UNKNOWN;

        internal DataTruth() { foreach (SimChannel c in Enum.GetValues(typeof(SimChannel))) tracks[c] = new Track(); }

        internal void Reset()
        {
            lock (gate) { foreach (SimChannel c in tracks.Keys.ToList()) tracks[c] = new Track(); noticed.Clear(); lastObserved = DateTime.MinValue; session = int.MinValue; }
        }

        private static string Round(float v, float step) => float.IsNaN(v) || float.IsInfinity(v) ? "x" : Math.Round(v / step).ToString(CultureInfo.InvariantCulture);
        private static string Sig(float step, params float[] values) => string.Join("|", values.Select(v => Round(v, step)));
        private static bool InRange(float v, float lo, float hi) => !float.IsNaN(v) && v >= lo && v <= hi;

        /// <summary>Signatur eines Kanals oder null, wenn die Simulation gerade keinen plausiblen Wert liefert.</summary>
        internal static string Signature(GameStateData gs, SimChannel c)
        {
            var t = gs.TyreData;
            switch (c)
            {
                case SimChannel.TyreWear:
                {
                    float[] w = { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
                    return t.TyreWearActive && w.All(v => InRange(v, 0, 100)) ? Sig(.01f, w) : null;
                }
                case SimChannel.TyreTemperature:
                {
                    float[] temps = { t.FrontLeft_CenterTemp, t.FrontRight_CenterTemp, t.RearLeft_CenterTemp, t.RearRight_CenterTemp };
                    return temps.All(v => InRange(v, 1, 250)) ? Sig(.1f, temps) : null;
                }
                case SimChannel.TyrePressure:
                {
                    float[] p = { t.FrontLeftPressure, t.FrontRightPressure, t.RearLeftPressure, t.RearRightPressure };
                    return p.All(v => InRange(v, 50, 500)) ? Sig(.1f, p) : null;
                }
                case SimChannel.Fuel:
                    return gs.FuelData.FuelUseActive && InRange(gs.FuelData.FuelLeft, 0, 1000) ? Sig(.001f, gs.FuelData.FuelLeft) : null;
                case SimChannel.Damage:
                {
                    var d = gs.CarDamageData;
                    if (d == null || !d.DamageEnabled) return null;
                    var v = new List<float> { d.AeroDamagePercent };
                    if (d.SuspensionDamagePercent != null) v.AddRange(d.SuspensionDamagePercent);
                    return v.Any(x => InRange(x, 0, 100)) ? Sig(.1f, v.ToArray()) : null;
                }
                case SimChannel.Incidents:
                    return gs.SessionData.CurrentIncidentCount >= 0 && gs.SessionData.CurrentIncidentCount < 10000
                        ? gs.SessionData.CurrentIncidentCount.ToString(CultureInfo.InvariantCulture) : null;
                case SimChannel.OwnSectors:
                {
                    var lap = gs.SessionData.PlayerLapData.LastOrDefault();
                    if (lap?.SectorTimes == null) return null;
                    var good = lap.SectorTimes.Where(x => x > 1 && x < 3600).ToArray();
                    return good.Length == 0 ? null : lap.LapNumber + ":" + Sig(.001f, good);
                }
                case SimChannel.OpponentLapTimes:
                {
                    var laps = gs.OpponentData.Values.Where(o => o != null && o.LastLapTime > 1 && o.LastLapTime < 3600)
                        .Select(o => o.LastLapTime).OrderBy(x => x).Take(40).ToArray();
                    return laps.Length == 0 ? null : Sig(.001f, laps);
                }
                case SimChannel.OpponentPositions:
                {
                    var d = gs.OpponentData.Values.Where(o => o != null && o.DistanceRoundTrack > 0).Select(o => o.DistanceRoundTrack).Take(40).ToArray();
                    return d.Length == 0 ? null : Sig(1f, d);
                }
            }
            return null;
        }

        private static bool Driving(GameStateData gs) =>
            gs.SessionData.SessionPhase == SessionPhase.Green && !gs.PitData.InPitlane && gs.PositionAndMotionData.CarSpeed > 15;

        internal void Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return;
            lock (gate)
            {
                if (gs.SessionData.SessionId != session || Game.gameType != game)
                {
                    foreach (SimChannel c in tracks.Keys.ToList()) tracks[c] = new Track();
                    noticed.Clear();
                    session = gs.SessionData.SessionId; game = Game.gameType; lastObserved = DateTime.MinValue;
                }
                double dt = lastObserved == DateTime.MinValue ? 0 : (now - lastObserved).TotalSeconds;
                if (dt < 0 || dt > 2) dt = 0; // Pause, Menü oder Sprung: zählt nicht als Fahrt
                lastObserved = now;
                bool driving = Driving(gs);
                foreach (var pair in tracks)
                {
                    string sig = Signature(gs, pair.Key);
                    var t = pair.Value;
                    if (sig == null) { if (driving) t.DrivenTotal += dt; continue; }
                    if (!t.HasValue) { t.HasValue = true; t.Signature = sig; t.LastChangeUtc = now; continue; }
                    if (sig != t.Signature)
                    {
                        if (t.Changes > 0) { t.ChangeIntervalSum += (now - t.LastChangeUtc).TotalSeconds; t.ChangeIntervals++; }
                        t.Signature = sig; t.Changes++; t.LastChangeUtc = now; t.DrivenSinceChange = 0;
                    }
                    else if (driving) t.DrivenSinceChange += dt;
                    if (driving) t.DrivenTotal += dt;
                }
            }
        }

        internal ChannelState State(SimChannel c)
        {
            if (c == SimChannel.TyreWear && !SimCapabilities.TyreWear(Game.gameType)) return ChannelState.Missing;
            if (c == SimChannel.Damage && !SimCapabilities.DamageValues(Game.gameType)) return ChannelState.Missing;
            lock (gate)
            {
                var t = tracks[c];
                var limit = StaleAfter(c);
                if (!t.HasValue) return limit != TimeSpan.MaxValue && t.DrivenTotal >= limit.TotalSeconds ? ChannelState.Missing : ChannelState.Unknown;
                if (limit != TimeSpan.MaxValue && t.DrivenSinceChange >= limit.TotalSeconds) return ChannelState.Stale;
                return t.Changes > 0 ? ChannelState.Live : ChannelState.Unknown;
            }
        }

        /// <summary>
        /// AMS2 Spa 23.11.: Verbrauch und Verschleiß waren serverseitig aus; Tank und Reifen standen still. Die Crew sagt das einmal
        /// pro Session kurz und schweigt danach zu diesem Kanal. true nur beim ersten Aufruf, solange der Kanal eingefroren ist.
        /// </summary>
        internal bool ClaimFrozenNotice(SimChannel c)
        {
            if (State(c) != ChannelState.Stale) return false;
            lock (gate) return noticed.Add(c);
        }

        /// <summary>Darf die Crew Werte dieses Kanals als aktuell nennen? Unbekannt zählt als ja (Bestandsschutz).</summary>
        internal bool MaySpeakAsCurrent(SimChannel c)
        {
            var s = State(c);
            return s == ChannelState.Live || s == ChannelState.Unknown;
        }

        /// <summary>Für Sprachmodell und Diagnose: „live“, „eingefroren seit 3 min Fahrt“, „nicht geliefert“, „unbekannt“.</summary>
        internal string Describe(SimChannel c)
        {
            switch (State(c))
            {
                case ChannelState.Live: return "live";
                case ChannelState.Missing: return "nicht geliefert";
                case ChannelState.Stale:
                    double s; lock (gate) s = tracks[c].DrivenSinceChange;
                    return "eingefroren seit " + Math.Round(s / 60, 1).ToString(CultureInfo.InvariantCulture) + " min Fahrt";
                default: return "unbekannt";
            }
        }

        /// <summary>Zusammenfassung für die Live-Diagnose (Fähigkeitstabelle der laufenden Simulation).</summary>
        internal Dictionary<string, object> Report()
        {
            var r = new Dictionary<string, object> { ["game"] = Game.gameType.ToString(), ["static"] = SimCapabilities.Describe(Game.gameType) };
            lock (gate)
                foreach (var pair in tracks)
                {
                    var t = pair.Value;
                    r[pair.Key.ToString()] = new Dictionary<string, object>
                    {
                        ["state"] = StateUnlocked(pair.Key, t).ToString(), ["changes"] = t.Changes,
                        ["mean_change_s"] = t.ChangeIntervals > 0 ? Math.Round(t.ChangeIntervalSum / t.ChangeIntervals, 3) : (double?)null,
                        ["driven_s"] = Math.Round(t.DrivenTotal, 1), ["driven_since_change_s"] = Math.Round(t.DrivenSinceChange, 1)
                    };
                }
            return r;
        }

        private ChannelState StateUnlocked(SimChannel c, Track t)
        {
            if (c == SimChannel.TyreWear && !SimCapabilities.TyreWear(Game.gameType)) return ChannelState.Missing;
            if (c == SimChannel.Damage && !SimCapabilities.DamageValues(Game.gameType)) return ChannelState.Missing;
            var limit = StaleAfter(c);
            if (!t.HasValue) return limit != TimeSpan.MaxValue && t.DrivenTotal >= limit.TotalSeconds ? ChannelState.Missing : ChannelState.Unknown;
            if (limit != TimeSpan.MaxValue && t.DrivenSinceChange >= limit.TotalSeconds) return ChannelState.Stale;
            return t.Changes > 0 ? ChannelState.Live : ChannelState.Unknown;
        }
    }
}
