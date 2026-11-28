using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CrewChiefV4.AiTeam
{
    /// <summary>At most one regular and one urgent render. Urgent rendering never waits for a normal cloud request.
    /// This is preparation only; the existing audio queue still gives Sara exclusive safety priority.</summary>
    public sealed class RadioRenderGate
    {
        private int regular,urgent,preparedRoutine,result;
        public bool PreparedRoutineBusy => Volatile.Read(ref preparedRoutine) != 0;
        public bool TryHoldRoutine() => Interlocked.CompareExchange(ref preparedRoutine, 1, 0) == 0;
        public void ReleaseRoutine() => Interlocked.Exchange(ref preparedRoutine, 0);
        public bool Busy {get{return Volatile.Read(ref regular)!=0 || Volatile.Read(ref urgent)!=0;}}
        public bool ResultBusy => Volatile.Read(ref result) != 0;
        public bool TryEnterResult() => !UrgentBusy && Interlocked.CompareExchange(ref result, 1, 0) == 0;
        public void ExitResult() => Interlocked.Exchange(ref result, 0);
        public bool UrgentBusy {get{return Volatile.Read(ref urgent)!=0;}}
        public bool TryEnter(bool urgentCall)
        {
            if(urgentCall) return Interlocked.CompareExchange(ref urgent,1,0)==0;
            if(UrgentBusy || Interlocked.CompareExchange(ref regular,1,0)!=0) return false;
            if(UrgentBusy) {Interlocked.Exchange(ref regular,0);return false;}
            return true;
        }
        public void Exit(bool urgentCall)
        {if(urgentCall)Interlocked.Exchange(ref urgent,0);else Interlocked.Exchange(ref regular,0);}
        public static bool IsUrgentPit(ObservedRaceCall call)
        {return call!=null && call.Priority>=7 && (call.Topic=="pit-limit" || call.Topic=="pit-speed");}
    }

    /// <summary>One voice identity throughout local and cloud radio; never silently substitute a provider.</summary>
    public static class CrewSpeechPolicy
    {
        /// <summary>
        /// N12 (iRacing Navarra 28.11.): Nach der eigenen Disqualifikation im Rennen spricht die Crew nur noch die Meldung selbst,
        /// die Bilanz und Boxen-/Sicherheitshinweise – keine Plätze, Vorfallpunkte oder Zuspruch mehr.
        /// </summary>
        internal static bool AllowedAfterDisqualification(string topic, CrewChiefV4.GameState.GameStateData gs)
        {
            if (gs == null || gs.SessionData.SessionType != CrewChiefV4.GameState.SessionType.Race || !gs.SessionData.IsDisqualified) return true;
            string t = topic ?? "";
            return t == "race-disqualified" || t == "race-summary" || t == "session-debrief" || t == "pit-finish" ||
                t.StartsWith("race-hazard", StringComparison.Ordinal) || t == "warning-incident-behind" || t.StartsWith("pit-limit", StringComparison.Ordinal);
        }

        private static readonly string[] FirstTrackLimitPhrasesDe = {
            "Pass auf die Streckenbegrenzung auf.",
            "Die Rennkommissare melden Tracklimits. Lass etwas Platz zum Rand.",
            "Achte auf die Limits. Bleib innerhalb der Begrenzung.",
            "Track Limits im Blick behalten, die Kommissare schauen hin.",
            "Da warst du drüber. Die Linie gehört zur Strecke, nicht weiter.",
            "Tracklimit notiert. Einen halben Meter Reserve zum Rand, dann passt das."
        };
        private static readonly string[] FirstTrackLimitPhrasesEn = {
            "Watch the track limits.",
            "The sim is flagging track limits. Leave some margin to the edge.",
            "Mind the limits. Stay inside the lines.",
            "Mind the track limits, the stewards are watching.",
            "That was over the line. The white line is the limit, no further.",
            "Track limit noted. Half a metre of margin to the edge and you're fine."
        };
        private static readonly string[] FirstTrackLimitPhrasesEs = {
            "Cuidado con los límites de pista.",
            "El simulador marca límites de pista. Deja algo de margen al borde.",
            "Atento a los límites. Mantente dentro de la pista.",
            "Ojo con los límites de pista, los comisarios están mirando.",
            "Te has pasado. La línea blanca es el límite, no más allá.",
            "Límite de pista anotado. Medio metro de margen al borde y todo bien."
        };
        public static string[] FirstTrackLimitPhrases => RadioLanguage.Pick(FirstTrackLimitPhrasesDe, FirstTrackLimitPhrasesEn, FirstTrackLimitPhrasesEs);
        private static readonly string[] RepeatedTrackLimitPhrasesDe = {
            "Wieder außerhalb der Limits. Lass etwas mehr Platz zum Rand.",
            "Wieder Tracklimits von den Rennkommissaren. Lass etwas mehr Reserve.",
            "Schon wieder über den Limits. Bleib sauber auf der Strecke.",
            "Noch einmal über den Limits. Das zählt mit – bitte etwas Reserve.",
            "Die Limits sammeln sich. Lieber ein Zehntel liegen lassen als eine Strafe.",
            "Wieder gemeldet. Rand meiden, die Strafe droht."
        };
        private static readonly string[] RepeatedTrackLimitPhrasesEn = {
            "Off the limits again. Leave a bit more margin to the edge.",
            "Another track limits warning. Give yourself more margin at the edge.",
            "Over the limits again. Keep it clean on track.",
            "Over the limits again. It all counts – leave some margin, please.",
            "The limits are adding up. Better to leave a tenth than take a penalty.",
            "Flagged again. Stay off the edge, a penalty is close."
        };
        private static readonly string[] RepeatedTrackLimitPhrasesEs = {
            "Otra vez fuera de los límites. Deja algo más de margen al borde.",
            "Otro aviso de límites de pista. Guarda algo más de margen en el borde.",
            "Otra vez por fuera de los límites. Mantente limpio en pista.",
            "Otra vez fuera de límites. Todo cuenta, deja algo de margen.",
            "Los límites se acumulan. Mejor perder una décima que una sanción.",
            "Otra vez marcado. Evita el borde, la sanción está cerca."
        };
        public static string[] RepeatedTrackLimitPhrases => RadioLanguage.Pick(RepeatedTrackLimitPhrasesDe, RepeatedTrackLimitPhrasesEn, RepeatedTrackLimitPhrasesEs);
        private static readonly string[] FirstInvalidLapPhrasesDe = {
            "Diese Runde ist ungültig. Konzentrier dich auf die nächste Runde.",
            "Die Rundenzeit ist ungültig. Konzentrier dich auf die nächste gültige Zeit.",
            "Runde ungültig. Ruhig bleiben und wieder eine gültige Zeit setzen.",
            "Die Zeit ist ungültig, die zählt nicht. Abhaken, nächste Runde.",
            "Runde ungültig, gestrichen. Neu aufbauen, du hast das Tempo.",
            "Leider ungültig. Kopf frei und weiter."
        };
        private static readonly string[] FirstInvalidLapPhrasesEn = {
            "This lap is invalid. Focus on the next one.",
            "The lap time is invalid. Focus on the next valid time.",
            "Lap invalid. Stay calm and set a valid time again.",
            "That time is invalid, it won't count. Forget it, next lap.",
            "Lap invalid, deleted. Build it again, you've got the pace.",
            "Invalid, unfortunately. Clear your head and go again."
        };
        private static readonly string[] FirstInvalidLapPhrasesEs = {
            "Esta vuelta no es válida. Concéntrate en la siguiente.",
            "El tiempo de vuelta no es válido. Concéntrate en el próximo tiempo válido.",
            "Vuelta invalidada. Tranquilo y vuelve a marcar un tiempo válido.",
            "Ese tiempo no es válido, no cuenta. Olvídalo, la próxima.",
            "Vuelta inválida, anulada. Vuelve a construirla, tienes el ritmo.",
            "Inválida, por desgracia. Cabeza despejada y otra vez."
        };
        public static string[] FirstInvalidLapPhrases => RadioLanguage.Pick(FirstInvalidLapPhrasesDe, FirstInvalidLapPhrasesEn, FirstInvalidLapPhrasesEs);
        private static readonly string[] RepeatedInvalidLapPhrasesDe = {
            "Noch eine ungültige Runde. Wir brauchen wieder eine gültige Zeit.",
            "Wieder keine gültige Rundenzeit. Bleib konzentriert.",
            "Die Runde ist erneut ungültig. Konzentrier dich auf die nächste.",
            "Wieder ungültig. Durchatmen, die nächste zählt.",
            "Auch diese Zeit ist ungültig. Ruhig, die gültige kommt.",
            "Schon wieder ungültig. Konzentration, eine saubere Runde reicht."
        };
        private static readonly string[] RepeatedInvalidLapPhrasesEn = {
            "Another invalid lap. We need a valid time again.",
            "No valid lap time again. Stay focused.",
            "The lap is invalid again. Focus on the next one.",
            "Invalid again. Breathe, the next one counts.",
            "This time is invalid too. Stay calm, the valid one will come.",
            "Invalid once more. Focus, one clean lap is enough."
        };
        private static readonly string[] RepeatedInvalidLapPhrasesEs = {
            "Otra vuelta inválida. Necesitamos de nuevo un tiempo válido.",
            "Otra vez sin tiempo válido. Mantén la concentración.",
            "La vuelta vuelve a ser inválida. Concéntrate en la siguiente.",
            "Inválida otra vez. Respira, la siguiente cuenta.",
            "Este tiempo tampoco es válido. Tranquilo, la válida llegará.",
            "Inválida de nuevo. Concentración, basta una vuelta limpia."
        };
        public static string[] RepeatedInvalidLapPhrases => RadioLanguage.Pick(RepeatedInvalidLapPhrasesDe, RepeatedInvalidLapPhrasesEn, RepeatedInvalidLapPhrasesEs);
        /// <summary>Zusatz, wenn eine Tracklimit-Meldung die Runde ungültig macht.</summary>
        public static string LapInvalidSuffix => RadioLanguage.T(" Diese Runde ist ungültig.", " This lap is invalid.", " Esta vuelta no es válida.");
        // Both forms are prepared before driving; a track-limit warning alone does not prove lap invalidity.
        public static string[] TrackLimitPhrases => FirstTrackLimitPhrases.Concat(RepeatedTrackLimitPhrases)
            .Concat(FirstTrackLimitPhrases.Concat(RepeatedTrackLimitPhrases).Select(p => p + LapInvalidSuffix))
            .Concat(FirstInvalidLapPhrases).Concat(RepeatedInvalidLapPhrases).ToArray();
        private static readonly string[] FinishPhrasesDe = {
            "Jaaa, gewonnen! Ganz stark, Glückwunsch!",
            "Das ist der Sieg! Klasse gemacht, wir haben es geschafft!",
            "Im Ziel und ganz vorne! Stark gefahren, Glückwunsch zum Sieg!",
            "Ja, das ist Platz eins! Glückwunsch zum Sieg!",
            "Gewonnen! Stark gemacht. Freu dich darüber!",
            "Der Sieg ist deiner! Glückwunsch!"
        };
        private static readonly string[] FinishPhrasesEn = {
            "Yesss, you won it! Brilliant, congratulations!",
            "That's the win! Great job, we did it!",
            "Across the line in front! Strong drive, congratulations on the win!",
            "Yes, that's P1! Congratulations on the win!",
            "You won! Great work. Enjoy that one!",
            "The win is yours! Congratulations!"
        };
        private static readonly string[] FinishPhrasesEs = {
            "¡Síii, has ganado! ¡Enorme, enhorabuena!",
            "¡Es la victoria! ¡Gran trabajo, lo hemos conseguido!",
            "¡En meta y por delante! ¡Gran carrera, enhorabuena por la victoria!",
            "¡Sí, eso es P1! ¡Enhorabuena por la victoria!",
            "¡Ganado! Muy bien hecho. ¡Disfrútalo!",
            "¡La victoria es tuya! ¡Enhorabuena!"
        };
        public static string[] FinishPhrases => RadioLanguage.Pick(FinishPhrasesDe, FinishPhrasesEn, FinishPhrasesEs);

        private static readonly string[] RainStartPhrasesDe = {
            "Regen setzt ein. Achte auf die Bedingungen.",
            "Es fängt an zu regnen. Fahr vorausschauend.",
            "Erste Regentropfen gemeldet. Bleib aufmerksam.",
            "Regen kommt. Weiße Linien und Randsteine jetzt meiden.",
            "Tropfen gemeldet. Sanft mit Gas und Bremse.",
            "Es wird nass. Früher bremsen, Grip abtasten."
        };
        private static readonly string[] RainStartPhrasesEn = {
            "Rain is starting. Watch the conditions.",
            "It's starting to rain. Drive with anticipation.",
            "First raindrops reported. Stay alert.",
            "Rain is coming. Stay off white lines and kerbs now.",
            "Drops reported. Gentle with throttle and brake.",
            "It's getting wet. Brake earlier, feel for the grip."
        };
        private static readonly string[] RainStartPhrasesEs = {
            "Empieza a llover. Atento a las condiciones.",
            "Está empezando a llover. Conduce con previsión.",
            "Primeras gotas de lluvia. Mantente atento.",
            "Llega la lluvia. Evita ya las líneas blancas y los pianos.",
            "Gotas en pista. Suave con gas y freno.",
            "Se está mojando. Frena antes, busca el agarre."
        };
        public static string[] RainStartPhrases => RadioLanguage.Pick(RainStartPhrasesDe, RainStartPhrasesEn, RainStartPhrasesEs);
        private static readonly string[] RainStopPhrasesDe = {
            "Der Regen ist abgeklungen. Die Strecke kann weiter nass sein.",
            "Kein nennenswerter Regen mehr gemeldet. Pass weiter auf die Nässe auf.",
            "Der Regen lässt nach. Mit der Strecke weiter vorsichtig bleiben.",
            "Kein Regen mehr. Die Linie trocknet zuerst ab.",
            "Regen vorbei. Abseits der Linie bleibt es noch rutschig.",
            "Es hört auf zu regnen. Grip kommt Runde für Runde zurück."
        };
        private static readonly string[] RainStopPhrasesEn = {
            "The rain has eased off. The track may still be wet.",
            "No significant rain reported anymore. Keep watching for wet patches.",
            "The rain is easing. Stay careful on the track.",
            "No more rain. The racing line will dry first.",
            "Rain is over. Off-line it stays slippery for a while.",
            "It's stopping. Grip will come back lap by lap."
        };
        private static readonly string[] RainStopPhrasesEs = {
            "La lluvia ha amainado. La pista puede seguir mojada.",
            "Ya no se reporta lluvia relevante. Sigue atento a la humedad.",
            "La lluvia remite. Sigue con cuidado en pista.",
            "Ya no llueve. La trazada se secará primero.",
            "Se acabó la lluvia. Fuera de la trazada seguirá resbaladizo.",
            "Deja de llover. El agarre volverá vuelta a vuelta."
        };
        public static string[] RainStopPhrases => RadioLanguage.Pick(RainStopPhrasesDe, RainStopPhrasesEn, RainStopPhrasesEs);
        private static readonly string[] WetterPhrasesDe = {
            "Mehr Streckennässe gemeldet. Bleib vorsichtig.",
            "Die gemeldete Streckennässe nimmt zu. Fahr mit Reserve.",
            "Die Strecke wird nasser. Mehr Reserve beim Bremsen.",
            "Mehr Wasser auf der Strecke. Ruhige Hände, ruhige Füße.",
            "Nässe nimmt zu. Aquaplaning in den Senken beachten.",
            "Es wird rutschiger. Lieber ein Zehntel verschenken als den Wagen."
        };
        private static readonly string[] WetterPhrasesEn = {
            "Track reported wetter. Stay careful.",
            "Reported track wetness is increasing. Drive with margin.",
            "The track is getting wetter. More margin on the brakes.",
            "More water on track. Calm hands, calm feet.",
            "Wetness increasing. Watch for aquaplaning in the dips.",
            "It's getting slippier. Better to give away a tenth than the car."
        };
        private static readonly string[] WetterPhrasesEs = {
            "Se reporta más agua en pista. Ten cuidado.",
            "La humedad reportada en pista aumenta. Conduce con margen.",
            "La pista está más mojada. Más margen en la frenada.",
            "Más agua en pista. Manos y pies tranquilos.",
            "Aumenta la humedad. Cuidado con el aquaplaning en las zonas bajas.",
            "Está más resbaladizo. Mejor regalar una décima que el coche."
        };
        public static string[] WetterPhrases => RadioLanguage.Pick(WetterPhrasesDe, WetterPhrasesEn, WetterPhrasesEs);
        private static readonly string[] DrierPhrasesDe = {
            "Weniger Streckennässe gemeldet. Das heißt noch nicht, dass überall Grip da ist.",
            "Die gemeldete Streckennässe nimmt ab. Geh weiter mit Gefühl ans Gas.",
            "Die Strecke trocknet. Auf der Linie kommt der Grip zuerst.",
            "Weniger Wasser gemeldet. Taste dich ans Limit ran.",
            "Es wird trockener. Bremspunkte schrittweise später.",
            "Nässe geht zurück. Neben der Linie aber noch vorsichtig."
        };
        private static readonly string[] DrierPhrasesEn = {
            "Track reported drier. That doesn't mean there's grip everywhere yet.",
            "Reported track wetness is dropping. Stay gentle on the throttle.",
            "The track is drying. Grip comes back on the line first.",
            "Less water reported. Feel your way up to the limit.",
            "It's getting drier. Move the braking points later step by step.",
            "Wetness is dropping. Still careful off the line though."
        };
        private static readonly string[] DrierPhrasesEs = {
            "Se reporta menos agua en pista. Eso no significa que haya agarre en todas partes.",
            "La humedad reportada en pista baja. Sigue suave con el gas.",
            "La pista se seca. El agarre vuelve primero en la trazada.",
            "Menos agua en pista. Acércate al límite poco a poco.",
            "Está más seco. Retrasa la frenada poco a poco.",
            "Baja la humedad. Pero fuera de la trazada aún con cuidado."
        };
        public static string[] DrierPhrases => RadioLanguage.Pick(DrierPhrasesDe, DrierPhrasesEn, DrierPhrasesEs);
        private static readonly string[] PitSpeedPhrasesDe = {
            "Boxenlimit, du bist zu schnell. Tempo runter.",
            "Achte aufs Boxenlimit. Reduziere dein Tempo.",
            "Zu schnell in der Boxengasse! Runter vom Gas.",
            "Speed in der Box zu hoch – Limiter rein.",
            "Tempo raus, Boxenlimit! Sonst gibt's eine Strafe.",
            "Vorsicht, über dem Limit in der Box. Bremsen."
        };
        private static readonly string[] PitSpeedPhrasesEn = {
            "Pit limit, you're too fast. Slow down.",
            "Watch the pit speed limit. Bring your speed down.",
            "Too fast in the pit lane! Off the throttle.",
            "Pit lane speed too high – limiter on.",
            "Slow down, pit limit! Otherwise it's a penalty.",
            "Careful, over the limit in the pits. Brake."
        };
        private static readonly string[] PitSpeedPhrasesEs = {
            "Límite de boxes, vas demasiado rápido. Baja la velocidad.",
            "Atento al límite de boxes. Reduce la velocidad.",
            "¡Demasiado rápido en el pit lane! Levanta.",
            "Velocidad en boxes demasiado alta, activa el limitador.",
            "¡Baja el ritmo, límite de boxes! Si no, hay sanción.",
            "Cuidado, por encima del límite en boxes. Frena."
        };
        public static string[] PitSpeedPhrases => RadioLanguage.Pick(PitSpeedPhrasesDe, PitSpeedPhrasesEn, PitSpeedPhrasesEs);
        public static string[] WeatherPhrases => PitSpeedPhrases.Concat(RainStartPhrases).Concat(RainStopPhrases).Concat(WetterPhrases).Concat(DrierPhrases).ToArray();

        private static readonly string[] ContactPhrasesDe = {
            "Kontakt. Ich schau mir den Schaden an.",
            "Das hat gerumst. Ich prüf das Auto.",
            "Kontakt gesehen. Moment, ich check den Schaden.",
            "Autsch. Ich schau, was am Auto ist.",
            "Da war Kontakt. Ich prüfe gleich das Auto.",
            "Berührung registriert. Ich schau nach dem Auto.",
            "Kontakt. Fahr erst mal weiter, ich check die Werte."
        };
        private static readonly string[] ContactPhrasesEn = {
            "Contact. I'll check the damage.",
            "That was a hit. Checking the car.",
            "Saw the contact. Stand by, checking the damage.",
            "Ouch. Let me see what's up with the car.",
            "There was contact. Checking the car now.",
            "Contact registered. I'm looking at the car.",
            "Contact. Keep going, I'm checking the numbers."
        };
        private static readonly string[] ContactPhrasesEs = {
            "Contacto. Reviso los daños.",
            "Eso ha sido un golpe. Reviso el coche.",
            "He visto el contacto. Un momento, compruebo los daños.",
            "Uf. Miro a ver cómo está el coche.",
            "Ha habido contacto. Reviso el coche ahora.",
            "Contacto registrado. Miro el coche.",
            "Contacto. Sigue rodando, reviso los datos."
        };
        public static string[] ContactPhrases => RadioLanguage.Pick(ContactPhrasesDe, ContactPhrasesEn, ContactPhrasesEs);
        // Nur bei belegtem Kontakt mit starkem Tempoverlust oder schwerem Schaden, danach kommt der Schadensbericht.
        private static readonly string[] HeavyContactPhrasesDe = {
            "Uff, das war heftig. Alles okay bei dir?",
            "Oh, das sah nicht gut aus. Geht's dir gut?",
            "Das war ein harter Einschlag. Alles in Ordnung?",
            "Oha. Alles okay bei dir? Ich schau mir das Auto an.",
            "Das war ein ordentlicher Schlag. Bist du okay?",
            "Puh, das hat gekracht. Alles in Ordnung bei dir?"
        };
        private static readonly string[] HeavyContactPhrasesEn = {
            "Oof, that was a big one. You okay?",
            "Oh, that didn't look good. Are you alright?",
            "That was a hard impact. Everything okay?",
            "Whoa. You okay? I'll check the car.",
            "That was a proper hit. Are you okay?",
            "Phew, that was a big hit. Everything alright?"
        };
        private static readonly string[] HeavyContactPhrasesEs = {
            "Uf, eso ha sido fuerte. ¿Estás bien?",
            "Uy, eso no tenía buena pinta. ¿Todo bien?",
            "Ha sido un impacto duro. ¿Todo en orden?",
            "Vaya. ¿Estás bien? Reviso el coche.",
            "Ha sido un buen golpe. ¿Estás bien?",
            "Uf, eso ha sonado fuerte. ¿Todo en orden?"
        };
        public static string[] HeavyContactPhrases => RadioLanguage.Pick(HeavyContactPhrasesDe, HeavyContactPhrasesEn, HeavyContactPhrasesEs);
        private static readonly string[] StoppedPhrasesDe = {
            "Alles okay? Beim Wiederanfahren auf den Verkehr achten.",
            "Alles gut bei dir? Erst schauen, dann wieder rein.",
            "Alles klar? Ganz ruhig, pass beim Anfahren auf den Verkehr auf.",
            "Alles okay? Lass den Verkehr vorbei, dann geht's weiter.",
            "Ruhig bleiben. Erst Lücke abwarten, dann anfahren.",
            "Geht's? Schau in die Spiegel, bevor du losfährst."
        };
        private static readonly string[] StoppedPhrasesEn = {
            "You okay? Watch the traffic when you get going again.",
            "All good? Look first, then rejoin.",
            "Alright? Nice and calm, watch the traffic as you pull away.",
            "You okay? Let the traffic pass, then go.",
            "Stay calm. Wait for a gap, then pull away.",
            "Alright? Check your mirrors before you get going."
        };
        private static readonly string[] StoppedPhrasesEs = {
            "¿Todo bien? Cuidado con el tráfico al arrancar.",
            "¿Estás bien? Primero mira, luego vuelve a pista.",
            "¿Todo en orden? Con calma, atento al tráfico al arrancar.",
            "¿Todo bien? Deja pasar el tráfico y sigue.",
            "Tranquilo. Espera un hueco y luego arranca.",
            "¿Bien? Mira los espejos antes de arrancar."
        };
        public static string[] StoppedPhrases => RadioLanguage.Pick(StoppedPhrasesDe, StoppedPhrasesEn, StoppedPhrasesEs);
        private static readonly string[] OffTrackPhrasesDe = {
            "Neben der Strecke. Beim Zurückfahren auf den Verkehr achten.",
            "Du bist draußen. Schau auf den Verkehr, bevor du zurückkommst.",
            "Achtung, neben der Strecke. Komm kontrolliert zurück.",
            "Draußen. Ruhig zurück auf die Strecke, Spiegel im Blick.",
            "Neben der Bahn. Nicht hektisch zurückziehen, Verkehr beachten.",
            "Du bist von der Strecke. Kontrolliert und mit Blick nach hinten zurück."
        };
        private static readonly string[] OffTrackPhrasesEn = {
            "Off track. Watch the traffic when you rejoin.",
            "You're off. Check the traffic before you come back on.",
            "Careful, off track. Rejoin under control.",
            "You're off. Ease back on, mirrors first.",
            "Off the circuit. Don't rush back, watch the traffic.",
            "You're off track. Come back under control and check behind."
        };
        private static readonly string[] OffTrackPhrasesEs = {
            "Fuera de pista. Cuidado con el tráfico al volver.",
            "Estás fuera. Mira el tráfico antes de volver a entrar.",
            "Atención, fuera de pista. Vuelve con control.",
            "Estás fuera. Vuelve con calma, primero los espejos.",
            "Fuera de la pista. Sin prisas al volver, atento al tráfico.",
            "Te has salido. Vuelve con control y mirando atrás."
        };
        public static string[] OffTrackPhrases => RadioLanguage.Pick(OffTrackPhrasesDe, OffTrackPhrasesEn, OffTrackPhrasesEs);
        private static readonly string[] DamageDetectedPhrasesDe = {
            "Neuer Schaden gemeldet. Ich schau mir das an.",
            "Die Telemetrie meldet mehr Schaden. Ich prüf das Auto.",
            "Am Auto ist neuer Schaden gemeldet. Ich sehe nach.",
            "Ein Schadenswert ist gestiegen. Ich prüfe, was betroffen ist.",
            "Da ist Schaden dazugekommen. Ich schau, wie schlimm es ist.",
            "Neuer Schaden in den Daten. Ich melde mich gleich mit Details."
        };
        private static readonly string[] DamageDetectedPhrasesEn = {
            "New damage reported. I'll take a look.",
            "Telemetry shows more damage. Checking the car.",
            "New damage reported on the car. I'm looking into it.",
            "A damage value has gone up. Checking what's affected.",
            "Some damage has been added. Checking how bad it is.",
            "New damage in the data. I'll come back with details."
        };
        private static readonly string[] DamageDetectedPhrasesEs = {
            "Nuevo daño reportado. Lo reviso.",
            "La telemetría marca más daños. Reviso el coche.",
            "Hay daño nuevo en el coche. Lo miro.",
            "Ha subido un valor de daño. Compruebo qué está afectado.",
            "Hay daño nuevo. Miro cómo de grave es.",
            "Daño nuevo en los datos. Enseguida te doy detalles."
        };
        public static string[] DamageDetectedPhrases => RadioLanguage.Pick(DamageDetectedPhrasesDe, DamageDetectedPhrasesEn, DamageDetectedPhrasesEs);
        public static string RainTrackHint => RadioLanguage.T(" Es regnet gerade, vorsichtig am Gas.", " It's raining right now, careful on the throttle.", " Está lloviendo, cuidado con el gas.");
        public static string WetTrackHint => RadioLanguage.T(" Die Strecke ist nass, vorsichtig am Gas.", " The track is wet, careful on the throttle.", " La pista está mojada, cuidado con el gas.");
        public static string[] IncidentPhrases => ContactPhrases.Concat(HeavyContactPhrases).Concat(DamageDetectedPhrases).Concat(StoppedPhrases).Concat(OffTrackPhrases)
            .Concat(OffTrackPhrases.Select(p => p + WetTrackHint)).Concat(OffTrackPhrases.Select(p => p + RainTrackHint)).ToArray();

        /// <summary>Rob 17.11.2026: Nur Sara als Spotterin darf eine laufende Crew-Ansage unterbrechen; Ben und Mik warten.</summary>
        internal static bool MayInterruptCrew(TeamMember speaker) => speaker == TeamMember.Sara;

        /// <summary>Gefahr auf der Strecke: Unfall, stehendes oder langsames Auto vor dir.</summary>
        internal static bool IsHazardTopic(string topic) =>
            topic == "race-hazard-ahead" || topic == "traffic-catching" ||
            (topic ?? "").StartsWith("warning-native-incident-", StringComparison.Ordinal);

        /// <summary>Rob 18.11.2026: Gefahrenwarnungen spricht Sara, damit sie eine laufende Ansage unterbrechen darf.
        /// Spottet Sara gerade nicht (aus, oder Realistisch auf einer Rundstrecke), bleibt die Warnung bei Ben – ohne Unterbrechung.</summary>
        internal static TeamMember HazardSpeaker(string topic, TeamMember planned, bool saraSpotting) =>
            planned == TeamMember.Ben && saraSpotting && IsHazardTopic(topic) ? TeamMember.Sara : planned;

        /// <summary>Alle vorbereiteten Gefahrensätze, die Sara statt Ben sprechen kann.</summary>
        internal static IEnumerable<string> HazardPhrases =>
            RaceTrafficWatch.PreparedPhrases.Concat(NativeIncidentWatch.Phrases)
                .Concat(TrackSituationWatch.CatchingPhrases).Concat(TrackSituationWatch.StoppedPhrases);

        public static bool CanInterruptForHazard(bool currentIsAi, bool currentIsSpotter, bool currentIsBleep, int currentPriority, int incomingPriority)
        { return currentIsAi && !currentIsSpotter && !currentIsBleep && incomingPriority >= 7 && currentPriority < incomingPriority; }

        /// <summary>Stufe, ab der eine Gefahr eine Antwort an Rob abschneiden darf (dringende Gefahr, Flagge, Schaden mit „Box“).</summary>
        internal const int InterruptAnswerPriority = 8;

        /// <summary>
        /// LMU 21.11.: Sara schnitt viermal Antworten an Rob ab, darunter mit „Der stehende Wagen ist noch da“ und einem langsamen
        /// Auto 200 m voraus. Eine Antwort auf Robs Frage (Clip ohne Thema) weicht nur noch einer dringenden Gefahr ab Stufe 8.
        /// </summary>
        internal static bool CanInterruptForHazard(bool currentIsAi, bool currentIsSpotter, bool currentIsBleep, int currentPriority,
            int incomingPriority, string currentTopic)
        {
            bool answer = currentIsAi && string.IsNullOrEmpty(currentTopic);
            return CanInterruptForHazard(currentIsAi, currentIsSpotter, currentIsBleep, currentPriority, incomingPriority) &&
                (!answer || incomingPriority >= InterruptAnswerPriority);
        }

        /// <summary>Ab dieser Stufe hat eine Meldung von Sara Vorrang vor laufendem Funk von Ben und Mik.</summary>
        internal const int SaraPrecedencePriority = 5;

        /// <summary>
        /// Rob 24.11. (LMU Algarve): „Sara war fast stumm, Ben und Mik haben sie überchattet, dabei sollte Sara mit ihren Meldungen
        /// immer Vorrang haben.“ Ihre Warnung wartete hinter Bens 25 s langem Zwischenstand und verfiel. Eine Sara-Meldung ab Stufe 5
        /// löst jeden laufenden Funk von Ben und Mik mit niedrigerer Stufe ab (sinnvolle Meldungen werden später nachgereicht).
        /// Antworten auf Robs Fragen (Clip ohne Thema) weichen nur einer dringenden Gefahr ab Stufe 8; Spotter und Funkton laufen zu Ende.
        /// </summary>
        internal static bool CanSaraCut(bool currentIsAi, bool currentIsSpotter, bool currentIsBleep, int currentPriority, string currentTopic,
            int incomingPriority)
        {
            if (!currentIsAi || currentIsSpotter || currentIsBleep || incomingPriority < SaraPrecedencePriority || currentPriority >= incomingPriority)
                return false;
            return !string.IsNullOrEmpty(currentTopic) || incomingPriority >= InterruptAnswerPriority;
        }

        internal static bool CanInterruptForResult(bool currentIsAi, bool currentIsSpotter, bool currentIsBleep,
            int currentPriority, string currentTopic = null, string nextTopic = null)
        {
            return currentIsAi && !currentIsSpotter && !currentIsBleep &&
                // Quali 20.11.: Die neue Bestzeit kam 9 s nach der Ziellinie, weil eine Routinemeldung lief. Die eigene Rundenzeit
                // darf jede laufende Routinemeldung bis Stufe 4 ablösen; Antworten auf Robs Fragen (ohne Thema) laufen zu Ende.
                (currentPriority < 4 || nextTopic == "own-lap" && currentPriority == 4 && !string.IsNullOrEmpty(currentTopic));
        }

        internal static DateTime EngineerPlaybackDeadline(DateTime deadline, DateTime queued, bool safetyRadio, bool engineerCritical)
        {
            DateTime spare = queued.AddMilliseconds(500);
            return safetyRadio && engineerCritical && spare < deadline ? spare : deadline;
        }

        public static bool HasSpokenText(string text)
        { return text != null && text.Any(char.IsLetterOrDigit); }

        public static string VoiceIdentity(AiTeamSettings s, TeamMember member)
        {
            string voice = s.TtsProvider == AiTtsProvider.ELEVENLABS ? s.ElevenLabsModel + "|" + s.ElevenLabsVoiceFor(member)
                : s.TtsProvider == AiTtsProvider.WINDOWS ? s.WindowsVoiceFor(member) : s.TtsModel + "|" + s.VoiceFor(member);
            return s.TtsProvider + "|" + voice + "|radio=" + s.RadioEffect;
        }

        public static string CacheKey(AiTeamSettings s, TeamMember member, string text)
        { return VoiceIdentity(s, member) + "|" + text; }

        public static bool IsPreparedPhrase(TeamMember member, string text)
        {
            return member == TeamMember.Sara && (FlagWatch.Phrases.Contains(text) || text.Length <= 60 || HazardPhrases.Contains(text)) ||
                member == TeamMember.Mik && (LiveWarningDispatcher.MikPhrases.Contains(text) || ThermalWatch.PreparedPhrases.Contains(text) || TrackLimitPhrases.Contains(text) || IncidentPhrases.Contains(text) || WeatherPhrases.Contains(text) || PitLaneWatch.IsPreparedPhrase(text)) || member == TeamMember.Mik && (FinishPhrases.Contains(text) || text == RaceRadioLanguage.DisqualificationCall || text == SessionLapObserver.TimedLapStartPhrase || text == SessionLapObserver.TimedLapApproachPhrase) ||
                member == TeamMember.Ben && (TrackSituationWatch.ClosingAheadPhrases.Contains(text) || text == IncidentWatch.DamagePitAdvicePhrase ||
                    TrackSituationWatch.CatchingPhrases.Contains(text) || TrackSituationWatch.StoppedPhrases.Contains(text) ||
                    TrackSituationWatch.ApproachingPhrases.Contains(text) || RaceSituationWatch.IsPreparedPhrase(text) ||
                    RaceTrafficWatch.PreparedPhrases.Contains(text) || NativeIncidentWatch.Phrases.Contains(text) || MulticlassTraffic.PreparedPhrases.Contains(text));
        }

        public static bool CacheOnly(bool automatic, int priority, TeamMember member)
        { return automatic && (priority >= 8 && member == TeamMember.Sara || priority >= 7 && member == TeamMember.Ben); }

        // End-to-end lifetime includes synthesis and the audio queue, not only time after enqueue.
        public static int LifetimeSeconds(bool automatic, int priority)
        { return automatic ? (priority >= 8 ? 2 : priority >= 6 ? 6 : 10) : 20; }
    }
}
