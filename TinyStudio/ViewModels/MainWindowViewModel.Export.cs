using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using SixLabors.ImageSharp;
using TinyStudio.Models;
using TinyStudio.Views;
using UnityAsset.NET.AssetHelper;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace TinyStudio.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task SaveImage()
    {
        var fileType = new FilePickerFileType("Image files")
        {
            Patterns = [ "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" ],
            AppleUniformTypeIdentifiers = [ "public.image" ],
            MimeTypes = [ "image/*" ]
        };
        
        var file = await _window!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Image",
            SuggestedFileName = SelectedAsset!.Name,
            DefaultExtension = "png",
            ShowOverwritePrompt = true,
            FileTypeChoices = [ fileType ]
        });

        if (file is null)
        {
            LogStatus("Image save canceled.");
            return;
        }
        try
        {
            using var image = await Task.Run(
                () => {
                    return SelectedAsset.Value switch
                    {
                        ITexture2D texture2D => _assetManager.DecodeTexture2DToImage(texture2D),
                        ISprite sprite => _assetManager.DecodeSpriteToImage(sprite),
                        _ => throw new Exception("Unsupported asset type for image saving.")
                    };
                    
                });
            await using var stream = await file.OpenWriteAsync();
            await image.SaveAsPngAsync(stream);
        
            LogStatus($"Image saved: {file.Path.LocalPath}");
        }
        catch (Exception ex)
        {
            LogStatus($"Failed to save image: {ex.Message}", LogLevel.Error);
        }
    }

    [RelayCommand]
    private async Task ExportObj()
    {
        var fileType = new FilePickerFileType("Mesh files")
        {
            Patterns = [ "*.obj" ],
            MimeTypes = [ "model/obj", "application/obj" ]
        };
        
        var file = await _window!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Obj",
            SuggestedFileName = SelectedAsset!.Name,
            DefaultExtension = "obj",
            ShowOverwritePrompt = true,
            FileTypeChoices = [ fileType ]
        });

        if (file is null)
        {
            LogStatus("Obj export canceled.");
            return;
        }
        
        try
        {
            var meshPreview = PreviewControl as MeshPreview;
            if (meshPreview == null)
                throw new Exception("Current preview is not a mesh.");
            var processedMesh = meshPreview.MeshData;
            if (processedMesh == null)
                throw new Exception("No mesh data available for export.");
            
            string fullPath = file.Path.LocalPath;
            string directoryName = Path.GetDirectoryName(fullPath)!;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
            
            MeshHelper.ExportToObj(processedMesh, directoryName, fileNameWithoutExtension);
        
            LogStatus($"obj exported: {file.Path.LocalPath}");
        }
        catch (Exception ex)
        {
            LogStatus($"Failed to export obj: {ex.Message}", LogLevel.Error);
        }
    }
}