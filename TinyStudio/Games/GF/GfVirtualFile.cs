using System.IO;
using TinyStudio.IO;
using UnityAsset.NET.Enums;
using UnityAsset.NET.FileSystem;

namespace TinyStudio.Games.GF;

public class GfVirtualFile : IVirtualFile
{
    public string Path { get; }
    public string Name { get; }
    public long Length { get; }
    public FileType FileType => FileType.BundleFile;

    private readonly long _offset;
    private readonly byte[] _key;
    private readonly bool _isEncrypted;

    public GfVirtualFile(string physicalPath, string name, long offset, long length, byte[] key, bool isEncrypted = true)
    { 
        Path = physicalPath;
        Name = name;
        _offset = offset;
        Length = length;
        _key = key;
        _isEncrypted = isEncrypted;
    }

    public Stream OpenStream()
    {
        var fileStream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        var slicedStream = new SlicedStream(fileStream, _offset, Length);

        return new GfCryptoStream(slicedStream, _key, _isEncrypted);
    }
}
