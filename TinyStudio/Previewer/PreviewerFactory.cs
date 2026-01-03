using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using TinyStudio.Models;
using UnityAsset.NET;

namespace TinyStudio.Previewer;

public static class PreviewerFactory
{
    private static readonly List<IPreviewer> Previewers = 
    [
        new TexturePreviewer(),
        new TextPreviewer(),
        new MeshPreviewer(),
        // Add other previewers here
        new DefaultPreviewer()
    ];

    public static Control GetPreview(AssetWrapper? asset, AssetManager assetManager)
    {
        if (asset == null)
        {
            return new TextBlock { Text = "Select an asset to see the preview." };
        }

        var previewer = Previewers.First(p => p.CanHandle(asset));
        foreach (var preview in Previewers)
        {
            if (preview != previewer)
            {
                preview.CleanContext();
            }
        }
        return previewer.CreatePreview(asset, assetManager);
    }
}
