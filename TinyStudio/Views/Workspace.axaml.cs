using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using TinyStudio.ViewModels;

namespace TinyStudio.Views;

public partial class Workspace : UserControl
{
    public Workspace()
    {
        InitializeComponent();
    }
    
    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is MainWindowViewModel viewModel && sender is TextBox searchBox)
            {
                viewModel.SearchTextWorkspace = searchBox.Text ?? string.Empty;
            }
        }
    }
}