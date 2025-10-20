using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TinyStudio.ViewModels;

public class TabItemViewModel : ObservableObject
{
    public string Header { get; set; } = string.Empty;
    public object? Content { get; set; }
    public double Height { get; set; } = 14;
    public Thickness Padding { get; set; } = new Thickness(4, 4);
}