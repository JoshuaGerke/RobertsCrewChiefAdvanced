using System;
using System.Collections.Generic;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rennkopf, Teil 2: Zusammenhang zwischen Abstand und Rundentempo erkennen, wie es ein Renningenieur tut.
    /// Pro Runde misst der Rennkopf den Abstand zum Klassen-Vorder- und Hintermann (nur derselbe Gegner, keine Box- oder
    /// reinen Streckenmeter-Schätzungen). Nach mindestens drei Runden mit gleichgerichtetem Trend rechnet Ben hoch: „Du holst drei Zehntel pro Runde auf
    /// Keller, in etwa fünf Runden bist du dran“ – oder er ordnet ein, dass es bis zum Ende nicht reicht bzw. der Hintermann
    /// uns bis zur Flagge nicht erreicht. Kein Zweikampf (unter 1 s), nicht in der letzten Runde, je Gegner höchstens alle vier Runden.
    /// </summary>
    internal sealed class GapTrendWatch
    {
        internal const float MinRate = .15f, CatchMargin = .8f;
        private sealed class Announced { public int Lap = -100; public string Id; }

        private readonly Announced front = new Announced(), rear = new Announced();
        private int session = int.MinValue, lastLap = -1, lastCallLap = -100, variant;
        private ObservedRaceCall call;

        internal void Reset()
        {
            session = int.MinValue; lastLap = -1; lastCallLap = -100; call = null;
            foreach (var t in new[] { front, rear }) { t.Lap = -100; t.Id = null; }
        }

        /// <summary>Hat Ben über diesen Gegner in den letzten Runden schon einen Trend gemeldet? (Verhindert doppelte KI-Trendmeldung.)</summary>
        internal bool CoversRecently(bool ahead, int completedLaps) => completedLaps - (ahead ? front : rear).Lap < 4;

        /// <summary>Zeit pro Runde, um die sich der Abstand verkleinert (positiv = schrumpft); 0 ohne klaren, gleichgerichteten Trend.</summary>
        internal static float ClosingRate(IList<float> gaps)
        {
            if (gaps == null || gaps.Count < 4) return 0;
            float rate = (gaps[0] - gaps[gaps.Count - 1]) / (gaps.Count - 1);
            int agree = 0;
            for (int i = 1; i < gaps.Count; i++)
            {
                float step = gaps[i - 1] - gaps[i];
                if (Math.Sign(step) == Math.Sign(rate)) agree++;
                else if (Math.Abs(step) > Math.Abs(rate)) return 0; // großer Rücksprung: kein verlässlicher Trend
            }
            return agree >= gaps.Count - 2 ? rate : 0; // höchstens ein kleiner Ausreißer
        }

        /// <summary>Runden, bis der Abstand auf Angriffsdistanz (0,8 s) geschrumpft ist; -1 ohne Annäherung.</summary>
        internal static int LapsToCatch(float gap, float rate) => rate < MinRate || gap <= CatchMargin ? -1 : (int)Math.Ceiling((gap - CatchMargin) / rate);

        /// <summary>Die Abstandsmessung je Runde führt der Rennkopf (RaceMind); hier wird nur bewertet und formuliert.</summary>
        internal void Observe(GameStateData gs, RaceMind mind, Func<RaceDataContext> context, DateTime now)
        {
            if (gs == null || mind == null) return;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) Reset(); return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (s.SessionPhase != SessionPhase.Green || mind.SampledLap == lastLap || mind.SampledLap < 2) return;
            lastLap = mind.SampledLap;
            if (call != null || s.CompletedLaps - lastCallLap < 2) return;
            int left = LapWatch.LapsLeft(s);
            if (left == 1) return;
            var ctx = context();
            string text = Decide(ctx, mind.Front, front, true, left) ?? Decide(ctx, mind.Rear, rear, false, left);
            if (text == null) return;
            lastCallLap = s.CompletedLaps;
            call = new ObservedRaceCall
            {
                Topic = "gap-trend", Text = text, Evidence = "measured_class_gap_over_laps_same_rival", Member = TeamMember.Ben, Priority = 3,
                Position = s.ClassPosition, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(20),
                AdditionalValidity = live => live.SessionData.ClassPosition == s.ClassPosition && !live.PitData.InPitlane
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        private string Decide(RaceDataContext ctx, GapHistory t, Announced said, bool ahead, int lapsLeft)
        {
            var s = ctx.GameState.SessionData;
            if (t.Gaps.Count < 4 || t.Id == null) return null;
            var op = RaceDataTools.Neighbour(ctx.GameState, ahead);
            if (op == null || OpponentStopLog.Key(op) != t.Id) return null;
            if (said.Id == t.Id && s.CompletedLaps - said.Lap < 4) return null;
            if (ahead && s.ClassPosition <= 1) return null;
            float gap = t.Gaps[t.Gaps.Count - 1], rate = ClosingRate(t.Gaps);
            if (gap < 1f || gap > (ahead ? 10f : 8f) || rate < MinRate) return null;
            int laps = LapsToCatch(gap, rate);
            if (laps < 1 || laps > (ahead ? 15 : 10)) return null;
            string name = ctx.SendDriverNames && op.CanUseName ? RadioPhraseCatalog.SafeRivalName(op.DriverRawName) : "";
            // Bis zur Flagge sind lapsLeft - 1 volle Runden zum Aufholen übrig (die aktuelle läuft schon).
            bool tooLate = lapsLeft > 1 && laps > lapsLeft - 1;
            said.Id = t.Id; said.Lap = s.CompletedLaps;
            // Rennverlauf: trifft die Hochrechnung ein, sagt Ben später „wie angekündigt“.
            if (!tooLate) RaceStorylineWatch.Expect(s.SessionId, t.Id, ahead, s.CompletedLaps + laps);
            int v = variant++;
            return ahead ? AheadText(name, rate, laps, tooLate, lapsLeft, gap, v) : BehindText(name, rate, laps, tooLate, v);
        }

        internal static string Rate(float rate)
        {
            if (rate >= .95f) return GermanRadioText.Interval(rate);
            int tenths = Math.Max(1, (int)Math.Round(rate * 10, MidpointRounding.AwayFromZero));
            return tenths == 1 ? RadioLanguage.T("ein Zehntel", "a tenth", "una décima")
                : tenths + RadioLanguage.T(" Zehntel", " tenths", " décimas");
        }

        private static string Laps(int laps) => laps == 1 ? RadioLanguage.T("einer Runde", "one lap", "una vuelta")
            : RadioLanguage.T("etwa " + laps + " Runden", "about " + laps + " laps", "unas " + laps + " vueltas");

        internal static string AheadText(string name, float rate, int laps, bool tooLate, int lapsLeft, float gap, int variant)
        {
            string who = name.Length > 0 ? name : RadioLanguage.T("den Vordermann", "the car ahead", "el de delante");
            string r = Rate(rate);
            if (tooLate)
            {
                float needed = (gap - CatchMargin) / Math.Max(1, lapsLeft - 1);
                return RadioLanguage.T("Du holst " + r + " pro Runde auf " + who + ", das reicht so knapp nicht bis zum Ende. Du bräuchtest etwa " + Rate(needed) + " pro Runde.",
                    "You're gaining " + r + " a lap on " + who + ", not quite enough before the flag. You'd need about " + Rate(needed) + " a lap.",
                    "Recortas " + r + " por vuelta a " + who + ", no basta antes de la bandera. Necesitarías unas " + Rate(needed) + " por vuelta.");
            }
            switch (((variant % 6) + 6) % 6)
            {
                case 3: return RadioLanguage.T("Der Abstand " + (name.Length > 0 ? "zu " + name : "nach vorn") + " schmilzt, " + r + " pro Runde. In " + Laps(laps) + " kannst du angreifen.",
                    "The gap to " + who + " is melting, " + r + " a lap. In " + Laps(laps) + " you can attack.",
                    "La diferencia con " + who + " se derrite, " + r + " por vuelta. En " + Laps(laps) + " puedes atacar.");
                case 4: return RadioLanguage.T("Weiter so, du kommst " + r + " pro Runde näher an " + who + ", in " + Laps(laps) + " bist du da.",
                    "Keep it up, you're closing " + r + " a lap on " + who + ", you'll be there in " + Laps(laps) + ".",
                    "Sigue así, recortas " + r + " por vuelta sobre " + who + ", en " + Laps(laps) + " estás ahí.");
                case 5: return RadioLanguage.T((name.Length > 0 ? name : "Der Vordermann") + " ist in Reichweite: " + r + " pro Runde für dich, in " + Laps(laps) + " bist du an der Stoßstange.",
                    Cap(who) + " is in range: " + r + " a lap to you, in " + Laps(laps) + " you're on the bumper.",
                    Cap(who) + " está a tiro: " + r + " por vuelta a tu favor, en " + Laps(laps) + " estás en su parachoques.");
                case 0: return RadioLanguage.T("Du holst " + r + " pro Runde auf " + who + ". In " + Laps(laps) + " bist du dran.",
                    "You're gaining " + r + " a lap on " + who + ". You'll be on him in " + Laps(laps) + ".",
                    "Recortas " + r + " por vuelta a " + who + ". En " + Laps(laps) + " lo tienes.");
                case 1: return RadioLanguage.T((name.Length > 0 ? name : "Der Vordermann") + " verliert " + r + " pro Runde auf dich. So bist du in " + Laps(laps) + " an ihm dran.",
                    Cap(who) + " is losing " + r + " a lap to you. At this rate you're on him in " + Laps(laps) + ".",
                    Cap(who) + " pierde " + r + " por vuelta contigo. A este ritmo lo alcanzas en " + Laps(laps) + ".");
                default: return RadioLanguage.T("Gutes Tempo: " + r + " pro Runde schneller als " + (name.Length > 0 ? name : "der Vordermann") + ". Rechnerisch " + Laps(laps) + " bis zum Angriff.",
                    "Good pace: " + r + " a lap quicker than " + who + ". On paper " + Laps(laps) + " until you can attack.",
                    "Buen ritmo: " + r + " por vuelta más rápido que " + who + ". En teoría " + Laps(laps) + " hasta atacar.");
            }
        }

        internal static string BehindText(string name, float rate, int laps, bool tooLate, int variant)
        {
            string who = name.Length > 0 ? name : RadioLanguage.T("der Hintermann", "the car behind", "el de detrás");
            string r = Rate(rate);
            if (tooLate)
                return RadioLanguage.T(Cap(who) + " kommt " + r + " pro Runde näher, aber bis zur Flagge reicht es ihm so nicht. Ruhig und sauber weiter.",
                    Cap(who) + " is closing " + r + " a lap, but at this rate he won't make it before the flag. Calm and clean.",
                    Cap(who) + " se acerca " + r + " por vuelta, pero así no llega antes de la bandera. Tranquilo y limpio.");
            switch (((variant % 6) + 6) % 6)
            {
                case 3: return RadioLanguage.T("Info nach hinten: " + who + " ist " + r + " pro Runde schneller, in " + Laps(laps) + " bei dir. Konzentrier dich auf deine Ausfahrten.",
                    "Info behind: " + who + " is " + r + " a lap quicker, with you in " + Laps(laps) + ". Focus on your exits.",
                    "Información detrás: " + who + " es " + r + " por vuelta más rápido, contigo en " + Laps(laps) + ". Céntrate en las salidas.");
                case 4: return RadioLanguage.T(Cap(who) + " kommt, " + r + " pro Runde. Bleib bei deinem Rhythmus, keine Fehler.",
                    Cap(who) + " is coming, " + r + " a lap. Stay in your rhythm, no mistakes.",
                    Cap(who) + " se acerca, " + r + " por vuelta. Mantén tu ritmo, sin errores.");
                case 5: return RadioLanguage.T("Hinten wird es enger: " + r + " pro Runde für " + (name.Length > 0 ? name : "den Hintermann") + ". Rechnerisch " + Laps(laps) + ", dann ist er da.",
                    "It's getting closer behind: " + r + " a lap for " + who + ". On paper " + Laps(laps) + " and he's there.",
                    "Se estrecha detrás: " + r + " por vuelta para " + who + ". En teoría " + Laps(laps) + " y está ahí.");
                case 0: return RadioLanguage.T(Cap(who) + " holt " + r + " pro Runde auf. In " + Laps(laps) + " ist er dran.",
                    Cap(who) + " is gaining " + r + " a lap. He'll be with you in " + Laps(laps) + ".",
                    Cap(who) + " recorta " + r + " por vuelta. En " + Laps(laps) + " te alcanza.");
                case 1: return RadioLanguage.T("Achtung, " + who + " ist " + r + " pro Runde schneller. Rechnerisch in " + Laps(laps) + " hinter dir.",
                    "Heads up, " + who + " is " + r + " a lap quicker. On paper he's with you in " + Laps(laps) + ".",
                    "Ojo, " + who + " es " + r + " por vuelta más rápido. En teoría te alcanza en " + Laps(laps) + ".");
                default: return RadioLanguage.T(Cap(who) + " macht Druck: " + r + " pro Runde. Saubere Runden, dann bleibt er hinten.",
                    Cap(who) + " is pushing: " + r + " a lap. Clean laps and he stays behind.",
                    Cap(who) + " aprieta: " + r + " por vuelta. Vueltas limpias y se queda detrás.");
            }
        }

        private static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
