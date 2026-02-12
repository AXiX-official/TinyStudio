using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TinyStudio.Models;
using TinyStudio.Views;

namespace TinyStudio.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty]
    private string _searchTextWorkspace = string.Empty;
    
    [ObservableProperty]
    private DataGridCollectionView _filteredAssetsWorkspace;
    
    private List<AssetWrapper> _assetsWorkspace;
    
    public ObservableCollection<SelectableType> AssetTypesWorkspace { get; private set; } = new();
    private readonly AssetFilter _assetFilterWorkspace = new();
    
    [ObservableProperty]
    private bool _useRegexWorkspace;
    
    [ObservableProperty]
    private AssetWrapper? _selectedAssetWorkspace;

    [ObservableProperty] 
    private ObservableCollection<AssetWrapper> _selectedAssetsWorkspace = new();

    private void ResetWorkspace()
    {
        SelectedAssetWorkspace = null;
        SearchTextWorkspace = string.Empty;
        foreach (var selectableType in AssetTypesWorkspace)
            selectableType.PropertyChanged -= OnSelectableTypeWorkspacePropertyChanged;
        AssetTypesWorkspace = new();
        _assetsWorkspace = new();
        _assetFilterWorkspace.Reset();
        SelectedAssetsWorkspace = new();
        FilteredAssetsWorkspace = new DataGridCollectionView(Array.Empty<AssetWrapper>())
        {
            Filter = FilterAssetsWorkspace
        };
    }
    
    private bool FilterAssetsWorkspace(object item)
    {
        return item is AssetWrapper asset && _assetFilterWorkspace.Matches(asset);
    }
    
    partial void OnSearchTextWorkspaceChanged(string value)
    {
        _assetFilterWorkspace.UpdateSearchFilter(value, UseRegexWorkspace);
        FilteredAssetsWorkspace.Refresh();
    }

    partial void OnUseRegexWorkspaceChanged(bool value)
    {
        _assetFilterWorkspace.UpdateSearchFilter(SearchTextWorkspace, value);
        FilteredAssetsWorkspace.Refresh();
    }
    
    [RelayCommand]
    private void RemoveFromWorkspace(AssetWrapper asset)
    {
        _assetsWorkspace.Remove(asset);
        FilteredAssetsWorkspace = new(_assetsWorkspace)
        {
            Filter = FilterAssetsWorkspace
        };
        var type = asset.Type;
        if (_assetsWorkspace.All(a => a.Type != type))
        {
            var selectableType = AssetTypesWorkspace.FirstOrDefault(t => t.TypeName == type);
            selectableType!.PropertyChanged -= OnSelectableTypeWorkspacePropertyChanged;
            AssetTypesWorkspace.Remove(selectableType);
        }
    }
    
    partial void OnSelectedAssetWorkspaceChanged(AssetWrapper? value)
    {
        if (SelectedFileTab!.Content is not Workspace)
            return;
        if (IsDumpEnabled)
        {
            _dumpViewModel?.UpdateDumpContent(SelectedAssetWorkspace);
        }
        SelectedAsset?.OnPropertyChanged(nameof(AssetWrapper.Name));
        UpdatePreviewControl(value);
    }

    private bool _isUpdatingAssetTypesWorkspace;
    
    private void OnSelectableTypeWorkspacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableType.IsSelected) || _isUpdatingAssetTypesWorkspace)
            return;

        try
        {
            _isUpdatingAssetTypesWorkspace = true;
            if (sender is SelectableType { TypeName: "All" } allType)
            {
                var selectAll = allType.IsSelected;
                foreach (var type in AssetTypesWorkspace)
                    type.IsSelected = selectAll;
                _assetFilter.UpdateTypeFilter(AssetTypesWorkspace.Where(t => t.IsSelected && t.TypeName != "All").Select(t => t.TypeName).ToHashSet(), selectAll);
                if (selectAll)
                    FilteredAssetsWorkspace.Refresh();
            }
            else
            {
                var all = AssetTypesWorkspace.FirstOrDefault(t => t.TypeName == "All");
                if (all is null) return;
                if (AssetTypesWorkspace.Skip(1).Any(t => !t.IsSelected))
                {
                    all.IsSelected = false;
                }
                else if (AssetTypesWorkspace.Skip(1).All(t => t.IsSelected))
                {
                    all.IsSelected = true;
                }
                _assetFilterWorkspace.UpdateTypeFilter(AssetTypesWorkspace.Where(t => t.IsSelected && t.TypeName != "All").Select(t => t.TypeName).ToHashSet(), all.IsSelected);
                FilteredAssetsWorkspace.Refresh();
            }
        }
        finally
        {
            _isUpdatingAssetTypesWorkspace = false;
        }
    }
}