using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class WindowEffects
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public int dwFlags;
        public bool fEnable;
        public IntPtr hRgnBlur;
        public bool fTransitionOnMaximized;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
    }

    [ComImport, Guid("602D4995-B13A-429b-A66E-1935E44F4317"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3 : ITaskbarList
    {
        new void HrInit();
        new void AddTab(IntPtr hwnd);
        new void DeleteTab(IntPtr hwnd);
        new void ActivateTab(IntPtr hwnd);
        new void SetActiveAlt(IntPtr hwnd);

        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
        void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(IntPtr hwnd, int tbpFlags);
    }

    private static readonly ITaskbarList3 taskbarList = (ITaskbarList3)new CTaskbarList();

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    private class CTaskbarList { }

    public static void ExtendFrame(IntPtr hwnd, int margin)
    {
        var m = new MARGINS { cxLeftWidth = margin, cxRightWidth = margin, cyTopHeight = margin, cyBottomHeight = margin };
        DwmExtendFrameIntoClientArea(hwnd, ref m);
    }

    public static void EnableBlur(IntPtr hwnd)
    {
        var bb = new DWM_BLURBEHIND { dwFlags = 1, fEnable = true, hRgnBlur = IntPtr.Zero };
        DwmEnableBlurBehindWindow(hwnd, ref bb);
    }

    public static void SetAttribute(IntPtr hwnd, int attr, int value)
    {
        DwmSetWindowAttribute(hwnd, attr, ref value, Marshal.SizeOf(typeof(int)));
    }

    public static void SetTaskbarProgress(IntPtr hwnd, ulong current, ulong total)
    {
        taskbarList.SetProgressValue(hwnd, current, total);
    }

    public static void SetTaskbarState(IntPtr hwnd, int state)
    {
        taskbarList.SetProgressState(hwnd, state);
    }
}
