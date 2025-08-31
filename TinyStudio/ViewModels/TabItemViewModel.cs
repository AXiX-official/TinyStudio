using CommunityToolkit.Mvvm.ComponentModel;

namespace TinyStudio.ViewModels;

public class TabItemViewModel : ObservableObject
{
    public string Header { get; set; } = string.Empty;
    public object? Content { get; set; }
}