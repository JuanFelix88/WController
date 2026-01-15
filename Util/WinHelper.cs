using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WController.Util;

public static class WinHelper
{
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Obtém o caminho completo do executável associado ao WindowItem.
    /// </summary>
    /// <param name="windowItem">O item da janela</param>
    /// <returns>O caminho completo do executável ou string vazia se não for possível obter</returns>
    public static string GetPathFrom(MainForm.WindowItem windowItem)
    {
        if (windowItem == null || windowItem.Handle == IntPtr.Zero)
            return string.Empty;

        try
        {
            GetWindowThreadProcessId(windowItem.Handle, out uint processId);
            
            if (processId == 0)
                return string.Empty;

            using (Process process = Process.GetProcessById((int)processId))
            {
                return process.MainModule?.FileName ?? string.Empty;
            }
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Obtém o caminho completo do executável associado ao handle da janela.
    /// </summary>
    /// <param name="hWnd">O handle da janela</param>
    /// <returns>O caminho completo do executável ou string vazia se não for possível obter</returns>
    public static string GetPathFromHandle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return string.Empty;

        try
        {
            GetWindowThreadProcessId(hWnd, out uint processId);
            
            if (processId == 0)
                return string.Empty;

            using (Process process = Process.GetProcessById((int)processId))
            {
                return process.MainModule?.FileName ?? string.Empty;
            }
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static string GetSoftwareNameFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        try
        {
            return Path.GetFileNameWithoutExtension(path);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
