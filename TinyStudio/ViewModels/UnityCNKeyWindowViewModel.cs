using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TinyStudio.Models;
using TinyStudio.Views;

namespace TinyStudio.ViewModels;

public partial class UnityCNKeyWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<UnityCNKey> _keys = new();

    [ObservableProperty]
    private UnityCNKey? _selectedKey;

    [ObservableProperty]
    private string _customName = string.Empty;

    [ObservableProperty]
    private string _customKey = string.Empty;

    public UnityCNKey? Result { get; private set; }
    
    private TaskCompletionSource<UnityCNKey?>? _completionSource;
    private Window? _window;
    private readonly string _jsonPath = "Keys.json";
    
    public UnityCNKeyWindowViewModel()
    {
        LoadKeysFromJson();
    }

    private void LoadKeysFromJson()
    {
        if (File.Exists(_jsonPath))
        {
            var json = File.ReadAllText(_jsonPath);
            var keys = JsonSerializer.Deserialize<List<UnityCNKey>>(json);
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    Keys.Add(key);
                }
            }
        }
    }

    private async Task SaveKeysToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        await using var fileStream = new FileStream(_jsonPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fileStream, Keys, options);
    }

    [RelayCommand]
    private void SelectKey(UnityCNKey key)
    {
        Result = key;
        App.Settings.UnityCNKey = key;
        App.SettingsService.SaveSettings(App.Settings);
        _completionSource?.TrySetResult(Result);
        CloseWindow();
    }

    [RelayCommand]
    private async Task AddCustomKey()
    {
        if (!string.IsNullOrWhiteSpace(CustomName) && !string.IsNullOrWhiteSpace(CustomKey))
        {
            var newKey = new UnityCNKey
            {
                Name = CustomName,
                Key = CustomKey
            };
            
            Keys.Add(newKey);

            await SaveKeysToJson();
            App.Settings.UnityCNKey = newKey;
            App.SettingsService.SaveSettings(App.Settings);
            
            Result = newKey;
            _completionSource?.TrySetResult(Result);
            CloseWindow();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = null;
        _completionSource?.TrySetResult(null);
        CloseWindow();
    }
    
    private void CloseWindow()
    {
        _window?.Close();
    }
    
    public Task<UnityCNKey?> ShowDialogAsync(Window owner)
    {
        _completionSource = new TaskCompletionSource<UnityCNKey?>();
        var window = new UnityCNKeyWindow { DataContext = this };
        _window = window;
        window.Closed += (s, e) => _completionSource.TrySetResult(Result);
        window.ShowDialog(owner);
        return _completionSource.Task;
    }
}