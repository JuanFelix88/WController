using System;
using System.Runtime.InteropServices;

namespace WController;

internal static class WindowLoadingDetector
{
    private const string LoadingProperty = "WController.Loading";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetProp(IntPtr windowHandle, string propertyName);

    public static bool IsLoading(IntPtr windowHandle)
    {
        return windowHandle != IntPtr.Zero && GetProp(windowHandle, LoadingProperty) != IntPtr.Zero;
    }
}
