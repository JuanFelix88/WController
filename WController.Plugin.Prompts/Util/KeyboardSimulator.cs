using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WController.Plugin.Prompts.Util;
public static class KeyboardSimulator
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    private const int KEYEVENTF_KEYUP = 0x0002;

    public static void SendCtrlV()
    {
        keybd_event((byte)Keys.ControlKey, 0, 0, 0);
        keybd_event((byte)Keys.V, 0, 0, 0);
        keybd_event((byte)Keys.V, 0, KEYEVENTF_KEYUP, 0);
        keybd_event((byte)Keys.ControlKey, 0, KEYEVENTF_KEYUP, 0);
    }

    public static void SendEnter()
    {
        keybd_event((byte)Keys.Enter, 0, 0, 0);
        keybd_event((byte)Keys.Enter, 0, KEYEVENTF_KEYUP, 0);
    }
}