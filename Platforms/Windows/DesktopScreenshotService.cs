using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace ScreenTracker1.Platforms.Windows
{
    public class DesktopScreenshotService
    {
        public byte[] CaptureDesktop()
        {
            // Get the primary screen bounds
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;

            using var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);

            // Capture the full desktop
            g.CopyFromScreen(0, 0, 0, 0, bmp.Size);

            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

    }
}
