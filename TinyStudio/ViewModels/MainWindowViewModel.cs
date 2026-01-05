using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SixLabors.ImageSharp;
using TinyStudio.Games.GF;
using TinyStudio.Games.PerpetualNovelty;
using TinyStudio.Models;
using TinyStudio.Service;
using TinyStudio.Previewer;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.AssetHelper;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

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
    
    public ObservableCollection<SelectableType> AssetTypes { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _useRegex;
    
    [ObservableProperty]
    private AssetWrapper? _selectedAsset;
    //private AssetWrapper? _prevSelectedAsset;
    
    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isConsoleVisible;
    
    [ObservableProperty]
    private bool _isPreviewEnabled;
    
    [ObservableProperty]
    private bool _isDumpEnabled;

    #region File

    [RelayCommand]
    private async Task LoadFile()
    {
        Reset();
        if (_window == null)
        {
            StatusText = "Error: Window reference not set!";
            LogService.Error("Window reference not set!");
            return;
        }
        
        var fileTypes = new List<FilePickerFileType>
        {
            new("any file") { Patterns = [ "*" ] },
        };
        
        var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load File",
            AllowMultiple = true,
            FileTypeFilter = fileTypes
        });
        
        if (files.Any())
        {
            await LoadFilesFromPathsAsync(files.Select(f => f.Path.LocalPath));
        }
        else
        {
            StatusText = "File loading canceled.";
            LogService.Info("File loading canceled.");
        }
    }
    
    [RelayCommand]
    private async Task LoadFolder()
    {
        Reset();
        if (_window == null)
        {
            StatusText = "Error: Window reference not set!";
            LogService.Error("Window reference not set!");
            return;
        }
        
        var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
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
            StatusText = "Folder loading canceled.";
            LogService.Info("Folder loading canceled.");
        }
    }

    [RelayCommand]
    private async Task LoadFileList()
    {
        Reset();
        if (_window == null)
        {
            StatusText = "Error: Window reference not set!";
            LogService.Error("Window reference not set!");
            return;
        }
        
        var fileTypes = new List<FilePickerFileType>
        {
            new("FileList") { Patterns = [ "*.txt" ] },
        };
        
        var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load FileList",
            AllowMultiple = false,
            FileTypeFilter = fileTypes
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
            StatusText = "File loading canceled.";
            LogService.Info("File loading canceled.");
        }
    }

    public async Task LoadFilesFromPathsAsync(IEnumerable<string> paths)
    {
        Reset();
        try
        {
            var pathList = paths.ToList();
            if (!pathList.Any())
            {
                LogService.Info("No files to load.");
                StatusText = "No files to load.";
                return;
            }

            var progress = new ThrottledProgress(
                new Progress<LoadProgress>(p =>
                {
                    StatusText = p.StatusText;
                    ProgressValue = (int)p.Percentage;
                }), 
                TimeSpan.FromMilliseconds(100));
            
            LogService.Info($"Starting to load {pathList.Count} files.");
            var startTime = Stopwatch.StartNew();
            var virtualFiles = await _fileSystem.LoadAsync(pathList, progress);
            LogService.Info($"Loaded {pathList.Count} files in {startTime.Elapsed.TotalSeconds:F2} seconds.");
            
            progress.Flush();
            
            LoadedFiles.Clear();
            foreach (var virtualFile in virtualFiles)
            {
                LoadedFiles.Add(virtualFile);
            }
            
            LogService.Info($"Starting to load bundle file from {virtualFiles.Count} virtual files.");
            startTime.Restart();
            await _assetManager.LoadAsync(virtualFiles, true, progress);
            LogService.Info($"Loaded bundle files in {startTime.Elapsed.TotalSeconds:F2} seconds.");
            
            progress.Flush();
            
            StatusText = "Loading Assets...";
            LogService.Info(StatusText);
            startTime.Restart();
            ProgressValue = 50;
            var assets = await Task.Run(() =>
                _assetManager.LoadedAssets
                .Select(asset => new AssetWrapper(asset))
                .ToList());
            
            FilteredAssets = new DataGridCollectionView(new AvaloniaList<AssetWrapper>(assets))
            {
                Filter = FilterAssets
            };

            foreach (var selectableType in AssetTypes)
            {
                selectableType.PropertyChanged -= OnSelectableTypePropertyChanged;
            }
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
            StatusText = $"Loaded {assets.Count} Assets in {startTime.Elapsed.TotalSeconds:F2} seconds.";
            LogService.Info(StatusText);
           
            ProgressValue = 100;

            var unityVersion = _assetManager.Version;
            LogService.Info($"Unity version: {unityVersion}");
            var platform = _assetManager.BuildTarget;
            LogService.Info($"Build target: {platform}");
            
            if (_window != null) 
                _window.Title = $"{App.AppName} {platform} - {unityVersion}";
        }
        catch (Exception ex)
        {
            LogService.Error($"Error occurred: {ex.Message}\n{ex.StackTrace}");
            StatusText = $"Error: {ex.Message.Split('\n').FirstOrDefault()}";
            throw;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        if (_window != null)
            _window.Title = App.AppName;
        FilteredAssets = new DataGridCollectionView(Array.Empty<AssetWrapper>())
        {
            Filter = FilterAssets
        };
        foreach (var selectableType in AssetTypes)
        {
            selectableType.PropertyChanged -= OnSelectableTypePropertyChanged;
        }
        AssetTypes.Clear();
        SearchText = string.Empty;
        _assetManager.Clear();
        _fileSystem.Clear();
        UnityAsset.NET.TypeTree.TypeTreeCache.CleanCache();
        UnityAsset.NET.TypeTree.AssemblyManager.CleanCache();
        GC.Collect();
    }

    #endregion

    #region Options
    
    [ObservableProperty] 
    private string _defaultUnityVersion = string.Empty;

    [RelayCommand]
    private async Task SetUnityCNKey()
    {
        if (_window == null)
        {
            Console.WriteLine("Window ref not set！");
            return;
        }

        /*try
        {*/
        var viewModel = new UnityCNKeyWindowViewModel();
        var result = await viewModel.ShowDialogAsync(_window);
    
        if (result != null)
        {
            UnityAsset.NET.Setting.DefaultUnityCNKey = result.Key;
        }
        /*}
        catch (Exception ex)
        {
            Console.WriteLine($"Error while open UnityCNKey window: {ex.Message}");
        }*/
    }
    
    partial void OnDefaultUnityVersionChanged(string value)
    {
        UnityAsset.NET.Setting.DefaultUnityVerion = value;
        LogService.Debug($"Default Unity Version set to {value}");
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
            GC.Collect();
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

    public IReadOnlyList<LogLevel> AvailableLevels { get; } = Enum.GetValues<LogLevel>();
    
    [ObservableProperty]
    private LogLevel _selectedLogLevel;
    
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

    partial void OnSelectedLogLevelChanged(LogLevel value)
    {
        if (_consoleLogViewModel != null)
        {
            _consoleLogViewModel.MinimumLevel = value;
        }
        _settings.ConsoleLogLevel = value;
        _settingsService.SaveSettings(_settings);
    }

    #endregion
    
    private bool _isUpdatingAssetTypes; // Flag to prevent re-entrancy

    private void OnSelectableTypePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableType.IsSelected) || _isUpdatingAssetTypes)
        {
            return;
        }

        try
        {
            _isUpdatingAssetTypes = true;
            if (sender is SelectableType { TypeName: "All" } allType)
            {
                if (allType.IsSelected)
                {
                    foreach (var type in AssetTypes.Where(t => t != allType))
                        type.IsSelected = true;
                    FilteredAssets.Refresh();
                }
                else
                {
                    var allTypesSelected = AssetTypes.Skip(1).All(t => t.IsSelected);
                    if (!allTypesSelected)
                    {
                        FilteredAssets.Refresh();
                    }
                }
            }
            else
            {
                // Another type was changed
                var all = AssetTypes.FirstOrDefault(t => t.TypeName == "All");
                if (all != null)
                {
                    // If any item is unchecked, uncheck "All"
                    if (AssetTypes.Skip(1).Any(t => !t.IsSelected))
                    {
                        all.IsSelected = false;
                    }
                    // If all other items are checked, check "All"
                    else if (AssetTypes.Skip(1).All(t => t.IsSelected))
                    {
                        all.IsSelected = true;
                    }
                }
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
        FilteredAssets.Refresh();
    }

    partial void OnUseRegexChanged(bool value)
    {
        FilteredAssets.Refresh();
    }

    private bool FilterAssets(object item)
    {
        if (item is not AssetWrapper asset)
        {
            return false;
        }

        // Type filter
        var selectAll = AssetTypes.FirstOrDefault(t => t.TypeName == "All")?.IsSelected ?? true;
        if (!selectAll)
        {
            var selectedTypes = AssetTypes.Where(t => t.IsSelected && t.TypeName != "All").Select(t => t.TypeName).ToHashSet();
            if (selectedTypes.Any() && !selectedTypes.Contains(asset.Type))
            {
                return false;
            }
        }

        // Search text filter
        if (string.IsNullOrEmpty(SearchText))
        {
            return true;
        }

        try
        {
            var pattern = UseRegex ? SearchText : Regex.Escape(SearchText);
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            return regex.IsMatch(asset.Name) ||
                   regex.IsMatch(asset.Type) ||
                   regex.IsMatch(asset.PathId.ToString()) ||
                   regex.IsMatch(asset.Size.ToString());
        }
        catch (RegexParseException)
        {
            // If user enters invalid regex, treat as no match
            return false;
        }
    }

    public MainWindowViewModel()
    {
        FilteredAssets = new DataGridCollectionView(Array.Empty<AssetWrapper>())
        {
            Filter = FilterAssets
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
        
        InitializeTabs();
        if (_consoleLogViewModel != null)
        {
            _consoleLogViewModel.MinimumLevel = _settings.ConsoleLogLevel;
            SelectedLogLevel = _consoleLogViewModel.MinimumLevel;
        }
        LogService.Info("Application startup complete.");
        LogService.Debug("This is a debug message.");
        LogService.Verbose("This is a verbose message.");
    }

    private IFileSystem CreateFileSystem(Game game)
    {
        switch (game)
        {
            case Game.Normal:
                return new DirectFileSystem((filePath, ex, errorMessage) =>
                {
                    LogService.Error(errorMessage);
                });
            case Game.GF2:
                return new GfFileSystem((filePath, ex, errorMessage) =>
                {
                    LogService.Error(errorMessage);
                });
            case Game.PerpetualNovelty:
                return new PerpetualNoveltyFileSystem((filePath, ex, errorMessage) =>
                {
                    LogService.Error(errorMessage);
                });
            default:
                throw new ArgumentOutOfRangeException(nameof(game), game, null);
        }
    }
    
    

    partial void OnSelectedAssetChanged(AssetWrapper? value)
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
        UpdatePreviewControl();
    }

    private void UpdatePreviewControl()
    {
        if (!IsPreviewEnabled)
        {
            PreviewControl = PreviewerFactory.GetPreview(null, _assetManager);
            return;
        }
        PreviewControl = PreviewerFactory.GetPreview(SelectedAsset, _assetManager);
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
            Content = new TextBlock { Text = "To be impl", Margin = new Thickness(10) }
        });
        
        FileTabs.Add(new TabItemViewModel
        {
            Header = "Asset List",
            Content = new AssetListView { DataContext = this }
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
    
    public void SetWindow(Window window)
    {
        _window = window;
    }
    
    [RelayCommand]
    private void About()
    {
        
    }

    [RelayCommand]
    private async Task SaveImage()
    {
        if (_window == null)
        {
            LogService.Error("Window reference not set!");
            return;
        }
        
        var fileType = new FilePickerFileType("Image files")
        {
            Patterns = [ "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" ],
            AppleUniformTypeIdentifiers = [ "public.image" ],
            MimeTypes = [ "image/*" ]
        };
        
        var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Image",
            SuggestedFileName = SelectedAsset!.Name,
            DefaultExtension = "png",
            ShowOverwritePrompt = true,
            FileTypeChoices = [ fileType ]
        });
        
        if (file != null)
        {
            try
            {
                using var image = await Task.Run(
                    () => {
                        return SelectedAsset.Value switch
                        {
                            ITexture2D texture2D => _assetManager.DecodeTexture2DToImage(texture2D),
                            ISprite sprite => _assetManager.DecodeSpriteToImage(sprite),
                            _ => throw new Exception("Unsupported asset type for image saving.")
                        };
                        
                    });
                await using var stream = await file.OpenWriteAsync();
                await image.SaveAsPngAsync(stream);
            
                StatusText = $"Image saved successfully: {file.Name}";
                LogService.Info($"Image saved: {file.Path.LocalPath}");
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to save image: {ex.Message}");
            }
        }
        else
        {
            LogService.Info("Image save canceled.");
        }
    }

    [RelayCommand]
    private async Task ExportObj()
    {
        if (_window == null)
        {
            LogService.Error("Window reference not set!");
            return;
        }
        
        var fileType = new FilePickerFileType("Mesh files")
        {
            Patterns = [ "*.obj" ],
            MimeTypes = [ "model/obj", "application/obj" ]
        };
        
        var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Obj",
            SuggestedFileName = SelectedAsset!.Name,
            DefaultExtension = "obj",
            ShowOverwritePrompt = true,
            FileTypeChoices = [ fileType ]
        });
        
        if (file != null)
        {
            try
            {
                var meshPreview = PreviewControl as MeshPreview;
                if (meshPreview == null)
                    throw new Exception("Current preview is not a mesh.");
                var processedMesh = meshPreview.MeshData;
                if (processedMesh == null)
                    throw new Exception("No mesh data available for export.");
                
                string fullPath = file.Path.LocalPath;
                string directoryName = Path.GetDirectoryName(fullPath)!;
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
                
                MeshHelper.ExportToObj(processedMesh, directoryName, fileNameWithoutExtension);
            
                StatusText = $"obj exported successfully: {file.Name}";
                LogService.Info($"obj exported: {file.Path.LocalPath}");
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to export obj: {ex.Message}");
            }
        }
        else
        {
            LogService.Info("Obj export canceled.");
        }
    }
}