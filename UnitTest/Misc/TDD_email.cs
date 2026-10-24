using System.IO;
using System.Threading.Tasks;
using CrewChiefV4.UserInterface.Models;
using NUnit.Framework;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TDD_email
    {
        [Ignore("TDD, don't want to invoke email client")]
        [Test]
        public async Task TestMethod1()
        {
            string subject = "Test of trace files uploaded to file.io";
            string filePath = @"Documents\CrewChiefV4\debugLogs\ASSETTO_64BIT_2024-10-08__15-56.zip";
            var debugTraces = new DebugTraces();
            string url = "https:file.io/fakeURL";  // await debugTraces.UploadFileToFileIo(filePath);
            string body = $"Download {Path.GetFileName(filePath)} from {url}";

            debugTraces.FillInEmail(subject, body, "fred.jones@stivichall.uk");
        }
    }
}
