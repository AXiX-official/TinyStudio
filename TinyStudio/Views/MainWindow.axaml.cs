using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using TinyStudio.ViewModels;

namespace TinyStudio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        
        AddHandler(DragDrop.DropEvent, MainWindow_Drop);
    }

    private async Task MainWindow_Drop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            // A dropped file is loaded by the same path the menu uses, so a zip or an apk expands instead of being shown
            // as one unrecognised input. A dropped folder is expanded to its files first, and the pattern is "*" rather
            // than "*.*" because a Unity bundle does not have to have an extension at all.
            var allFilePaths = e.DataTransfer.Items
                .Select(x => x.TryGetFile()?.Path)
                .Where(x => x != null)
                .Select(x => x!.LocalPath)
                .SelectMany(localPath =>
                {
                    if (File.Exists(localPath))
                    {
                        return [localPath];
                    }
                    if (Directory.Exists(localPath))
                    {
                        return Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories);
                    }
                    return [];
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (allFilePaths.Any() && DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.LoadFilesFromPathsAsync(allFilePaths);
            }
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetWindow(this);
            DataContextChanged -= OnDataContextChanged;
        }
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}