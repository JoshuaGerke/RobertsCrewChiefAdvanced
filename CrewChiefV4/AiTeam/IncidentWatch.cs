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
        /// <summary>Strafart mit Grund, so wie das Spiel sie meldet.</summary>
        internal static string DetailedPenalty(PenatiesData.DetailedPenaltyType type, PenatiesData.DetailedPenaltyCause cause)
        {
            string what;
            switch (type)
            {
                case PenatiesData.DetailedPenaltyType.STOP_AND_GO: what = RadioLanguage.T("eine Stop-and-Go-Strafe", "a stop-and-go penalty", "un stop and go"); break;
                case PenatiesData.DetailedPenaltyType.DRIVE_THROUGH: what = RadioLanguage.T("eine Durchfahrtsstrafe", "a drive-through penalty", "un drive-through"); break;
                case PenatiesData.DetailedPenaltyType.TEN_SECONDS: what = RadioLanguage.T("eine Zehn-Sekunden-Strafe", "a ten-second penalty", "una penalización de diez segundos"); break;
                default: what = RadioLanguage.T("eine Strafe", "a penalty", "una penalización"); break;
            }
            string why;
            switch (cause)
            {
                case PenatiesData.DetailedPenaltyCause.SPEEDING_IN_PITLANE: why = RadioLanguage.T(" wegen zu schnell in der Boxengasse", " for speeding in the pit lane", " por exceso de velocidad en el pit lane"); break;
                case PenatiesData.DetailedPenaltyCause.FALSE_START: why = RadioLanguage.T(" wegen Frühstart", " for a false start", " por salida en falso"); break;
                case PenatiesData.DetailedPenaltyCause.CUT_TRACK: why = RadioLanguage.T(" wegen Abkürzen", " for cutting the track", " por recortar la pista"); break;
                case PenatiesData.DetailedPenaltyCause.EXITING_PITS_UNDER_RED: why = RadioLanguage.T(" wegen Boxenausfahrt bei Rot", " for leaving the pits on red", " por salir de boxes en rojo"); break;
                case PenatiesData.DetailedPenaltyCause.ILLEGAL_PASS_ROLLING_BEFORE_GREEN:
                case PenatiesData.DetailedPenaltyCause.ILLEGAL_PASS_FCY_BEFORE_GREEN: why = RadioLanguage.T(" wegen Überholen vor Grün", " for passing before green", " por adelantar antes de la verde"); break;
                case PenatiesData.DetailedPenaltyCause.IGNORED_BLUE_FLAG: why = RadioLanguage.T(" wegen ignorierter blauer Flagge", " for ignoring blue flags", " por ignorar la bandera azul"); break;
                default: why = ""; break;
            }
            return what + why;
        }

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
                        report = RadioVariants.Pick("incident-dropped",
                            RadioLanguage.T("Du bist von Platz " + before + " auf " + nowPos + " zurückgefallen. ",
                                "You've dropped from P" + before + " to P" + nowPos + ". ", "Has caído de P" + before + " a P" + nowPos + ". "),
                            RadioLanguage.T("Das hat Plätze gekostet, von " + before + " auf " + nowPos + ". ",
                                "That cost us places, P" + before + " down to P" + nowPos + ". ", "Eso nos ha costado puestos, de P" + before + " a P" + nowPos + ". "),
                            RadioLanguage.T("Wir sind jetzt Platz " + nowPos + ", vorher " + before + ". ",
                                "We're P" + nowPos + " now, we were P" + before + ". ", "Ahora somos P" + nowPos + ", antes P" + before + ". "),
                            RadioLanguage.T("Platz " + before + " ist weg, du bist jetzt " + nowPos + ". ",
                                "P" + before + " is gone, you're P" + nowPos + " now. ", "Perdimos P" + before + ", ahora vas P" + nowPos + ". ")) + report;
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
                if (current.SessionData.SessionType == SessionType.Race && behind >= 0.1f && behind < 15)
                {
                    string gap = SaraLocal.Seconds(behind);
                    text += RadioVariants.Pick("incident-stopped-gap",
                        RadioLanguage.T(" Der Nächste war zuletzt etwa " + gap + " hinter dir.", " The next car was last about " + gap + " behind you.",
                            " El siguiente iba hace un momento a unos " + gap + " detrás de ti."),
                        RadioLanguage.T(" Hinter dir zuletzt etwa " + gap + " Luft, schau beim Anfahren.", " About " + gap + " to the car behind last time, careful rejoining.",
                            " Hace un momento tenías unos " + gap + " al de detrás, cuidado al volver."),
                        RadioLanguage.T(" Der Hintermann lag zuletzt rund " + gap + " zurück.", " The car behind was last around " + gap + " back.",
                            " El de detrás estaba hace poco a unos " + gap + "."),
                        RadioLanguage.T(" Zuletzt etwa " + gap + " bis zum nächsten Auto hinter dir.", " Last gap to the next car behind was about " + gap + ".",
                            " La última diferencia con el siguiente coche era de unos " + gap + "."));
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
            if (p.HasDriveThrough && !pp.HasDriveThrough) penalty = RadioLanguage.T("eine Durchfahrtsstrafe", "a drive-through penalty", "un drive-through");
            else if (p.HasStopAndGo && !pp.HasStopAndGo) penalty = RadioLanguage.T("eine Stop-and-Go-Strafe", "a stop-and-go penalty", "un stop and go");
            else if (p.HasTimeDeduction && !pp.HasTimeDeduction) penalty = RadioLanguage.T("eine Zeitstrafe", "a time penalty", "una penalización de tiempo");
            else if (p.HasSlowDown && !pp.HasSlowDown) penalty = RadioLanguage.T("eine Slow-down-Strafe; reduziere das Tempo entsprechend der Spielanzeige",
                "a slow-down penalty; reduce your speed as the game display shows", "una penalización de slow-down; reduce la velocidad según indique el juego");
            // LMU 18.11. (Quali): Stop-and-go wurde nicht erkannt. LMU meldet Strafen als Spielmeldung, die Crew Chief in
            // Strafart und Grund übersetzt; die Anzahl offener Strafen bleibt dabei teils unverändert.
            else if (p.PenaltyType != pp.PenaltyType && p.PenaltyType != PenatiesData.DetailedPenaltyType.NONE) penalty = DetailedPenalty(p.PenaltyType, p.PenaltyCause);
            else if (p.NumOutstandingPenalties > pp.NumOutstandingPenalties) penalty = RadioLanguage.T("eine Strafe", "a penalty", "una penalización");
            if (penalty != null)
            {
                Penalties++;
                LiveDiagnostics.Decision("incident-penalty", "detected", "type=" + p.PenaltyType + ";cause=" + p.PenaltyCause + ";open=" + p.NumOutstandingPenalties, penalty);
                bool serve = p.HasDriveThrough || p.HasStopAndGo || p.PenaltyType == PenatiesData.DetailedPenaltyType.STOP_AND_GO ||
                    p.PenaltyType == PenatiesData.DetailedPenaltyType.DRIVE_THROUGH;
                string got = RadioVariants.Pick("incident-penalty",
                    RadioLanguage.T("Wir haben " + penalty + " bekommen.", "We've got " + penalty + ".", "Nos han puesto " + penalty + "."),
                    RadioLanguage.T("Die Rennleitung gibt uns " + penalty + ".", "Race control has given us " + penalty + ".", "Dirección de carrera nos pone " + penalty + "."),
                    RadioLanguage.T("Schlechte Nachricht: " + penalty + " für uns.", "Bad news: " + penalty + " for us.", "Mala noticia: " + penalty + " para nosotros."),
                    RadioLanguage.T("Es ist offiziell, " + penalty + ".", "It's official, " + penalty + ".", "Es oficial, " + penalty + "."));
                string serveText = !serve ? "" : RadioVariants.Pick("incident-penalty-serve",
                    RadioLanguage.T(" Sie muss in der Box abgesessen werden, ich behalte den Abstand nach hinten im Blick.", " It has to be served in the pits, I'm keeping an eye on the gap behind.", " Hay que cumplirla en boxes, vigilo la distancia por detrás."),
                    RadioLanguage.T(" Die holen wir in der Box ab, ich rechne mit dem Abstand nach hinten.", " We'll serve it in the pits, I'm working with the gap behind.", " La cumplimos en boxes, calculo con la distancia por detrás."),
                    RadioLanguage.T(" Muss durch die Box, ich sag dir, wann es am wenigsten kostet.", " It means a trip through the pits, I'll tell you when it costs least.", " Toca pasar por boxes, te digo cuándo cuesta menos."),
                    RadioLanguage.T(" Wir müssen dafür in die Box, ich beobachte den Verkehr hinter dir.", " We have to take it in the pits, I'm watching the traffic behind.", " Hay que entrar a boxes para cumplirla, vigilo el tráfico detrás."));
                result.Add(Say(TeamMember.Ben, got + serveText));
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
            if (invalid > 0) parts.Add(Count(invalid, "eine ungültige Runde", " ungültige Runden", "one invalid lap", " invalid laps", "una vuelta inválida", " vueltas inválidas"));
            if (TrackLimitWarnings > 0) parts.Add(Count(TrackLimitWarnings, "eine Tracklimit-Meldung", " Tracklimit-Meldungen", "one track limits warning", " track limits warnings", "un aviso de límites de pista", " avisos de límites de pista"));
            if (OffTracks > 0) parts.Add(Count(OffTracks, "ein Ausritt", " Ausritte", "one trip off track", " trips off track", "una salida de pista", " salidas de pista"));
            if (Spins > 0) parts.Add(Count(Spins, "eine starke Verzögerung", " starke Verzögerungen", "one big slowdown", " big slowdowns", "una fuerte pérdida de velocidad", " fuertes pérdidas de velocidad"));
            if (Penalties > 0) parts.Add(Count(Penalties, "eine Strafe", " Strafen", "one penalty", " penalties", "una penalización", " penalizaciones"));
            if (parts.Count == 0)
            {
                // Absence of an observed warning does not prove a penalty/incident-free race.
                return string.Empty;
            }
            string list = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + RadioLanguage.T(" und ", " and ", " y ") + parts[parts.Count - 1];
            return RadioLanguage.T(" Bilanz: ", " Summary: ", " Balance: ") + list + "." + (InvalidLaps + OffTracks + Spins + Penalties >= 4
                ? RadioLanguage.T(" Da ist beim nächsten Mal noch einiges drin.", " There's still plenty to gain next time.", " La próxima vez hay bastante margen de mejora.") : "");
        }

        private static string Count(int n, string oneDe, string manyDe, string oneEn, string manyEn, string oneEs, string manyEs)
        {
            return n == 1 ? RadioLanguage.T(oneDe, oneEn, oneEs) : n + RadioLanguage.T(manyDe, manyEn, manyEs);
        }

        /// <summary>Schadensbild in Worten, null wenn die Simulation keinen Schaden meldet bzw. kein Schadensmodell aktiv ist.</summary>
        public static string DamageReport(CarDamageData d, bool brief = false, bool includeAdvice = true)
        {
            // Im kurzen Routinebericht nur Werte ab 1 Prozent; die ausführliche Auskunft nennt weiter alles ab 0,05.
            var numeric = brief ? NumericDamageWatch.Report(d, 1f) : NumericDamageWatch.Report(d);
            if (!d.DamageEnabled) return numeric;
            var parts = new List<string>();
            if (numeric != null) parts.Add(numeric.TrimEnd('.'));
            bool serious = false;
            Add(parts, RadioLanguage.T("Aero", "Aero", "la aerodinámica"), d.OverallAeroDamage, d.AeroDamagePercent, ref serious);
            Add(parts, RadioLanguage.T("Motor", "Engine", "el motor"), d.OverallEngineDamage, d.EngineDamagePercent, ref serious);
            Add(parts, RadioLanguage.T("Getriebe", "Gearbox", "la caja de cambios"), d.OverallTransmissionDamage, d.TransmissionDamagePercent, ref serious);
            string where = SpokenCorner(d.SuspensionDamageStatus, DamageLevel.DESTROYED) ?? SpokenCorner(d.SuspensionDamageStatus, DamageLevel.MAJOR);
            if (where != null)
            {
                parts.Add(RadioLanguage.T("Aufhängung " + where + " stark beschädigt", "Suspension heavily damaged " + where, "Daño grave en la suspensión " + where));
                serious = true;
            }
            else if ((where = SpokenCorner(d.SuspensionDamageStatus, DamageLevel.MINOR)) != null)
            {
                parts.Add(RadioLanguage.T("Aufhängung " + where + " leicht beschädigt", "Suspension lightly damaged " + where, "Daño leve en la suspensión " + where));
            }
            where = SpokenCorner(d.BrakeDamageStatus, DamageLevel.DESTROYED) ?? SpokenCorner(d.BrakeDamageStatus, DamageLevel.MAJOR);
            if (where != null)
            {
                parts.Add(RadioLanguage.T("Bremse " + where + " stark beschädigt", "Brake heavily damaged " + where, "Daño grave en el freno " + where));
                serious = true;
            }
            else if ((where = SpokenCorner(d.BrakeDamageStatus, DamageLevel.MINOR)) != null)
                parts.Add(RadioLanguage.T("Bremse " + where + " leicht beschädigt", "Brake lightly damaged " + where, "Daño leve en el freno " + where));
            if (parts.Count == 0)
            {
                if(d.OverallAeroDamage==DamageLevel.UNKNOWN || d.OverallEngineDamage==DamageLevel.UNKNOWN || d.OverallTransmissionDamage==DamageLevel.UNKNOWN)
                    // Rob 16.11.: „fehlen Messwerte“ half nicht weiter. Gemeldetes sagen, Fehlendes ehrlich und mit Handlungshinweis.
                    return d.OverallAeroDamage == DamageLevel.NONE || d.OverallEngineDamage == DamageLevel.NONE || d.OverallTransmissionDamage == DamageLevel.NONE
                        ? RadioLanguage.T("Das Spiel meldet keinen Schaden, soweit es Werte liefert. Fühlt sich das Auto komisch an, sag Bescheid.",
                            "The game reports no damage as far as it gives data. If the car feels odd, tell me.",
                            "El juego no indica daños hasta donde da datos. Si notas algo raro, dímelo.")
                        // LMU 18.11.: „liefert das Spiel hier nicht“ war falsch – die Werte kamen kurz danach. Nur sagen, was gerade fehlt.
                        : RadioLanguage.T("Gerade noch keine Schadenswerte vom Spiel. Fühlt sich das Auto komisch an, sag Bescheid.",
                            "No damage values from the game just yet. If the car feels odd, tell me.",
                            "Todavía no hay valores de daño del juego. Si notas algo raro, dímelo.");
                return brief ? RadioLanguage.T("Aero, Motor und Getriebe laut Meldung unauffällig.", "Aero, engine and gearbox reported clear.", "Aerodinámica, motor y caja de cambios sin incidencias según los datos.") :
                    RadioLanguage.T("Keine nennenswerten Schäden an Aero, Motor und Getriebe gemeldet. Bremsen und Aufhängung sind nur soweit vom Spiel gemeldet beurteilbar.",
                        "No significant damage reported to aero, engine or gearbox. Brakes and suspension can only be judged as far as the game reports them.",
                        "Sin daños relevantes en aerodinámica, motor y caja de cambios. Frenos y suspensión solo se pueden valorar hasta donde informa el juego.");
            }
            string text = string.Join(", ", parts) + ".";
            // LMU 18.11.: Rob verlor ein Rad und hörte „wenn sich das Auto komisch anfühlt“. Schwerer Schaden heißt Box, nicht vielleicht.
            if (!serious || !includeAdvice) return text;
            bool destroyed = PitDamageWatch.Destroyed(d) || d.OverallEngineDamage == DamageLevel.DESTROYED;
            return text + (destroyed
                ? RadioLanguage.T(" Komm sofort an die Box.", " Box now.", " Entra a boxes ya.")
                : RadioLanguage.T(" Plan den Boxenstopp ein, so verlierst du viel Zeit.", " Plan the pit stop, you'll lose a lot of time like this.", " Prepara la parada, así pierdes mucho tiempo."));
        }

        internal static string PitAdviceAfterDamage(GameStateData state)
        {
            if (state == null || state.SessionData.SessionType != SessionType.Race || state.SessionData.SessionPhase != SessionPhase.Green ||
                state.FlagData.isLocalYellow || state.PitData.InPitlane || state.PitData.PitWindow != PitWindow.Open ||
                state.PitData.HasRequestedPitStop || !state.CarDamageData.DamageEnabled) return null;
            return IsSerious(state.CarDamageData) ? DamagePitAdvicePhrase : null;
        }

        internal const string DamagePitAdvicePhrase = "Box diese Runde. Wir prüfen den Schaden.";

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
            string p = percent >= 1 ? " (" + Math.Round(percent) + RadioLanguage.T(" Prozent)", " percent)", " por ciento)") : "";
            switch (level)
            {
                case DamageLevel.MINOR: parts.Add(RadioLanguage.T(name + " leicht beschädigt", name + " lightly damaged", "Daño leve en " + name) + p); break;
                case DamageLevel.MAJOR: parts.Add(RadioLanguage.T(name + " stark beschädigt", name + " heavily damaged", "Daño grave en " + name) + p); serious = true; break;
                case DamageLevel.DESTROYED: parts.Add(RadioLanguage.T(name + " kaputt", name + " destroyed", "Daño total en " + name) + p); serious = true; break;
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

        // CornerText bleibt deutsch (Daten für das Sprachmodell); gesprochen wird diese Fassung.
        private static string SpokenCorner(CornerData corners, DamageLevel level)
        {
            if (corners == null || RadioLanguage.IsGerman)
            {
                return CornerText(corners, level);
            }
            switch (corners.getCornersForStatus(level))
            {
                case CornerData.Corners.FRONT_LEFT: return RadioLanguage.T("", "front left", "delante a la izquierda");
                case CornerData.Corners.FRONT_RIGHT: return RadioLanguage.T("", "front right", "delante a la derecha");
                case CornerData.Corners.REAR_LEFT: return RadioLanguage.T("", "rear left", "detrás a la izquierda");
                case CornerData.Corners.REAR_RIGHT: return RadioLanguage.T("", "rear right", "detrás a la derecha");
                case CornerData.Corners.FRONTS: return RadioLanguage.T("", "at the front", "delante");
                case CornerData.Corners.REARS: return RadioLanguage.T("", "at the rear", "detrás");
                case CornerData.Corners.LEFTS: return RadioLanguage.T("", "on the left", "en el lado izquierdo");
                case CornerData.Corners.RIGHTS: return RadioLanguage.T("", "on the right", "en el lado derecho");
                case CornerData.Corners.ALL: return RadioLanguage.T("", "all round", "en las cuatro ruedas");
                default: return null;
            }
        }

        private static KeyValuePair<TeamMember, string> Say(TeamMember member, string text)
        {
            return new KeyValuePair<TeamMember, string>(member, text);
        }
    }
}
