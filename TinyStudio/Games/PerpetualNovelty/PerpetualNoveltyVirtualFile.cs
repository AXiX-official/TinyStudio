using System;
using System.IO;
using System.Text;
using TinyStudio.Games.GF;
using TinyStudio.IO;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace TinyStudio.Games.PerpetualNovelty;

public class PerpetualNoveltyVirtualFile : IVirtualFile
{
    public string Path { get; }
    public string Name { get; }
    public FileType FileType { get; }

    private readonly long _offset;
    private readonly byte _key;
    private readonly UInt32 _blocksAndDirectoryInfoLength;

    public PerpetualNoveltyVirtualFile(string physicalPath)
    { 
        Path = physicalPath;
        Name = System.IO.Path.GetFileName(physicalPath);
        
        var reader = new FileStreamReader(Path);

        var sign = Encoding.UTF8.GetString(reader.ReadBytes(7));
        if (sign == "UnityFS")
        {
            FileType = FileType.BundleFile;
            var header = Header.Parse(reader);
            BundleFile.AlignAfterHeader(reader, header);
        
            _offset = reader.Position;
            _blocksAndDirectoryInfoLength = header.CompressedBlocksInfoSize;
            ((IReader)reader).Advance(1);
            _key = reader.ReadByte();
        }
        else
        {
            FileType = FileType.SerializedFile;
        }
    }

    public Stream OpenStream()
    {
        switch (FileType)
        {
            case FileType.BundleFile:
            {
                var fileStream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return new PerpetualNoveltyStream(fileStream, _offset, _blocksAndDirectoryInfoLength, _key);
            }
            default:
                return new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}