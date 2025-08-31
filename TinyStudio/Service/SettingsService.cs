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
        return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
    }

    public void SaveSettings(Settings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }
}
