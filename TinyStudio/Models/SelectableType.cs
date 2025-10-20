using CommunityToolkit.Mvvm.ComponentModel;

namespace TinyStudio.Models;

public partial class SelectableType : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string TypeName { get; }

    public SelectableType(string typeName, bool isSelected = false)
    {
        TypeName = typeName;
        _isSelected = isSelected;
    }
}