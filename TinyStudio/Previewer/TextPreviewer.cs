using Avalonia.Controls;
using TinyStudio.Models;
using TinyStudio.ViewModels;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace TinyStudio.Previewer;

public class TextPreviewer : IPreviewer
{
    public bool CanHandle(AssetWrapper asset)
    {
        return asset.Value is ITextAsset;
    }

    public Control CreatePreview(AssetWrapper asset, AssetManager assetManager)
    {
        if (asset.Value is not ITextAsset textAsset)
        {
            return new TextBlock { Text = "Not a TextAsset." };
        }

        return new TextPreviewView
        {
            DataContext = new TextPreviewViewModel(textAsset)
        };
    }
}
