using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.Enums;
using UnityAsset.NET.FileSystem;

namespace TinyStudio.Games.GF2;

public class Gf2VirtualFileInfo : IVirtualFileInfo
{
    public SafeFileHandle Handle { get; }
    public string Path { get; }
    public string Name { get; }
    public long Length { get; }
    public FileType FileType => FileType.BundleFile;

    private readonly long _offset;
    private readonly byte[] _key;
    private readonly bool _isEncrypted;

    public Gf2VirtualFileInfo(SafeFileHandle handle, string physicalPath, string name, long offset, long length, byte[] key, bool isEncrypted = true)
    {
        Handle = handle;
        Path = physicalPath;
        Name = name;
        _offset = offset;
        Length = length;
        _key = key;
        _isEncrypted = isEncrypted;
    }

    public IVirtualFile GetFile() => new Gf2CryptoFile(Handle, _offset, Length, _key, _isEncrypted);
}
