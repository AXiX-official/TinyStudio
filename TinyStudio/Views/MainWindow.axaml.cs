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
                        return Directory.EnumerateFiles(localPath, "*.*", SearchOption.AllDirectories);
                    }
                    return [];
                }).ToList();

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