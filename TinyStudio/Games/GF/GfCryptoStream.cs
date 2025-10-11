using System;
using System.IO;

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

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _baseStream.Length;
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush() => _baseStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
