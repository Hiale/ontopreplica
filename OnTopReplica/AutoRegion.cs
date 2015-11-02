using System;
using System.Drawing;
using OnTopReplica.Native;

namespace OnTopReplica
{
    public class AutoRegion
    {
        private const bool Debug = true;

        public static Rectangle Find(Point point, IntPtr hWnd)
        {
            var foregroundWindow = WindowManagerMethods.GetForegroundWindow();
            WindowManagerMethods.SetForegroundWindow(hWnd);
            var bmp = CaptureWindow(hWnd);
            if (Debug)
            {
                bmp.SetPixel(point.X, point.Y, DebugInverseColor(bmp.GetPixel(point.X, point.Y)));
                bmp.Save("screenshot.png");
            }
            WindowManagerMethods.SetForegroundWindow(foregroundWindow);
            Otsu.Process(bmp);
            if (Debug)
            {
                bmp.Save("otsu.png");
            }
            var floodFillArea = FloodFiller.FloodFill(bmp, point, Color.Red);
            if (Debug)
            {
                bmp.Save("floodfill.png");
            }
            return new Rectangle(floodFillArea.MinX, floodFillArea.MinY, floodFillArea.MaxX - floodFillArea.MinX, floodFillArea.MaxY - floodFillArea.MinY);
        }

        private static Color DebugInverseColor(Color color)
        {
            return Color.FromArgb((byte)(255 - color.A), color.R, color.G, color.B);
        }

        private static Bitmap CaptureWindow(IntPtr handle)
        {
            var rect = WindowRect.GetWindowRectangle(handle);
            var bitmap = new Bitmap(rect.Width, rect.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }

    }
}
