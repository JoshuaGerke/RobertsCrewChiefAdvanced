using NUnit.Framework;

using NUnit.Framework;
using System.IO;
using CrewChiefV4;
using FluentAssertions;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestPaths
    {
        const string pathName = "pathToSomething";
        UserSettings userSettings = new UserSettings();

        [TestCase(@"%ProgramFiles(x86)%/Steam", @"C:\Program Files (x86)\Steam")]
        [TestCase(@"%ProgramFiles(x86)%\Steam", @"C:\Program Files (x86)\Steam")]
        public void test_getPath(string inPath, string fullPath)
        {
            var _path = userSettings.expandPath(inPath, pathName);
            _path.Should().Match(fullPath);
            if (_path != inPath)
            {
                ConsoleCapture.Contents.Should().Contain($"Path '{pathName}':'{inPath}' becomes '{_path}");
                ConsoleCapture.Clear();
            }
            Directory.Exists(fullPath).Should().BeTrue();
            Directory.Exists(inPath).Should().BeFalse();
        }

        [TestCase(@"'steam://rungameid/1066890", @"'steam://rungameid/1066890")]
        [TestCase(@"'Steam://rungameid/1066890", @"'Steam://rungameid/1066890")]
        public void test_getPath_steam(string inPath, string fullPath)
        {
            var _path = userSettings.expandPath(inPath, pathName);
            _path.Should().Match(fullPath);
            if (_path != inPath)
            {
                ConsoleCapture.Contents.Should().Contain($"Path '{pathName}':'{inPath}' becomes '{_path}");
                ConsoleCapture.Clear();
            }
        }

        // It parses that! [TestCase(@"c://ProgramFiles (x86)/Steam")]
        [TestCase(@"'steam:/rungameid/1066890")]
        public void test_getPath_fail(string inPath)
        {
            var _path = userSettings.expandPath(inPath, pathName);
            _path.Should().Match(inPath);
            ConsoleCapture.Contents.Should().Contain($"Path '{pathName}':'{inPath}' is not valid");
            ConsoleCapture.Clear();
        }
    }
}
