using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using TinyStudio.Models;
using UnityAsset.NET;

namespace TinyStudio.Previewer;

public class PreviewerFactory
{
    private readonly List<IPreviewer> _previewers;

    public PreviewerFactory()
    {
        _previewers = new List<IPreviewer>
        {
            new TexturePreviewer(),
            new TextPreviewer(),
            // Add other previewers here
            new DefaultPreviewer()
        };
    }

    public Control GetPreview(AssetWrapper? asset, AssetManager assetManager)
    {
        if (asset == null)
        {
            return new TextBlock { Text = "Select an asset to see the preview." };
        }

        var previewer = _previewers.First(p => p.CanHandle(asset));
        return previewer.CreatePreview(asset, assetManager);
    }
}
