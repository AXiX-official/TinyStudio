using System;
using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;

namespace TinyStudio.Games.GF2;

public class Gf2CryptoFile : DirectFile
{
    private readonly byte[] _key;
    private readonly bool _isEncrypted;
    private readonly long _encryptLength;

    public Gf2CryptoFile(SafeFileHandle handle, long start, long length, byte[] key, bool isEncrypted = true)
        : base(handle, start, length)
    {
        _key = key;
        _isEncrypted = isEncrypted;
        _encryptLength = Math.Min(length, 0x8000);
    }
    
    public override uint Read(Span<byte> buffer, uint offset, uint count)
    {
        var ret = base.Read(buffer, offset, count);
        if (_isEncrypted)
        {
            var start = Position - ret;
            for (var i = start; i < _encryptLength && i < Position; i++)
            {
                buffer[(int)(offset + i - start)] ^= _key[i % 0x10];
            }
        }

        return ret;
    }

    public override IVirtualFile Clone()
    {
        var ret = new Gf2CryptoFile(Handle, _start, Length, _key, _isEncrypted);
        ret.Position = Position;
        return ret;
    }
}
