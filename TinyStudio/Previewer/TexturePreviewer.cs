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
        return asset.Type == "Texture2D" || asset.Type == "Sprite";
    }

    public Control CreatePreview(AssetWrapper asset, AssetManager assetManager)
    {
        if (!CanHandle(asset))
        {
            return new TextBlock { Text = "Not a texture." };
        }

        var zoomableImageView = new ZoomableImageView();

        if (asset.Value is ITexture2D texture2D)
            _ = LoadTextureAsync(texture2D, assetManager, zoomableImageView);
        else if (asset.Value is ISprite sprite)
            _ = LoadSpriteAsync(sprite, assetManager, zoomableImageView);

        return zoomableImageView;
    }

    private async Task LoadTextureAsync(ITexture2D texture2D, AssetManager assetManager, ZoomableImageView zoomableImageView)
    {
        using var image = await Task.Run(() => assetManager.DecodeTexture2DToImage(texture2D));

        await LoadImageAsync(image, zoomableImageView);

        zoomableImageView.SetInfo($"Width: {texture2D.m_Width}\nHeight: {texture2D.m_Height}\nFormat: {((TextureFormat)texture2D.m_TextureFormat).ToString()}");
    }
    
    private async Task LoadSpriteAsync(ISprite sprite, AssetManager assetManager, ZoomableImageView zoomableImageView)
    {
        using var image = await Task.Run(() => assetManager.DecodeSpriteToImage(sprite));

        await LoadImageAsync(image, zoomableImageView);
    }
    
    private async Task LoadImageAsync(Image<Bgra32> image, ZoomableImageView zoomableImageView)
    {
        var memoryStream = new MemoryStream();
        await image.SaveAsPngAsync(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var bitmap = new Bitmap(memoryStream);
        zoomableImageView.SetImage(bitmap);
    }
}
