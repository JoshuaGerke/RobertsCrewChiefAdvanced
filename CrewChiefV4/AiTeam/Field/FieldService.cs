using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// Liest das Feld 20-mal pro Sekunde direkt aus der Sim in einem eigenen Hintergrundfaden und hält das
    /// <see cref="FieldPicture"/> aktuell – auch im Boxenmenü, bevor Crew Chief einen Spielzustand liefert.
    /// Läuft, solange Crew Chief das Spiel liest (<see cref="Ensure"/>), und beendet sich 15 s nach dem letzten Aufruf.
    /// </summary>
    internal static class FieldService
    {
        internal const int IntervalMs = 50;
        private static readonly object gate = new object();
        private static Thread thread;
        private static GameEnum wanted = GameEnum.NONE;
        private static long lastEnsure;
        private static volatile string status = "aus";

        internal static string Status => status;
        /// <summary>Empfänger für jedes neue Lagebild (im Lagebild-Faden aufgerufen, muss schnell zurückkehren).</summary>
        internal static Action<FieldView> ViewSink;
        internal static FieldPicture Picture => FieldPicture.Shared;

        /// <summary>Aktuelles Lagebild, wenn es höchstens <paramref name="maxAgeSeconds"/> alt ist; sonst null.</summary>
        internal static FieldView Fresh(double maxAgeSeconds = 1.0)
        {
            var v = FieldPicture.Shared.Current;
            return v != null && v.Fresh(DateTime.UtcNow, maxAgeSeconds) ? v : null;
        }

        internal static bool Supports(GameEnum game) => game == GameEnum.RACE_ROOM || game == GameEnum.AMS2 || game == GameEnum.LMU ||
            game == GameEnum.ASSETTO_32BIT || game == GameEnum.ASSETTO_64BIT || game == GameEnum.ASSETTO_128CARS || game == GameEnum.IRACING;

        internal static IFieldSource Create(GameEnum game)
        {
            switch (game)
            {
                case GameEnum.RACE_ROOM: return new R3EFieldSource();
                case GameEnum.AMS2: return new AMS2FieldSource();
                case GameEnum.LMU: return new LMUFieldSource();
                case GameEnum.ASSETTO_32BIT: case GameEnum.ASSETTO_64BIT: case GameEnum.ASSETTO_128CARS: return new ACFieldSource();
                case GameEnum.IRACING: return new IRacingFieldSource();
                default: return null;
            }
        }

        /// <summary>Von Crew Chiefs Lese-Schleife aufgerufen, solange ein Spiel gelesen wird.</summary>
        internal static void Ensure(GameEnum game)
        {
            if (!Supports(game)) return;
            Interlocked.Exchange(ref lastEnsure, Stopwatch.GetTimestamp());
            lock (gate)
            {
                wanted = game;
                if (thread != null && thread.IsAlive) return;
                thread = new Thread(Run) { IsBackground = true, Name = "RRCA Lagebild 20 Hz", Priority = ThreadPriority.AboveNormal };
                thread.Start();
            }
        }

        private static void Run()
        {
            IFieldSource source = null;
            GameEnum sourceGame = GameEnum.NONE;
            var frame = new FieldFrame();
            DateTime lastNative = DateTime.MinValue, lastRelative = DateTime.MinValue;
            try
            {
                while (true)
                {
                    long idle = (Stopwatch.GetTimestamp() - Interlocked.Read(ref lastEnsure)) / Stopwatch.Frequency;
                    if (idle > 15) break;
                    GameEnum want;
                    lock (gate) want = wanted;
                    if (source == null || sourceGame != want)
                    {
                        source?.Dispose();
                        sourceGame = want; source = Create(want); frame = new FieldFrame();
                        if (source == null) { Thread.Sleep(1000); continue; }
                    }
                    if (!source.IsOpen && !source.Open()) { status = "wartet auf " + source.Name; Thread.Sleep(2000); continue; }
                    var started = Stopwatch.GetTimestamp();
                    DateTime now = DateTime.UtcNow;
                    if (source.Read(frame, now))
                    {
                        var view = FieldPicture.Shared.Observe(frame);
                        try { ViewSink?.Invoke(view); } catch (Exception) { }
                        status = source.Name + " · " + view.Cars.Count + " Wagen · " + (view.LayoutVerified ? "geprüft" : "Layout ungeprüft");
                        if (LiveDiagnostics.Enabled)
                        {
                            if (now - lastNative >= TimeSpan.FromSeconds(2)) { lastNative = now; EmitNative(view); }
                            if (view.Player != null && !view.InMenus && now - lastRelative >= TimeSpan.FromMilliseconds(500)) { lastRelative = now; EmitRelative(view); }
                        }
                    }
                    int spent = (int)((Stopwatch.GetTimestamp() - started) * 1000 / Stopwatch.Frequency);
                    Thread.Sleep(Math.Max(5, IntervalMs - spent));
                }
            }
            catch (Exception e) { status = "Fehler · " + e.GetType().Name; }
            finally
            {
                source?.Dispose();
                lock (gate) { if (thread == Thread.CurrentThread) thread = null; }
                if (!status.StartsWith("Fehler", StringComparison.Ordinal)) status = "aus";
            }
        }

        private static void EmitNative(FieldView v)
        {
            var p = v.Player;
            LiveDiagnostics.Emit("field.native", LiveDiagnostics.Data("sim", v.Sim, "track", v.Track, "layout", v.Layout, "length_m", v.TrackLength,
                "session", v.Session.ToString(), "phase", v.Phase.ToString(), "in_menus", v.InMenus, "in_garage", v.PlayerInGarage, "paused", v.Paused,
                "timed", v.TimedSession, "time_left_s", v.TimeRemaining, "duration_s", v.TimeDuration, "session_laps", v.SessionLaps,
                "race_minutes", v.RaceMinutes, "race_laps", v.RaceLaps, "num_cars", v.NumCars, "listed_cars", v.Cars.Count, "max_place", v.MaxPlace,
                "player_place", p == null ? -1 : p.Place, "player_laps_left", v.PlayerLapsLeft, "give_back", v.PositionsToGiveBack,
                "layout_verified", v.LayoutVerified, "profile_coverage", v.ProfileCoverage,
                "moving", v.Cars.Count(c => c.Motion == CarMotion.Slow || c.Motion == CarMotion.Stopped || c.Motion == CarMotion.Spun)));
        }

        private static string Row(FieldCarView c) => c.Name + "|" + Math.Round(c.GapS, 2) + "s|" + Math.Round(c.GapM) + "m|" + c.Motion + "|" + c.LapDelta;

        private static void EmitRelative(FieldView v)
        {
            LiveDiagnostics.Emit("field.relative", LiveDiagnostics.Data("ahead", string.Join("; ", v.Ahead(3).Select(Row)),
                "behind", string.Join("; ", v.Behind(3).Select(Row)), "player_dist_m", v.Player.LapDistance, "player_speed", v.Player.Speed,
                "hazards", string.Join("; ", v.Cars.Where(c => c.Motion == CarMotion.Slow || c.Motion == CarMotion.Stopped || c.Motion == CarMotion.Spun)
                    .Select(Row).Take(6))));
        }
    }
}
