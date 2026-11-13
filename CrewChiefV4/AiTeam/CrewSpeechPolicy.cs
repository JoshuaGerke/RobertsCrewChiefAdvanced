using System;
using System.Linq;
using System.Threading;

namespace CrewChiefV4.AiTeam
{
    /// <summary>At most one regular and one urgent render. Urgent rendering never waits for a normal cloud request.
    /// This is preparation only; the existing audio queue still gives Sara exclusive safety priority.</summary>
    public sealed class RadioRenderGate
    {
        private int regular,urgent,preparedRoutine;
        public bool PreparedRoutineBusy => Volatile.Read(ref preparedRoutine) != 0;
        public bool TryHoldRoutine() => Interlocked.CompareExchange(ref preparedRoutine, 1, 0) == 0;
        public void ReleaseRoutine() => Interlocked.Exchange(ref preparedRoutine, 0);
        public bool Busy {get{return Volatile.Read(ref regular)!=0 || Volatile.Read(ref urgent)!=0;}}
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
        private static readonly string[] FirstTrackLimitPhrasesDe = {
            "Pass auf die Streckenbegrenzung auf.",
            "Die Simulation meldet Tracklimits. Lass etwas Platz zum Rand.",
            "Achte auf die Limits. Bleib innerhalb der Begrenzung."
        };
        private static readonly string[] FirstTrackLimitPhrasesEn = {
            "Watch the track limits.",
            "The sim is flagging track limits. Leave some margin to the edge.",
            "Mind the limits. Stay inside the lines."
        };
        private static readonly string[] FirstTrackLimitPhrasesEs = {
            "Cuidado con los límites de pista.",
            "El simulador marca límites de pista. Deja algo de margen al borde.",
            "Atento a los límites. Mantente dentro de la pista."
        };
        public static string[] FirstTrackLimitPhrases => RadioLanguage.Pick(FirstTrackLimitPhrasesDe, FirstTrackLimitPhrasesEn, FirstTrackLimitPhrasesEs);
        private static readonly string[] RepeatedTrackLimitPhrasesDe = {
            "Wieder außerhalb der Limits. Lass etwas mehr Platz zum Rand.",
            "Noch eine Tracklimit-Meldung. Nimm am Rand etwas mehr Reserve.",
            "Schon wieder über den Limits. Bleib sauber auf der Strecke."
        };
        private static readonly string[] RepeatedTrackLimitPhrasesEn = {
            "Off the limits again. Leave a bit more margin to the edge.",
            "Another track limits warning. Give yourself more margin at the edge.",
            "Over the limits again. Keep it clean on track."
        };
        private static readonly string[] RepeatedTrackLimitPhrasesEs = {
            "Otra vez fuera de los límites. Deja algo más de margen al borde.",
            "Otro aviso de límites de pista. Guarda algo más de margen en el borde.",
            "Otra vez por fuera de los límites. Mantente limpio en pista."
        };
        public static string[] RepeatedTrackLimitPhrases => RadioLanguage.Pick(RepeatedTrackLimitPhrasesDe, RepeatedTrackLimitPhrasesEn, RepeatedTrackLimitPhrasesEs);
        private static readonly string[] FirstInvalidLapPhrasesDe = {
            "Diese Runde ist ungültig. Konzentrier dich auf die nächste Runde.",
            "Die Rundenzeit ist ungültig. Konzentrier dich auf die nächste gültige Zeit.",
            "Runde ungültig. Ruhig bleiben und wieder eine gültige Zeit setzen."
        };
        private static readonly string[] FirstInvalidLapPhrasesEn = {
            "This lap is invalid. Focus on the next one.",
            "The lap time is invalid. Focus on the next valid time.",
            "Lap invalid. Stay calm and set a valid time again."
        };
        private static readonly string[] FirstInvalidLapPhrasesEs = {
            "Esta vuelta no es válida. Concéntrate en la siguiente.",
            "El tiempo de vuelta no es válido. Concéntrate en el próximo tiempo válido.",
            "Vuelta invalidada. Tranquilo y vuelve a marcar un tiempo válido."
        };
        public static string[] FirstInvalidLapPhrases => RadioLanguage.Pick(FirstInvalidLapPhrasesDe, FirstInvalidLapPhrasesEn, FirstInvalidLapPhrasesEs);
        private static readonly string[] RepeatedInvalidLapPhrasesDe = {
            "Noch eine ungültige Runde. Wir brauchen wieder eine gültige Zeit.",
            "Wieder keine gültige Rundenzeit. Bleib konzentriert.",
            "Die Runde ist erneut ungültig. Konzentrier dich auf die nächste."
        };
        private static readonly string[] RepeatedInvalidLapPhrasesEn = {
            "Another invalid lap. We need a valid time again.",
            "No valid lap time again. Stay focused.",
            "The lap is invalid again. Focus on the next one."
        };
        private static readonly string[] RepeatedInvalidLapPhrasesEs = {
            "Otra vuelta inválida. Necesitamos de nuevo un tiempo válido.",
            "Otra vez sin tiempo válido. Mantén la concentración.",
            "La vuelta vuelve a ser inválida. Concéntrate en la siguiente."
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
            "Erste Regentropfen gemeldet. Bleib aufmerksam."
        };
        private static readonly string[] RainStartPhrasesEn = {
            "Rain is starting. Watch the conditions.",
            "It's starting to rain. Drive with anticipation.",
            "First raindrops reported. Stay alert."
        };
        private static readonly string[] RainStartPhrasesEs = {
            "Empieza a llover. Atento a las condiciones.",
            "Está empezando a llover. Conduce con previsión.",
            "Primeras gotas de lluvia. Mantente atento."
        };
        public static string[] RainStartPhrases => RadioLanguage.Pick(RainStartPhrasesDe, RainStartPhrasesEn, RainStartPhrasesEs);
        private static readonly string[] RainStopPhrasesDe = {
            "Der Regen ist abgeklungen. Die Strecke kann weiter nass sein.",
            "Kein nennenswerter Regen mehr gemeldet. Pass weiter auf die Nässe auf.",
            "Der Regen lässt nach. Mit der Strecke weiter vorsichtig bleiben."
        };
        private static readonly string[] RainStopPhrasesEn = {
            "The rain has eased off. The track may still be wet.",
            "No significant rain reported anymore. Keep watching for wet patches.",
            "The rain is easing. Stay careful on the track."
        };
        private static readonly string[] RainStopPhrasesEs = {
            "La lluvia ha amainado. La pista puede seguir mojada.",
            "Ya no se reporta lluvia relevante. Sigue atento a la humedad.",
            "La lluvia remite. Sigue con cuidado en pista."
        };
        public static string[] RainStopPhrases => RadioLanguage.Pick(RainStopPhrasesDe, RainStopPhrasesEn, RainStopPhrasesEs);
        private static readonly string[] WetterPhrasesDe = {
            "Mehr Streckennässe gemeldet. Bleib vorsichtig.",
            "Die gemeldete Streckennässe nimmt zu. Fahr mit Reserve."
        };
        private static readonly string[] WetterPhrasesEn = {
            "Track reported wetter. Stay careful.",
            "Reported track wetness is increasing. Drive with margin."
        };
        private static readonly string[] WetterPhrasesEs = {
            "Se reporta más agua en pista. Ten cuidado.",
            "La humedad reportada en pista aumenta. Conduce con margen."
        };
        public static string[] WetterPhrases => RadioLanguage.Pick(WetterPhrasesDe, WetterPhrasesEn, WetterPhrasesEs);
        private static readonly string[] DrierPhrasesDe = {
            "Weniger Streckennässe gemeldet. Das heißt noch nicht, dass überall Grip da ist.",
            "Die gemeldete Streckennässe nimmt ab. Geh weiter mit Gefühl ans Gas."
        };
        private static readonly string[] DrierPhrasesEn = {
            "Track reported drier. That doesn't mean there's grip everywhere yet.",
            "Reported track wetness is dropping. Stay gentle on the throttle."
        };
        private static readonly string[] DrierPhrasesEs = {
            "Se reporta menos agua en pista. Eso no significa que haya agarre en todas partes.",
            "La humedad reportada en pista baja. Sigue suave con el gas."
        };
        public static string[] DrierPhrases => RadioLanguage.Pick(DrierPhrasesDe, DrierPhrasesEn, DrierPhrasesEs);
        private static readonly string[] PitSpeedPhrasesDe = {
            "Boxenlimit, du bist zu schnell. Tempo runter.",
            "Achte aufs Boxenlimit. Reduziere dein Tempo."
        };
        private static readonly string[] PitSpeedPhrasesEn = {
            "Pit limit, you're too fast. Slow down.",
            "Watch the pit speed limit. Bring your speed down."
        };
        private static readonly string[] PitSpeedPhrasesEs = {
            "Límite de boxes, vas demasiado rápido. Baja la velocidad.",
            "Atento al límite de boxes. Reduce la velocidad."
        };
        public static string[] PitSpeedPhrases => RadioLanguage.Pick(PitSpeedPhrasesDe, PitSpeedPhrasesEn, PitSpeedPhrasesEs);
        public static string[] WeatherPhrases => PitSpeedPhrases.Concat(RainStartPhrases).Concat(RainStopPhrases).Concat(WetterPhrases).Concat(DrierPhrases).ToArray();

        private static readonly string[] ContactPhrasesDe = {
            "Kontakt. Ich schau mir den Schaden an.",
            "Das hat gerumst. Ich prüf das Auto.",
            "Kontakt gesehen. Moment, ich check den Schaden.",
            "Autsch. Ich schau, was am Auto ist."
        };
        private static readonly string[] ContactPhrasesEn = {
            "Contact. I'll check the damage.",
            "That was a hit. Checking the car.",
            "Saw the contact. Stand by, checking the damage.",
            "Ouch. Let me see what's up with the car."
        };
        private static readonly string[] ContactPhrasesEs = {
            "Contacto. Reviso los daños.",
            "Eso ha sido un golpe. Reviso el coche.",
            "He visto el contacto. Un momento, compruebo los daños.",
            "Uf. Miro a ver cómo está el coche."
        };
        public static string[] ContactPhrases => RadioLanguage.Pick(ContactPhrasesDe, ContactPhrasesEn, ContactPhrasesEs);
        // Nur bei belegtem Kontakt mit starkem Tempoverlust oder schwerem Schaden, danach kommt der Schadensbericht.
        private static readonly string[] HeavyContactPhrasesDe = {
            "Uff, das war heftig. Alles okay bei dir?",
            "Oh, das sah nicht gut aus. Geht's dir gut?",
            "Das war ein harter Einschlag. Alles in Ordnung?",
            "Oha. Alles okay bei dir? Ich schau mir das Auto an."
        };
        private static readonly string[] HeavyContactPhrasesEn = {
            "Oof, that was a big one. You okay?",
            "Oh, that didn't look good. Are you alright?",
            "That was a hard impact. Everything okay?",
            "Whoa. You okay? I'll check the car."
        };
        private static readonly string[] HeavyContactPhrasesEs = {
            "Uf, eso ha sido fuerte. ¿Estás bien?",
            "Uy, eso no tenía buena pinta. ¿Todo bien?",
            "Ha sido un impacto duro. ¿Todo en orden?",
            "Vaya. ¿Estás bien? Reviso el coche."
        };
        public static string[] HeavyContactPhrases => RadioLanguage.Pick(HeavyContactPhrasesDe, HeavyContactPhrasesEn, HeavyContactPhrasesEs);
        private static readonly string[] StoppedPhrasesDe = {
            "Alles okay? Beim Wiederanfahren auf den Verkehr achten.",
            "Alles gut bei dir? Erst schauen, dann wieder rein.",
            "Alles klar? Ganz ruhig, pass beim Anfahren auf den Verkehr auf."
        };
        private static readonly string[] StoppedPhrasesEn = {
            "You okay? Watch the traffic when you get going again.",
            "All good? Look first, then rejoin.",
            "Alright? Nice and calm, watch the traffic as you pull away."
        };
        private static readonly string[] StoppedPhrasesEs = {
            "¿Todo bien? Cuidado con el tráfico al arrancar.",
            "¿Estás bien? Primero mira, luego vuelve a pista.",
            "¿Todo en orden? Con calma, atento al tráfico al arrancar."
        };
        public static string[] StoppedPhrases => RadioLanguage.Pick(StoppedPhrasesDe, StoppedPhrasesEn, StoppedPhrasesEs);
        private static readonly string[] OffTrackPhrasesDe = {
            "Neben der Strecke. Beim Zurückfahren auf den Verkehr achten.",
            "Du bist draußen. Schau auf den Verkehr, bevor du zurückkommst.",
            "Achtung, neben der Strecke. Komm kontrolliert zurück."
        };
        private static readonly string[] OffTrackPhrasesEn = {
            "Off track. Watch the traffic when you rejoin.",
            "You're off. Check the traffic before you come back on.",
            "Careful, off track. Rejoin under control."
        };
        private static readonly string[] OffTrackPhrasesEs = {
            "Fuera de pista. Cuidado con el tráfico al volver.",
            "Estás fuera. Mira el tráfico antes de volver a entrar.",
            "Atención, fuera de pista. Vuelve con control."
        };
        public static string[] OffTrackPhrases => RadioLanguage.Pick(OffTrackPhrasesDe, OffTrackPhrasesEn, OffTrackPhrasesEs);
        private static readonly string[] DamageDetectedPhrasesDe = {
            "Neuer Schaden gemeldet. Ich schau mir das an.",
            "Die Telemetrie meldet mehr Schaden. Ich prüf das Auto.",
            "Am Auto ist neuer Schaden gemeldet. Ich sehe nach.",
            "Ein Schadenswert ist gestiegen. Ich prüfe, was betroffen ist."
        };
        private static readonly string[] DamageDetectedPhrasesEn = {
            "New damage reported. I'll take a look.",
            "Telemetry shows more damage. Checking the car.",
            "New damage reported on the car. I'm looking into it.",
            "A damage value has gone up. Checking what's affected."
        };
        private static readonly string[] DamageDetectedPhrasesEs = {
            "Nuevo daño reportado. Lo reviso.",
            "La telemetría marca más daños. Reviso el coche.",
            "Hay daño nuevo en el coche. Lo miro.",
            "Ha subido un valor de daño. Compruebo qué está afectado."
        };
        public static string[] DamageDetectedPhrases => RadioLanguage.Pick(DamageDetectedPhrasesDe, DamageDetectedPhrasesEn, DamageDetectedPhrasesEs);
        public static string RainTrackHint => RadioLanguage.T(" Es regnet gerade, vorsichtig am Gas.", " It's raining right now, careful on the throttle.", " Está lloviendo, cuidado con el gas.");
        public static string WetTrackHint => RadioLanguage.T(" Die Strecke ist nass, vorsichtig am Gas.", " The track is wet, careful on the throttle.", " La pista está mojada, cuidado con el gas.");
        public static string[] IncidentPhrases => ContactPhrases.Concat(HeavyContactPhrases).Concat(DamageDetectedPhrases).Concat(StoppedPhrases).Concat(OffTrackPhrases)
            .Concat(OffTrackPhrases.Select(p => p + WetTrackHint)).Concat(OffTrackPhrases.Select(p => p + RainTrackHint)).ToArray();

        public static bool CanInterruptForHazard(bool currentIsAi, bool currentIsSpotter, bool currentIsBleep, int currentPriority, int incomingPriority)
        { return currentIsAi && !currentIsSpotter && !currentIsBleep && incomingPriority >= 7 && currentPriority < incomingPriority; }

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
            return member == TeamMember.Sara && (FlagWatch.Phrases.Contains(text) || text.Length <= 60) ||
                member == TeamMember.Mik && (LiveWarningDispatcher.MikPhrases.Contains(text) || ThermalWatch.PreparedPhrases.Contains(text) || TrackLimitPhrases.Contains(text) || IncidentPhrases.Contains(text) || WeatherPhrases.Contains(text) || PitLaneWatch.IsPreparedPhrase(text)) || member == TeamMember.Mik && (FinishPhrases.Contains(text) || text == SessionLapObserver.TimedLapStartPhrase) ||
                member == TeamMember.Ben && (TrackSituationWatch.CatchingPhrases.Contains(text) || TrackSituationWatch.StoppedPhrases.Contains(text) || TrackSituationWatch.ApproachingPhrases.Contains(text) || RaceSituationWatch.IsPreparedPhrase(text) || RaceTrafficWatch.PreparedPhrases.Contains(text) || NativeIncidentWatch.Phrases.Contains(text));
        }

        public static bool CacheOnly(bool automatic, int priority, TeamMember member)
        { return automatic && (priority >= 8 && member == TeamMember.Sara || priority >= 7 && member == TeamMember.Ben); }

        // End-to-end lifetime includes synthesis and the audio queue, not only time after enqueue.
        public static int LifetimeSeconds(bool automatic, int priority)
        { return automatic ? (priority >= 8 ? 2 : priority >= 6 ? 6 : 10) : 20; }
    }
}
