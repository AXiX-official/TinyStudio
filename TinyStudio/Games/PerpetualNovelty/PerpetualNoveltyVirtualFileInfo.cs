using System;
using System.IO;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace TinyStudio.Games.PerpetualNovelty;

public class PerpetualNoveltyVirtualFileInfo : IVirtualFileInfo
{
    public SafeFileHandle Handle { get; }
    public string Path { get; }
    public string Name { get; }
    public long Length { get; }
    public FileType FileType { get; }

    private readonly long _offset;
    private readonly byte _key;
    private readonly UInt32 _blocksAndDirectoryInfoLength;

    public PerpetualNoveltyVirtualFileInfo(string physicalPath)
    { 
        Path = physicalPath;
        Name = System.IO.Path.GetFileName(physicalPath);
        Length = new FileInfo(physicalPath).Length;
        Handle = File.OpenHandle(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        var reader = new CustomFileReader(this);

        var sign = Encoding.UTF8.GetString(reader.ReadBytes(7));
        if (sign == "UnityFS")
        {
            FileType = FileType.BundleFile;
            var header = Header.Parse(reader);
            BundleFile.AlignAfterHeader(reader, header);
        
            _offset = reader.Position;
            _blocksAndDirectoryInfoLength = header.CompressedBlocksInfoSize;
            ((IReader)reader).Seek(1, SeekOrigin.Current);
            _key = reader.ReadByte();
        }
        else
        {
            FileType = FileType.SerializedFile;
        }
    }

    public IVirtualFile GetFile()
    {
        return FileType switch
        {
            FileType.BundleFile => new PerpetualNoveltyFile(Handle, 0, Length, _offset, _blocksAndDirectoryInfoLength,
                _key),
            _ => new DirectFile(Handle, 0, Length),
        };
    }
}