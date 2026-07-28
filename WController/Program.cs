using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WController;
internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += Application_ThreadException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            LogException(ex);
            MessageBox.Show(
                $"Fatal startup error:\n\n{ex.Message}\n\nDetails logged to crash.log",
                "WController Fatal Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "WController Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        LogException(ex);
        MessageBox.Show(
            $"A fatal error occurred:\n\n{ex?.Message ?? e.ExceptionObject?.ToString() ?? "Unknown error"}",
            "WController Fatal Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static void LogException(Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WController");
            Directory.CreateDirectory(dir);
            var logPath = Path.Combine(dir, "crash.log");
            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Swallow - logging must never throw
        }
    }
}
