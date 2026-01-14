    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using UnityAsset.NET;
    using UnityAsset.NET.Enums;
    using UnityAsset.NET.FileSystem;
    using UnityAsset.NET.TypeTree.PreDefined;

namespace TinyStudio.Models;

public class AssetWrapper : INotifyPropertyChanged
{
    private readonly Asset m_Asset;
    private string? _pathIdStr;
    private string? _sizeStr;
    
    public IUnityAsset Value => m_Asset.Value;
    
    public string Type => m_Asset.Type;

    public string Name => m_Asset.Name;
    
    public string Container => m_Asset.Container;

    public long Size => m_Asset.Size;

    public long PathId => m_Asset.PathId;
    public string PathIdStr => _pathIdStr ??= PathId.ToString();
    public string SizeStr => _sizeStr ??= Size.ToString();

    public string ToDump => m_Asset.Value.ToPlainText();
    
    public Endianness Endianness => m_Asset.RawData.Endian;

    public AssetWrapper(Asset asset)
    {
        m_Asset = asset;
    }

    public void Release()
    {
        m_Asset.Release();
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public bool GetVirtualFile([NotNullWhen(true)] out IVirtualFile? file)
    {
        file = m_Asset.SourceFile?.SourceVirtualFile;
        return file != null;
    }
}