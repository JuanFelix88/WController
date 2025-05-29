using System.Runtime.InteropServices;

class ShowDesktopHelper
{
    const byte VK_LWIN = 0x5B;
    const byte D = 0x44;
    const int KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    public static void SimulateWinD()
    {
        keybd_event(VK_LWIN, 0, 0, 0);           // Press WIN
        keybd_event(D, 0, 0, 0);                 // Press D
        keybd_event(D, 0, KEYEVENTF_KEYUP, 0);   // Release D
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0); // Release WIN
    }
}
