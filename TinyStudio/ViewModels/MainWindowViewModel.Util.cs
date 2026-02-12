using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
#if DEBUG
        var verifier = new ResetVerifier();
        verifier.CaptureObjects(_assetManager.LoadedFiles.Values);
#endif
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
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
#if DEBUG
        verifier.VerifyReleased();
#endif
    }
}

#if DEBUG
public class ResetVerifier
{
    private WeakReference[] _refs = [];

    public void CaptureObjects(IEnumerable<object> objects)
    {
        _refs = objects.Select(o => new WeakReference(o)).ToArray();
    }

    public void VerifyReleased()
    {
        //ForceFullGC();

        int alive = _refs.Count(r => r.IsAlive);
        if (alive > 0)
            Console.WriteLine($"Warning: {alive} of {_refs.Length} objects still alive!");
        else
            Console.WriteLine("All captured objects released successfully.");
    }

    private void ForceFullGC()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
}
#endif
