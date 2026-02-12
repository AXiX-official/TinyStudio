using System;
using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using TinyStudio.Models;
using TinyStudio.Previewer;
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

    private void UnsubscribeFromSceneNodeEvents(IEnumerable<SceneNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.PropertyChanged -= SceneNode_PropertyChanged;
            UnsubscribeFromSceneNodeEvents(node.SubNodes);
        }
    }
    
    [RelayCommand]
    private void Reset()
    {
        _window!.Title = App.AppName;
        StatusText = "Ready";
        ProgressValue = 0;
        
        ResetWorkspace();
        
        LoadedFiles = new();
        SelectedAsset = null;
        
        _selectedNodes = new();
        UnsubscribeFromSceneNodeEvents(SceneHierarchyNodes);
        SceneHierarchyNodes = new();
        
        foreach (var selectableType in AssetTypes)
            selectableType.PropertyChanged -= OnSelectableTypePropertyChanged;
        AssetTypes = new();
        
        FilteredAssets = new DataGridCollectionView(Array.Empty<AssetWrapper>())
        {
            Filter = FilterAssets
        };
        
        _assetFilter.Reset();
        SearchText = string.Empty;
        
        _assetManager.Clear();
        //GC.Collect();
    }
}