using UnityAsset.NET;
using UnityAsset.NET.TypeTreeHelper.PreDefined;

namespace TinyStudio.Models;

public class AssetWrapper
{
    private readonly Asset m_Asset;
    
    public IAsset Value => m_Asset.Value;
    
    public string Type => m_Asset.Type;

    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(m_Asset.Name))
            {
                return m_Asset.Name;
            }

            if (m_Asset.Value is INamedAsset namedAsset)
                return namedAsset.m_Name;
            
            return string.Empty;
        }
    }

    public long Size => m_Asset.Info.ByteSize;

    public long PathId => m_Asset.Info.PathId;

    public string ToDump => m_Asset.Value.ToPlainText().ToString();

    public AssetWrapper(Asset asset)
    {
        m_Asset = asset;
    }

    public void Release()
    {
        m_Asset.Release();
    }
}