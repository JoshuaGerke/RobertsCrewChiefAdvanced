using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CrewChiefV4
{    
    public static class Extensions
    {
        /// <summary>
        /// Creates an infinite series from the enumeration, once the end of the enumeration is reached, it starts over from the beginning. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static IEnumerable<T> Cyclic<T>(this IEnumerable<T> enumerable, uint halt = 0)
        {
            int cycles = 0;
            while (true)
            {
                foreach (var x in enumerable)
                    yield return x;
                if (halt != 0 && cycles++ > halt)
                    break;
            }
        }

        public static void Invoke(this System.Windows.Forms.Control control, Action action)
        {
            control.Invoke((Delegate)action);
        }

        public static string ToHtmlColorString(this System.Drawing.Color color)
        {
            return System.Drawing.ColorTranslator.ToHtml(color);
        }

        public static SharpDX.Rectangle AsSharpDX(this System.Drawing.Rectangle rectangle)
        {
            return new SharpDX.Rectangle(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);
        }

        internal static SharpDX.Rectangle ToScreenRect(this Win32Stuff.WINDOWINFO info)
        {
            return new SharpDX.Rectangle(info.rcWindow.Left + (int)info.cxWindowBorders, info.rcWindow.Top, info.rcWindow.Width - (int)info.cxWindowBorders * 2, info.rcWindow.Height - (int)info.cyWindowBorders);
        }

        internal static SharpDX.Rectangle ToAbsScreenRect(this Win32Stuff.WINDOWINFO info, System.Drawing.Rectangle rectangle)
        {
            return new SharpDX.Rectangle(Math.Abs(rectangle.Left - info.rcWindow.Left) + (int)info.cxWindowBorders, Math.Abs(rectangle.Top - info.rcWindow.Top), info.rcWindow.Width - (int)info.cxWindowBorders * 2, info.rcWindow.Height - (int)info.cyWindowBorders);
        }

        /// <summary>
        /// Coverts a System Color to SharpDX
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static SharpDX.Mathematics.Interop.RawColor3 ToRawColor3(this System.Drawing.Color color)
        {
            return new SharpDX.Mathematics.Interop.RawColor3(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f);
        }

        public static SharpDX.Mathematics.Interop.RawColor4 ToRawColor4(this System.Drawing.Color color)
        {
            return new SharpDX.Mathematics.Interop.RawColor4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
        }
        public static System.Drawing.Bitmap Crop(this System.Drawing.Image image, int x, int y, int w, int h)
        {
            return Crop(image, new System.Drawing.Rectangle(x, y, w, h));
        }

        public static System.Drawing.Bitmap Crop(this System.Drawing.Image image, System.Drawing.Point point, System.Drawing.Size size)
        {
            return Crop(image, new System.Drawing.Rectangle(point.X, point.Y, size.Width, size.Height));
        }

        public static System.Drawing.Bitmap Crop(this System.Drawing.Image image, System.Drawing.Rectangle section)
        {
            var bitmap = new System.Drawing.Bitmap(section.Width, section.Height);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.DrawImage(image, 0, 0, section, System.Drawing.GraphicsUnit.Pixel);
                return bitmap;
            }
        }

        public static SharpDX.Direct3D11.Texture2D CreateTextureFromBitmap(this SharpDX.Direct3D11.Device device, string filename)
        {
            using (var bitmap = new System.Drawing.Bitmap(filename))
            {
                System.Drawing.Imaging.BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                var buffer = new SharpDX.Direct3D11.Texture2D(device, new SharpDX.Direct3D11.Texture2DDescription
                {
                    MipLevels = 1,
                    Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                    Width = bitmap.Width,
                    Height = bitmap.Height,
                    ArraySize = 1,
                    BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource,
                    Usage = SharpDX.Direct3D11.ResourceUsage.Default,
                    SampleDescription = { Count = 1, Quality = 0 }
                }, new SharpDX.DataRectangle(data.Scan0, data.Stride));
                bitmap.UnlockBits(data);


                return buffer;
            }
        }

        public static System.Drawing.Bitmap ToSystemBitmap(this SharpDX.Direct3D11.Texture2D texture, SharpDX.Direct3D11.Device device)
        {
            var mapSource = device.ImmediateContext.MapSubresource(texture, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

            // Create Drawing.Bitmap
            int width = texture.Description.Width;
            int height = texture.Description.Height;
            var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var boundsRect = new System.Drawing.Rectangle(0, 0, width, height);

            // Copy pixels from screen capture Texture to GDI bitmap
            var mapDest = bitmap.LockBits(boundsRect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
            var sourcePtr = mapSource.DataPointer;
            var destPtr = mapDest.Scan0;
            for (int y = 0; y < height; y++)
            {
                // Copy a single line 
                SharpDX.Utilities.CopyMemory(destPtr, sourcePtr, width * 4);

                // Advance pointers
                sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                destPtr = IntPtr.Add(destPtr, mapDest.Stride);
            }

            // Release source and dest locks
            bitmap.UnlockBits(mapDest);
            device.ImmediateContext.UnmapSubresource(texture, 0);
            return bitmap;
        }

        public static double PercentageDifference(this System.Drawing.Bitmap image, System.Drawing.Bitmap compare)
        {
            double totalError = 0.0;
            for (int i = 0; i < image.Width; i++)
            {
                for (int j = 0; j < image.Height; j++)
                {
                    totalError += image.GetPixel(i, j).PythagoreanDistance(compare.GetPixel(i, j));
                }
            }
            return totalError / (image.Width * image.Height);
        }

        /// <summary>
        ///  calculate the Pythagorean distance between the Red, Green and Blue components
        ///  this will return an number between 0 (when the Colors are identical) and 1 (Black vs White)
        /// </summary>
        /// <param name="color"></param>
        /// <param name="otherColor"></param>
        /// <returns></returns>
        public static double PythagoreanDistance(this System.Drawing.Color color, System.Drawing.Color otherColor)
        {
            var result = new [] { color.R - otherColor.R, color.B - otherColor.B, color.G - otherColor.G }
                            .Select(x => Math.Pow(x, 2))
                            .Sum() / (Math.Pow(256, 2) * 3);
            return result;
        }

        public static bool TryCreate(this DirectoryInfo dir, Action<Exception> onException = null)
        {
            try
            {
                if (!dir.Exists)
                {
                    dir.Create();
                }
                return true;
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);
                return false;
            }
        }

        /*public static int IndexOfMin<T>(this IList<T> list) where T : IComparable
        {
            if (list == null)
                throw new ArgumentNullException("list");

            IEnumerator<T> enumerator = list.GetEnumerator();
            bool isEmptyList = !enumerator.MoveNext();

            if (isEmptyList)
                throw new ArgumentOutOfRangeException("list", "list is empty");

            int minOffset = 0;
            T minValue = enumerator.Current;
            for (int i = 1; enumerator.MoveNext(); ++i)
            {
                if (enumerator.Current.CompareTo(minValue) >= 0)
                    continue;

                minValue = enumerator.Current;
                minOffset = i;
            }

            return minOffset;
        }*/
        // stackoverflow...
        public static int IndexOfMin<T>(this IEnumerable<T> source, IComparer<T> comparer = null)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            if (comparer == null)
                comparer = Comparer<T>.Default;

            using (var enumerator = source.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return -1;    // or maybe throw InvalidOperationException

                int minIndex = 0;
                T minValue = enumerator.Current;

                int index = 0;
                while (enumerator.MoveNext())
                {
                    index++;
                    if (comparer.Compare(enumerator.Current, minValue) < 0)
                    {
                        minIndex = index;
                        minValue = enumerator.Current;
                    }
                }
                return minIndex;
            }
        }
    }

    public static class MathExtensions
    {
        /// <summary>
        /// Project a 2-dimensional vector to a raw float array
        /// </summary>
        /// <param name="vector">The vector</param>
        /// <returns>A float array containing the vector x-component at index 0 and the y-component at index 1</returns>
        public static float[] toFloatArray(this Vector2 vector) => new float[2] { vector.X, vector.Y };

        /// <summary>
        /// Project a 3-dimensional vector to a raw float array
        /// </summary>
        /// <param name="vector">The vector</param>
        /// <returns>A float array containing the vector x-component at index 0, the y-component at index 1, the z-component at index 2</returns>
        public static float[] toFloatArray(this Vector3 vector) => new float[3] { vector.X, vector.Y, vector.Z };
    }

    public static class MemoryManagementExtensions
    {
        /// <summary>
        /// Reinterpret an handle as an object of the specified type
        /// </summary>
        /// <typeparam name="T">The target type</typeparam>
        /// <param name="handle">The handle to recast</param>
        /// <returns>An instance of T</returns>
        public static T PointerCast<T>(this GCHandle handle) => (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));

        /// <summary>
        /// Reinterpret a pointer as an object of the specified type
        /// </summary>
        /// <typeparam name="T">The target type</typeparam>
        /// <param name="ptr">The pointer to recast</param>
        /// <returns>An instance of T</returns>
        public static T PointerCast<T>(this IntPtr ptr) => (T)Marshal.PtrToStructure(ptr, typeof(T));
    }
}
