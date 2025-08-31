using Avalonia;
using System;
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
        var settings = new SettingsService().LoadSettings();
        LogService.Initialize(settings);
        AppDomain.CurrentDomain.ProcessExit += (s, e) => LogService.Shutdown();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}