using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class WindowPreview
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUnregisterThumbnail(IntPtr thumb);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnail, ref DWM_THUMBNAIL_PROPERTIES props);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_THUMBNAIL_PROPERTIES
    {
        public int dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        public bool fVisible;
        public bool fSourceClientAreaOnly;
    }

    private const int DWM_TNP_RECTDESTINATION = 0x00000001;
    private const int DWM_TNP_RECTSOURCE = 0x00000002;
    private const int DWM_TNP_OPACITY = 0x00000004;
    private const int DWM_TNP_VISIBLE = 0x00000008;
    private const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

    public static IntPtr ShowPreview(IntPtr targetHwnd, IntPtr windowHwnd, Rectangle area, byte opacity = 255)
    {
        if (!IsWindow(windowHwnd))
            return IntPtr.Zero;

        if (DwmRegisterThumbnail(targetHwnd, windowHwnd, out IntPtr thumb) != 0)
            return IntPtr.Zero;

        var rect = new RECT { Left = area.Left, Top = area.Top, Right = area.Right, Bottom = area.Bottom };

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_OPACITY,
            rcDestination = rect,
            opacity = opacity,
            fVisible = true,
            fSourceClientAreaOnly = false,
        };

        DwmUpdateThumbnailProperties(thumb, ref props);

        return thumb;
    }

    public static void ClosePreview(IntPtr thumbnailHandle)
    {
        if (thumbnailHandle != IntPtr.Zero)
            DwmUnregisterThumbnail(thumbnailHandle);
    }
}
