using CommunityToolkit.Mvvm.ComponentModel;

namespace TinyStudio.Models;

public partial class SelectableLogLevel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;
    [ObservableProperty]
    private bool _isEnabled ;

    public LogLevel Level { get; }

    public SelectableLogLevel(LogLevel level, bool isSelected = false, bool isEnabled = true)
    {
        Level = level;
        _isSelected = isSelected;
        _isEnabled = isEnabled;
    }
}