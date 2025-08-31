using System;
using UnityAsset.NET;
using UnityAsset.NET.Classes;
using UnityAsset.NET.Files;

namespace TinyStudio.Models;

public class AssetWrapper
{
    private readonly Asset m_Asset;
    
    public IAsset Value => m_Asset.Value;
    
    public string Type => m_Asset.Type;

    public string Name => m_Asset.Name;

    public long Size => m_Asset.Info.ByteSize;

    public long PathId => m_Asset.Info.PathId;

    public string ToDump => m_Asset.Value.ToPlainText().ToString();

    public AssetWrapper(Asset asset)
    {
        m_Asset = asset;
    }
}