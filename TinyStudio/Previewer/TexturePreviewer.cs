using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TinyStudio.Models;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.AssetHelper;
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
        var (pixelData, width, height) = await Task.Run(() =>
        {
            var data = assetManager.DecodeTexture2D(texture2D);
            FlipVertical(data, texture2D.m_Width, texture2D.m_Height, 4);
            return (data, texture2D.m_Width, texture2D.m_Height);
        });

        await LoadImageAsync(pixelData, width, height, zoomableImageView);

        zoomableImageView.SetInfo($"Width: {texture2D.m_Width}\nHeight: {texture2D.m_Height}\nFormat: {((TextureFormat)texture2D.m_TextureFormat).ToString()}");
    }
    
    private async Task LoadSpriteAsync(ISprite sprite, AssetManager assetManager, ZoomableImageView zoomableImageView)
    {
        // TODO: Optimize this path to also use WriteableBitmap
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
    
    private Task LoadImageAsync(byte[] data, int width, int height, ZoomableImageView zoomableImageView)
    {
        if (data.Length == 0)
        {
            return Task.CompletedTask;
        }
        
        var writeableBitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var framebuffer = writeableBitmap.Lock())
        {
            Marshal.Copy(data, 0, framebuffer.Address, data.Length);
        }
        
        zoomableImageView.SetImage(writeableBitmap);
        return Task.CompletedTask;
    }
    
    private static void FlipVertical(byte[] data, int width, int height, int bpp)
    {
        if (data.Length == 0)
        {
            return;
        }
        var rowStride = width * bpp;
        var rowBuffer = new byte[rowStride];
        for (var y = 0; y < height / 2; y++)
        {
            var topRowOffset = y * rowStride;
            var bottomRowOffset = (height - y - 1) * rowStride;
            
            data.AsSpan(topRowOffset, rowStride).CopyTo(rowBuffer.AsSpan(0, rowStride));
            data.AsSpan(bottomRowOffset, rowStride).CopyTo(data.AsSpan(topRowOffset, rowStride));
            rowBuffer.AsSpan(0, rowStride).CopyTo(data.AsSpan(bottomRowOffset, rowStride));
        }
    }
}