using System;
using System.IO;

namespace TinyStudio.Games.PerpetualNovelty;

public class PerpetualNoveltyStream : Stream
{
    private readonly Stream _baseStream;
    private readonly byte _key;
    private readonly long _blocksAndDirectoryInfoOffset;
    private readonly long _blocksAndDirectoryInfoEnd;

    public PerpetualNoveltyStream(Stream baseStream, long offset, UInt32 blocksAndDirectoryInfoLength, byte key)
    {
        _baseStream = baseStream;
        _blocksAndDirectoryInfoOffset = offset;
        _blocksAndDirectoryInfoEnd = offset + Math.Min(68, blocksAndDirectoryInfoLength);
        _key = key;
    }
    
    public override int ReadByte()
    {
        return (_baseStream.Position >= _blocksAndDirectoryInfoOffset && _baseStream.Position < _blocksAndDirectoryInfoEnd) 
            ? _baseStream.ReadByte() ^ _key 
            : _baseStream.ReadByte();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var start = _baseStream.Position;
        var bytesRead = _baseStream.Read(buffer, offset, count);
        for (var i = Math.Max(start, _blocksAndDirectoryInfoOffset); i < _blocksAndDirectoryInfoEnd && i - start < bytesRead + offset; i++)
        {
            buffer[(int)(i - start) + offset] ^= _key;
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