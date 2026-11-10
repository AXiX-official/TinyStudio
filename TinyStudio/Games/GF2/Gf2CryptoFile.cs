using System;
using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.FileSystem;

namespace TinyStudio.Games.GF2;

public class Gf2CryptoFile : IVirtualFile
{
    private readonly IVirtualFile _source;
    private readonly long _start;
    private readonly byte[] _key;
    private readonly bool _isEncrypted;
    private readonly long _encryptLength;
    private long _position;
    
    public Gf2CryptoFile(IVirtualFile source, long start, long length, byte[] key, bool isEncrypted = true)
    {
        _source = source.Clone();
        _start = start;
        Length = length;
        _key = key;
        _isEncrypted = isEncrypted;
        _encryptLength = Math.Min(length, 0x8000);
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

        _source.Position = _start + _position;
        var read = (int)_source.Read(buffer, offset, (uint)toRead);

        if (_isEncrypted)
        {
            var start = _position;
            for (var i = start; i < _encryptLength && i < _position + read; i++)
                buffer[(int)(offset + i - start)] ^= _key[(int)(i % 0x10)];
        }

        _position += read;
        return (uint)read;
    }

    public IVirtualFile Clone()
        => new Gf2CryptoFile(_source, _start, Length, _key, _isEncrypted) { Position = Position };
}
