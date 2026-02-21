using System;
using System.IO;
using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;

namespace TinyStudio.Games.PerpetualNovelty;

public class PerpetualNoveltyFile : DirectFile
{
    private readonly byte _key;
    private readonly long _blocksAndDirectoryInfoOffset;
    private readonly long _blocksAndDirectoryInfoEnd;

    public PerpetualNoveltyFile(SafeFileHandle handle, long start, long length, long offset, UInt32 blocksAndDirectoryInfoLength, byte key)
        : base(handle, start, length)
    {
        _blocksAndDirectoryInfoOffset = offset;
        _blocksAndDirectoryInfoEnd = offset + Math.Min(68, blocksAndDirectoryInfoLength);
        _key = key;
    }

    public override uint Read(Span<byte> buffer, uint offset, uint count)
    {
        var ret = base.Read(buffer, offset, count);
        var start = Position - ret;
        for (var i = Math.Max(start, _blocksAndDirectoryInfoOffset); i < _blocksAndDirectoryInfoEnd && i - start < Position; i++)
        {
            buffer[(int)(offset + i - start)] ^= _key;
        }

        return ret;
    }
    
    public override IVirtualFile Clone()
    {
        var ret = new PerpetualNoveltyFile(Handle, _start, Length, _blocksAndDirectoryInfoOffset,
            (uint)(_blocksAndDirectoryInfoEnd - _blocksAndDirectoryInfoOffset), _key);
        ret.Position = Position;
        return ret;
    }
}