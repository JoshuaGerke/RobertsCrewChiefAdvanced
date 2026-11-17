using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Reifen-Gripeinordnung (Robs Wunsch 16.11.2026 nach Mosport): „Ziemlich heiß, dir fehlt Traktion und Grip zurzeit.
    /// Warte, bis sie sich wieder abgekühlt haben" – und dasselbe bei dreckigen Reifen, wenn die Simulation Schmutz berechnet.
    /// Mik meldet:
    ///  - überhitzte Reifen je Ecke (über dem Crew-Chief-Grenzwert für den Reifentyp oder deutlich heißer als die übrigen drei),
    ///    mit Folge für den Grip und Abkühlhinweis;
    ///  - einmal je Stint, wenn die Reifen nach dem Anwärmen im Fenster sind („jetzt kannst du pushen");
    ///  - Schmutz auf den Reifen, wenn das Spiel ihn liefert (RaceRoom, rFactor 2/LMU), sonst nach einem Ausflug neben die
    ///    Strecke ausdrücklich als Vermutung.
    /// Das Temperaturfenster stammt aus Crew Chiefs Grenzwerten für Klasse/Reifentyp – ein Richtwert, kein Herstellerwert.
    /// </summary>
    internal sealed class TyreGripWatch
    {
        internal const float ImbalanceDegrees = 12f, MinHotForImbalance = 85f;
        private static readonly string[] CornerDe = { "vorn links", "vorn rechts", "hinten links", "hinten rechts" };
        private static readonly string[] CornerEn = { "front left", "front right", "rear left", "rear right" };
        private static readonly string[] CornerEs = { "el delantero izquierdo", "el delantero derecho", "el trasero izquierdo", "el trasero derecho" };

        private int session = int.MinValue, variant;
        private DateTime hotSince = DateTime.MinValue, coolSince = DateTime.MinValue, lastCall = DateTime.MinValue, offSince = DateTime.MinValue, backOnSince = DateTime.MinValue;
        private bool hotArmed = true, coldSeen, warmReported, dirtArmed = true, offLong;
        private string lastHotKey;
        private ObservedRaceCall call;

        internal void Reset()
        {
            session = int.MinValue; hotSince = coolSince = lastCall = offSince = backOnSince = DateTime.MinValue;
            hotArmed = dirtArmed = true; coldSeen = warmReported = offLong = false; lastHotKey = null; call = null;
        }

        /// <summary>Temperaturfenster (warm) aus den Crew-Chief-Grenzwerten; false ohne Klasse oder bei unplausiblen Werten.</summary>
        /// <summary>
        /// Robs Richtwert für Rennreifen (16.11.2026, Bathurst: „ab 70 Grad in Ordnung, ab 80 gut, 85 bis 90 sehr gut“).
        /// Die Crew-Chief-Sammelgrenzen (z. B. 55–95 °C) waren für Slicks zu niedrig und haben zu früh „pushen“ gemeldet.
        /// </summary>
        internal const float SlickGood = 80f, SlickTop = 100f;

        /// <summary>Regen-, Intermediate- und Straßenreifen haben andere Fenster; dort gelten weiter die Crew-Chief-Grenzwerte.</summary>
        internal static bool IsRaceSlick(TyreType t) => t != TyreType.Wet && t != TyreType.Intermediate && t != TyreType.Road && t != TyreType.Ice &&
            t != TyreType.Snow && t != TyreType.AllTerrain && t != TyreType.Bias_Ply;

        /// <summary>Gutes Temperaturfenster: für Slicks Robs Richtwert (ab 80 °C gut, bis etwa 100 °C), sonst Crew-Chief-Grenzwert mit engem Fenster.</summary>
        internal static bool Window(GameStateData gs, out float cold, out float hot)
        {
            cold = hot = -1;
            if (gs == null) return false;
            if (IsRaceSlick(gs.TyreData.FrontLeftTyreType)) { cold = SlickGood; hot = SlickTop; return true; }
            if (gs.carClass == null) return false;
            try
            {
                var t = CarData.getTyreTempThresholds(gs.carClass, gs.TyreData.FrontLeftTyreType);
                var warm = t.FirstOrDefault(x => x.e is TyreTemp && (TyreTemp)x.e == TyreTemp.WARM);
                if (warm == null || warm.lowerThreshold < 20 || warm.upperThreshold > 200 || warm.upperThreshold <= warm.lowerThreshold) return false;
                // Ein sehr breites Sammelfenster (unbekannter Reifentyp, z. B. 60–117 °C) wäre als Zahl irreführend.
                if (warm.upperThreshold - warm.lowerThreshold > 45) return false;
                cold = warm.lowerThreshold; hot = warm.upperThreshold;
                return true;
            }
            catch (Exception) { return false; }
        }

        private static bool Good(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v > 1 && v < 400;
        private static float Mean(float a, float b, float c) => Good(a) && Good(b) && Good(c) ? (a + b + c) / 3 : -1;

        internal static float[] Temps(GameStateData gs)
        {
            var t = gs.TyreData;
            return new[] { Mean(t.FrontLeft_LeftTemp, t.FrontLeft_CenterTemp, t.FrontLeft_RightTemp),
                Mean(t.FrontRight_LeftTemp, t.FrontRight_CenterTemp, t.FrontRight_RightTemp),
                Mean(t.RearLeft_LeftTemp, t.RearLeft_CenterTemp, t.RearLeft_RightTemp),
                Mean(t.RearRight_LeftTemp, t.RearRight_CenterTemp, t.RearRight_RightTemp) };
        }

        /// <summary>Überhitzte Ecken: über dem Fenster (+3 °C) oder mindestens 12 °C heißer als der Schnitt der anderen drei und über 85 °C.</summary>
        internal static List<int> HotCorners(float[] temps, float windowTop)
        {
            var hot = new List<int>();
            if (temps == null || temps.Length != 4 || temps.Any(v => v <= 0)) return hot;
            for (int i = 0; i < 4; i++)
            {
                float others = (temps.Sum() - temps[i]) / 3;
                if (windowTop > 0 && temps[i] > windowTop + 3 || temps[i] >= MinHotForImbalance && temps[i] - others >= ImbalanceDegrees) hot.Add(i);
            }
            return hot;
        }

        /// <summary>Deutsch, auch für den Kontext des Sprachmodells (RaceDataTools); am Funk RadioCorners.</summary>
        internal static string Corners(List<int> c) => Join(c, "alle vier Reifen", "beide Hinterreifen", "beide Vorderreifen", CornerDe, " und ");

        internal static string RadioCorners(List<int> c) => RadioLanguage.T(Corners(c),
            Join(c, "all four tyres", "both rears", "both fronts", CornerEn, " and "),
            Join(c, "los cuatro neumáticos", "los dos traseros", "los dos delanteros", CornerEs, " y "));

        private static string Join(List<int> c, string all, string rears, string fronts, string[] names, string and)
        {
            if (c.Count == 4) return all;
            if (c.Count == 2 && c.Contains(2) && c.Contains(3)) return rears;
            if (c.Count == 2 && c.Contains(0) && c.Contains(1)) return fronts;
            return string.Join(and, c.Select(i => names[i]));
        }

        internal static string HotText(List<int> corners, float[] temps, float windowTop, int v)
        {
            string where = RadioCorners(corners);
            string deg = RadioLanguage.Num(corners.Max(i => temps[i]), "0");
            string window = windowTop > 0 ? RadioLanguage.T(", das Fenster endet bei etwa " + RadioLanguage.Num(windowTop, "0") + " Grad",
                ", the window tops out around " + RadioLanguage.Num(windowTop, "0") + " degrees", ", la ventana acaba en unos " + RadioLanguage.Num(windowTop, "0") + " grados") : "";
            bool rears = corners.All(i => i >= 2);
            switch (((v % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T(Cap(where) + " ziemlich heiß, " + deg + " Grad" + window + ". Dir fehlt gerade " + (rears ? "Traktion und Grip" : "Grip") + ". Ein, zwei Kurven sanfter, bis sie runterkommen.",
                    Cap(where) + " running hot, " + deg + " degrees" + window + ". You're short of " + (rears ? "traction and grip" : "grip") + " right now. Ease off for a corner or two until they cool.",
                    "Mucha temperatura en " + where + ", " + deg + " grados" + window + ". Te falta " + (rears ? "tracción y agarre" : "agarre") + ". Suaviza una o dos curvas hasta que baje.");
                case 1: return RadioLanguage.T("Achtung, " + where + " überhitzt mit " + deg + " Grad. Da baut der Grip ab, lass sie kurz abkühlen.",
                    "Careful, " + where + " overheating at " + deg + " degrees. Grip is dropping, let them cool a little.",
                    "Ojo, sobrecalentamiento en " + where + ", " + deg + " grados. El agarre cae, deja que baje la temperatura.");
                case 2: return RadioLanguage.T(Cap(where) + " zu heiß, " + deg + " Grad. " + (rears ? "Am Kurvenausgang weicher aufs Gas" : "Weniger in die Kurve reinbremsen") + ", dann kommt der Grip zurück.",
                    Cap(where) + " too hot, " + deg + " degrees. " + (rears ? "Softer on the throttle on exit" : "Less trail braking into the corners") + " and the grip comes back.",
                    "Demasiada temperatura en " + where + ", " + deg + " grados. " + (rears ? "Más suave con el gas a la salida" : "Frena menos dentro de la curva") + " y vuelve el agarre.");
                default: return RadioLanguage.T("Die Reifen " + where + " sind über dem Fenster, " + deg + " Grad. Gib ihnen eine ruhigere Runde, sonst rutschst du.",
                    Cap(where) + " above the window, " + deg + " degrees. Give it a calmer lap or you'll slide.",
                    "Por encima de la ventana en " + where + ", " + deg + " grados. Haz una vuelta más tranquila o vas a deslizar.");
            }
        }

        internal static string WarmText(float cold, float top, int v)
        {
            bool slickGuide = cold == SlickGood && top == SlickTop;
            string window = slickGuide
                ? RadioLanguage.T(" Ab 80 Grad gut, 85 bis 90 sehr gut.", " 80 degrees and up is good, 85 to 90 is ideal.", " Desde 80 grados bien, de 85 a 90 ideal.")
                : top > 0 ? RadioLanguage.T(" Fenster etwa " + RadioLanguage.Num(cold, "0") + " bis " + RadioLanguage.Num(top, "0") + " Grad.",
                    " Window about " + RadioLanguage.Num(cold, "0") + " to " + RadioLanguage.Num(top, "0") + " degrees.",
                    " Ventana de unos " + RadioLanguage.Num(cold, "0") + " a " + RadioLanguage.Num(top, "0") + " grados.") : "";
            switch (((v % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T("Alle vier Reifen jetzt über " + RadioLanguage.Num(cold, "0") + " Grad, du kannst pushen.",
                    "All four tyres above " + RadioLanguage.Num(cold, "0") + " degrees now, you can push.",
                    "Los cuatro neumáticos ya por encima de " + RadioLanguage.Num(cold, "0") + " grados, puedes apretar.") + window;
                case 1: return RadioLanguage.T("Die Reifen haben Temperatur. Jetzt geht's los.", "Tyres are up to temperature. Let's go.", "Los neumáticos tienen temperatura. Vamos.") + window;
                default: return RadioLanguage.T("Reifen sind warm, jetzt kannst du richtig angreifen.", "Tyres are warm, now you can really attack.", "Neumáticos calientes, ahora sí puedes atacar.") + window;
            }
        }

        internal static string DirtText(List<int> corners, bool measured, int v)
        {
            string where = corners == null || corners.Count == 0 || corners.Count == 4 ? RadioLanguage.T("die Reifen", "the tyres", "los neumáticos") : RadioCorners(corners);
            if (!measured)
                return RadioLanguage.T("Nach dem Ausflug haben " + where + " vermutlich Dreck aufgenommen. Ein, zwei Kurven vorsichtig rantasten.",
                    "After that trip off track " + where + " have probably picked up dirt. Feel your way in for a corner or two.",
                    "Tras la salida de pista " + where + " probablemente llevan suciedad. Tantea una o dos curvas.");
            return (((v % 2) + 2) % 2) == 0
                ? RadioLanguage.T("Dreck auf " + where + ". Weniger Grip, bis sie wieder sauber sind, also die nächsten Kurven vorsichtig.",
                    "Dirt on " + where + ". Less grip until they clean up, so careful in the next corners.",
                    "Suciedad en " + where + ". Menos agarre hasta que se limpien, cuidado en las próximas curvas.")
                : RadioLanguage.T(Cap(where) + " sind dreckig, das kostet Grip. Sauber fahren, dann putzen sie sich frei.",
                    "Dirty " + where + ", that costs grip. Keep it tidy and it'll clean off.",
                    "Hay suciedad en " + where + ", eso resta agarre. Conduce limpio y se irá.");
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        internal void Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            bool driving = s.SessionPhase == SessionPhase.Green && !gs.PitData.InPitlane && gs.PositionAndMotionData.CarSpeed > 15;
            if (gs.PitData.InPitlane) { warmReported = false; coldSeen = false; } // neuer Stint
            if (!driving || call != null && call.IsValid(gs, now) || now - lastCall < TimeSpan.FromSeconds(40)) { TrackOffTrack(gs, now); return; }

            float[] temps = Temps(gs);
            // Datenwahrheit: eingefrorene Temperaturen (z. B. erst beim Stopp aktualisiert) nicht als aktuellen Zustand melden.
            bool tempsKnown = temps.All(v => v > 0) && DataTruth.Current.MaySpeakAsCurrent(SimChannel.TyreTemperature);
            Window(gs, out float cold, out float top);

            // 1. Überhitzt
            if (tempsKnown)
            {
                var hot = HotCorners(temps, top);
                if (hot.Count > 0)
                {
                    if (hotSince == DateTime.MinValue) hotSince = now;
                    coolSince = DateTime.MinValue;
                    string key = string.Join(",", hot);
                    if (hotArmed && now - hotSince >= TimeSpan.FromSeconds(4))
                    {
                        hotArmed = false; lastHotKey = key;
                        Offer(gs, now, "tyre-hot", HotText(hot, temps, top, variant++), 5,
                            "measured_tyre_temperature_" + (top > 0 ? "above_crewchief_window_or_imbalance" : "imbalance"),
                            live => HotCorners(Temps(live), top).Count > 0);
                        return;
                    }
                }
                else
                {
                    hotSince = DateTime.MinValue;
                    if (coolSince == DateTime.MinValue) coolSince = now;
                    if (!hotArmed && now - coolSince >= TimeSpan.FromSeconds(10)) hotArmed = true; // wieder abgekühlt: neu scharf
                }
                // 2. Im Fenster nach dem Anwärmen (einmal je Stint)
                if (cold > 0)
                {
                    if (temps.Any(v => v < cold - 2)) coldSeen = true;
                    else if (coldSeen && !warmReported && temps.All(v => v >= cold + 2) && hot.Count == 0)
                    {
                        warmReported = true;
                        Offer(gs, now, "tyre-warm", WarmText(cold, top, variant++), 3, "measured_tyre_temperature_entered_crewchief_window", live => true);
                        return;
                    }
                }
            }

            // 3. Schmutz
            var t = gs.TyreData;
            if (t.DirtPickupEmulationActive)
            {
                var dirty = Enumerable.Range(0, 4).Where(i => t.TyreDirtPickupStatus.hasValueAtLevel(TyreDirtPickupState.MAJOR) &&
                    DirtyCorner(t, i)).ToList();
                if (dirty.Count > 0 && dirtArmed)
                {
                    dirtArmed = false;
                    Offer(gs, now, "tyre-dirt", DirtText(dirty, true, variant++), 5, "game_tyre_dirt_level", live => true);
                    return;
                }
                if (dirty.Count == 0) dirtArmed = true;
            }
            else if (offLong && backOnSince != DateTime.MinValue && now - backOnSince >= TimeSpan.FromSeconds(2))
            {
                offLong = false;
                Offer(gs, now, "tyre-dirt", DirtText(null, false, variant++), 4, "off_track_excursion_dirt_assumed", live => !live.PenaltiesData.IsOffRacingSurface);
                return;
            }
            TrackOffTrack(gs, now);
        }

        private static bool DirtyCorner(TyreData t, int i)
        {
            float v = i == 0 ? t.FrontLeftDirtPickupSeverity : i == 1 ? t.FrontRightDirtPickupSeverity : i == 2 ? t.RearLeftDirtPickupSeverity : t.RearRightDirtPickupSeverity;
            // Ohne Einzelwerte (RaceRoom liefert nur den Status) gelten alle Ecken als betroffen.
            return v < 0 || v >= .3f;
        }

        /// <summary>Ausflug neben die Strecke: mindestens 1 s abseits der Fahrbahn mit über 15 m/s.</summary>
        private void TrackOffTrack(GameStateData gs, DateTime now)
        {
            bool off = gs.PenaltiesData.IsOffRacingSurface && !gs.PitData.InPitlane;
            if (off)
            {
                if (offSince == DateTime.MinValue) offSince = now;
                if (now - offSince >= TimeSpan.FromSeconds(1) && gs.PositionAndMotionData.CarSpeed > 15) offLong = true;
                backOnSince = DateTime.MinValue;
            }
            else
            {
                offSince = DateTime.MinValue;
                if (offLong && backOnSince == DateTime.MinValue) backOnSince = now;
                if (backOnSince != DateTime.MinValue && now - backOnSince > TimeSpan.FromSeconds(20)) { offLong = false; backOnSince = DateTime.MinValue; }
            }
        }

        private void Offer(GameStateData gs, DateTime now, string topic, string text, int priority, string evidence, Func<GameStateData, bool> stillTrue)
        {
            lastCall = now;
            call = new ObservedRaceCall
            {
                Topic = topic, Text = text, Evidence = evidence, Member = TeamMember.Mik, Priority = priority, Position = -1,
                SessionId = gs.SessionData.SessionId, SessionType = gs.SessionData.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(12),
                AdditionalValidity = live => !live.PitData.InPitlane && stillTrue(live)
            };
            LiveDiagnostics.Decision(topic, "detected", evidence, text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool mikEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !mikEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
