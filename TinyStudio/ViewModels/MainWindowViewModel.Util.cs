using System;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using TinyStudio.Models;
using TinyStudio.Service;

namespace TinyStudio.ViewModels;

public partial class MainWindowViewModel
{
    public void SetWindow(Window window)
    {
        _window = window;
    }

    private void LogStatus(string msg, LogLevel level = LogLevel.Info)
    {
        StatusText = msg;
        LogService.Log(level, msg);
    }
    
    [RelayCommand]
    private void Reset()
    {
        _window!.Title = App.AppName;
        foreach (var selectableType in AssetTypes)
            selectableType.PropertyChanged -= OnSelectableTypePropertyChanged;
        AssetTypes.Clear();
        
        LoadedFiles.Clear();
        _assetFilter.Reset();
        FilteredAssets = new DataGridCollectionView(Array.Empty<AssetWrapper>())
        {
            Filter = FilterAssets
        };
        SearchText = string.Empty;
        _assetManager.Clear();
        _fileSystem.Clear();
        
        ResetWorkspace();
        
        UnityAsset.NET.TypeTree.TypeTreeCache.CleanCache();
        UnityAsset.NET.TypeTree.AssemblyManager.CleanCache();
        GC.Collect();
    }
}