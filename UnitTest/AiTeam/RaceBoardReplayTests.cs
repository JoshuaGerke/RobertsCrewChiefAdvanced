using System;
using System.Globalization;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Board;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Lagetafel gegen die Aufnahme RaceRoom Mantorp 21.11.: je Zeile das native Bild (Session, Phase, Autos, Platz, Runden übrig)
    /// und Crew Chiefs Spielzustand (Session, Phase, Platz) zum selben Zeitpunkt. Keine Namen, keine persönlichen Daten.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class RaceBoardReplayTests
    {
        private static readonly string[] Qualifying = {
            "15:15:41.0|Qualify|Green|2|1|-1|HotLap|Green|1",
            "15:15:49.1|Qualify|Green|2|1|-1|HotLap|Green|1",
            "15:15:57.2|Qualify|Green|2|1|-1|HotLap|Green|1",
            "15:16:05.3|Qualify|Green|2|1|-1|HotLap|Green|1",
            "15:16:13.4|Qualify|Green|2|1|-1|HotLap|Green|1",
            "15:16:21.5|Qualify|Green|2|1|-1|HotLap|Green|1",
            "15:16:29.6|Qualify|Green|2|1|-1|HotLap|Green|1",
            "15:16:37.7|Qualify|Green|2|1|-1|HotLap|Green|1",
            "15:16:45.8|Qualify|Green|2|1|-1|Qualify|Green|1",
            "15:16:53.9|Qualify|Green|2|1|-1|Qualify|Green|1",
            "15:17:02.0|Qualify|Green|2|1|-1|Qualify|Green|1",
            "15:17:10.1|Qualify|Green|2|1|-1|Qualify|Green|1",
            "15:17:18.2|Qualify|Green|2|1|-1|Qualify|Green|1",
            "15:17:26.4|Qualify|Green|2|1|-1|Qualify|Green|1",
            "15:17:34.6|Qualify|Green|2|1|-1|Qualify|Green|1",
        };

        private static readonly string[] Race = {
            "15:31:31.0|Race|Garage|0|-1|-1|Race|Finished|18",
            "15:32:01.6|Race|Garage|0|-1|-1|Race|Finished|18",
            "15:32:32.2|Race|Green|22|18|12|Race|Green|18",
            "15:33:02.6|Race|Green|22|18|11|Race|Green|20",
            "15:33:33.0|Race|Green|22|18|11|Race|Green|18",
            "15:34:03.4|Race|Green|22|17|11|Race|Green|17",
            "15:34:33.8|Race|Green|22|17|9|Race|Green|17",
            "15:35:04.2|Race|Green|22|21|9|Race|Green|21",
            "15:35:34.6|Race|Green|22|21|9|Race|Green|21",
            "15:36:05.0|Race|Green|22|20|8|Race|Green|20",
            "15:36:35.3|Race|Green|22|20|8|Race|Green|20",
            "15:37:05.7|Race|Green|22|20|8|Race|Green|20",
            "15:37:36.1|Race|Green|22|20|8|Race|Green|20",
            "15:38:06.5|Race|Green|22|20|8|Race|Green|20",
            "15:38:36.9|Race|Green|22|19|8|Race|Green|19",
            "15:39:07.3|Race|Green|22|19|8|Race|Green|19",
            "15:39:37.7|Race|Green|22|17|8|Race|Green|17",
            "15:40:08.1|Race|Green|22|17|7|Race|Green|17",
            "15:40:38.5|Race|Green|22|17|7|Race|Green|17",
            "15:41:08.9|Race|Green|22|17|6|Race|Green|17",
            "15:41:39.3|Race|Green|22|17|6|Race|Green|17",
            "15:42:09.7|Race|Green|22|16|6|Race|Green|16",
            "15:42:40.1|Race|Green|22|16|5|Race|Green|16",
            "15:43:10.5|Race|Green|22|16|5|Race|Green|16",
            "15:43:40.9|Race|Green|22|16|5|Race|Green|16",
            "15:44:11.3|Race|Green|22|16|4|Race|Green|16",
            "15:44:41.7|Race|Green|22|16|4|Race|Green|16",
            "15:45:12.0|Race|Green|22|16|3|Race|Green|16",
            "15:45:42.4|Race|Green|22|16|3|Race|Green|16",
            "15:46:12.8|Race|Green|22|16|3|Race|Green|16",
            "15:46:43.2|Race|Green|22|16|2|Race|Green|16",
            "15:47:13.6|Race|Green|22|16|2|Race|Green|16",
            "15:47:44.0|Race|Green|22|16|2|Race|Green|16",
            "15:48:14.4|Race|Green|22|16|1|Race|Green|16",
            "15:48:44.8|Race|Checkered|22|16|1|Race|Checkered|16",
        };

        private static (GameStateData gs, FieldView v, DateTime utc) Row(string row)
        {
            var p = row.Split('|');
            var utc = DateTime.ParseExact("2026-11-21 " + p[0], "yyyy-MM-dd HH:mm:ss.f", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var v = new FieldView
            {
                Utc = utc, Sim = "RaceRoom", Track = "Mantorp Park", Session = (FieldSession)Enum.Parse(typeof(FieldSession), p[1]),
                Phase = (FieldPhase)Enum.Parse(typeof(FieldPhase), p[2]), NumCars = int.Parse(p[3]), MaxPlace = int.Parse(p[3]),
                PlayerLapsLeft = int.Parse(p[5]),
                Player = int.Parse(p[4]) > 0 ? new FieldCarView { IsPlayer = true, Place = int.Parse(p[4]), PlaceClass = int.Parse(p[4]) } : null,
            };
            var gs = new GameStateData(utc.Ticks);
            gs.SessionData.SessionId = -1;
            gs.SessionData.SessionType = (SessionType)Enum.Parse(typeof(SessionType), p[6]);
            gs.SessionData.SessionPhase = (SessionPhase)Enum.Parse(typeof(SessionPhase), p[7]);
            gs.SessionData.ClassPosition = int.Parse(p[8]);
            return (gs, v, utc);
        }

        [Test]
        public void Qualifying_stays_one_session_although_Crew_Chief_flips_from_hotlap()
        {
            var keys = Qualifying.Select(Row).Select(r => AiTeamService.SessionPhaseKey(r.gs, RaceBoard.Compose(r.gs, r.v, r.utc), r.utc)).Distinct().ToList();
            Assert.AreEqual(1, keys.Count, string.Join(" ", keys));
            var first = Row(Qualifying[0]); var b = RaceBoard.Compose(first.gs, first.v, first.utc);
            Assert.AreEqual(FieldSession.Qualify, b.Session.Value); Assert.AreEqual(BoardSource.Native, b.Session.Source);
            Assert.IsTrue(b.Conflicts.ContainsKey("session"), "Hotlap gegen Qualifying wird als Widerspruch erkannt");
            var later = Row(Qualifying.Last()); Assert.IsFalse(RaceBoard.Compose(later.gs, later.v, later.utc).Conflicts.ContainsKey("session"));
        }

        [Test]
        public void Race_board_has_22_cars_and_the_garage_phase_from_the_game()
        {
            var rows = Race.Select(Row).ToList();
            var garage = RaceBoard.Compose(rows[0].gs, rows[0].v, rows[0].utc);
            Assert.AreEqual(FieldPhase.Garage, garage.Phase.Value, "Crew Chief meldete noch „Finished“ aus dem Training");
            Assert.AreEqual(BoardSource.Native, garage.Phase.Source);
            foreach (var r in rows.Where(r => r.v.Phase == FieldPhase.Green))
            {
                var b = RaceBoard.Compose(r.gs, r.v, r.utc);
                Assert.AreEqual(22, b.FieldSize.Value, r.utc.ToString("HH:mm:ss")); Assert.AreEqual(BoardSource.Native, b.FieldSize.Source);
                Assert.AreEqual(BoardSource.CrewChief, b.Place.Source);
            }
            int conflicts = rows.Count(r => RaceBoard.Compose(r.gs, r.v, r.utc).Conflicts.ContainsKey("place"));
            Assert.LessOrEqual(conflicts, 1, "RaceRoom: Platz aus beiden Quellen fast immer gleich");
        }

        [Test]
        public void Stale_native_picture_falls_back_to_Crew_Chief()
        {
            var r = Row(Race[5]);
            var b = RaceBoard.Compose(r.gs, r.v, r.utc.AddSeconds(3));
            Assert.AreEqual(BoardSource.CrewChief, b.Session.Source, "natives Bild älter als 1 s gilt nicht");
            Assert.IsFalse(b.FieldSize.Known || b.FieldSize.Source == BoardSource.Native);
            Assert.AreEqual(FieldSession.Race, b.Session.Value);
        }

        [Test]
        public void Assetto_corsa_place_uses_the_live_order_in_the_race()
        {
            // Monza 14:53:24: Streckenpositionen ergaben Platz 10, Crew Chief (Echtzeit) 10, offizielle Rangliste 7.
            Assert.AreEqual(10, ACFieldSource.PlaceOf(7, 9, 1, true));
            Assert.AreEqual(7, ACFieldSource.PlaceOf(7, 9, 1, false), "Training/Quali: offizielle Rangliste");
            Assert.AreEqual(9, ACFieldSource.PlaceOf(7, 9, 0, true), "Echtzeit-Rangliste ab 1");
            Assert.AreEqual(7, ACFieldSource.PlaceOf(7, -1, 0, true), "ohne Echtzeitwert die offizielle");
        }
    }
}
