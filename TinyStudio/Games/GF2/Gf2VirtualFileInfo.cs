using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.FileSystem;

namespace TinyStudio.Games.GF2;

public class Gf2VirtualFileInfo : IVirtualFileInfo
{
    private readonly IVirtualFile _container;
    private readonly long _offset;
    private readonly byte[] _key;
    private readonly bool _isEncrypted;

    public Gf2VirtualFileInfo(IVirtualFile container, string path, string name, long offset, long length, byte[] key,
        bool isEncrypted = true)
    {
        _container = container;
        Path = path;
        Name = name;
        _offset = offset;
        Length = length;
        _key = key;
        _isEncrypted = isEncrypted;
    }


    public SafeFileHandle Handle => _container.Handle;

    public string Path { get; }

    public string Name { get; }

    public long Length { get; }

    public FileType FileType => FileType.BundleFile;

    public IVirtualFile GetFile() => new Gf2CryptoFile(_container, _offset, Length, _key, _isEncrypted);
}
