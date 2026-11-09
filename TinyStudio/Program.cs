using Avalonia;
using System;
using System.IO;
using TinyStudio.Models;
using TinyStudio.Service;

namespace TinyStudio;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            var settings = new SettingsService().LoadSettings();
            LogService.Initialize(settings);
            AppDomain.CurrentDomain.ProcessExit += (s, e) => LogService.Shutdown();
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // WinExe has no console when double-clicked, so an unhandled startup
            // exception just exits silently. Always leave a crash log behind.
            LogService.Error("Fatal startup error", ex);
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "startup-error.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {ex}{Environment.NewLine}");
            }
            catch
            {
                // Never let the crash handler mask the original exception.
            }
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}