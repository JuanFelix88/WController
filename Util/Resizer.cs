using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsController.Util
{
    internal static class Resizer
    {
        public static int DefaultIconViewSize { get; set; } = 18;
        public static Image ResizeImage(Image image, int width, int height)
        {
            Bitmap destImage = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(destImage))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                g.DrawImage(image, 0, 0, width, height);
            }
            return destImage;
        }
    }
}
