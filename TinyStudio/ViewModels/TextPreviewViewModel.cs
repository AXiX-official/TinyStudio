using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace TinyStudio.ViewModels;

public enum TextPreviewMode
{
    Text,
    Hex
}

public partial class TextPreviewViewModel : ViewModelBase
{
    private readonly byte[] _scriptBytes;

    [ObservableProperty]
    private string? _textContent;

    [ObservableProperty]
    private TextPreviewMode _currentMode;
    
    [ObservableProperty]
    private string _toggleButtonText;

    public TextPreviewViewModel(ITextAsset textAsset)
    {
        _scriptBytes = Encoding.UTF8.GetBytes(textAsset.m_Script);
        _currentMode = IsReadableText(_scriptBytes) ? TextPreviewMode.Text : TextPreviewMode.Hex;
        _toggleButtonText = "";
        UpdateContent();
    }

    [RelayCommand]
    private void ToggleMode()
    {
        CurrentMode = CurrentMode == TextPreviewMode.Text ? TextPreviewMode.Hex : TextPreviewMode.Text;
        UpdateContent();
    }

    private void UpdateContent()
    {
        TextContent = CurrentMode == TextPreviewMode.Text ? Encoding.UTF8.GetString(_scriptBytes) : ToHexString(_scriptBytes);
        ToggleButtonText = CurrentMode == TextPreviewMode.Text ? "View as Hex" : "View as Text";
    }

    private static bool IsReadableText(byte[] data)
    {
        if (data.Length == 0)
        {
            return true;
        }
        // Heuristic: if more than 10% of bytes are control characters (excluding CR, LF, TAB), consider it binary.
        var controlCharCount = 0;
        foreach (var b in data)
        {
            if (char.IsControl((char)b) && b != '\r' && b != '\n' && b != '\t')
            {
                controlCharCount++;
            }
        }
        return (double)controlCharCount / data.Length < 0.1;
    }

    private static string ToHexString(byte[] data)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < data.Length; i += 16)
        {
            sb.AppendFormat("{0:x8}: ", i); 
            for (var j = 0; j < 16; j++)
            {
                if (i + j < data.Length)
                {
                    sb.AppendFormat("{0:x2} ", data[i + j]);
                }
                else
                {
                    sb.Append("   ");
                }
            }
            sb.Append(" ");
            for (var j = 0; j < 16; j++)
            {
                if (i + j < data.Length)
                {
                    var c = (char)data[i + j];
                    sb.Append(char.IsControl(c) ? "." : c);
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
