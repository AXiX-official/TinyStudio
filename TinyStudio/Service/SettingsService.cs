using System;
using System.IO;
using System.Text.Json;
using TinyStudio.Models;

namespace TinyStudio.Service;

public class SettingsService
{
    private readonly string _settingsFilePath;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolderPath = Path.Combine(appDataPath, "TinyStudio");
        if (!Directory.Exists(appFolderPath))
        {
            Directory.CreateDirectory(appFolderPath);
        }
        _settingsFilePath = Path.Combine(appFolderPath, "settings.json");
    }

    public Settings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new Settings();
        }
        
        var json = File.ReadAllText(_settingsFilePath);
        return JsonSerializer.Deserialize(json, AppJsonContext.Default.Settings) ?? new Settings();
    }

    public void SaveSettings(Settings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.Settings);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are non-critical; a locked or read-only user profile must
            // never prevent the application from starting.
            LogService.Warning($"Failed to save settings: {ex.Message}");
        }
    }
}
