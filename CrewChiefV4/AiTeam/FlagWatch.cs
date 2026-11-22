using System;
using System.Collections.Generic;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Flaggen und Gefahren auf Deutsch durch Sara, lokal und ohne KI. Ersetzt im Modus "Nur noch das KI-Team spricht"
    /// die englischen Flaggen-Ansagen von Crew Chief. Quelle sind die Flaggendaten, die Crew Chief aus dem Spiel liest.
    /// </summary>
    public class FlagWatch
    {
        private static readonly string[] PhrasesDe =
        {
            "Gelb! Nicht überholen.", "Gelb aufgehoben.", "Gelb voraus.", "Doppelt Gelb, Vorsicht.", "Blaue Flagge, lass ihn vorbei.",
            "Schwarze Flagge.", "Full Course Yellow, Tempo runter.", "Grün, wieder freie Fahrt.", "Unfall voraus!",
        };
        private static readonly string[] PhrasesEn =
        {
            "Yellow! No overtaking.", "Yellow lifted.", "Yellow ahead.", "Double yellow, careful.", "Blue flag, let him by.",
            "Black flag.", "Full course yellow, slow down.", "Green, track is clear.", "Incident ahead!",
        };
        private static readonly string[] PhrasesEs =
        {
            "¡Amarilla! No adelantes.", "Amarilla retirada.", "Amarilla delante.", "Doble amarilla, cuidado.", "Bandera azul, déjale pasar.",
            "Bandera negra.", "Full course yellow, baja el ritmo.", "Verde, vía libre.", "¡Accidente delante!",
        };
        // Index in all three arrays: 0 local yellow, 1 yellow clear, 2 sector yellow, 3 double yellow, 4 blue, 5 black, 6 FCY, 7 green, 8 incident.
        public static string[] Phrases => RadioLanguage.Pick(PhrasesDe, PhrasesEn, PhrasesEs);

        // LMU 22.11. (Rob): „Ein ganz kurzes Rob, Gelb – das reicht. Gelb aufgehoben auch kurz.“ Die früheren, längeren Sätze
        // (vor diesem Build vorbereitet oder in der Chronik) werden weiter erkannt.
        private static readonly string[] LegacyPhrases =
        {
            "Gelb! Langsam, nicht überholen.", "Gelb vorbei.", "Gelb im Sektor.",
            "Yellow! Slow down, no overtaking.", "Yellow's clear.", "Yellow in the sector.",
            "¡Amarilla! Despacio, no adelantes.", "Fin de amarilla.", "Amarilla en el sector.",
        };

        // A phrase offered before a language switch is still recognised.
        private static int IndexOf(string phrase)
        {
            int i = Array.IndexOf(PhrasesDe, phrase);
            if (i < 0) i = Array.IndexOf(PhrasesEn, phrase);
            if (i < 0) i = Array.IndexOf(PhrasesEs, phrase);
            if (i < 0) { int legacy = Array.IndexOf(LegacyPhrases, phrase); if (legacy >= 0) i = legacy % 3; }
            return i;
        }

        private DateTime lastIncidentCallUtc = DateTime.MinValue;
        private DateTime lastBlueUtc = DateTime.MinValue;
        private DateTime localYellowSinceUtc = DateTime.MinValue;
        internal static readonly TimeSpan ShortYellow = TimeSpan.FromSeconds(4);
        /// <summary>
        /// AC Monza 21.11.: Die Sektor-Gelbflagge stand in 10 von 15 Fällen unter 1,5 s (0,3–1,3 s) und wurde jedes Mal
        /// angesagt; das einzige lange Gelb (25 s ab 14:50:36) ging dabei unter. Sektor-Gelb gilt erst, wenn es 1,5 s steht.
        /// </summary>
        internal static readonly TimeSpan SectorYellowConfirm = TimeSpan.FromSeconds(1.5);
        private DateTime sectorYellowSinceUtc = DateTime.MinValue;
        private bool sectorYellowAnnounced;
        /// <summary>
        /// LMU 22.11.: „Gelb kommt nie.“ rF2/LMU (auch rF1, GTR2, ACC, R3E) melden Gelb nur je Sektor (<see cref="FlagData.sectorFlags"/>),
        /// nicht als Flagge des eigenen Wagens. Crew Chief sagt das nur im Ein-Stimmen-Modus an; im KI-Modus kam es so gut wie nie
        /// (4 Mal in 85 Minuten, das „wieder grün“ nie). Sara verfolgt die Sektoren jetzt selbst.
        /// </summary>
        private DateTime sectorAnnouncedUtc = DateTime.MinValue, sectorClearSinceUtc = DateTime.MinValue;
        private int announcedSector = -1;
        /// <summary>So lange muss ein angesagtes Sektor-Gelb grün sein, bevor Sara „Gelb aufgehoben“ sagt (kein Flackern).</summary>
        internal static readonly TimeSpan SectorClearConfirm = TimeSpan.FromSeconds(2);
        /// <summary>Später als das kommt kein „aufgehoben“ mehr – dann ist Rob längst woanders.</summary>
        internal static readonly TimeSpan SectorClearRelevantFor = TimeSpan.FromSeconds(120);

        /// <summary>
        /// RaceRoom Mantorp 21.11., 15:33:05: „Gelb vorbei.“ schnitt Miks „Alles klar? Ganz ruhig …“ nach dem Dreher ab.
        /// Entwarnungen (Gelb vorbei, wieder Grün) sind Auskunft, keine Gefahr: Sie warten, statt zu unterbrechen.
        /// </summary>
        internal static int Priority(string phrase) { int i = IndexOf(phrase); return i == 1 || i == 7 ? 6 : 10; }

        internal static bool SectorYellowConfirmed(DateTime since, DateTime now) =>
            since != DateTime.MinValue && now >= since && now - since >= SectorYellowConfirm;

        public void Reset()
        {
            lastIncidentCallUtc = lastBlueUtc = localYellowSinceUtc = sectorYellowSinceUtc = DateTime.MinValue;
            sectorAnnouncedUtc = sectorClearSinceUtc = DateTime.MinValue;
            announcedSector = -1;
            sectorYellowAnnounced = false;
        }

        internal static bool BlueCoveredByClassCall(DateTime now) =>
            now >= MulticlassTraffic.LastRearClassCallUtc && now - MulticlassTraffic.LastRearClassCallUtc <= MulticlassTraffic.BlueCoveredFor;

        internal static bool FreshClear(string phrase, DateTime detected, DateTime now)
        {
            int i = IndexOf(phrase);
            return (i != 1 && i != 7) ||
                (now >= detected && now - detected <= TimeSpan.FromSeconds(3));
        }

        /// <summary>Sprachunabhängige Art einer Flaggenansage für die Rennchronik.</summary>
        internal static string Kind(string phrase)
        {
            switch (IndexOf(phrase))
            {
                case 0: return "gelb_lokal";
                case 2: return "gelb_sektor";
                case 3: return "doppelt_gelb";
                case 8: return "unfall_voraus";
                default: return "flagge";
            }
        }

        /// <summary>Gelb, Doppelt Gelb und Unfall voraus nennen den Betroffenen, wenn er belegt ist (AC/RaceRoom 20.11.).</summary>
        internal static bool NamesIncident(string phrase)
        {
            int i = IndexOf(phrase);
            return i == 0 || i == 2 || i == 3 || i == 8;
        }

        /// <summary>
        /// Wer ist betroffen? Zuerst die gemessene Gefahr voraus der letzten 20 s (Dreher, steht, bricht ein), sonst das nächste
        /// langsame Auto bis 300 m vor uns. Nur mit erlaubtem Namen; ohne Beleg null.
        /// </summary>
        internal static string IncidentName(GameStateData gs, DateTime now) =>
            gs == null ? null : RaceTrafficWatch.RecentHazard(gs.SessionData.SessionId, now, TimeSpan.FromSeconds(20)) ?? IncidentDriver(gs);

        /// <summary>Wer steht am Unfall voraus? Nächstes langsames Auto bis 300 m vor uns, nur mit erlaubtem Namen.</summary>
        internal static string IncidentDriver(GameStateData gs)
        {
            if (gs == null || gs.OpponentData == null || gs.SessionData.TrackDefinition == null) return null;
            float length = gs.SessionData.TrackDefinition.trackLength, own = gs.PositionAndMotionData.DistanceRoundTrack;
            if (!(length > 100) || float.IsNaN(own)) return null;
            OpponentData best = null; float bestGap = 301;
            foreach (var o in gs.OpponentData.Values)
            {
                // iRacing meldet je Auto, ob es neben der Strecke ist (trackSurface 0) – dann zählt es auch bei mehr Tempo.
                bool offTrack = o != null && o.trackSurface == 0;
                if (o == null || !o.IsActive || o.InPits || !o.CanUseName || !(o.Speed >= 0) || o.Speed > 20 && !offTrack) continue;
                float gap = ((o.DistanceRoundTrack - own) % length + length) % length;
                if (gap >= 5 && gap < bestGap) { bestGap = gap; best = o; }
            }
            return best == null ? null : RadioPhraseCatalog.SafeRivalName(best.DriverRawName);
        }

        /// <summary>
        /// Gelber Sektor, den Rob gerade fährt oder als nächsten erreicht (Index 0–2, sonst -1). Unbekannter eigener Sektor:
        /// jeder gelbe Sektor zählt. Sektorzählung wie in Crew Chiefs Flaggenmonitor (Index i = Sektor i+1).
        /// </summary>
        internal static int YellowSectorAhead(GameStateData gs)
        {
            if (gs == null) return -1;
            var flags = gs.FlagData.sectorFlags;
            int own = gs.SessionData.SectorNumber;
            for (int i = 0; i < flags.Length && i < 3; i++)
            {
                if (flags[i] != FlagEnum.YELLOW && flags[i] != FlagEnum.DOUBLE_YELLOW) continue;
                if (own < 1 || own > 3) return i;
                int next = own == 3 ? 1 : own + 1;
                if (i + 1 == own || i + 1 == next) return i;
            }
            return -1;
        }

        internal static bool AnySectorYellow(GameStateData gs)
        {
            if (gs == null) return false;
            foreach (var f in gs.FlagData.sectorFlags) if (f == FlagEnum.YELLOW || f == FlagEnum.DOUBLE_YELLOW) return true;
            return false;
        }

        /// <summary>Gelb für Rob: Flagge des eigenen Wagens oder ein gelber Sektor jetzt bzw. direkt voraus.</summary>
        internal static bool YellowForPlayer(GameStateData gs) =>
            gs != null && (gs.SessionData.Flag == FlagEnum.YELLOW || YellowSectorAhead(gs) >= 0);

        public static bool StillRelevant(string phrase, GameStateData gs)
        {
            // Die schwarze Flagge ist Pflicht: Versetzt die Sim Rob danach in die Box, bleibt die Ansage trotzdem stehen.
            if (IndexOf(phrase) == 5 && RaceExclusion.Active(gs)) return true;
            if (gs == null || gs.PitData.InPitlane || (gs.SessionData.SessionPhase != SessionPhase.Green && gs.SessionData.SessionPhase != SessionPhase.FullCourseYellow)) return false;
            var f=gs.FlagData;
            switch (IndexOf(phrase))
            {
                case 8: return f.distanceToNearestIncident > 0 && f.distanceToNearestIncident < 250;
                case 0: return f.isLocalYellow;
                case 1: return !f.isLocalYellow && !f.isFullCourseYellow && gs.SessionData.Flag != FlagEnum.YELLOW && gs.SessionData.Flag != FlagEnum.DOUBLE_YELLOW &&
                        YellowSectorAhead(gs) < 0;
                case 2: return YellowForPlayer(gs);
                case 3: return gs.SessionData.Flag == FlagEnum.DOUBLE_YELLOW;
                case 4: return gs.SessionData.Flag == FlagEnum.BLUE;
                case 5: return gs.SessionData.Flag == FlagEnum.BLACK;
                case 6: return f.isFullCourseYellow;
                case 7: return !f.isFullCourseYellow && !f.isLocalYellow;
                default: return false;
            }
        }

        public List<string> Evaluate(GameStateData previous, GameStateData current, DateTime nowUtc)
        {
            var result = new List<string>();
            if (previous == null || current == null || current.PitData.InPitlane)
            {
                return result;
            }
            var phase = current.SessionData.SessionPhase;
            if (phase != SessionPhase.Green && phase != SessionPhase.FullCourseYellow)
            {
                return result;
            }
            var f = current.FlagData;
            var pf = previous.FlagData;

            // Full Course Yellow / Safety Car
            if (f.isFullCourseYellow && !pf.isFullCourseYellow)
            {
                result.Add(Phrases[6]);
                return result;
            }
            if (!f.isFullCourseYellow && pf.isFullCourseYellow)
            {
                result.Add(Phrases[7]);
                return result;
            }

            // Unfall unmittelbar voraus (innerhalb 250 m), höchstens alle 20 s
            float d = f.distanceToNearestIncident;
            float pd = pf.distanceToNearestIncident;
            if (d > 0 && d < 250 && (pd <= 0 || pd >= 250) && nowUtc - lastIncidentCallUtc > TimeSpan.FromSeconds(20))
            {
                lastIncidentCallUtc = nowUtc;
                result.Add(Phrases[8]);
            }

            // lokale Gelbphase
            if (f.isLocalYellow && !pf.isLocalYellow)
            {
                localYellowSinceUtc = nowUtc;
                result.Add(Phrases[0]);
            }
            else if (!f.isLocalYellow && pf.isLocalYellow)
            {
                // RaceRoom Monza 17.11.: „Gelb!“ und zwei Sekunden später „Gelb vorbei“ im Minutentakt. Ein Gelb, das kürzer als
                // vier Sekunden stand, braucht kein eigenes „vorbei“.
                if (localYellowSinceUtc == DateTime.MinValue || nowUtc - localYellowSinceUtc >= ShortYellow) result.Add(Phrases[1]);
                localYellowSinceUtc = DateTime.MinValue;
            }

            // Flagge für den eigenen Wagen oder gelber Sektor jetzt/voraus (rF2, LMU, rF1, GTR2, ACC, R3E melden Gelb je Sektor)
            var flag = current.SessionData.Flag;
            var prevFlag = previous.SessionData.Flag;
            int yellowSector = YellowSectorAhead(current);
            bool yellowNow = flag == FlagEnum.YELLOW || yellowSector >= 0;
            if (!yellowNow) { sectorYellowSinceUtc = DateTime.MinValue; sectorYellowAnnounced = false; }
            else if (sectorYellowSinceUtc == DateTime.MinValue) sectorYellowSinceUtc = nowUtc;
            if (yellowNow && !sectorYellowAnnounced && SectorYellowConfirmed(sectorYellowSinceUtc, nowUtc))
            {
                sectorYellowAnnounced = true;
                // Rob fährt schon unter lokalem Gelb: das „Gelb!“ dafür reicht.
                if (!f.isLocalYellow && !pf.isLocalYellow)
                {
                    result.Add(Phrases[2]);
                    announcedSector = yellowSector >= 0 ? yellowSector : 3; // 3 = Flagge des eigenen Wagens
                    sectorAnnouncedUtc = nowUtc; sectorClearSinceUtc = DateTime.MinValue;
                }
            }
            // „Gelb aufgehoben“, sobald der angesagte Sektor (bzw. die eigene Flagge) wieder grün ist – nicht, wenn Rob nur
            // aus dem gelben Sektor herausfährt und es dort noch gelb ist.
            if (announcedSector >= 0)
            {
                bool lifted = announcedSector == 3 ? flag != FlagEnum.YELLOW && flag != FlagEnum.DOUBLE_YELLOW :
                    current.FlagData.sectorFlags[announcedSector] != FlagEnum.YELLOW && current.FlagData.sectorFlags[announcedSector] != FlagEnum.DOUBLE_YELLOW;
                if (nowUtc - sectorAnnouncedUtc > SectorClearRelevantFor) { announcedSector = -1; sectorClearSinceUtc = DateTime.MinValue; }
                else if (!lifted) sectorClearSinceUtc = DateTime.MinValue;
                else if (sectorClearSinceUtc == DateTime.MinValue) sectorClearSinceUtc = nowUtc;
                else if (nowUtc - sectorClearSinceUtc >= SectorClearConfirm)
                {
                    bool longEnough = sectorClearSinceUtc - sectorAnnouncedUtc >= ShortYellow;
                    announcedSector = -1; sectorClearSinceUtc = DateTime.MinValue;
                    if (longEnough && !f.isLocalYellow && !f.isFullCourseYellow && YellowSectorAhead(current) < 0 && flag != FlagEnum.YELLOW &&
                        !result.Contains(Phrases[1]))
                        result.Add(Phrases[1]);
                }
            }
            if (flag != prevFlag)
            {
                switch (flag)
                {
                    case FlagEnum.YELLOW:
                        break;
                    case FlagEnum.DOUBLE_YELLOW:
                        result.Add(Phrases[3]);
                        break;
                    case FlagEnum.BLUE:
                        // Multiclass (LMU 18.11.): Hat die Crew den schnelleren Wagen der anderen Klasse gerade mit Klasse angekündigt,
                        // bringt „Blaue Flagge“ nichts Neues.
                        if (nowUtc - lastBlueUtc > TimeSpan.FromSeconds(15) && !BlueCoveredByClassCall(nowUtc))
                        {
                            lastBlueUtc = nowUtc;
                            result.Add(Phrases[4]);
                        }
                        break;
                    case FlagEnum.BLACK:
                        result.Add(Phrases[5]);
                        break;
                }
            }
            return result;
        }
    }
}
