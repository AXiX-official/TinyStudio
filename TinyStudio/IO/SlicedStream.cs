using System;
using System.IO;

namespace TinyStudio.IO;

public class SlicedStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _startOffset;
    private readonly long _length;
    private long _position;
    private bool _ownBaseStream;

    public SlicedStream(Stream baseStream, long startOffset, long length, bool ownBaseStream = false)
    {
        if (!baseStream.CanRead)
            throw new ArgumentException("Base stream must be readable.", nameof(baseStream));
        if (!baseStream.CanSeek)
            throw new ArgumentException("Base stream must be seekable.", nameof(baseStream));
        if (startOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        _baseStream = baseStream;
        _startOffset = startOffset;
        _length = length;
        _position = 0;
        _ownBaseStream = ownBaseStream;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Position cannot be negative.");
            if (value > _length)
                value = _length;
            _position = value;
        }
    }

    public override int ReadByte()
    {
        _baseStream.Seek(_startOffset + _position, SeekOrigin.Begin);
        _position++;
        if (Position >= _length)
            throw new EndOfStreamException();
        return _baseStream.ReadByte();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = _length - _position;
        if (remaining <= 0)
            return 0;

        if (count > remaining)
            count = (int)remaining;

        _baseStream.Seek(_startOffset + _position, SeekOrigin.Begin);
        int bytesRead = _baseStream.Read(buffer, offset, count);
        _position += bytesRead;
        if (Position >= _length)
            throw new EndOfStreamException();
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition;
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPosition = offset;
                break;
            case SeekOrigin.Current:
                newPosition = _position + offset;
                break;
            case SeekOrigin.End:
                newPosition = _length + offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin));
        }

        Position = newPosition;
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => _baseStream.Flush();

    protected override void Dispose(bool disposing)
    {
        if (disposing && _ownBaseStream)
        {
            _baseStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
