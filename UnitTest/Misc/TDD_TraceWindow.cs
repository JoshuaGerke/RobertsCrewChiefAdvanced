using System;
using System.Windows.Forms;

using CrewChiefV4;

using NUnit.Framework;

namespace CrewChiefV4.Tests
{
    [TestFixture]
    public class TraceWindowTests
    {
        [Ignore("WHY DID THIS BREAK?")]
        [Test]
        public void TestTraceWindow()
        {
            // Run the test on the UI thread
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TraceWindow traceWindow = Tracepoints._testWindowHandle;
            if (true) //  Why does this not compile now???  UnitTest.UnitTest.Debugging)
            {
                var thread = new System.Threading.Thread(() =>
                {
                    Application.Run(traceWindow);
                });

                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();

                // Wait for the window to initialize
                while (traceWindow == null || !traceWindow.IsHandleCreated)
                {
                    System.Threading.Thread.Sleep(100);
                }

                // Ensure the window is visible
                traceWindow.Invoke(new Action(() =>
                {
                    traceWindow.Show();
                }));

                Assert.IsNotNull(traceWindow);
                Assert.IsTrue(traceWindow.Visible);

                // Keep the window open until it is manually closed
                thread.Join();
            }
            else
            {
                Log.Error("Test run not debugged");
            }
        }
    }
}

