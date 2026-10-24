//#define TEST_REFACTOR

#if TEST_REFACTOR
using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;

namespace UnitTest.Refactoring
{
    // This is a belt and braces unit test to confirm the refactor
    [TestFixture]
    public class RefactorNoisyCartesianCoordinateSpotter
    {
        [Test]
        public void ABTest()
        {
            // From the original code 
            var oldList = new String[] { folderStillThere, folderCarLeft, folderCarRight, folderCarInside, folderCarOutside, folderClearAllRound,
                folderClearLeft, folderClearRight, folderClearInside, folderClearOutside, folderThreeWideYoureOnRight, folderThreeWideYoureOnLeft,
                folderThreeWideYoureOnInside, folderThreeWideYoureOnOutside }
                .ToList();
            // Refactored code
            var newList = removeAllImmediateMessagesExcept(new List<string>
            {
                folderInTheMiddle
            });

            check(newList, oldList);

            oldList = new String[] {
                folderStillThere, folderCarLeft, folderCarRight, folderCarInside, folderCarOutside, folderClearAllRound, 
                folderClearLeft, folderClearRight, folderClearInside, folderClearOutside, folderInTheMiddle, folderThreeWideYoureOnRight, folderThreeWideYoureOnOutside }
                .ToList();
            newList = removeAllImmediateMessagesExcept(new List<string>
            {
                folderThreeWideYoureOnInside,
                folderThreeWideYoureOnLeft
            });

            check(newList, oldList);
        }

        private static void check(List<string> newList, List<string> oldList)
        {
            newList.Count.Should().Be(oldList.Count);
            foreach (var folder in newList)
            {
                Assert.IsTrue(oldList.Contains(folder));
            }
            foreach (var folder in oldList)
            {
                Assert.IsTrue(newList.Contains(folder));
            }
        }
        // The following folders are used in the original code and are not changed in the refactor.
        private static String folderStillThere = "spotter/still_there";
        private static String folderInTheMiddle = "spotter/in_the_middle";
        private static String folderCarLeft = "spotter/car_left";
        private static String folderCarRight = "spotter/car_right";
        private static String folderCarInside = "spotter/car_inside";
        private static String folderCarOutside = "spotter/car_outside";
        private static String folderClearLeft = "spotter/clear_left";
        private static String folderClearRight = "spotter/clear_right";
        private static String folderClearInside = "spotter/clear_inside";
        private static String folderClearOutside = "spotter/clear_outside";
        private static String folderClearAllRound = "spotter/clear_all_round";
        private static String folderThreeWideYoureOnRight = "spotter/three_wide_on_right";
        private static String folderThreeWideYoureOnLeft = "spotter/three_wide_on_left";
        private static String folderThreeWideYoureOnInside = "spotter/three_wide_on_inside";
        private static String folderThreeWideYoureOnOutside = "spotter/three_wide_on_outside";

        // Copied from the refactored NoisyCartesianCoordinateSpotter.cs
        static readonly List<string> allImmediateMessages = new List<string>
        {
            folderStillThere,
            folderInTheMiddle,
            folderCarLeft,
            folderCarRight,
            folderCarInside,
            folderCarOutside,
            folderClearLeft,
            folderClearRight,
            folderClearInside,
            folderClearOutside,
            folderClearAllRound,
            folderThreeWideYoureOnRight,
            folderThreeWideYoureOnLeft,
            folderThreeWideYoureOnInside,
            folderThreeWideYoureOnOutside
        };
        // Copied from the refactored NoisyCartesianCoordinateSpotter.cs and tweaked
        private List<string> removeAllImmediateMessagesExcept(List<string> immediateMessagesToKeep)
        {
            // Create a new list excluding the messages to keep
            var messagesToRemove = allImmediateMessages
                .Where(msg => !immediateMessagesToKeep.Contains(msg))
                .ToList();
            return messagesToRemove;
        }
    }
}
#endif
