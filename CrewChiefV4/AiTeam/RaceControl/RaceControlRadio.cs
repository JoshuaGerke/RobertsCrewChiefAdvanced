using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Was aus einer Rennleitungs-Meldung gesagt wird: Pflichtmeldungen (Strafe, Disqualifikation, Startvergehen, Box zu/offen,
    /// Abkürzen mit Frist) mit hoher Priorität, Hintergrund (Vorfallart, Grund für Gelb, Fast Repair) nur in Chronik und Lagebild.
    /// Eigene Formulierungen in DE/EN/ES; die zuletzt gehörte wird gemieden.
    /// </summary>
    internal static class RaceControlRadio
    {
        internal sealed class Line
        {
            public string Topic, Text;
            public TeamMember Member;
            public int Priority;
            public double ValidSeconds = 15;
        }

        private static string T(string de, string en, string es) => RadioLanguage.T(de, en, es);
        private static string N(double v) => Math.Abs(v - Math.Round(v)) < .05
            ? ((int)Math.Round(v)).ToString(RadioLanguage.Culture) : RadioLanguage.Num(v, "0.0");

        private static Line L(string topic, TeamMember member, int priority, double valid, params string[] options) =>
            new Line { Topic = "warning-rc-" + topic, Member = member, Priority = priority, ValidSeconds = valid, Text = RadioVariants.Pick("rc2-" + topic, options) };

        /// <summary>Funkzeile oder null (nur Chronik und Lagebild).</summary>
        internal static Line Call(RaceControlEvent e, GameStateData gs)
        {
            if (e == null) return null;
            switch (e.Code)
            {
                case "dq":
                    return L("dq", TeamMember.Ben, 9, 30,
                        T("Die Rennleitung hat uns disqualifiziert. Bring das Auto ruhig an die Box.", "Race control has disqualified us. Bring the car back to the pits calmly.", "Dirección de carrera nos ha descalificado. Lleva el coche a boxes con calma."),
                        T("Disqualifikation gemeldet. Das Rennen ist für uns vorbei, sicher reinkommen.", "Disqualification confirmed. Our race is over, come in safely.", "Descalificación confirmada. Se acabó la carrera, entra con seguridad."),
                        T("Schlechte Nachricht: Wir sind disqualifiziert. Langsam an die Box.", "Bad news: we're disqualified. Slowly back to the pits.", "Mala noticia: estamos descalificados. Despacio a boxes."));
                case "start-jump":
                    return L("start-offence", TeamMember.Ben, 9, 20,
                        T("Frühstart registriert. Die Strafe kommt, sobald unsere Gruppe grün hat.", "Jump start recorded. The penalty comes once our group goes green.", "Salida anticipada registrada. La sanción llega cuando nuestro grupo tenga verde."),
                        T("Achtung, die Rennleitung hat einen Frühstart notiert. Rechne mit einer Strafe.", "Careful, race control has noted a jump start. Expect a penalty.", "Atención, dirección de carrera ha anotado salida anticipada. Cuenta con sanción."),
                        T("Das war zu früh. Frühstart gemeldet, die Strafe folgt.", "That was too early. Jump start reported, penalty to follow.", "Fue demasiado pronto. Salida anticipada, habrá sanción."));
                case "start-side":
                    return L("start-offence", TeamMember.Ben, 9, 20,
                        T("Falsche Seite in der Formation. Sofort zurück auf deine Seite, die Rennleitung hat es notiert.", "Wrong side in the formation. Back to your side now, race control has noted it.", "Lado equivocado en la formación. Vuelve a tu lado ya, dirección de carrera lo ha anotado."),
                        T("Achtung, du bist auf der falschen Seite. Das gibt beim Grün eine Strafe.", "Careful, you're on the wrong side. That's a penalty at the green.", "Cuidado, vas por el lado equivocado. Eso es sanción con la verde."));
                case "formation-side":
                    bool left = e.Count == 0;
                    return L("formation-side", TeamMember.Sara, 6, 20,
                        left ? T("Formation: Bleib auf der linken Seite.", "Formation: stay on the left side.", "Formación: mantente en el lado izquierdo.")
                             : T("Formation: Bleib auf der rechten Seite.", "Formation: stay on the right side.", "Formación: mantente en el lado derecho."),
                        left ? T("Unsere Seite in der Formation ist links.", "Our side in the formation is the left.", "Nuestro lado en la formación es el izquierdo.")
                             : T("Unsere Seite in der Formation ist rechts.", "Our side in the formation is the right.", "Nuestro lado en la formación es el derecho."));
                case "pitlane-start":
                    if (double.IsNaN(e.Value)) return null;
                    string sec = N(e.Value);
                    return L("pitlane-start", TeamMember.Ben, 6, Math.Min(30, Math.Max(5, e.Value)),
                        T("Boxengassenstart. In " + sec + " Sekunden darfst du raus.", "Pit lane start. You can go in " + sec + " seconds.", "Salida desde boxes. Puedes salir en " + sec + " segundos."),
                        T("Wir starten aus der Box, Freigabe in " + sec + " Sekunden.", "We start from the pits, release in " + sec + " seconds.", "Salimos desde boxes, liberación en " + sec + " segundos."));
                case "meatball":
                    return L("meatball", TeamMember.Ben, 8, 30,
                        T("Technische Flagge für uns. Komm an die Box, der Schaden muss repariert werden.", "Meatball flag for us. Box, the damage has to be repaired.", "Bandera técnica para nosotros. A boxes, hay que reparar el daño."),
                        T("Die Rennleitung zeigt uns die Technik-Flagge. Diese Runde rein.", "Race control is showing us the meatball. In this lap.", "Dirección de carrera nos muestra la bandera técnica. Entra esta vuelta."));
                case "red":
                    return L("red", TeamMember.Sara, 9, 20,
                        T("Rote Flagge! Session unterbrochen, langsam und vorsichtig weiter.", "Red flag! Session stopped, slow down and take care.", "¡Bandera roja! Sesión detenida, despacio y con cuidado."),
                        T("Rot, Rot! Tempo raus, die Session ist gestoppt.", "Red, red! Slow down, the session is stopped.", "¡Roja, roja! Reduce, la sesión está parada."));
                case "slippery":
                    return L("slippery", TeamMember.Sara, 7, 20,
                        T("Achtung, rutschige Strecke voraus. Öl oder Schmutz, vorsichtig.", "Careful, slippery track ahead. Oil or debris, take care.", "Cuidado, pista resbaladiza delante. Aceite o suciedad, con cuidado."),
                        T("Rutschflagge, da vorne ist es glatt.", "Slippery surface flag, it's greasy up ahead.", "Bandera de pista resbaladiza, delante está deslizante."));
                case "warning":
                    return L("warning", TeamMember.Ben, 8, 20,
                        T("Verwarnung der Rennleitung wegen unsportlichen Verhaltens. Sauber bleiben, sonst gibt es eine Strafe.", "Race control warning for unsporting behaviour. Keep it clean or it's a penalty.", "Advertencia de dirección de carrera por conducta antideportiva. Limpio o habrá sanción."),
                        T("Schwarz-weiße Flagge für uns, das ist die letzte Warnung.", "Black and white flag for us, that's the last warning.", "Bandera blanca y negra para nosotros, es el último aviso."));
                case "cut-giveback":
                    string gained = double.IsNaN(e.Value) ? null : N(e.Value);
                    string metres = e.Remaining > 0 ? e.Remaining.ToString(RadioLanguage.Culture) : null;
                    string deadline = metres == null ? "" : T(" Du hast noch " + metres + " Meter dafür.", " You have " + metres + " metres to do it.", " Tienes " + metres + " metros para hacerlo.");
                    string what = gained == null ? T("Zeit", "time", "tiempo") : gained + T(" Sekunden", " seconds", " segundos");
                    return L("cut-giveback", TeamMember.Mik, 8, 15,
                        T("Abgekürzt und " + what + " gewonnen. Gib die Zeit zurück, sonst gibt es eine Strafe." + deadline, "You cut and gained " + what + ". Give it back or it's a penalty." + deadline, "Recortaste y ganaste " + what + ". Devuélvelo o habrá sanción." + deadline),
                        T("Track Limits: " + what + " zu viel. Zurücknehmen." + deadline, "Track limits: " + what + " too much. Lift and give it back." + deadline, "Límites de pista: " + what + " de más. Levanta y devuélvelo." + deadline));
                case "cut-penalty":
                    if (double.IsNaN(e.Value)) return null;
                    string total = N(e.Value);
                    return L("cut-penalty", TeamMember.Ben, 8, 20,
                        T("Strafzeit fürs Abkürzen, jetzt insgesamt " + total + " Sekunden.", "Time penalty for cutting, now " + total + " seconds in total.", "Penalización por recortar, ahora " + total + " segundos en total."),
                        T("Die Rennleitung rechnet uns " + total + " Sekunden fürs Abkürzen an.", "Race control is adding " + total + " seconds for cutting.", "Dirección de carrera nos suma " + total + " segundos por recortar."));
                case "input-lock":
                    string lockS = double.IsNaN(e.Value) ? null : N(e.Value);
                    return L("input-lock", TeamMember.Ben, 8, 10,
                        lockS == null ? T("Kollisionsstrafe: Die Eingaben sind kurz gesperrt.", "Collision penalty: your inputs are locked for a moment.", "Sanción por colisión: los mandos están bloqueados un momento.")
                                      : T("Kollisionsstrafe: Die Eingaben sind " + lockS + " Sekunden gesperrt.", "Collision penalty: inputs locked for " + lockS + " seconds.", "Sanción por colisión: mandos bloqueados " + lockS + " segundos."),
                        T("Die Rennleitung sperrt uns nach dem Kontakt kurz. Ruhig bleiben.", "Race control has locked us briefly after the contact. Stay calm.", "Dirección de carrera nos bloquea un momento tras el contacto. Tranquilo."));
                case "fcy-pits-closed":
                    return L("fcy-pits", TeamMember.Ben, 8, 20,
                        T("Box ist geschlossen. Jetzt nicht reinfahren.", "Pits are closed. Don't come in now.", "Boxes cerrados. No entres ahora."),
                        T("Achtung, Boxengasse zu. Draußen bleiben.", "Careful, pit lane closed. Stay out.", "Atención, pit lane cerrado. Quédate fuera."));
                case "fcy-pits-lead-lap":
                    return L("fcy-pits", TeamMember.Ben, 7, 20,
                        T("Box offen, aber nur für Autos in der Führungsrunde.", "Pits open, but only for cars on the lead lap.", "Boxes abiertos, solo para coches en la vuelta del líder."));
                case "fcy-pits-open":
                    return L("fcy-pits", TeamMember.Ben, 7, 20,
                        T("Box ist jetzt offen.", "Pits are open now.", "Boxes abiertos ahora."),
                        T("Boxengasse wieder offen, wenn wir reinwollen.", "Pit lane open again if we want to stop.", "Pit lane abierto otra vez si queremos parar."));
                case "fcy-last-lap-next":
                    return L("fcy-last", TeamMember.Sara, 7, 30,
                        T("Das Safety Car kommt nächste Runde rein.", "The safety car comes in next lap.", "El coche de seguridad entra la próxima vuelta."),
                        T("Noch eine Runde unter Gelb, dann wird neu gestartet.", "One more lap under yellow, then the restart.", "Una vuelta más bajo amarilla y luego la relanzada."));
                case "fcy-last-lap":
                case "one-to-green":
                    return L("fcy-last", TeamMember.Sara, 8, 30,
                        T("Letzte Runde unter Gelb. Bereit machen für den Restart.", "Last lap under yellow. Get ready for the restart.", "Última vuelta bajo amarilla. Prepárate para la relanzada."),
                        T("Restart kommt. Reifen warm halten, Abstand im Blick.", "Restart coming. Keep the tyres warm, watch the gap.", "Llega la relanzada. Mantén los neumáticos calientes, vigila la distancia."));
                case "rival-penalty":
                    if (gs == null || e.Car == null) return null;
                    var front = RaceDataTools.Neighbour(gs, true);
                    var rear = RaceDataTools.Neighbour(gs, false);
                    bool ahead = front != null && front.DriverRawName == e.Car, behind = rear != null && rear.DriverRawName == e.Car;
                    if (!ahead && !behind) return null;
                    return L("rival-penalty", TeamMember.Ben, 5, 20,
                        ahead ? T("Der Wagen vor dir hat eine Strafe bekommen.", "The car ahead of you has been given a penalty.", "El coche de delante ha recibido una sanción.")
                              : T("Der Wagen hinter dir hat eine Strafe bekommen.", "The car behind you has been given a penalty.", "El coche de detrás ha recibido una sanción."),
                        ahead ? T("Strafe für deinen Vordermann. Dranbleiben.", "Penalty for the car in front. Stay with him.", "Sanción para el de delante. No le pierdas.")
                              : T("Strafe für deinen Hintermann. Sauber weiterfahren.", "Penalty for the car behind. Keep it clean.", "Sanción para el de detrás. Sigue limpio."));
            }
            return null;
        }

        /// <summary>Gilt die Meldung beim Sprechen noch?</summary>
        internal static Func<GameStateData, bool> StillCurrent(RaceControlEvent e)
        {
            switch (e.Code)
            {
                case "fcy-pits-closed": return live => live.FlagData.fcyPhase == FullCourseYellowPhase.PITS_CLOSED;
                case "fcy-pits-lead-lap": return live => live.FlagData.fcyPhase == FullCourseYellowPhase.PITS_OPEN_LEAD_LAP_VEHICLES;
                case "fcy-pits-open": return live => live.FlagData.fcyPhase == FullCourseYellowPhase.PITS_OPEN;
                case "fcy-last-lap-next": return live => live.FlagData.fcyPhase == FullCourseYellowPhase.LAST_LAP_NEXT;
                case "fcy-last-lap": return live => live.FlagData.fcyPhase == FullCourseYellowPhase.LAST_LAP_CURRENT;
                case "cut-giveback": return live => { var s = AceRaceControlDetector.Read(live); return !s.Valid || s.CutDelta > .05f; };
                case "formation-side": return live => { var s = R3ERaceControlDetector.Read(live); return !s.Valid || s.StartPhase == 0; };
                case "pitlane-start": return live => live.PitData.InPitlane;
                case "meatball": return live => !live.PitData.InPitlane;
                default: return live => true;
            }
        }
    }
}
