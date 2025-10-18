using System;
using System.IO;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace TinyStudio.Games.GF;

public class GfCryptoStream : Stream
{
    private readonly Stream _baseStream;
    private readonly byte[] _key;
    private readonly bool _isEncrypted;

    public GfCryptoStream(Stream baseStream, byte[] key, bool isEncrypted = true)
    {
        _baseStream = baseStream;
        _key = key;
        _isEncrypted = isEncrypted;
    }
    
    public override int ReadByte()
    {
        return (_isEncrypted && _baseStream.Position < Math.Min(_baseStream.Length, 0x8000)) ? _baseStream.ReadByte() ^ _key[
            (_baseStream.Position - 1) % 0x10] : _baseStream.ReadByte();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var start = _baseStream.Position;
        var bytesRead = _baseStream.Read(buffer, offset, count);
        for (var i = start; i < Math.Min(_baseStream.Length, 0x8000) && i < start + bytesRead; i++)
        {
            buffer[(int)(i - start)] ^= _key[i % 0x10];
        }
        return bytesRead;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _baseStream.Length;
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _baseStream.Position + offset,
            SeekOrigin.End => _baseStream.Length + offset,
            _ => throw new ArgumentException("Invalid seek origin")
        };

        if (newPosition < 0 || newPosition >= _baseStream.Length)
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");

        _baseStream.Position = newPosition;
        return newPosition;
    }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
