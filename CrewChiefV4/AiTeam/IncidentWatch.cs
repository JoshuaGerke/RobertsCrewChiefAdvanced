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
        private int invalidCount;
        private int positionBeforeIncident;
        private DamageAssessment assessment;
        // AMS2 meldet gelegentlich kurz Platz 1 (Spieler taucht als Gegner auf) → Median statt Einzelwert
        private readonly List<KeyValuePair<DateTime, int>> recentPositions = new List<KeyValuePair<DateTime, int>>();

        public void Reset()
        {
            lastContactUtc = pendingDamageCheckUtc = lastFastUtc = lastStopUtc = DateTime.MinValue;
            lastInvalidLap = -1;
            invalidCount = 0;
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
            if (previous == null || current == null || current.SessionData.SessionPhase != SessionPhase.Green)
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
            if ((impactChanged || DamageIncreased(previous.CarDamageData, current.CarDamageData)) && !inPit)
            {
                if (nowUtc - lastContactUtc > TimeSpan.FromSeconds(15))
                {
                    result.Add(Say(TeamMember.Mik, "Kontakt. Ich schau mir den Schaden an."));
                }
                if (positionBeforeIncident <= 0)
                {
                    positionBeforeIncident = StablePosition(previous.SessionData.ClassPosition);
                }
                lastContactUtc = nowUtc;
                pendingDamageCheckUtc = nowUtc + DamageCheckDelay;
            }
            // Bericht erst, wenn der Wagen wieder auf Tempo ist (dann stehen Schaden und Position fest), spätestens nach 25 s
            if (pendingDamageCheckUtc != DateTime.MinValue && nowUtc >= pendingDamageCheckUtc &&
                (kmh > 50 || nowUtc - lastContactUtc > DamageCheckMaxDelay))
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

            // 2. Dreher oder Stillstand: eben noch schnell, jetzt fast stehend, außerhalb der Box
            if (kmh > 70)
            {
                lastFastUtc = nowUtc;
            }
            else if (kmh < 10 && !inPit && nowUtc - lastFastUtc < TimeSpan.FromSeconds(4) && nowUtc - lastStopUtc > TimeSpan.FromSeconds(30))
            {
                lastStopUtc = nowUtc;
                string text = "Alles okay? Beim Wiederanfahren auf den Verkehr achten.";
                float behind = current.SessionData.TimeDeltaBehind;
                if (behind > 0 && behind < 15)
                {
                    text += " Der Nächste war zuletzt etwa " + SaraLocal.Seconds(behind) + " hinter dir.";
                }
                result.Add(Say(TeamMember.Mik, text));
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
                result.Add(Say(TeamMember.Ben, "Wir haben " + penalty + " bekommen." +
                    (p.HasDriveThrough || p.HasStopAndGo ? " Sie muss in der Box abgesessen werden, ich behalte den Abstand nach hinten im Blick." : "")));
            }

            // 4. Runde ungültig (Track Limits), nicht im Funkmodus Minimal
            int lap = current.SessionData.CompletedLaps;
            if (!minimalMode && previous.SessionData.CurrentLapIsValid && !current.SessionData.CurrentLapIsValid && !inPit && lap != lastInvalidLap)
            {
                lastInvalidLap = lap;
                result.Add(Say(TeamMember.Mik, CrewSpeechPolicy.TrackLimitPhrases[invalidCount++ % CrewSpeechPolicy.TrackLimitPhrases.Length]));
            }
            return result;
        }

        /// <summary>Schadensbild in Worten, null wenn die Simulation keinen Schaden meldet bzw. kein Schadensmodell aktiv ist.</summary>
        public static string DamageReport(CarDamageData d)
        {
            if (!d.DamageEnabled)
            {
                return null;
            }
            var parts = new List<string>();
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
            return serious ? text + " Wenn sich das Auto komisch anfühlt, komm an die Box." : text + " Sollte fahrbar sein.";
        }

        public static bool DamageIncreased(CarDamageData before, CarDamageData now)
        {
            if (!now.DamageEnabled)
            {
                return false;
            }
            return Increased(before.OverallAeroDamage, now.OverallAeroDamage) || Increased(before.OverallEngineDamage, now.OverallEngineDamage) ||
                Increased(before.OverallTransmissionDamage, now.OverallTransmissionDamage);
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
