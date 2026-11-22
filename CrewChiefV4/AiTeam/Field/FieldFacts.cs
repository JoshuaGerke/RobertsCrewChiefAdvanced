using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// Das Lagebild als Werkzeugantwort für die Sprach-KI (Rob 21.11.2026: „Kann ja nicht unbekannt sein, wir haben jede Zeit
    /// von allen Fahrern“). Alles direkt aus der Sim gemessen; Abstände in Sekunden sind über das Feldtempo gerechnet und als
    /// solche gekennzeichnet.
    /// </summary>
    internal static class FieldFacts
    {
        private static JToken N(float v, int digits = 3) => float.IsNaN(v) || float.IsInfinity(v) ? (JToken)JValue.CreateNull() : new JValue(Math.Round(v, digits));

        internal static string Label(FieldCarView c, bool names) => names && !string.IsNullOrWhiteSpace(c.Name) ? c.Name : "P" + c.Place;

        internal static string Motion(CarMotion m)
        {
            switch (m)
            {
                case CarMotion.Normal: return "fährt";
                case CarMotion.Slow: return "langsam";
                case CarMotion.Stopped: return "steht";
                case CarMotion.Spun: return "dreher";
                case CarMotion.InPit: return "box";
                case CarMotion.Finished: return "im_ziel";
                case CarMotion.Retired: return "ausgefallen";
                default: return "unbekannt";
            }
        }

        private static JObject Row(FieldCarView c, bool names, DateTime at) => new JObject
        {
            ["fahrer"] = Label(c, names), ["platz"] = c.Place, ["klassenplatz"] = c.PlaceClass,
            ["abstand_s"] = N(c.GapS, 2), ["abstand_m"] = N(c.GapM, 0), ["runden_unterschied"] = c.LapDelta,
            ["zustand"] = Motion(c.Motion), ["tempo_kmh"] = N(c.Speed * 3.6f, 0),
            ["daten_alter_ms"] = Math.Round(c.DataAgeMs(at)), ["fortgeschrieben"] = c.Extrapolated
        };

        internal static JObject ToJson(FieldView v, DateTime now, bool names)
        {
            if (v == null) return new JObject { ["verfuegbar"] = false, ["grund"] = "Kein Lagebild direkt aus der Sim (RaceRoom, AMS2, LMU, AC mit Crew-Chief-Plugin, AC EVO, iRacing) oder Sim nicht aktiv." };
            var p = v.Player;
            var o = new JObject
            {
                ["verfuegbar"] = true, ["quelle"] = v.Sim + " direkt (Speicher der Sim, Abtastung alle 50 ms)",
                ["neue_bilder_hz"] = double.IsNaN(v.MeasuredHz) ? (JToken)JValue.CreateNull() : Math.Round(v.MeasuredHz, 1), ["alter_s"] = Math.Round(Math.Max(0, (now - v.Utc).TotalSeconds), 2),
                ["strecke"] = v.Track, ["layout"] = v.Layout, ["laenge_m"] = N(v.TrackLength, 0),
                ["session"] = v.Session.ToString(), ["phase"] = v.Phase.ToString(), ["im_menue"] = v.InMenus,
                ["restzeit_s"] = v.TimedSession ? N(v.TimeRemaining, 0) : JValue.CreateNull(),
                ["sessiondauer_s"] = N(v.TimeDuration, 0), ["rundenzahl_session"] = v.SessionLaps > 0 ? (JToken)v.SessionLaps : JValue.CreateNull(),
                ["autos_im_feld"] = Math.Max(v.NumCars, v.MaxPlace),
                ["abstaende_hinweis"] = "abstand_s über das gemessene Feldtempo an jeder Stelle der Strecke gerechnet (wie eine Relativ-Anzeige); positiv = vor Rob.",
                ["feldtempo_gelernt_anteil"] = Math.Round(v.ProfileCoverage, 2)
            };
            if (p == null) { o["rob"] = JValue.CreateNull(); return o; }
            o["rob"] = new JObject
            {
                ["platz"] = p.Place, ["klassenplatz"] = p.PlaceClass, ["runden"] = p.CompletedLaps, ["in_box"] = p.InPit,
                ["restrunden_inkl_laufender"] = v.PlayerLapsLeft >= 0 ? (JToken)v.PlayerLapsLeft : JValue.CreateNull(),
                ["letzte_runde"] = v.PlayerOnLastLap,
                ["beste_gueltige_runde_s"] = N(p.BestLap), ["schnellste_runde_auch_gestrichen_s"] = N(p.FastestAnyLap),
                ["letzte_runde_s"] = N(p.LastLap), ["letzte_runde_gueltig"] = float.IsNaN(p.LastLap) ? (JToken)JValue.CreateNull() : p.LastLapValid,
                ["hoechsttempo_kmh"] = N(p.TopSpeed * 3.6f, 1), ["hoechsttempo_bei_m"] = N(p.TopSpeedAtM, 0),
                ["zurueckzugebende_plaetze"] = v.PositionsToGiveBack >= 0 ? (JToken)v.PositionsToGiveBack : JValue.CreateNull()
            };
            o["vor_rob"] = new JArray(v.Ahead(4).Select(c => Row(c, names, v.Utc)));
            o["hinter_rob"] = new JArray(v.Behind(4).Select(c => Row(c, names, v.Utc)));
            var ahead = v.Ahead(1).FirstOrDefault(); var behind = v.Behind(1).FirstOrDefault();
            o["freie_runde"] = new JObject
            {
                ["vorne_frei"] = ahead == null || ahead.GapS > 3, ["hinten_frei"] = behind == null || behind.GapS < -2,
                ["hinweis"] = "frei = vorne mehr als 3 s, hinten mehr als 2 s Luft auf der Strecke"
            };
            o["gefahren_auf_strecke"] = new JArray(v.Cars.Where(c => !c.IsPlayer && (c.Motion == CarMotion.Slow || c.Motion == CarMotion.Stopped || c.Motion == CarMotion.Spun))
                .OrderBy(c => c.GapM < 0 ? c.GapM + v.TrackLength : c.GapM).Take(6).Select(c => Row(c, names, v.Utc)));

            // Zeitenliste: beste gültige Runde je Fahrer; Robs schnellste (auch gestrichene) dagegen eingeordnet.
            var timed = v.Cars.Where(c => !float.IsNaN(c.BestLap) && c.BestLap > 0).OrderBy(c => c.BestLap).ToList();
            o["bestzeiten"] = new JArray(timed.Take(10).Select((c, i) => new JObject { ["rang"] = i + 1, ["fahrer"] = Label(c, names), ["zeit_s"] = N(c.BestLap) }));
            float mine = !float.IsNaN(p.BestLap) ? p.BestLap : p.FastestAnyLap;
            if (!float.IsNaN(mine) && timed.Count > 0)
            {
                int wouldBe = timed.Count(c => !c.IsPlayer && c.BestLap < mine) + 1;
                var gaps = new JObject();
                foreach (int rank in new[] { 1, 3, 5 })
                    if (timed.Count >= rank) gaps["p" + rank] = N(mine - timed[rank - 1].BestLap);
                o["robs_zeit_im_vergleich"] = new JObject
                {
                    ["verglichene_zeit_s"] = N(mine), ["zeit_ist_gestrichen"] = float.IsNaN(p.BestLap),
                    ["waere_rang"] = wouldBe, ["rueckstand_s"] = gaps
                };
            }
            var fastest = v.Cars.Where(c => !float.IsNaN(c.TopSpeed)).OrderByDescending(c => c.TopSpeed).ToList();
            o["hoechstgeschwindigkeiten"] = new JArray(fastest.Take(8).Select((c, i) => new JObject
            {
                ["rang"] = i + 1, ["fahrer"] = Label(c, names), ["kmh"] = N(c.TopSpeed * 3.6f, 1), ["bei_m"] = N(c.TopSpeedAtM, 0)
            }));
            int robSpeedRank = fastest.FindIndex(c => c.IsPlayer);
            o["robs_hoechsttempo_rang"] = robSpeedRank >= 0 ? (JToken)(robSpeedRank + 1) : JValue.CreateNull();
            o["beste_sektoren"] = new JObject
            {
                ["s1"] = Best(v, c => c.BestS1, names), ["s2"] = Best(v, c => c.BestS2, names), ["s3"] = Best(v, c => c.BestS3, names),
                ["rob"] = new JObject { ["s1"] = N(p.BestS1), ["s2"] = N(p.BestS2), ["s3"] = N(p.BestS3) }
            };
            return o;
        }

        private static JToken Best(FieldView v, Func<FieldCarView, float> pick, bool names)
        {
            var best = v.Cars.Where(c => !float.IsNaN(pick(c)) && pick(c) > 0).OrderBy(pick).FirstOrDefault();
            return best == null ? (JToken)JValue.CreateNull() : new JObject { ["fahrer"] = Label(best, names), ["zeit_s"] = N(pick(best)) };
        }
    }
}
