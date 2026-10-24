#define TEST_REFACTOR

#if TEST_REFACTOR
using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

using Newtonsoft.Json;

using CrewChiefV4;
using CrewChiefV4.VirtualReality;

namespace UnitTest.Refactoring
{
    // This is a belt and braces unit test to confirm the refactor
    [TestClass]
    public class RefactorExtensions
    {
        [TestMethod]
        public void TestDeserialization()
        {
            var fileInfo = new FileInfo(DataFiles.VRconfig);
            var before = fileInfo.TryDeserializeJson<VROverlayConfiguration>();
            var refactor = fileInfo.TryReadFile<VROverlayConfiguration>();
            Assert.IsNotNull(refactor);
            refactor.Should().BeEquivalentTo(before, "The new and old configurations should be equal");
        }
        [TestMethod]
        public void TestSerialization()
        {
            var before = new FileInfo("before");
            var refactor = new FileInfo("refactor");
            var content = new VROverlayConfiguration();
            
            content.SerializeJson(before.FullName);
            JsonFiles.WriteFile(refactor.FullName, content);

            string fileContent1 = File.ReadAllText(before.FullName);
            string fileContent2 = File.ReadAllText(refactor.FullName);

            fileContent1.Should().Be(fileContent2, "the files should have the same content");
            before.Delete();
            refactor.Delete();
        }
    }
    // Copied from the original code
    static class Extensions
    {
        static readonly FileInfo VrSettingsFile = new FileInfo(DataFiles.VRconfig);

        public static T DeserializeJson<T>(this FileInfo file) where T : class
        {
            using (var r = new StreamReader(file.FullName))
            {
                string json = r.ReadToEnd();
                T data = JsonConvert.DeserializeObject<T>(json);
                if (data != null)
                {
                    return data;
                }
            }
            return default(T);
        }

        public static T TryDeserializeJson<T>(this FileInfo file, Action<Exception> onException = null) where T : class
        {
            try
            {
                return file.DeserializeJson<T>();
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);
                return default(T);
            }
        }

        public static void SerializeJson<T>(this T settings, string path)
        {
            using (StreamWriter file = File.CreateText(path))
            {
                var serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(file, settings);
            }
        }
    }
}
#endif
