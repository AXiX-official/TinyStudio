using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TinyStudio.Models;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.AssetHelper;
using UnityAsset.NET.AssetHelper.TextureHelper;
using UnityAsset.NET.Enums;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace TinyStudio.Previewer;

public class TexturePreviewer : IPreviewer
{
    public bool CanHandle(AssetWrapper asset)
    {
        return asset.Value is ITexture2D;
    }

    public Control CreatePreview(AssetWrapper asset, AssetManager assetManager)
    {
        if (asset.Value is not ITexture2D texture2D)
        {
            return new TextBlock { Text = "Not a texture." };
        }

        var zoomableImageView = new ZoomableImageView();

        _ = LoadTextureAsync(texture2D, assetManager, zoomableImageView);

        return zoomableImageView;
    }

    private async Task LoadTextureAsync(ITexture2D texture2D, AssetManager assetManager, ZoomableImageView zoomableImageView)
    {
        try
        {
            var imgData = await Task.Run(() => assetManager.DecodeTexture2D(texture2D));

            using var image = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(imgData, texture2D.m_Width, texture2D.m_Height);
            image.Mutate(x => x.Flip(FlipMode.Vertical));

            var memoryStream = new MemoryStream();
            await image.SaveAsPngAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            var bitmap = new Bitmap(memoryStream);
            zoomableImageView.SetImage(bitmap, texture2D.m_Width, texture2D.m_Height,
                ((TextureFormat)texture2D.m_TextureFormat).ToString());
        }
        catch (System.Exception e)
        {
            // Handle error display in a future implementation, maybe by adding a method to ZoomableImageView
        }
    }
}
