using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TinyStudio.Games.GF;
using TinyStudio.Models;
using TinyStudio.Service;
using TinyStudio.Previewer;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;

namespace TinyStudio.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly Settings _settings;
    private IFileSystem _fileSystem;
    private AssetManager _assetManager;
    private Window? _window;
    private TabItemViewModel? _consoleTab;
    private ConsoleLogViewModel? _consoleLogViewModel;
    private readonly PreviewerFactory _previewerFactory;

    [ObservableProperty] 
    private string _defaultUnityVersion;

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
    private ObservableCollection<AssetWrapper> _loadedAssets = new();
    
    [ObservableProperty]
    private AssetWrapper? _selectedAsset;
    
    [ObservableProperty]
    private TextDocument _dumpDocument = new();
    
    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isConsoleVisible = true;

    [ObservableProperty]
    private Game _currentGame;

    public bool IsNormalGame => CurrentGame == Game.Normal;
    public bool IsGfGame => CurrentGame == Game.GF;

    partial void OnCurrentGameChanged(Game value)
    {
        OnPropertyChanged(nameof(IsNormalGame));
        OnPropertyChanged(nameof(IsGfGame));
    }

    [RelayCommand]
    private void SetGame(Game game)
    {
        if (CurrentGame == game)
        {
            return;
        }

        Reset();
        CurrentGame = game;
        _settings.GameType = game;
        _settingsService.SaveSettings(_settings);

        _fileSystem = CreateFileSystem(game);
        _assetManager = new AssetManager(_fileSystem, null);
    
        LogService.Info($"Game type set to {game}.");
    }


    public IReadOnlyList<LogLevel> AvailableLevels { get; } = Enum.GetValues<LogLevel>();
    
    public MainWindowViewModel()
    {
        _settingsService = new SettingsService();
        _settings = _settingsService.LoadSettings();
        _currentGame = _settings.GameType;

        _fileSystem = CreateFileSystem(CurrentGame);
        _assetManager = new AssetManager(_fileSystem, null);
        _previewerFactory = new PreviewerFactory();
        _previewControl = _previewerFactory.GetPreview(null, _assetManager);
        _loadedFiles = new ();
        InitializeTabs();
        PropertyChanged += OnPropertyChanged;
        LogService.Info("Application startup complete.");
        LogService.Debug("This is a debug message.");
        LogService.Verbose("This is a verbose message.");
        OnCurrentGameChanged(CurrentGame);
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
            case Game.GF:
                return new GfFileSystem((filePath, ex, errorMessage) =>
                {
                    LogService.Error(errorMessage);
                });
            default:
                throw new ArgumentOutOfRangeException(nameof(game), game, null);
        }
    }
    
    partial void OnDefaultUnityVersionChanged(string value)
    {
        UnityAsset.NET.Setting.DefaultUnityVerion = value;
        LogService.Debug($"Default Unity Version set to {value}");
    }
    
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs? e)
    {
        if (e.PropertyName == nameof(SelectedAsset))
        {
            _ = UpdateDumpContentAsync();
            UpdatePreviewControl();
        }
    }
    
    private async Task UpdateDumpContentAsync()
    {
        if (SelectedAsset == null)
        {
            DumpDocument.Text = "Select an asset to view its content";
            return;
        }
        
            
        var content = await Task.Run(() => SelectedAsset.ToDump);
            
        DumpDocument.Text = content;
    }

    private void UpdatePreviewControl()
    {
        PreviewControl = _previewerFactory.GetPreview(SelectedAsset, _assetManager);
    }
    
    public void Cleanup()
    {
        PropertyChanged -= OnPropertyChanged;
    }
    
    private void InitializeTabs()
    {
        FileTabs.Add(new TabItemViewModel
        {
            Header = "Virtual Files",
            Content = new VirtualFilesView { DataContext = this }
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
        
        ViewTabs.Add(new TabItemViewModel
        {
            Header = "Preview",
            Content = new PreviewView { DataContext = this }
        });

        ViewTabs.Add(new TabItemViewModel
        {
            Header = "Dump",
            Content = new DumpView { DataContext = this }
        });

        _consoleLogViewModel = new ConsoleLogViewModel();
        _consoleTab = new TabItemViewModel
        {
            Header = "Console",
            Content = new ConsoleLogView { DataContext = _consoleLogViewModel }
        };
        ViewTabs.Add(_consoleTab);

        SelectedFileTab = FileTabs.FirstOrDefault();
        SelectedViewTab = ViewTabs.FirstOrDefault();
    }
    
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
    }

    [RelayCommand]
    private void SetConsoleLogLevel(LogLevel level)
    {
        if (_consoleLogViewModel != null)
        {
            _consoleLogViewModel.MinimumLevel = level;
        }
    }
    
    public void SetWindow(Window window)
    {
        _window = window;
    }
    
    [RelayCommand]
    private async Task LoadFile()
    {
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
            var virtualFiles = await _fileSystem.LoadAsync(pathList, progress);
            LogService.Info($"Loaded {pathList.Count} files.");
            
            progress.Flush();
            
            LoadedFiles.Clear();
            foreach (var virtualFile in virtualFiles)
            {
                LoadedFiles.Add(virtualFile);
            }
            
            await _assetManager.LoadAsync(virtualFiles, true, progress);
            
            progress.Flush();
            
            StatusText = "Loading Assets.";
            ProgressValue = 50;
            var assets = await Task.Run(() =>
                _assetManager.LoadedAssets
                .Select(asset => new AssetWrapper(asset))
                .ToList());
            LoadedAssets = new ObservableCollection<AssetWrapper>(assets);
            StatusText = $"Loaded {assets.Count} Assets.";
            ProgressValue = 100;
        }
        catch (Exception ex)
        {
            LogService.Error($"Error occurred: {ex.Message}\n{ex.StackTrace}");
            StatusText = $"Error: {ex.Message}";
            throw;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        LoadedAssets.Clear();
        _assetManager.Clear();
        _fileSystem.Clear();
        UnityAsset.NET.TypeTreeHelper.TypeTreeCache.CleanCache();
        UnityAsset.NET.TypeTreeHelper.AssemblyManager.CleanCache();
        GC.Collect();
    }

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
    
    [RelayCommand]
    private void Setting()
    {
        
    }
    
    [RelayCommand]
    private void About()
    {
        
    }
}