using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace CrewChiefV4.ScreenCapture.Tests
{
    [TestClass()]
    public class OverlayDecoratorTests
    {
        TestScope _Scope = null;
        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            SharpDX.Configuration.EnableObjectTracking = true;
        }

        [TestInitialize]
        public void TestInitialize()
        {          
            _Scope = new TestScope(this);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _Scope.Dispose();
            _Scope.AssertDXObjects();
        }

        [TestMethod]
        public void ImageDiff()
        {
            using (var black = new System.Drawing.Bitmap(32, 32))
            using (var white = new System.Drawing.Bitmap(32, 32))
            using (var g1 = System.Drawing.Graphics.FromImage(black))
            using (var g2 = System.Drawing.Graphics.FromImage(white))
            {
                g1.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.Black), new System.Drawing.RectangleF(0, 0, 32, 32));
                g1.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.White), new System.Drawing.RectangleF(0, 0, 32, 32));

                var diff = black.PercentageDifference(white);
                Assert.IsTrue(diff > .99);

                diff = black.PercentageDifference(black);
                Assert.IsTrue(diff < .01);
            }
        }
  
        [TestMethod()]
        public void ImageProcessorTest_Default_NoMouse()
        {
            _Scope.Overlay.IsSelected = false;
            _Scope.Overlay.Chromakey = false;

            _Scope.Test(bitmap =>
            {
                var pixel = bitmap.GetPixel(_Scope.rect.Width - 1, 0);
                Assert.AreNotEqual(pixel.A, 0, "Pixel should not be transparent.");
            });
        }

        [TestMethod()]
        public void ImageProcessorTest_Chromakey_NoMouse_NotSelected()
        {
            _Scope.Overlay.Chromakey = true;
            _Scope.Overlay.IsSelected = false;

            _Scope.Test((bitmap) =>
            {
                var x = bitmap.GetPixel(0, 0);
                Assert.AreEqual(x.A, 0, "Pixel should be transparent.");
            });
        }

        [TestMethod()]
        public void ImageProcessorTest_Selected_NoMouse_NoChroma()
        {
            _Scope.Overlay.IsSelected = true;
            _Scope.Overlay.Chromakey = false;

            _Scope.Test((bitmap) =>
            {
                var x = bitmap.GetPixel(_Scope.rect.Width - 1, 0);
                Assert.AreNotEqual(x.A, 0, "Pixel should not be transparent.");
                Assert.AreEqual(_Scope.imageProcessor.HighlightColor.ToArgb(), bitmap.GetPixel(0, 0).ToArgb(), "Pixel should the the highlight color.");
            });
        }

        [TestMethod()]
        public void ImageProcessorTest_Chroma_Selected_NoMouse()
        {
            _Scope.Overlay.IsSelected = true;
            _Scope.Overlay.Chromakey = true;

            _Scope.Test((bitmap) =>
            {
                var x = bitmap.GetPixel(_Scope.rect.Width - 1, 0);
                Assert.AreEqual(x.A, 0, "Pixel should be transparent.");
                Assert.AreEqual(_Scope.imageProcessor.HighlightColor.ToArgb(), bitmap.GetPixel(0, 0).ToArgb(), "Pixel should the the highlight color.");
            });
        }

        [TestMethod()]
        public void ImageProcessorTest_Mouse_Chromakey_NotSelected()
        {
            _Scope.Overlay.IsSelected = false;
            _Scope.Overlay.Chromakey = true;
            _Scope.imageProcessor.MousePoint = new System.Drawing.Point(5, 5);

            _Scope.Test(bitmap =>
            {
                var x = bitmap.GetPixel(0, 0);

                Assert.AreEqual(x.A, 0, "Pixel should be transparent.");

                using (var crop = bitmap.Crop(_Scope.imageProcessor.MousePoint, _Scope.mouseIcon.Size))
                {
                    var distance = _Scope.mouseIcon.PercentageDifference(crop);
                    Assert.IsTrue(distance < .1);
                }
            });
        }
               

        [TestMethod()]
        public void ImageProcessorTest_Mouse_Selected_NoChroma()
        {
            _Scope.Overlay.IsSelected = true;
            _Scope.Overlay.Chromakey = false;
            _Scope.imageProcessor.MousePoint = new System.Drawing.Point(5, 5);

            _Scope.Test(bitmap =>
            {
                var px = bitmap.GetPixel(_Scope.rect.Width - 1, 0);
                Assert.AreNotEqual(px.A, 0, "Pixel should not be transparent.");
                Assert.AreEqual(_Scope.imageProcessor.HighlightColor.ToArgb(), bitmap.GetPixel(0, 0).ToArgb(), "Pixel should the the highlight color.");

                using (var crop = bitmap.Crop(_Scope.imageProcessor.MousePoint, _Scope.mouseIcon.Size))
                {
                    crop.MakeTransparent(System.Drawing.ColorTranslator.FromHtml(_Scope.Overlay.ChromakeyColor));
                    var distance = _Scope.mouseIcon.PercentageDifference(crop);
                    Assert.IsTrue(distance < .1);
                }
            });            
        }

        [TestMethod()]
        public void ImageProcessorTest_Mouse_Chromakey_Selected()
        {
            _Scope.Overlay.IsSelected = true;
            _Scope.Overlay.Chromakey = true;
            _Scope.imageProcessor.MousePoint = new System.Drawing.Point(5, 5);

            _Scope.Test(bitmap =>
            {
                var x = bitmap.GetPixel(_Scope.rect.Width - 1, 0);
                Assert.AreEqual(x.A, 0, "Pixel should be transparent.");

                Assert.AreEqual(_Scope.imageProcessor.HighlightColor.ToArgb(), bitmap.GetPixel(0, 0).ToArgb(), "Pixel should the the highlight color.");

                using (var crop = bitmap.Crop(_Scope.imageProcessor.MousePoint, _Scope.mouseIcon.Size))
                {
                    var distance = _Scope.mouseIcon.PercentageDifference(crop);
                    Assert.IsTrue(distance < .1);
                }
            });
        }

        [TestMethod()]
        public void ImageProcessorTest_Mouse_NoChroma_NotSelected()
        {
            _Scope.Overlay.IsSelected = false;
            _Scope.Overlay.Chromakey = false; 
            _Scope.imageProcessor.MousePoint = new System.Drawing.Point(5, 5);

            _Scope.Test(bitmap =>
            {
                var x = bitmap.GetPixel(_Scope.rect.Width - 1, 0);
                Assert.AreNotEqual(x.A, 0, "Pixel should not be transparent.");

                using (var crop = bitmap.Crop(_Scope.imageProcessor.MousePoint, _Scope.mouseIcon.Size))
                {
                    crop.MakeTransparent(System.Drawing.ColorTranslator.FromHtml(_Scope.Overlay.ChromakeyColor));
                    var distance = _Scope.mouseIcon.PercentageDifference(crop);
                    Assert.IsTrue(distance < .1);
                }
            });
        }

        class TestScope
        {
            public OverlayDecoratorTests Parent = null;
            private int dxObjects = 0;
            public DeviceManager DeviceManager;
            public Texture2D Texture;
            public Rectangle rect;
            public Texture2DDescription desc;
            public VirtualReality.VROverlayWindow Overlay;
            public Direct3D11CaptureSource.OverlayDecorator imageProcessor;
            public System.Drawing.Bitmap mouseIcon;

            public TestScope(OverlayDecoratorTests parent)
            {
                Parent = parent;
                dxObjects = SharpDX.Diagnostics.ObjectTracker.FindActiveObjects().Count;
                DeviceManager = new DeviceManager(null);
                Texture = DeviceManager.device.CreateTextureFromBitmap(".\\..\\..\\..\\HelpFiles\\CrewChief.png");

                rect = new Rectangle(0, 0, Texture.Description.Width, Texture.Description.Height);
                desc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read | CpuAccessFlags.Write,
                    BindFlags = BindFlags.None,
                    Format = DXGI.Format.B8G8R8A8_UNorm,
                    Width = rect.Width,
                    Height = rect.Height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };
                Overlay = new VirtualReality.VROverlayWindow
                {
                    Name = "UnitTest",
                    Chromakey = true,
                    ChromakeyColor = "#2E1800", // brown background of crewchief image
                    ChromakeyTolerance = 0.1f,
                    IsSelected = false,
                    isDisplay = false,
                    rectAbs = rect
                };

                using (var icon = System.Drawing.Icon.FromHandle(Direct3D11CaptureSource.OverlayDecorator.cursorIcon))
                    mouseIcon = icon.ToBitmap();

                Overlay.copiedScreenTexture = new Texture2D(DeviceManager.device, desc);
                imageProcessor = new Direct3D11CaptureSource.OverlayDecorator(DeviceManager, Overlay, new Size2(rect.Width, rect.Height));
            }
            
            public TestScope Test(Action<System.Drawing.Bitmap> assertions)
            {
                imageProcessor.Draw(Texture);
                using (var bitmap = Overlay.copiedScreenTexture.ToSystemBitmap(DeviceManager.device))
                {
                    // save the file so a human can look at it if needed
                    bitmap.Save($"{Parent.TestContext.TestName}.png");
                    assertions(bitmap);
                }
                return this;
            }

            public TestScope AssertDXObjects()
            {
                Trace.WriteLine(SharpDX.Diagnostics.ObjectTracker.ReportActiveObjects());
                Assert.AreEqual(dxObjects, SharpDX.Diagnostics.ObjectTracker.FindActiveObjects().Count, "DX Objects should be disposed.");
                return this;
            }

            public void Dispose()
            {
                GetDisposables()
                    .ToList()
                    .ForEach(disposable => disposable.Dispose());
            }

            public IEnumerable<IDisposable> GetDisposables()
            {
                yield return DeviceManager;
                yield return Texture;
                yield return Overlay.copiedScreenTexture;
                yield return imageProcessor;
                yield return mouseIcon;
            }

        }
    }
}