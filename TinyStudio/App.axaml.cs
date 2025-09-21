using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using TinyStudio.ViewModels;
using TinyStudio.Views;

namespace TinyStudio;

using TinyStudio.Models;
using TinyStudio.Service;

public partial class App : Application
{
    public static Settings Settings { get; private set; } = new();
    public static SettingsService SettingsService { get; private set; } = new();
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);
            
            Settings = SettingsService.LoadSettings();
            if (Settings.UnityCNKey != null)
                UnityAsset.NET.Setting.DefaultUnityCNKey = Settings.UnityCNKey.Key;
            
            var viewModel = new MainWindowViewModel();

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}