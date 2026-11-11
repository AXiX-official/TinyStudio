using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using TinyStudio.Models;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.AssetHelper;
using UnityAsset.NET.Enums;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.Types.PreDefined.Interfaces;

namespace TinyStudio.Previewer;

public class TexturePreviewer : IPreviewer
{
    private ZoomableImageView? _zoomableImageView;
    public bool CanHandle(AssetWrapper asset)
    {
        return asset.TypeName == "Texture2D" || asset.TypeName == "Sprite";
    }

    public Control CreatePreview(AssetWrapper asset, AssetManager assetManager)
    {
        if (!CanHandle(asset))
        {
            return new TextBlock { Text = "Not a texture." };
        }

        _zoomableImageView ??= new ZoomableImageView();

        if (asset.Value is ITexture2D texture2D)
        {
            _ = asset.GetVirtualFile(out var src);
            _ = LoadTextureAsync(texture2D, src, assetManager, _zoomableImageView);
        }
        else if (asset.Value is ISprite sprite)
        {
            _ = asset.GetVirtualFile(out var src);
            _ = LoadSpriteAsync(sprite, src, assetManager, _zoomableImageView);
        }
        
        return _zoomableImageView;
    }
    
    public void CleanContext()
    {
        _zoomableImageView?.CleanContext();
    }

    private static async Task LoadTextureAsync(ITexture2D texture2D, IVirtualFileInfo? src, AssetManager assetManager, ZoomableImageView zoomableImageView)
    {
        var (pixelData, width, height) = await Task.Run(() =>
        {
            var data = assetManager.DecodeTexture2D(texture2D, src);
            FlipVertical(data, texture2D.m_Width, texture2D.m_Height, 4);
            return (data, texture2D.m_Width, texture2D.m_Height);
        });

        await LoadImageAsync(pixelData, width, height, zoomableImageView);

        zoomableImageView.SetInfo($"Width: {texture2D.m_Width}\nHeight: {texture2D.m_Height}\nFormat: {((TextureFormat)texture2D.m_TextureFormat).ToString()}");
    }
    
    private static async Task LoadSpriteAsync(ISprite sprite, IVirtualFileInfo? src, AssetManager assetManager, ZoomableImageView zoomableImageView)
    {
        using var image = await Task.Run(() => assetManager.DecodeSpriteToImage(sprite, src));
        await LoadImageAsync(image, zoomableImageView);
    }
    
    private static Task LoadImageAsync(SKBitmap image, ZoomableImageView zoomableImageView)
    {
        var writeableBitmap = new WriteableBitmap(
            new PixelSize(image.Width, image.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        // Straight from the decoded pixels into the framebuffer: encoding a PNG only to decode it again was the whole
        // cost of this path, and the buffer the decoder already produced is the thing the control draws.
        using (var framebuffer = writeableBitmap.Lock())
        {
            var pixels = image.GetPixelSpan();
            var rowBytes = Math.Min(image.RowBytes, framebuffer.RowBytes);
            for (var y = 0; y < image.Height; y++)
            {
                Marshal.Copy(pixels.Slice(y * image.RowBytes, rowBytes).ToArray(), 0,
                    IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), rowBytes);
            }
        }

        zoomableImageView.SetImage(writeableBitmap);
        return Task.CompletedTask;
    }
    
    private static Task LoadImageAsync(byte[] data, int width, int height, ZoomableImageView zoomableImageView)
    {
        if (data.Length == 0)
        {
            return Task.CompletedTask;
        }
        
        var writeableBitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            // Unity stores straight alpha; declaring it premultiplied darkened the edges of every translucent pixel.
            AlphaFormat.Unpremul);

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