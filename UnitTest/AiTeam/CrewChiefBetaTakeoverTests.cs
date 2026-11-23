using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrewChiefV4;
using CrewChiefV4.Events;
using CrewChiefV4.Events.PubSub;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Übernahmen aus Crew Chief 4.19.5.11 (Beta vom 18.11.2026) und dem Upstream-Stand vom 21.11.2026.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class CrewChiefBetaTakeoverTests
    {
        private static List<Action<GameStateData>> SectorChangedHandlers()
        {
            var field = typeof(EventBus).GetField("subscribers", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "EventBus.subscribers nicht gefunden");
            var subscribers = (Dictionary<GameEvent, List<Action<GameStateData>>>)field.GetValue(null);
            return subscribers.TryGetValue(GameEvent.SectorChanged, out var handlers) ? handlers : new List<Action<GameStateData>>();
        }

        [Test]
        public void Lapped_car_ahead_is_not_announced_from_sector_events_outside_the_race()
        {
            // 4.19.5.8 hing die Meldung „nächstes Auto ist eine Runde zurück“ an jedes Sektorende, auch im Training und in der Quali.
            // Jetzt prüft Opponents das nur noch im Rennablauf (racing, nicht unter FCY, Namen erlaubt).
            var opponents = new Opponents(null);
            Assert.IsFalse(SectorChangedHandlers().Any(h => h.Target is Opponents),
                "Opponents darf sich nicht mehr auf SectorChanged anmelden");
            GC.KeepAlive(opponents);
        }

        private static CarData.CarClass Class(CarData.CarClassEnum e)
        {
            var c = new CarData.CarClass();
            c.carClassEnum = e;
            // wie beim Laden der Klassendaten: setzt das Gruppen-Kennzeichen (DTM 2013–2016, TC1) vorab
            c.getClassIdentifier();
            return c;
        }

        [Test]
        public void Raceroom_DTM_generations_that_race_together_count_as_one_class()
        {
            CarData.loadCarClassData();
            Assert.IsTrue(CarData.IsCarClassEqual(Class(CarData.CarClassEnum.DTM_2003), Class(CarData.CarClassEnum.DTM_2005), false));
            Assert.IsTrue(CarData.IsCarClassEqual(Class(CarData.CarClassEnum.DTM_2002), Class(CarData.CarClassEnum.DTM_2003), false));
            Assert.IsTrue(CarData.IsCarClassEqual(Class(CarData.CarClassEnum.DTM_2020), Class(CarData.CarClassEnum.DTM_2026), false));
            Assert.IsTrue(CarData.IsCarClassEqual(Class(CarData.CarClassEnum.DTM_2013), Class(CarData.CarClassEnum.DTM_2016), false),
                "DTM 2013 bis 2016 bleiben wie bisher eine Gruppe");
            Assert.IsFalse(CarData.IsCarClassEqual(Class(CarData.CarClassEnum.DTM_2003), Class(CarData.CarClassEnum.DTM_2020), false),
                "verschiedene DTM-Generationen bleiben getrennt");
            Assert.IsFalse(CarData.IsCarClassEqual(Class(CarData.CarClassEnum.SPEC_MIATA), Class(CarData.CarClassEnum.TOYOTA_GR86), false),
                "iRacing-Klassen bleiben, wie das Spiel sie wertet");
        }

        [Test]
        public void Similar_class_check_is_safe_without_data()
        {
            Assert.IsFalse(CarData.IsCarClassSimilar(null, Class(CarData.CarClassEnum.DTM_2003)));
            Assert.IsFalse(CarData.IsCarClassSimilar(Class(CarData.CarClassEnum.DTM_2003), null));
        }
    }
}
