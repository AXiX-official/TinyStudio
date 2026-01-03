using Avalonia.Controls;
using TinyStudio.Models;
using UnityAsset.NET;

namespace TinyStudio.Previewer;

public interface IPreviewer
{
    bool CanHandle(AssetWrapper asset);
    Control CreatePreview(AssetWrapper asset, AssetManager assetManager);
    void CleanContext() { }
}
