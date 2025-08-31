using Avalonia.Controls;
using TinyStudio.Models;
using UnityAsset.NET;

namespace TinyStudio.Previewer;

public class DefaultPreviewer : IPreviewer
{
    public bool CanHandle(AssetWrapper asset)
    {
        return true;
    }

    public Control CreatePreview(AssetWrapper asset, AssetManager assetManager)
    {
        return new TextBlock { Text = "Preview not available for this asset type." };
    }
}
