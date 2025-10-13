using System;
using System.IO;
using System.Linq;
using Avalonia;
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

    private void MainWindow_Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            
            if (files == null) return;

            var allFilePaths = files.SelectMany(file =>
            {
                var uri = new Uri(file.Path.ToString());
                var localPath = uri.LocalPath;
                
                if (File.Exists(localPath))
                {
                    return new[] { localPath };
                }
                if (Directory.Exists(localPath))
                {
                    return Directory.EnumerateFiles(localPath, "*.*", SearchOption.AllDirectories);
                }
                return Enumerable.Empty<string>();
            }).ToList();

            if (allFilePaths.Any() && DataContext is MainWindowViewModel viewModel)
            {
                _ = viewModel.LoadFilesFromPathsAsync(allFilePaths);
            }
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetWindow(this);
            DataContextChanged -= OnDataContextChanged; // Unsubscribe after setting
        }
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}