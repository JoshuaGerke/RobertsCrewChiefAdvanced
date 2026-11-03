using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Reaktion des Teams auf Zwischenfälle, ohne KI und deshalb sofort:
    /// Kontakt (Mik meldet sich, wenige Sekunden später Schadensbild aus der Telemetrie),
    /// Dreher/Stillstand (Hinweis auf den Verkehr), neue Strafe (Ben), ungültige Runde (Mik).
    /// Liest nur den GameState, Texte enthalten nur Werte aus der Telemetrie.
    /// </summary>
    /// <summary>Lage nach einem Zwischenfall, sobald der Wagen wieder fährt. Grundlage für Bens Bewertung.</summary>
    public class DamageAssessment
    {
        public int PositionBefore;
        public int PositionNow;
        public bool Serious;
        /// <summary>Lokaler Bericht ohne KI (Ersatz, wenn die KI nicht erreichbar ist).</summary>
        public string LocalReport;
    }

    public class IncidentWatch
    {
        public static readonly TimeSpan DamageCheckDelay = TimeSpan.FromSeconds(4);
        public static readonly TimeSpan DamageCheckMaxDelay = TimeSpan.FromSeconds(25);

        private DateTime lastContactUtc = DateTime.MinValue;
        private DateTime pendingDamageCheckUtc = DateTime.MinValue;
        private DateTime lastFastUtc = DateTime.MinValue;
        private DateTime lastStopUtc = DateTime.MinValue;
        private int lastInvalidLap = -1;
        private int lastReportedCutWarnings = -1;
        private readonly RaceRadioLanguage language = new RaceRadioLanguage();
        // Compatibility name: speed loss alone proves a strong slowdown, not an actual spin.
        public int Spins { get; private set; }
        public int TrackLimitWarnings { get; private set; }
        public int InvalidLaps { get; private set; }
        public int Penalties { get; private set; }
        public int OffTracks { get; private set; }
        private DateTime offTrackSinceUtc = DateTime.MinValue;
        private bool offTrackCounted;
        private DateTime lastOffTrackCallUtc = DateTime.MinValue;
        private int positionBeforeIncident;
        private DamageAssessment assessment;
        // AMS2 meldet gelegentlich kurz Platz 1 (Spieler taucht als Gegner auf) → Median statt Einzelwert
        private readonly List<KeyValuePair<DateTime, int>> recentPositions = new List<KeyValuePair<DateTime, int>>();

        public void Reset()
        {
            lastContactUtc = pendingDamageCheckUtc = lastFastUtc = lastStopUtc = DateTime.MinValue;
            lastInvalidLap = -1;
            lastReportedCutWarnings = -1;
            TrackLimitWarnings = Spins = InvalidLaps = Penalties = OffTracks = 0;
            offTrackSinceUtc = lastOffTrackCallUtc = DateTime.MinValue;
            offTrackCounted = false;
            positionBeforeIncident = 0;
            assessment = null;
            recentPositions.Clear();
        }

        private int StablePosition(int fallback)
        {
            if (recentPositions.Count == 0)
            {
                return fallback;
            }
            var sorted = recentPositions.Select(p => p.Value).OrderBy(p => p).ToList();
            return sorted[sorted.Count / 2];
        }

        /// <summary>Fällige Schadensbewertung (einmalig abholen), sonst null.</summary>
        public DamageAssessment TakeDamageAssessment()
        {
            var a = assessment;
            assessment = null;
            return a;
        }

        /// <summary>Ein Telemetrie-Tick. Liefert die fälligen Funksprüche (meist keinen).</summary>
        public List<KeyValuePair<TeamMember, string>> Evaluate(GameStateData previous, GameStateData current, DateTime nowUtc, bool minimalMode)
        {
            var result = new List<KeyValuePair<TeamMember, string>>();
            if (previous == null || current == null || current.SessionData.SessionPhase != SessionPhase.Green ||
                current.SessionData.IsNewSession || previous.SessionData.SessionId != current.SessionData.SessionId ||
                previous.SessionData.SessionType != current.SessionData.SessionType)
            {
                return result;
            }
            bool inPit = current.PitData.InPitlane || previous.PitData.InPitlane;
            float kmh = current.PositionAndMotionData.CarSpeed * 3.6f;
            if (previous.SessionData.ClassPosition > 0)
            {
                recentPositions.Add(new KeyValuePair<DateTime, int>(nowUtc, previous.SessionData.ClassPosition));
            }
            recentPositions.RemoveAll(x => nowUtc - x.Key > TimeSpan.FromSeconds(15));

            // 1. Kontakt: Einschlagzeitpunkt hat sich geändert (AMS2, iRacing, rFactor 2 …) oder – für Spiele ohne
            //    Einschlagzeitpunkt wie RaceRoom und Assetto Corsa – ein Schadenswert ist gestiegen
            float impact = current.CarDamageData.LastImpactTime;
            bool impactChanged = impact > 0 && impact != previous.CarDamageData.LastImpactTime;
            bool heavyContact = impactChanged && (previous.PositionAndMotionData.CarSpeed * 3.6f - kmh > 40 || IsSerious(current.CarDamageData));
            if ((impactChanged || DamageIncreased(previous.CarDamageData, current.CarDamageData)) && !inPit)
            {
                if ((impactChanged || !IsSerious(current.CarDamageData)) && nowUtc - lastContactUtc > TimeSpan.FromSeconds(15))
                {
                    result.Add(Say(TeamMember.Mik, impactChanged ? language.Contact(heavyContact) : language.DamageChanged()));
                }
                if (positionBeforeIncident <= 0)
                {
                    positionBeforeIncident = StablePosition(previous.SessionData.ClassPosition);
                }
                lastContactUtc = nowUtc;
                pendingDamageCheckUtc = nowUtc + DamageCheckDelay;
                // Preserve Joshua's driver check before the damage report after a proven heavy impact.
                // A serious damage change without an impact signal is reported immediately, without inventing a collision.
                if (IsSerious(current.CarDamageData) && !heavyContact)
                {
                    assessment = new DamageAssessment { PositionBefore=positionBeforeIncident,
                        PositionNow=current.SessionData.ClassPosition, Serious=true, LocalReport=DamageReport(current.CarDamageData) };
                    pendingDamageCheckUtc = DateTime.MinValue;
                    positionBeforeIncident = 0;
                }
            }
            // Bericht erst, wenn der Wagen wieder auf Tempo ist (dann stehen Schaden und Position fest), spätestens nach 25 s
            if (pendingDamageCheckUtc != DateTime.MinValue && nowUtc >= pendingDamageCheckUtc &&
                (IsSerious(current.CarDamageData) || kmh > 50 || nowUtc - lastContactUtc > DamageCheckMaxDelay))
            {
                pendingDamageCheckUtc = DateTime.MinValue;
                string report = DamageReport(current.CarDamageData);
                if (report != null)
                {
                    int before = positionBeforeIncident, nowPos = current.SessionData.ClassPosition;
                    if (before > 0 && nowPos > before)
                    {
                        report = "Du bist von Platz " + before + " auf " + nowPos + " zurückgefallen. " + report;
                    }
                    assessment = new DamageAssessment
                    {
                        PositionBefore = before, PositionNow = nowPos, Serious = IsSerious(current.CarDamageData), LocalReport = report,
                    };
                }
                positionBeforeIncident = 0;
            }

            // 2. Starker Geschwindigkeitsabfall: Ursache (Dreher, Bremsen, Kontakt) ist damit noch nicht belegt.
            if (kmh > 70)
            {
                lastFastUtc = nowUtc;
            }
            else if (kmh < 10 && !inPit && nowUtc - lastFastUtc < TimeSpan.FromSeconds(4) && nowUtc - lastStopUtc > TimeSpan.FromSeconds(30))
            {
                lastStopUtc = nowUtc;
                Spins++;
                string text = language.Stopped();
                float behind = current.SessionData.TimeDeltaBehind;
                if (current.SessionData.SessionType == SessionType.Race && behind > 0 && behind < 15)
                {
                    text += " Der Nächste war zuletzt etwa " + SaraLocal.Seconds(behind) + " hinter dir.";
                }
                result.Add(Say(TeamMember.Mik, text));
            }

            // Ausritt: nur Spiele, die den Untergrund melden (RaceRoom z. B. nicht). Kurz übers Gras zählt nicht.
            if (current.PenaltiesData.IsOffRacingSurface && !inPit)
            {
                if (offTrackSinceUtc == DateTime.MinValue) offTrackSinceUtc = nowUtc;
                if (!offTrackCounted && nowUtc - offTrackSinceUtc > TimeSpan.FromSeconds(2))
                {
                    offTrackCounted = true;
                    OffTracks++;
                    if (!minimalMode && nowUtc - lastOffTrackCallUtc > TimeSpan.FromSeconds(20))
                    {
                        lastOffTrackCallUtc = nowUtc;
                        var conditions = current.Conditions != null ? current.Conditions.LatestConditions : null;
                        bool fresh = conditions != null && current.Now >= conditions.Time && current.Now-conditions.Time <= TimeSpan.FromSeconds(30);
                        bool wet = fresh && (conditions.TrackWetness >= ConditionsMonitor.TrackWetness.VeryLightlyWet && conditions.TrackWetness <= ConditionsMonitor.TrackWetness.ExtremelyWet ||
                            conditions.TrackStatus == ConditionsMonitor.TrackStatus.DAMP || conditions.TrackStatus == ConditionsMonitor.TrackStatus.WET ||
                            conditions.TrackStatus == ConditionsMonitor.TrackStatus.FLOODED);
                        string text = language.OffTrack(wet);
                        if (!wet && fresh && conditions.RainDensity > 0 && conditions.RainDensity <= 1) text += CrewSpeechPolicy.RainTrackHint;
                        result.Add(Say(TeamMember.Mik, text));
                    }
                }
            }
            else
            {
                offTrackSinceUtc = DateTime.MinValue;
                offTrackCounted = false;
            }

            // 3. Neue Strafe
            var p = current.PenaltiesData;
            var pp = previous.PenaltiesData;
            string penalty = null;
            if (p.HasDriveThrough && !pp.HasDriveThrough) penalty = "eine Durchfahrtsstrafe";
            else if (p.HasStopAndGo && !pp.HasStopAndGo) penalty = "eine Stop-and-Go-Strafe";
            else if (p.HasTimeDeduction && !pp.HasTimeDeduction) penalty = "eine Zeitstrafe";
            else if (p.HasSlowDown && !pp.HasSlowDown) penalty = "eine Slow-down-Strafe; reduziere das Tempo entsprechend der Spielanzeige";
            else if (p.NumOutstandingPenalties > pp.NumOutstandingPenalties) penalty = "eine Strafe";
            if (penalty != null)
            {
                Penalties++;
                result.Add(Say(TeamMember.Ben, "Wir haben " + penalty + " bekommen." +
                    (p.HasDriveThrough || p.HasStopAndGo ? " Sie muss in der Box abgesessen werden, ich behalte den Abstand nach hinten im Blick." : "")));
            }

            // 4. An invalid lap does not prove why it was invalidated. Only a mapped cut-warning
            // increase supports a track-limit comment; a warning need not invalidate the lap.
            int lap = current.SessionData.CompletedLaps;
            bool invalidated = previous.SessionData.CurrentLapIsValid && !current.SessionData.CurrentLapIsValid && !inPit &&
                !current.PitData.OnOutLap && !previous.PitData.OnOutLap && !current.SessionData.IsNewLap && lap != lastInvalidLap;
            if (invalidated)
            {
                lastInvalidLap = lap;
                InvalidLaps++;
            }
            if (p.CutTrackWarnings < pp.CutTrackWarnings) lastReportedCutWarnings = -1;
            bool limitWarning = !inPit && pp.CutTrackWarnings >= 0 && p.CutTrackWarnings > pp.CutTrackWarnings &&
                p.CutTrackWarnings != lastReportedCutWarnings;
            if (limitWarning)
            {
                lastReportedCutWarnings = p.CutTrackWarnings;
                TrackLimitWarnings++;
            }
            if (!minimalMode && (limitWarning || invalidated))
            {
                string text = limitWarning ? language.TrackLimit(TrackLimitWarnings > 1, invalidated)
                    : language.InvalidLap(InvalidLaps > 1);
                result.Add(Say(TeamMember.Mik, text));
            }
            return result;
        }

        /// <summary>Kurze Bilanz erfasster Ungültigkeiten, Cut-Warnungen, Verzögerungen und Strafen. Startet mit Leerzeichen.</summary>
        public string FinishSummary(int recordedInvalidLaps = -1)
        {
            var parts = new List<string>();
            int invalid = recordedInvalidLaps >= 0 ? recordedInvalidLaps : InvalidLaps;
            if (invalid > 0) parts.Add(invalid == 1 ? "eine ungültige Runde" : invalid + " ungültige Runden");
            if (TrackLimitWarnings > 0) parts.Add(TrackLimitWarnings == 1 ? "eine Tracklimit-Meldung" : TrackLimitWarnings + " Tracklimit-Meldungen");
            if (OffTracks > 0) parts.Add(OffTracks == 1 ? "ein Ausritt" : OffTracks + " Ausritte");
            if (Spins > 0) parts.Add(Spins == 1 ? "eine starke Verzögerung" : Spins + " starke Verzögerungen");
            if (Penalties > 0) parts.Add(Penalties == 1 ? "eine Strafe" : Penalties + " Strafen");
            if (parts.Count == 0)
            {
                // Absence of an observed warning does not prove a penalty/incident-free race.
                return string.Empty;
            }
            string list = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " und " + parts[parts.Count - 1];
            return " Bilanz: " + list + "." + (InvalidLaps + OffTracks + Spins + Penalties >= 4 ? " Da ist beim nächsten Mal noch einiges drin." : "");
        }

        /// <summary>Schadensbild in Worten, null wenn die Simulation keinen Schaden meldet bzw. kein Schadensmodell aktiv ist.</summary>
        public static string DamageReport(CarDamageData d)
        {
            var numeric = NumericDamageWatch.Report(d);
            if (!d.DamageEnabled) return numeric;
            var parts = new List<string>();
            if (numeric != null) parts.Add(numeric.TrimEnd('.'));
            bool serious = false;
            Add(parts, "Aero", d.OverallAeroDamage, d.AeroDamagePercent, ref serious);
            Add(parts, "Motor", d.OverallEngineDamage, d.EngineDamagePercent, ref serious);
            Add(parts, "Getriebe", d.OverallTransmissionDamage, d.TransmissionDamagePercent, ref serious);
            string where = CornerText(d.SuspensionDamageStatus, DamageLevel.DESTROYED) ?? CornerText(d.SuspensionDamageStatus, DamageLevel.MAJOR);
            if (where != null)
            {
                parts.Add("Aufhängung " + where + " stark beschädigt");
                serious = true;
            }
            else if ((where = CornerText(d.SuspensionDamageStatus, DamageLevel.MINOR)) != null)
            {
                parts.Add("Aufhängung " + where + " leicht beschädigt");
            }
            where = CornerText(d.BrakeDamageStatus, DamageLevel.DESTROYED) ?? CornerText(d.BrakeDamageStatus, DamageLevel.MAJOR);
            if (where != null)
            {
                parts.Add("Bremse " + where + " stark beschädigt");
                serious = true;
            }
            else if ((where = CornerText(d.BrakeDamageStatus, DamageLevel.MINOR)) != null)
                parts.Add("Bremse " + where + " leicht beschädigt");
            if (parts.Count == 0)
            {
                if(d.OverallAeroDamage==DamageLevel.UNKNOWN || d.OverallEngineDamage==DamageLevel.UNKNOWN || d.OverallTransmissionDamage==DamageLevel.UNKNOWN)
                    return "Für eine vollständige Schadensbewertung fehlen Messwerte.";
                return "Keine nennenswerten Schäden an Aero, Motor und Getriebe gemeldet. Bremsen und Aufhängung sind nur soweit vom Spiel gemeldet beurteilbar.";
            }
            string text = string.Join(", ", parts) + ".";
            return serious ? text + " Wenn sich das Auto komisch anfühlt, komm an die Box." : text;
        }

        public static bool DamageIncreased(CarDamageData before, CarDamageData now)
        {
            if (!now.DamageEnabled)
            {
                return false;
            }
            return Increased(before.OverallAeroDamage, now.OverallAeroDamage) || Increased(before.OverallEngineDamage, now.OverallEngineDamage) ||
                Increased(before.OverallTransmissionDamage, now.OverallTransmissionDamage) ||
                CornerDamageIncreased(before.SuspensionDamageStatus, now.SuspensionDamageStatus) ||
                CornerDamageIncreased(before.BrakeDamageStatus, now.BrakeDamageStatus);
        }

        private static bool CornerDamageIncreased(CornerData before, CornerData now)
        {
            for (int corner = 0; corner < 4; corner++)
                if (Increased(CornerLevel(before, corner), CornerLevel(now, corner))) return true;
            return false;
        }

        private static DamageLevel CornerLevel(CornerData data, int corner)
        {
            if (data == null) return DamageLevel.UNKNOWN;
            DamageLevel result = DamageLevel.UNKNOWN;
            foreach (var entry in data.cornersForEachStatus)
            {
                if (!(entry.Key is DamageLevel)) continue;
                int mask;
                switch (entry.Value)
                {
                    case CornerData.Corners.FRONT_LEFT: mask = 1; break;
                    case CornerData.Corners.FRONT_RIGHT: mask = 2; break;
                    case CornerData.Corners.REAR_LEFT: mask = 4; break;
                    case CornerData.Corners.REAR_RIGHT: mask = 8; break;
                    case CornerData.Corners.FRONTS: mask = 3; break;
                    case CornerData.Corners.REARS: mask = 12; break;
                    case CornerData.Corners.LEFTS: mask = 5; break;
                    case CornerData.Corners.RIGHTS: mask = 10; break;
                    case CornerData.Corners.ALL: mask = 15; break;
                    default: mask = 0; break;
                }
                if ((mask & (1 << corner)) != 0 && (DamageLevel)entry.Key > result) result = (DamageLevel)entry.Key;
            }
            return result;
        }

        // UNKNOWN (0) → bekannter Wert ist kein Schaden, sondern nur der erste gültige Messwert
        private static bool Increased(DamageLevel before, DamageLevel now)
        {
            return before != DamageLevel.UNKNOWN && now > before && now >= DamageLevel.MINOR;
        }

        private static void Add(List<string> parts, string name, DamageLevel level, float percent, ref bool serious)
        {
            string p = percent >= 1 ? " (" + Math.Round(percent) + " Prozent)" : "";
            switch (level)
            {
                case DamageLevel.MINOR: parts.Add(name + " leicht beschädigt" + p); break;
                case DamageLevel.MAJOR: parts.Add(name + " stark beschädigt" + p); serious = true; break;
                case DamageLevel.DESTROYED: parts.Add(name + " kaputt" + p); serious = true; break;
            }
        }

        public static bool IsSerious(CarDamageData d)
        {
            return d.OverallAeroDamage >= DamageLevel.MAJOR || d.OverallEngineDamage >= DamageLevel.MAJOR || d.OverallTransmissionDamage >= DamageLevel.MAJOR ||
                CornerText(d.SuspensionDamageStatus, DamageLevel.MAJOR) != null || CornerText(d.SuspensionDamageStatus, DamageLevel.DESTROYED) != null ||
                CornerText(d.BrakeDamageStatus, DamageLevel.MAJOR) != null || CornerText(d.BrakeDamageStatus, DamageLevel.DESTROYED) != null;
        }

        /// <summary>Welche Ecke(n) diesen Schadensgrad haben, auf Deutsch ("vorne links", "hinten"), null wenn keine.</summary>
        public static string CornerText(CornerData corners, DamageLevel level)
        {
            if (corners == null)
            {
                return null;
            }
            switch (corners.getCornersForStatus(level))
            {
                case CornerData.Corners.FRONT_LEFT: return "vorne links";
                case CornerData.Corners.FRONT_RIGHT: return "vorne rechts";
                case CornerData.Corners.REAR_LEFT: return "hinten links";
                case CornerData.Corners.REAR_RIGHT: return "hinten rechts";
                case CornerData.Corners.FRONTS: return "vorne";
                case CornerData.Corners.REARS: return "hinten";
                case CornerData.Corners.LEFTS: return "links";
                case CornerData.Corners.RIGHTS: return "rechts";
                case CornerData.Corners.ALL: return "rundum";
                default: return null;
            }
        }

        private static KeyValuePair<TeamMember, string> Say(TeamMember member, string text)
        {
            return new KeyValuePair<TeamMember, string>(member, text);
        }
    }
}
