using Avalonia.Controls;
using Avalonia.Controls.Templates;
using TinyStudio.ViewModels;
using TinyStudio.Views;

namespace TinyStudio;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        // Native AOT trims types that are only reachable through reflection, so the
        // ViewModel -> View mapping is written out instead of resolved with Type.GetType.
        return data switch
        {
            ConsoleLogViewModel viewModel => Wire(new ConsoleLogView(), viewModel),
            TextPreviewViewModel viewModel => Wire(new TextPreviewView(), viewModel),
            _ => new TextBlock { Text = "Not Found: " + data.GetType().FullName }
        };

        static Control Wire(Control control, object viewModel)
        {
            control.DataContext = viewModel;
            return control;
        }
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}