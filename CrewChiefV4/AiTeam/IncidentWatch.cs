using System;
using System.Collections.Generic;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Reaktion des Teams auf Zwischenfälle, ohne KI und deshalb sofort:
    /// Kontakt (Ben meldet sich, wenige Sekunden später Schadensbild aus der Telemetrie),
    /// Dreher/Stillstand (Hinweis auf den Verkehr), neue Strafe (Mik), ungültige Runde (Ben).
    /// Liest nur den GameState, Texte enthalten nur Werte aus der Telemetrie.
    /// </summary>
    public class IncidentWatch
    {
        public static readonly TimeSpan DamageCheckDelay = TimeSpan.FromSeconds(3);

        private DateTime lastContactUtc = DateTime.MinValue;
        private DateTime pendingDamageCheckUtc = DateTime.MinValue;
        private DateTime lastFastUtc = DateTime.MinValue;
        private DateTime lastStopUtc = DateTime.MinValue;
        private int lastInvalidLap = -1;

        public void Reset()
        {
            lastContactUtc = pendingDamageCheckUtc = lastFastUtc = lastStopUtc = DateTime.MinValue;
            lastInvalidLap = -1;
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

            // 1. Kontakt: Einschlagzeitpunkt hat sich geändert (AMS2, iRacing, rFactor 2 …) oder – für Spiele ohne
            //    Einschlagzeitpunkt wie RaceRoom und Assetto Corsa – ein Schadenswert ist gestiegen
            float impact = current.CarDamageData.LastImpactTime;
            bool impactChanged = impact > 0 && impact != previous.CarDamageData.LastImpactTime;
            if ((impactChanged || DamageIncreased(previous.CarDamageData, current.CarDamageData)) && !inPit)
            {
                if (nowUtc - lastContactUtc > TimeSpan.FromSeconds(15))
                {
                    result.Add(Say(TeamMember.Ben, "Kontakt. Ich schau mir den Schaden an."));
                }
                lastContactUtc = nowUtc;
                pendingDamageCheckUtc = nowUtc + DamageCheckDelay;
            }
            if (pendingDamageCheckUtc != DateTime.MinValue && nowUtc >= pendingDamageCheckUtc)
            {
                pendingDamageCheckUtc = DateTime.MinValue;
                string report = DamageReport(current.CarDamageData);
                if (report != null)
                {
                    result.Add(Say(TeamMember.Ben, report));
                }
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
                result.Add(Say(TeamMember.Ben, text));
            }

            // 3. Neue Strafe
            var p = current.PenaltiesData;
            var pp = previous.PenaltiesData;
            string penalty = null;
            if (p.HasDriveThrough && !pp.HasDriveThrough) penalty = "eine Durchfahrtsstrafe";
            else if (p.HasStopAndGo && !pp.HasStopAndGo) penalty = "eine Stop-and-Go-Strafe";
            else if (p.HasTimeDeduction && !pp.HasTimeDeduction) penalty = "eine Zeitstrafe";
            else if (p.HasSlowDown && !pp.HasSlowDown) penalty = "eine Slow-down-Strafe, lass den Wagen hinter dir vorbei";
            else if (p.NumOutstandingPenalties > pp.NumOutstandingPenalties) penalty = "eine Strafe";
            if (penalty != null)
            {
                result.Add(Say(TeamMember.Mik, "Wir haben " + penalty + " bekommen." +
                    (p.HasDriveThrough || p.HasStopAndGo ? " Sie muss in der Box abgesessen werden, ich behalte den Abstand nach hinten im Blick." : "")));
            }

            // 4. Runde ungültig (Track Limits), nicht im Funkmodus Minimal
            int lap = current.SessionData.CompletedLaps;
            if (!minimalMode && previous.SessionData.CurrentLapIsValid && !current.SessionData.CurrentLapIsValid && !inPit && lap != lastInvalidLap)
            {
                lastInvalidLap = lap;
                result.Add(Say(TeamMember.Ben, "Die Runde ist ungültig, Streckenbegrenzung."));
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
            Add(parts, "Aero", d.OverallAeroDamage, ref serious);
            Add(parts, "Motor", d.OverallEngineDamage, ref serious);
            Add(parts, "Getriebe", d.OverallTransmissionDamage, ref serious);
            if (HasCornerDamage(d.SuspensionDamageStatus, DamageLevel.MAJOR) || HasCornerDamage(d.SuspensionDamageStatus, DamageLevel.DESTROYED))
            {
                parts.Add("Aufhängung stark beschädigt");
                serious = true;
            }
            else if (HasCornerDamage(d.SuspensionDamageStatus, DamageLevel.MINOR))
            {
                parts.Add("Aufhängung leicht beschädigt");
            }
            if (HasCornerDamage(d.BrakeDamageStatus, DamageLevel.MAJOR) || HasCornerDamage(d.BrakeDamageStatus, DamageLevel.DESTROYED))
            {
                parts.Add("Bremse stark beschädigt");
                serious = true;
            }
            if (parts.Count == 0)
            {
                return "Kein nennenswerter Schaden, weiter.";
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

        private static void Add(List<string> parts, string name, DamageLevel level, ref bool serious)
        {
            switch (level)
            {
                case DamageLevel.MINOR: parts.Add(name + " leicht beschädigt"); break;
                case DamageLevel.MAJOR: parts.Add(name + " stark beschädigt"); serious = true; break;
                case DamageLevel.DESTROYED: parts.Add(name + " kaputt"); serious = true; break;
            }
        }

        private static bool HasCornerDamage(CornerData corners, DamageLevel level)
        {
            return corners != null && corners.getCornersForStatus(level) != CornerData.Corners.NONE;
        }

        private static KeyValuePair<TeamMember, string> Say(TeamMember member, string text)
        {
            return new KeyValuePair<TeamMember, string>(member, text);
        }
    }
}
