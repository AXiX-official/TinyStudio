using System;
using System.IO;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace TinyStudio.Games.PerpetualNovelty;

public class PerpetualNoveltyVirtualFileInfo : IVirtualFileInfo
{
    private readonly IVirtualFile _source;
    private readonly long _offset;
    private readonly long _maskedLength;
    private readonly byte _key;
    
    public PerpetualNoveltyVirtualFileInfo(IVirtualFileInfo source, long maskedLength)
    {
        if (source.FileType != FileType.BundleFile)
            throw new ArgumentException($"Not a bundle: {source.Path}", nameof(source));

        _source = source.GetFile();
        Path = source.Path;
        Name = source.Name;
        Length = source.Length;
        FileType = source.FileType;
        _maskedLength = maskedLength;

        var reader = new CustomFileReader(source);

        // The signature is read before the header for the same reason it always was: this format keeps the header's own
        // signature, so the parse starts after it, and the check is what keeps a wrong input from being masked.
        var signature = Encoding.UTF8.GetString(reader.ReadBytes(7));
        if (signature != "UnityFS")
            throw new Exception($"Not a UnityFS bundle: {source.Path}");

        var header = Header.Parse(reader);
        BundleFile.AlignAfterHeader(reader, header);

        _offset = reader.Position;
        ((IReader)reader).Seek(1, SeekOrigin.Current);
        _key = reader.ReadByte();
    }
    
    public SafeFileHandle Handle => _source.Handle;

    public string Path { get; }

    public string Name { get; }

    public long Length { get; }

    public FileType FileType { get; }

    public IVirtualFile GetFile() => new PerpetualNoveltyFile(_source, _offset, _maskedLength, _key);
}