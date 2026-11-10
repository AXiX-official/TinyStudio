using System;
using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.FileSystem;

namespace TinyStudio.Games.PerpetualNovelty;

public class PerpetualNoveltyFile : IVirtualFile
{
    private readonly IVirtualFile _source;
    private readonly long _maskStart;
    private readonly long _maskEnd;
    private readonly byte _key;
    private long _position;

    public PerpetualNoveltyFile(IVirtualFile source, long offset, long maskedLength, byte key)
    {
        _source = source.Clone();
        Length = source.Length;
        _maskStart = offset;
        _maskEnd = Math.Min(offset + maskedLength, Length);
        _key = key;
    }

    public SafeFileHandle Handle => _source.Handle;

    public long Length { get; }

    public long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            _position = value;
        }
    }

    public uint Read(Span<byte> buffer, uint offset, uint count)
    {
        if (offset > (uint)buffer.Length || count > (uint)buffer.Length - offset)
            throw new ArgumentOutOfRangeException(
                nameof(count),
                $"Reading {count} bytes at offset {offset} does not fit in a {buffer.Length}-byte buffer.");

        var toRead = (int)Math.Min(count, (uint)Math.Max(0, Length - _position));
        if (toRead == 0)
            return 0;

        var start = _position;
        _source.Position = start;
        var read = (int)_source.Read(buffer, offset, (uint)toRead);

        for (var i = Math.Max(start, _maskStart); i < _maskEnd && i - start < read; i++)
            buffer[(int)(offset + i - start)] ^= _key;

        _position += read;
        return (uint)read;
    }

    public IVirtualFile Clone()
        => new PerpetualNoveltyFile(_source, _maskStart, _maskEnd - _maskStart, _key) { Position = Position };
}