using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace TinyStudio.Models;

public partial class SceneNode : ObservableObject
{
    [ObservableProperty]
    private string _name;
    
    public List<SceneNode> SubNodes { get; }

    [ObservableProperty]
    private bool? _isChecked = false;
    
    private bool _isUpdating;

    partial void OnIsCheckedChanged(bool? value)
    {
        if (_isUpdating) return;
        
        try
        {
            _isUpdating = true;
            if (value.HasValue)
            {
                foreach (var child in SubNodes)
                {
                    child.IsChecked = value;
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public SceneNode(string name)
    {
        _name = name;
        SubNodes = new();
    }
}

public class GameObjectNode : SceneNode
{
    public GameObject GameObject { get; }

    public GameObjectNode(GameObject gameObject) : base(gameObject.m_Name)
    {
        GameObject = gameObject;
    }
}