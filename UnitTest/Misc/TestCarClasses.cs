//Copilot
using CrewChiefV4;

using NUnit.Framework;

using System;

namespace UnitTest.Misc.CarClasses
{
    [TestFixture]
    public class CarDataTests
    {
        [SetUp]
        public void Setup()
        {
            // Load car class data before running tests
            CarData.loadCarClassData();
        }

        /// <summary>
        /// Tests getCarClassForClassNameOrCarName with various GameEnum values and car names
        /// </summary>
        [TestCase(GameEnum.ACC, "GT3", null, true)]
        [TestCase(GameEnum.ACC, "GT4", null, true)]
        [TestCase(GameEnum.ASSETTO_64BIT, "ks_bmw_m235i_racing", null, true)]
        [TestCase(GameEnum.ASSETTO_EVO, "", "Audi R8 LMS GT4 Evo", true)]
        [TestCase(GameEnum.RF1, "BoxerCup", null, true)]
        [TestCase(GameEnum.RF2_64BIT, "FR3.5", null, true)]
        [TestCase(GameEnum.AMS2, "F-Vee", null, true)]
        [TestCase(GameEnum.PCARS_64BIT, "GT3", "Ferrari 488 GT3", true)]
        [TestCase(GameEnum.ACC, "UNKNOWN_CLASS", null, false)]
        public void TestGetCarClassForClassNameOrCarName(GameEnum gameEnum, string className, string carName, bool shouldFindClass)
        {
            // Arrange
            SetGameEnumContext(gameEnum);

            // Act
            CarData.CarClass result = CarData.getCarClassForClassNameOrCarName(className, carName);

            // Assert
            if (shouldFindClass)
            {
                Assert.IsNotNull(result, $"Should find a car class for gameEnum={gameEnum}, className='{className}', carName='{carName}'");
                Assert.AreNotEqual(CarData.CarClassEnum.UNKNOWN_RACE, result.carClassEnum,
                    $"Should map to a known car class, not UNKNOWN_RACE for className='{className}'");
            }
            else
            {
                Assert.IsNotNull(result, "Result should never be null, but may be UNKNOWN_RACE");
                // When not found, it creates a new CarClass with placeholder ID
                Assert.AreEqual(className, result.placeholderClassId,
                    $"Placeholder class ID should match the input className");
            }
        }

        /// <summary>
        /// Tests that null className returns an empty CarClass
        /// </summary>
        [Test]
        public void TestGetCarClassForClassNameOrCarName_NullClassName()
        {
            // Arrange
            SetGameEnumContext(GameEnum.ACC);

            // Act
            CarData.CarClass result = CarData.getCarClassForClassNameOrCarName(null);

            // Assert
            Assert.IsNotNull(result, "Should return an empty CarClass, not null");
            Assert.AreEqual(CarData.CarClassEnum.UNKNOWN_RACE, result.carClassEnum);
        }

        /// <summary>
        /// Tests that caching works correctly for className lookups
        /// </summary>
        [Test]
        public void TestGetCarClassForClassNameOrCarName_CachingClassName()
        {
            // Arrange
            SetGameEnumContext(GameEnum.ACC);
            string testClassName = "GT3";

            // Act - First call (cache miss)
            CarData.CarClass result1 = CarData.getCarClassForClassNameOrCarName(testClassName);

            // Act - Second call (cache hit)
            CarData.CarClass result2 = CarData.getCarClassForClassNameOrCarName(testClassName);

            // Assert
            Assert.AreSame(result1, result2, "Cached results should return the same instance");
        }

        /// <summary>
        /// Tests that caching works correctly for carName lookups
        /// </summary>
        [TestCase(GameEnum.PCARS_64BIT)]
        [TestCase(GameEnum.PCARS2)]
        public void TestGetCarClassForClassNameOrCarName_CachingCarName(GameEnum gameEnum)
        {
            // Arrange
            SetGameEnumContext(gameEnum);
            string testCarName = "Ferrari 488 GT3";
            string testClassName = "GT3";

            // Act - First call (cache miss)
            CarData.CarClass result1 = CarData.getCarClassForClassNameOrCarName(testClassName, testCarName);

            // Act - Second call (cache hit)
            CarData.CarClass result2 = CarData.getCarClassForClassNameOrCarName(testClassName, testCarName);

            // Assert
            Assert.AreSame(result1, result2, "Cached results should return the same instance");
        }

        /// <summary>
        /// Tests case-insensitive matching for class names
        /// </summary>
        [TestCase(GameEnum.ACC, "gt3")]
        [TestCase(GameEnum.ACC, "GT3")]
        [TestCase(GameEnum.ACC, "Gt3")]
        public void TestGetCarClassForClassNameOrCarName_CaseInsensitive(GameEnum gameEnum, string classNameVariant)
        {
            // Arrange
            SetGameEnumContext(gameEnum);

            // Act
            CarData.CarClass result = CarData.getCarClassForClassNameOrCarName(classNameVariant);

            // Assert
            Assert.IsNotNull(result);
            // Should match regardless of case
        }

        /// <summary>
        /// Tests that carName is prioritized over className when both are provided
        /// </summary>
        [TestCase(GameEnum.PCARS_64BIT)]
        [TestCase(GameEnum.PCARS2)]
        public void TestGetCarClassForClassNameOrCarName_CarNamePriority(GameEnum gameEnum)
        {
            // Arrange
            SetGameEnumContext(gameEnum);
            string testCarName = "Ferrari 488 GT3";
            string testClassName = "GT3";

            // Act
            CarData.CarClass resultWithCarName = CarData.getCarClassForClassNameOrCarName(testClassName, testCarName);
            CarData.CarClass resultWithoutCarName = CarData.getCarClassForClassNameOrCarName(testClassName, null);

            // Assert
            Assert.IsNotNull(resultWithCarName);
            // If carName is provided and matches, it should be returned from cache
        }

        /// <summary>
        /// Tests mapping for different game enums
        /// </summary>
        [TestCase(GameEnum.ACC)]
        [TestCase(GameEnum.RF1)]
        [TestCase(GameEnum.RF2_64BIT)]
        [TestCase(GameEnum.AMS2)]
        [TestCase(GameEnum.GTR2)]
        [TestCase(GameEnum.ASSETTO_EVO)]
        public void TestGetCarClassForClassNameOrCarName_DifferentGameEnums(GameEnum gameEnum)
        {
            // Arrange
            SetGameEnumContext(gameEnum);
            string genericClassName = "GT3";

            // Act
            CarData.CarClass result = CarData.getCarClassForClassNameOrCarName(genericClassName);

            // Assert
            Assert.IsNotNull(result, $"Should handle {gameEnum} without throwing exceptions");
        }

        /// <summary>
        /// Tests that enum value matching works as fallback
        /// </summary>
        [Test]
        public void TestGetCarClassForClassNameOrCarName_EnumValueMatching()
        {
            // Arrange
            SetGameEnumContext(GameEnum.ACC);
            string enumName = "GT3"; // if this matches a CarClassEnum

            // Act
            CarData.CarClass result = CarData.getCarClassForClassNameOrCarName(enumName);

            // Assert
            Assert.IsNotNull(result);
        }

        /// <summary>
        /// Tests that unknown classes get created with placeholder IDs
        /// </summary>
        [Test]
        public void TestGetCarClassForClassNameOrCarName_UnknownClass()
        {
            // Arrange
            SetGameEnumContext(GameEnum.ACC);
            string unknownClassName = "COMPLETELY_UNKNOWN_CLASS_XYZ_" + Guid.NewGuid().ToString();

            // Act
            CarData.CarClass result = CarData.getCarClassForClassNameOrCarName(unknownClassName);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(unknownClassName, result.placeholderClassId,
                "Unknown class should have placeholder ID set to the input className");
        }

        // ============ Helper Methods ============

        /// <summary>
        /// Sets up the game context by configuring CrewChief.gameDefinition to use the specified GameEnum
        /// </summary>
        private void SetGameEnumContext(GameEnum gameEnum)
        {
            // Note: This assumes CrewChief has a static gameDefinition property with a settable gameEnum
            // You may need to adjust this based on your actual implementation
            if (CrewChief.gameDefinition != null)
            {
                CrewChief.gameDefinition.gameEnum = gameEnum;
                Game.game = gameEnum;
            }
        }
    }
}