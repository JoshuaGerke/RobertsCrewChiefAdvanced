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

        /// <summary>RaceRoom 27.11.: „Rang 2, 0,0 Sekunden Rückstand“ – dieselbe Runde lag mit 83,88214 statt 83,88257 im Feld.
        /// Zeiten innerhalb von 5 ms gelten als gleich.</summary>
        internal const float SameTimeS = .005f;

        /// <summary>Mehr als eine Klasse mit gezeiteten Runden im Feld.</summary>
        internal static bool Multiclass(FieldView v) =>
            v?.Cars != null && v.Cars.Where(c => c != null && !float.IsNaN(c.BestLap) && c.BestLap > 0 && c.ClassId >= 0).Select(c => c.ClassId).Distinct().Count() > 1;

        /// <summary>Zeitenliste für Robs Vergleich: im Mehrklassenfeld nur seine Klasse (RaceRoom Laguna 27.11.: „Platz 1“ bzw.
        /// „Rang 2“, Rob sah Platz 2 gesamt und war Klassenerster).</summary>
        internal static List<FieldCarView> Timesheet(FieldView v, bool ownClassOnly)
        {
            var p = v?.Player;
            if (v?.Cars == null || p == null) return new List<FieldCarView>();
            bool cls = ownClassOnly && p.ClassId >= 0 && Multiclass(v);
            return v.Cars.Where(c => c != null && !float.IsNaN(c.BestLap) && c.BestLap > 0 && (!cls || c.ClassId == p.ClassId)).OrderBy(c => c.BestLap).ToList();
        }

        /// <summary>Rang einer Zeit in der Zeitenliste (andere Fahrer, die mehr als 5 ms schneller sind, plus eins).</summary>
        internal static int RankOf(IEnumerable<FieldCarView> timed, float mine) => timed.Count(c => !c.IsPlayer && c.BestLap < mine - SameTimeS) + 1;

        /// <summary>Spielplatz ist ein echter Platz (iRacing meldet ohne Zeit den Platzhalter 1000).</summary>
        internal static bool RealPlace(int place) => place > 0 && place < 200;

        /// <summary>
        /// iRacing Navarra 28.11. (N5): „Deine Marke 1:46,404, das ist Platz 5“, 12 s später „Quali durch, Platz 10“ – Startplatz war
        /// 10. Die Zeitenliste im Lagebild hatte noch nicht alle Zeiten. Der Rang aus der Zeitenliste gilt nur, wenn sie mindestens
        /// so viele Zeiten hat, wie der Spielplatz zählt; sonst der Spielplatz. Ohne echten Spielplatz (N1: 1000) gilt die Liste.
        /// </summary>
        internal static int TrustedRank(IList<FieldCarView> timed, float mine, int gamePlace)
        {
            if (timed == null || timed.Count == 0) return RealPlace(gamePlace) ? gamePlace : 0;
            int rank = RankOf(timed, mine);
            return !RealPlace(gamePlace) || timed.Count >= gamePlace ? rank : gamePlace;
        }

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
            // IR5 (iRacing): Wer hat seinen Fast Repair schon verbraucht? Das heißt, er hatte Schaden.
            var repaired = v.Cars.Where(c => !c.IsPlayer && c.FastRepairsUsed > 0).Take(12).ToList();
            if (repaired.Count > 0)
                o["fast_repair_genutzt"] = new JArray(repaired.Select(c => new JObject { ["fahrer"] = Label(c, names), ["anzahl"] = c.FastRepairsUsed }));

            // Zeitenliste: beste gültige Runde je Fahrer; Robs schnellste (auch gestrichene) dagegen eingeordnet.
            var timed = v.Cars.Where(c => !float.IsNaN(c.BestLap) && c.BestLap > 0).OrderBy(c => c.BestLap).ToList();
            o["bestzeiten"] = new JArray(timed.Take(10).Select((c, i) => new JObject { ["rang"] = i + 1, ["fahrer"] = Label(c, names), ["zeit_s"] = N(c.BestLap) }));
            float mine = !float.IsNaN(p.BestLap) ? p.BestLap : p.FastestAnyLap;
            // Gestrichenes Rundenstück (Mantorp 22.11.: 28 s auf 3,1 km) nicht als Zeit einordnen.
            if (float.IsNaN(p.BestLap) && !float.IsNaN(mine) && (!FieldPicture.PlausibleLap(mine, v.TrackLength) || timed.Count > 0 && mine < timed[0].BestLap * .9f))
                mine = float.NaN;
            if (!float.IsNaN(mine) && timed.Count > 0)
            {
                var own = Timesheet(v, true);
                bool multi = Multiclass(v);
                var gaps = new JObject();
                foreach (int rank in new[] { 1, 3, 5 })
                    if (own.Count >= rank) gaps["p" + rank] = N(Math.Max(0, mine - own[rank - 1].BestLap));
                o["robs_zeit_im_vergleich"] = new JObject
                {
                    ["verglichene_zeit_s"] = N(mine), ["zeit_ist_gestrichen"] = float.IsNaN(p.BestLap),
                    ["waere_rang"] = RankOf(own, mine), ["rang_bezieht_sich_auf"] = multi ? "eigene Klasse" : "ganzes Feld",
                    ["waere_rang_gesamt"] = multi ? (JToken)RankOf(timed, mine) : JValue.CreateNull(), ["rueckstand_s"] = gaps
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
