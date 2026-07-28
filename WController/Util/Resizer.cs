using System.Drawing;
using System.Drawing.Drawing2D;

namespace WController.Util;

internal static class Resizer
{
    public static int DefaultIconViewSize { get; set; } = 24;
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

    public static Image? ResizeImageFromFile(string filePath, int width, int height)
    {
        try
        {
            Image source;
            if (filePath.EndsWith(".ico", System.StringComparison.OrdinalIgnoreCase))
            {
                using (var icon = new System.Drawing.Icon(filePath))
                    source = icon.ToBitmap();
            }
            else
            {
                source = Image.FromFile(filePath);
            }

            using (source)
                return ResizeImage(source, width, height);
        }
        catch
        {
            return null;
        }
    }
}
