using Avalonia.Controls;
using TinyStudio.Models;
using TinyStudio.ViewModels;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace TinyStudio.Previewer;

public class TextPreviewer : IPreviewer
{
    private TextPreviewView? _textPreview;
    
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
        
        _textPreview ??= new TextPreviewView();

        _textPreview.DataContext = new TextPreviewViewModel(textAsset);

        return _textPreview;
    }
    
    public void CleanContext()
    {
        if (_textPreview != null)
        {
            _textPreview.DataContext = null;
        }
    }
}
