using System.Collections.ObjectModel;

namespace TinyStudio.Models;

public class SceneNode
{
    public string Name { get; set; }
    public ObservableCollection<SceneNode> SubNodes { get; }

    public SceneNode(string name)
    {
        Name = name;
        SubNodes = new();
    }
}