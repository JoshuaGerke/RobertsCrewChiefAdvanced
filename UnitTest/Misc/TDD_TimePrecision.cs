using NUnit.Framework;

using System;

using CrewChiefV4.Events;
using CrewChiefV4.NumberProcessing;
using CrewChiefV4;

namespace UnitTestTimePrecision
{
    public class UnitTestTimePrecision
    {
        /// <summary>
        /// Extracting the algorithm from getSectorDeltaMessages
        /// Aim:
        /// delta < 5 hundredths : "fast"
        /// delta ~ 1 tenth : "a tenth"
        /// delta ~ 2 tenths : "two tenths"
        /// delta ~ 1 second : "a second"
        /// delta < 10 seconds : "x point x seconds"
        ///       (message calculated later)
        /// </summary>
        /// <param name="delta"></param>
        /// <returns></returns>
        private Precision copyOf_getSectorDeltaMessages(float delta)
        {
            Precision precision = Precision.AUTO_GAPS;
            string result = String.Empty;

            if (delta < 0.05)
            {
                Console.WriteLine($"{delta} Fast");
            }
            else if (LapTimes.nearlyEqual(delta, 0.1f))
            { // > 0.06 and < 0.14
                Console.WriteLine($"{delta} SectorATenthOffThePace");
            }
            else if (LapTimes.nearlyEqual(delta, 0.2f))
            { // > 0.1 and < 0.3
                Console.WriteLine($"{delta} SectorTwoTenthsOffThePace");
            }
            else if (LapTimes.nearlyEqual(delta, 1))
            { // > 0.85 and < 1.15
                Console.WriteLine($"{delta} SectorASecondOffThePace");
            }
            else if (delta < 10 && delta > 0)
            {
                var gap = TimeSpanWrapper.FromSeconds(delta, Precision.AUTO_GAPS);
                var msg = MessageFragment.Time(gap);
                precision = gap.getPrecisionInner(false, false);
                Console.WriteLine($"{delta} Sector 'x point x' seconds");
                // Trying to get the actual speech opens up the whole "run CrewChief" can of worms
                //NumberReader numberReader = NumberReaderFactory.GetNumberReader();
                //numberReader.ConvertTimeToSounds(gap, false);
            }
            return precision;
        }

        private Precision new_getSectorDeltaMessages(float delta)
        {
            Precision precision = Precision.AUTO_GAPS;
            string result = String.Empty;

            if (delta < 0.05f)
            {
                Console.WriteLine($"{delta} Fast");
            }
            else if (delta < 0.15f)
            {
                Console.WriteLine($"{delta} SectorATenthOffThePace");
            }
            else if (delta < 0.25f)
            {
                Console.WriteLine($"{delta} SectorTwoTenthsOffThePace");
            }
            else if (delta > 0.95f && delta < 1.05f)
            {
                Console.WriteLine($"{delta} SectorASecondOffThePace");
            }
            else if (delta < 10)
            {
                var gap = TimeSpanWrapper.FromSeconds(delta, Precision.AUTO_GAPS);
                var msg = MessageFragment.Time(gap);
                precision = gap.getPrecisionInner(false, false);
                Console.WriteLine($"{delta} Sector 'x point x' seconds");
            }
            return precision;
        }

        private Precision getSectorDeltaMessages_usingGetDeltas(float delta)
        {
            Precision precision = Precision.AUTO_GAPS;
            switch (LapTimes.GetDelta(delta))
            {
                case LapTimes.Delta.FAST:
                    Console.WriteLine($"{delta} Fast");
                    break;
                case LapTimes.Delta.A_TENTH:
                    Console.WriteLine($"{delta} SectorATenthOffThePace");
                    break;
                case LapTimes.Delta.TWO_TENTHS:
                    Console.WriteLine($"{delta} SectorTwoTenthsOffThePace");
                    break;
                case LapTimes.Delta.A_SECOND:
                    Console.WriteLine($"{delta} SectorASecondOffThePace");
                    break;
                case LapTimes.Delta.AUTO_GAPS:
                    var gap = TimeSpanWrapper.FromSeconds(delta, Precision.AUTO_GAPS);
                    var msg = MessageFragment.Time(gap);
                    precision = gap.getPrecisionInner(false, false);
                    Console.WriteLine($"{delta} Sector 'x point x' seconds");
                    break;
                default:
                    break;
            }
            return precision;
        }

        [Ignore("Problem solved")]
        [TestCase(0.05f, Precision.TENTHS, LapTimes.Delta.A_TENTH)]
        [TestCase(0.30f, Precision.TENTHS, LapTimes.Delta.AUTO_GAPS)]
        [TestCase(1.50f, Precision.TENTHS, LapTimes.Delta.AUTO_GAPS)]
        [TestCase(10.50f, Precision.AUTO_GAPS, LapTimes.Delta.NONE)]
        public void TestMethodOldDelta(float delta, Precision precision)//LapTimes.Delta ENUM)
        {
            Precision _precision;
            _precision = copyOf_getSectorDeltaMessages(delta);
            Assert.AreEqual(precision, _precision);

            //Assert.AreEqual(ENUM, LapTimes.GetDelta(delta));
        }

        [Test, Ignore("finding errors in the old method")]
        public void TestMethodListDeltas()
        {
            float[] deltas = {
                0.04f,
                0.05f,
                0.06f,
                0.061f,
                0.1f,
                0.11f,
                0.139f,
                0.14f,
                0.15f,
                0.2f,
                0.24f,
                0.25f,
                0.29f,
                0.3f,
                0.85f,
                0.95f,
                0.96f,
                1.00f,
                1.04f,
                1.05f,
            };
            Precision precision;
            Console.WriteLine("Old method");
            foreach (float delta in deltas)
            {
                precision = copyOf_getSectorDeltaMessages(delta);
            }
            Console.WriteLine("");
            Console.WriteLine("New method");
            foreach (float delta in deltas)
            {
                precision = new_getSectorDeltaMessages(delta);
            }
            Console.WriteLine("");
        }
    }
}