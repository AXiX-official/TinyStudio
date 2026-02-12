using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TinyStudio.Games.GF;
using TinyStudio.Games.PerpetualNovelty;
using TinyStudio.Models;
using TinyStudio.Service;
using TinyStudio.Previewer;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;
using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace TinyStudio.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly Settings _settings;
    private IFileSystem _fileSystem;
    private AssetManager _assetManager;
    private Window? _window;
    private TabItemViewModel? _previewTab;
    private TabItemViewModel? _dumpTab;
    private DumpViewModel? _dumpViewModel;
    private TabItemViewModel? _consoleTab;
    private ConsoleLogViewModel? _consoleLogViewModel;

    [ObservableProperty]
    private Control _previewControl;

    [ObservableProperty]
    private ObservableCollection<TabItemViewModel> _fileTabs = new();

    [ObservableProperty]
    private TabItemViewModel? _selectedFileTab;
    
    [ObservableProperty]
    private ObservableCollection<TabItemViewModel> _viewTabs = new();

    [ObservableProperty]
    private TabItemViewModel? _selectedViewTab;
    
    [ObservableProperty]
    private ObservableCollection<IVirtualFile> _loadedFiles;
    
    [ObservableProperty]
    private DataGridCollectionView _filteredAssets;
    
    public ObservableCollection<SelectableType> AssetTypes { get; private set; } = new();
    private readonly AssetFilter _assetFilter = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _useRegex;
    
    [ObservableProperty]
    private AssetWrapper? _selectedAsset;
    
    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private string _statusText = "Ready";
    
    [ObservableProperty]
    private ObservableCollection<SceneNode> _sceneHierarchyNodes = new();

    private ObservableCollection<SceneNode> _selectedNodes = new();

    #region File

    [RelayCommand]
    private async Task LoadFile()
    {
        var files = await _window!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load File",
            AllowMultiple = true,
            FileTypeFilter = [new("any file") { Patterns = [ "*" ] }]
        });
        
        if (files.Any())
        {
            await LoadFilesFromPathsAsync(files.Select(f => f.Path.LocalPath));
        }
        else
        {
            LogStatus("File loading canceled.");
        }
    }
    
    [RelayCommand]
    private async Task LoadFolder()
    {
        var folders = await _window!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Load Folder",
            AllowMultiple = false
        });
        
        if (folders.Any())
        {
            var folderPath = folders[0].Path.LocalPath;
            var filePaths = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            await LoadFilesFromPathsAsync(filePaths);
        }
        else
        {
            LogStatus("Folder loading canceled.");
        }
    }

    [RelayCommand]
    private async Task LoadFileList()
    {
        var files = await _window!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load FileList",
            AllowMultiple = false,
            FileTypeFilter = [new("FileList") { Patterns = [ "*.txt" ] }]
        });
        
        if (files.Any())
        {
            var folder = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose Directory",
                AllowMultiple = false
            });

            var baseDirectory = folder.Any() ? folder[0].Path.LocalPath : string.Empty;
            
            await using FileStream fs = new FileStream(files[0].Path.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using StreamReader reader = new StreamReader(fs);
            var filePath = new List<string>();
            while (await reader.ReadLineAsync() is {} line)
            {
                filePath.Add(Path.Combine(baseDirectory, line));
            }
            await LoadFilesFromPathsAsync(filePath);
        }
        else
        {
            LogStatus("File loading canceled.");
        }
    }

    public async Task LoadFilesFromPathsAsync(IEnumerable<string> paths)
    {
        Reset();
        
        try
        {
            var pathList = paths.ToList();
            if (pathList.Count == 0)
            {
                LogStatus("No files to load.");
                return;
            }

            var progress = new ThrottledProgress(
                new Progress<LoadProgress>(p =>
                {
                    StatusText = p.StatusText;
                    ProgressValue = (int)p.Percentage;
                }), 
                TimeSpan.FromMilliseconds(100));
            
            LogStatus($"Starting to load {pathList.Count} files.");
            var startTime = Stopwatch.StartNew();
            var virtualFiles = await _fileSystem.LoadAsync(pathList, progress);
            progress.Flush();
            LogStatus($"Loaded {pathList.Count} files in {startTime.Elapsed.TotalSeconds:F2} seconds.");
            
            LoadedFiles.Clear();
            foreach (var virtualFile in virtualFiles)
                LoadedFiles.Add(virtualFile);
            
            LogStatus($"Starting to load bundle file from {virtualFiles.Count} virtual files.");
            startTime.Restart();
            await _assetManager.LoadAsync(virtualFiles, true, progress);
            progress.Flush();
            LogStatus($"Loaded bundle files in {startTime.Elapsed.TotalSeconds:F2} seconds.");

            /*if (_assetManager.NeedTpk)
            {
                if (File.Exists(UnityAsset.NET.Setting.DefaultTpkFilePath))
                {
                    await _assetManager.LoadTpkFile(UnityAsset.NET.Setting.DefaultTpkFilePath, progress: progress);
                }
                else
                {
                    var file = await _window!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Load Tpk file",
                        AllowMultiple = false,
                        FileTypeFilter = [new("Tpk") { Patterns = [ "*.tpk" ] }]
                    });
        
                    if (file.Any())
                    {
                        await _assetManager.LoadTpkFile(file[0].Path.LocalPath, progress: progress);
                    }
                    else
                    {
                        LogStatus("Tpk loading canceled.");
                        Reset();
                        return;
                    }
                }
            }*/
            
            LogStatus("Loading Assets...");
            startTime.Restart();
            ProgressValue = 50;
            
            var assets = new AssetWrapper[_assetManager.LoadedAssets.Count];
            var assetWrapperToAssetMap = new ConcurrentDictionary<long, AssetWrapper>();

            Parallel.ForEach(
                Enumerable.Range(0, _assetManager.LoadedAssets.Count),
                (i, _) =>
                {
                    var asset = _assetManager.LoadedAssets[i];
                    var wrapper = new AssetWrapper(asset);
                    assets[i] = wrapper;
                    assetWrapperToAssetMap.TryAdd(asset.PathId, wrapper);
                });
            
            LogStatus($"Loaded {assets.Length} Assets in {startTime.Elapsed.TotalSeconds:F2} seconds.");
            ProgressValue = 100;
            progress.Flush();
            startTime.Restart();
            
            LogStatus("Building Scene Hierarchy...");
            
            await BuildSceneHierarchy(assetWrapperToAssetMap, progress);
            progress.Flush();
            foreach (var selectableType in AssetTypes)
                selectableType.PropertyChanged -= OnSelectableTypePropertyChanged;
            AssetTypes.Clear();
            
            var all = new SelectableType("All", true);
            all.PropertyChanged += OnSelectableTypePropertyChanged;
            AssetTypes.Add(all);
            
            var distinctTypes = assets.Select(a => a.Type).Distinct().OrderBy(t => t);
            foreach (var typeName in distinctTypes)
            {
                var selectableType = new SelectableType(typeName, true);
                selectableType.PropertyChanged += OnSelectableTypePropertyChanged;
                AssetTypes.Add(selectableType);
            }
            
            FilteredAssets = new DataGridCollectionView(new AvaloniaList<AssetWrapper>(assets))
            {
                Filter = FilterAssets
            };
            
            var unityVersion = _assetManager.Version;
            LogService.Info($"Unity version: {unityVersion}");
            var platform = _assetManager.BuildTarget;
            LogService.Info($"Build target: {platform}");
            _window!.Title = $"{App.AppName} {platform} - {unityVersion}";
        }
        catch (Exception ex)
        {
            LogService.Error($"Error occurred: {ex.Message}\n{ex.StackTrace}");
            StatusText = $"Error: {ex.Message.Split('\n').FirstOrDefault()}";
            //throw;
        }
    }

    private async Task BuildSceneHierarchy(ConcurrentDictionary<long, AssetWrapper> map, IProgress<LoadProgress>? progress = null)
    {
        await Task.Run(() =>
        {
            var nodes = new ConcurrentBag<SceneNode>();
            var nodeMap = new ConcurrentDictionary<GameObject, SceneNode>();
            int progressCount = 0;
            var total = _assetManager.VirtualFileToFileMap.Count;

            Parallel.ForEach(_assetManager.VirtualFileToFileMap, (kvp, _) =>
            {
                var vf = kvp.Key;
                var file = kvp.Value;

                var rootNode = new SceneNode(vf.Name);
                
                if (file is SerializedFile sf)
                {
                    BuildSceneHierarchy(sf, map, nodeMap, rootNode);
                }
                else if (file is BundleFile bf)
                {
                    foreach (var subFile in bf.Files)
                    {
                        if (subFile.File is SerializedFile subSf)
                        {
                            var subNode = new SceneNode(subFile.Info.Path);
                            BuildSceneHierarchy(subSf, map, nodeMap, subNode);
                            if (subNode.SubNodes.Count > 0)
                                rootNode.SubNodes.Add(subNode);
                        }
                    }
                }
                if (rootNode.SubNodes.Count > 0)
                    nodes.Add(rootNode);
                int currentProgress = Interlocked.Increment(ref progressCount);
                progress?.Report(new LoadProgress($"Build Scene Hierarchy: Processing {vf.Name}", total, currentProgress));
            });

            SceneHierarchyNodes = new(nodes);
            SubscribeToSceneNodeEvents(SceneHierarchyNodes);
        });
    }

    private void BuildSceneHierarchy(SerializedFile sf, ConcurrentDictionary<long, AssetWrapper> map, ConcurrentDictionary<GameObject, SceneNode> nodeMap, SceneNode parent)
    {
        foreach (var asset in sf.Assets)
        {
            if (asset.Type == "GameObject")
            {
                var gameObject = (GameObject)asset.Value;
                var node = nodeMap.GetOrAdd(gameObject, go => new GameObjectNode(go));
                
                var parentNode = parent;

                foreach (var componentPair in gameObject.m_Component)
                {
                    if (componentPair.component.TryGet(_assetManager, out var component))
                    {
                        map[componentPair.component.m_PathID].SceneNode = node;
                        switch (component)
                        {
                            case ITransform t:
                            {
                                gameObject.m_Transform = t;
                                if (t.m_Father.TryGet(_assetManager, out var m_Father))
                                {
                                    if (m_Father.m_GameObject.TryGet(_assetManager, out var parentGameObject))
                                    {
                                        parentNode = nodeMap.GetOrAdd(parentGameObject, 
                                            go => new GameObjectNode(go));
                                    }
                                }
                                break;
                            }
                            case IMeshRenderer mr:
                            {
                                gameObject.m_MeshRenderer = mr;
                                break;
                            }
                            case IMeshFilter mf:
                            {
                                gameObject.m_MeshFilter = mf;
                                if (mf.m_Mesh.TryGet(_assetManager, out _))
                                {
                                    map[mf.m_Mesh.m_PathID].SceneNode = node;
                                }
                                break;
                            }
                            case ISkinnedMeshRenderer smr:
                            {
                                gameObject.m_SkinnedMeshRenderer = smr;
                                if (smr.m_Mesh.TryGet(_assetManager, out _))
                                {
                                    map[smr.m_Mesh.m_PathID].SceneNode = node;
                                }
                                break;
                            }
                            case IAnimator animator:
                            {
                                gameObject.m_Animator = animator;
                                break;
                            }
                            case IAnimation animation:
                            {
                                gameObject.m_Animation = animation;
                                break;
                            }
                        }
                    }
                }
                
                parentNode.SubNodes.Add(node);
            }
        }
    }
    
    #endregion

    #region Options
    
    [ObservableProperty] 
    private string _defaultUnityVersion = string.Empty;

    [RelayCommand]
    private async Task SetUnityCnKey()
    {
        var viewModel = new UnityCNKeyWindowViewModel();
        var result = await viewModel.ShowDialogAsync(_window!);
    
        if (result != null)
        {
            UnityAsset.NET.Setting.DefaultUnityCNKey = result.Key;
        }
    }
    
    partial void OnDefaultUnityVersionChanged(string value)
    {
        UnityAsset.NET.Setting.DefaultUnityVerion = value;
        LogService.Info($"Default Unity Version set to {value}");
    }
    
    [RelayCommand]
    private void Setting()
    {
        
    }
    
    public IReadOnlyList<Game> AvailableGames { get; } = Enum.GetValues<Game>();

    [ObservableProperty]
    private Game _currentGame;

    partial void OnCurrentGameChanged(Game value)
    {
        Reset();
        _settings.GameType = value;
        _settingsService.SaveSettings(_settings);

        _fileSystem = CreateFileSystem(value);
        _assetManager = new AssetManager(_fileSystem);
    
        LogService.Info($"Game type set to {value}.");
    }
    
    [ObservableProperty]
    private bool _isPreviewEnabled;
    
    [ObservableProperty]
    private bool _isDumpEnabled;
    
    [RelayCommand]
    private void TogglePreview()
    {
        if (_previewTab == null) return;

        if (ViewTabs.Contains(_previewTab))
        {
            ViewTabs.Remove(_previewTab);
            IsPreviewEnabled = false;
        }
        else
        {
            ViewTabs.Add(_previewTab);
            SelectedViewTab = _previewTab;
            IsPreviewEnabled = true;
        }
        _settings.EnablePreview = IsPreviewEnabled;
        _settingsService.SaveSettings(_settings);
    }
    
    [RelayCommand]
    private void ToggleDump()
    {
        
        if (_dumpTab != null && ViewTabs.Contains(_dumpTab))
        {
            if (_dumpTab == null) return;
            ViewTabs.Remove(_dumpTab);
            _dumpViewModel?.SetText(string.Empty);
            _dumpViewModel = null;
            _dumpTab = null;
            IsDumpEnabled = false;
        }
        else
        {
            _dumpViewModel = new DumpViewModel();
            _dumpTab = new TabItemViewModel
            {
                Header = "Dump",
                Content = new DumpView { DataContext = _dumpViewModel }
            };
            ViewTabs.Add(_dumpTab);
            SelectedViewTab = _dumpTab;
            _dumpViewModel.UpdateDumpContent(SelectedAsset);
            IsDumpEnabled = true;
        }
        _settings.EnableDump = IsDumpEnabled;
        _settingsService.SaveSettings(_settings);
    }

    #endregion

    #region Debug

    public IReadOnlyList<SelectableLogLevel> AvailableLevels { get; } = Enum.GetValues<LogLevel>().Select(l => new SelectableLogLevel(l, isEnabled: l != LogLevel.Error)).ToList();

    private bool _internalUpdate;
    
    private void OnSelectableLogLevelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableLogLevel.IsSelected) || _internalUpdate)
            return;

        if (sender is SelectableLogLevel changedItem)
        {
            var min = changedItem.IsSelected ? changedItem.Level : (LogLevel)Math.Min((int)changedItem.Level + 1, (int)LogLevel.Error);
            if (_consoleLogViewModel != null)
            {
                _consoleLogViewModel.MinimumLevel = min;
            }
            _settings.ConsoleLogLevel = min;
            _settingsService.SaveSettings(_settings);
            _internalUpdate = true;
            foreach (var selectableLogLevel in AvailableLevels)
            {
                selectableLogLevel.IsSelected = selectableLogLevel.Level >= min;
            }
            _internalUpdate = false;
        }
    }
    
    [ObservableProperty]
    private bool _isConsoleVisible;
    
    [RelayCommand]
    private void ToggleConsole()
    {
        if (_consoleTab == null) return;

        if (ViewTabs.Contains(_consoleTab))
        {
            ViewTabs.Remove(_consoleTab);
            IsConsoleVisible = false;
        }
        else
        {
            ViewTabs.Add(_consoleTab);
            SelectedViewTab = _consoleTab;
            IsConsoleVisible = true;
        }
        _settings.EnableConsole = IsConsoleVisible;
        _settingsService.SaveSettings(_settings);
    }

    #endregion

    #region AssetListView

    private bool _isUpdatingAssetTypes;

    private void OnSelectableTypePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableType.IsSelected) || _isUpdatingAssetTypes)
            return;

        try
        {
            _isUpdatingAssetTypes = true;
            if (sender is SelectableType { TypeName: "All" } allType)
            {
                var selectAll = allType.IsSelected;
                foreach (var type in AssetTypes)
                    type.IsSelected = selectAll;
                _assetFilter.UpdateTypeFilter(AssetTypes.Where(t => t.IsSelected && t.TypeName != "All").Select(t => t.TypeName).ToHashSet(), selectAll);
                if (selectAll)
                    FilteredAssets.Refresh();
            }
            else
            {
                var all = AssetTypes.FirstOrDefault(t => t.TypeName == "All");
                if (all is null) return;
                if (AssetTypes.Skip(1).Any(t => !t.IsSelected))
                {
                    all.IsSelected = false;
                }
                else if (AssetTypes.Skip(1).All(t => t.IsSelected))
                {
                    all.IsSelected = true;
                }
                _assetFilter.UpdateTypeFilter(AssetTypes.Where(t => t.IsSelected && t.TypeName != "All").Select(t => t.TypeName).ToHashSet(), all.IsSelected);
                FilteredAssets.Refresh();
            }
        }
        finally
        {
            _isUpdatingAssetTypes = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _assetFilter.UpdateSearchFilter(value, UseRegex);
        FilteredAssets.Refresh();
    }

    partial void OnUseRegexChanged(bool value)
    {
        _assetFilter.UpdateSearchFilter(SearchText, value);
        FilteredAssets.Refresh();
    }

    private bool FilterAssets(object item)
    {
        return item is AssetWrapper asset && _assetFilter.Matches(asset);
    }
    
    partial void OnSelectedAssetChanged(AssetWrapper? value)
    {
        if (SelectedFileTab?.Content is not Workspace)
        {
            /*if (_prevSelectedAsset?.Size >= 0x1000000)
            {
                _prevSelectedAsset?.Release();
            }
            _prevSelectedAsset = value;*/
            if (IsDumpEnabled)
            {
                _dumpViewModel?.UpdateDumpContent(SelectedAsset);
            }
            SelectedAsset?.OnPropertyChanged(nameof(AssetWrapper.Name));
            UpdatePreviewControl(value);
        }
    }

    #endregion

    public MainWindowViewModel()
    {
        FilteredAssets = new DataGridCollectionView(Array.Empty<AssetWrapper>())
        {
            Filter = FilterAssets
        };
        
        FilteredAssetsWorkspace = new DataGridCollectionView(Array.Empty<AssetWrapper>())
        {
            Filter = FilterAssetsWorkspace
        };
        
        _settingsService = new SettingsService();
        _settings = _settingsService.LoadSettings();
        _currentGame = _settings.GameType;

        _fileSystem = CreateFileSystem(CurrentGame);
        _assetManager = new AssetManager(_fileSystem);
        _previewControl = PreviewerFactory.GetPreview(null, _assetManager);
        _loadedFiles = new ();
        
        IsPreviewEnabled = _settings.EnablePreview;
        IsDumpEnabled = _settings.EnableDump;
        IsConsoleVisible = _settings.EnableConsole;

        _assetsWorkspace = new();
        
        InitializeTabs();
        foreach (var selectableLogLevel in AvailableLevels)
            selectableLogLevel.PropertyChanged += OnSelectableLogLevelPropertyChanged;
        if (_consoleLogViewModel != null)
        {
            _consoleLogViewModel.MinimumLevel = _settings.ConsoleLogLevel;
        }
        AvailableLevels.FirstOrDefault(l => l.Level == _settings.ConsoleLogLevel)!.IsSelected = true;
    }

    private IFileSystem CreateFileSystem(Game game)
    {
        switch (game)
        {
            case Game.Normal:
                return new DirectFileSystem((_, _, errorMessage) =>
                {
                    LogService.Error(errorMessage);
                });
            case Game.GF2:
                return new GfFileSystem((_, _, errorMessage) =>
                {
                    LogService.Error(errorMessage);
                });
            case Game.PerpetualNovelty:
                return new PerpetualNoveltyFileSystem((_, _, errorMessage) =>
                {
                    LogService.Error(errorMessage);
                });
            default:
                throw new ArgumentOutOfRangeException(nameof(game), game, null);
        }
    }

    private void UpdatePreviewControl(AssetWrapper? toPreviewAsset = null)
    {
        if (!IsPreviewEnabled)
        {
            PreviewControl = PreviewerFactory.GetPreview(null, _assetManager);
            return;
        }
        PreviewControl = PreviewerFactory.GetPreview(toPreviewAsset, _assetManager);
    }
    
    private void InitializeTabs()
    {
        FileTabs.Add(new TabItemViewModel
        {
            Header = "Virtual Files",
            Content = new VirtualFilesView { DataContext = this },
        });

        FileTabs.Add(new TabItemViewModel
        {
            Header = "Scene Hierarchy",
            Content = new SceneHierarchyView { DataContext = this }
        });
        
        FileTabs.Add(new TabItemViewModel
        {
            Header = "Asset List",
            Content = new AssetListView { DataContext = this }
        });
        
        FileTabs.Add(new TabItemViewModel
        {
            Header = "Workspace",
            Content = new Workspace { DataContext = this }
        });
        
        _previewTab = new TabItemViewModel
        {
            Header = "Preview",
            Content = new PreviewView { DataContext = this }
        };
        
        _dumpViewModel = new DumpViewModel();

        _dumpTab = new TabItemViewModel
        {
            Header = "Dump",
            Content = new DumpView { DataContext = _dumpViewModel }
        };

        _consoleLogViewModel = new ConsoleLogViewModel();
        _consoleTab = new TabItemViewModel
        {
            Header = "Console",
            Content = new ConsoleLogView { DataContext = _consoleLogViewModel }
        };

        if (IsPreviewEnabled)
        {
            ViewTabs.Add(_previewTab);
        }
        if (IsDumpEnabled)
        {
            ViewTabs.Add(_dumpTab);
        }
        if (IsConsoleVisible)
        {
            ViewTabs.Add(_consoleTab);
        }

        SelectedFileTab = FileTabs.FirstOrDefault();
        SelectedViewTab = ViewTabs.FirstOrDefault();
    }
    
    [RelayCommand]
    private void About()
    {
        
    }

    [RelayCommand]
    private void ShowOriginalFile(AssetWrapper asset)
    {
        var hasSourceFile = asset.GetVirtualFile(out var file);

        if (hasSourceFile)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"/select,\"{file!.Path}\"");
            }
        }
        else
        {
            LogService.Error("No source file linked to selected asset.");
        }
    }

    [RelayCommand]
    private void AddToWorkspace(AssetWrapper asset)
    {
        _assetsWorkspace.Add(asset);
        FilteredAssetsWorkspace = new(_assetsWorkspace)
        {
            Filter = FilterAssetsWorkspace
        };
        var type = asset.Type;
        if (AssetTypesWorkspace.All(s => s.TypeName != type))
        {
            SelectableType selectableType = new(type, true);
            selectableType.PropertyChanged += OnSelectableTypeWorkspacePropertyChanged;
            AssetTypesWorkspace.Add(selectableType);
        }
    }

    partial void OnSelectedFileTabChanged(TabItemViewModel? value)
    {
        if (value?.Content is Workspace)
        {
            OnSelectedAssetWorkspaceChanged(SelectedAssetWorkspace);
        }
        else if (value?.Content is AssetListView)
        {
            OnSelectedAssetChanged(SelectedAsset);
        }
    }

    private void SubscribeToSceneNodeEvents(IEnumerable<SceneNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.PropertyChanged += SceneNode_PropertyChanged;
            if (node.IsChecked == true)
            {
                _selectedNodes.Add(node);
            }
            SubscribeToSceneNodeEvents(node.SubNodes);
        }
    }

    private void SceneNode_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SceneNode.IsChecked) && sender is SceneNode node)
        {
            if (node.IsChecked == true)
            {
                if (!_selectedNodes.Contains(node))
                {
                    _selectedNodes.Add(node);
                }
            }
            else
            {
                _selectedNodes.Remove(node);
            }
        }
    }
}