using CrewChiefV4;
using CrewChiefV4.ScreenCapture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX.DXGI;
using System;
using System.Diagnostics;
using System.Linq;

namespace UnitTest.VROverlayWindow
{
    [TestClass]
    public class Direct3D11CaptureSourceTests
    {
        [TestMethod]
        public void Dispose_leaves_no_dxObjects()
        {
            SharpDX.Configuration.EnableObjectTracking = true;
            var dxObjects = SharpDX.Diagnostics.ObjectTracker.FindActiveObjects().Count;
            using (var mgr = new DeviceManager(null))
            using (var source = new Direct3D11CaptureSource(mgr, new CrewChiefV4.VirtualReality.VROverlayConfiguration(), null))
            {
            }

            Trace.WriteLine(SharpDX.Diagnostics.ObjectTracker.ReportActiveObjects());
            Assert.AreEqual(dxObjects, SharpDX.Diagnostics.ObjectTracker.FindActiveObjects().Count, "DX Objects should be disposed.");
        }
    }
}
