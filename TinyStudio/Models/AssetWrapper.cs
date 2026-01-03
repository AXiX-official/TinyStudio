    using System.ComponentModel;
    using UnityAsset.NET;
    using UnityAsset.NET.Enums;
    using UnityAsset.NET.TypeTree.PreDefined;

namespace TinyStudio.Models;

public class AssetWrapper : INotifyPropertyChanged
{
    private readonly Asset m_Asset;
    
    public IUnityAsset Value => m_Asset.Value;
    
    public string Type => m_Asset.Type;

    public string Name => m_Asset.Name;

    public long Size => m_Asset.Info.ByteSize;

    public long PathId => m_Asset.Info.PathId;

    public string ToDump => m_Asset.Value.ToPlainText().ToString();
    
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
}