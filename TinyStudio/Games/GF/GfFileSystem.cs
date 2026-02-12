using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityAsset.NET;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace TinyStudio.Games.GF;

public class GfFileSystem : IFileSystem
{
    private static readonly byte[] OriginalHeader = [ 0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x00, 0x00, 0x00, 0x00, 0x07, 0x35, 0x2E, 0x78, 0x2E ];

    public IFileSystem.ErrorHandler? OnError { get; set; }
    public List<IVirtualFile> LoadedFiles { get; private set; } = new();

    public GfFileSystem(IFileSystem.ErrorHandler? onError)
    {
        OnError = onError;
    }

    public Task<List<IVirtualFile>> LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            var allFiles = new List<IVirtualFile>();
            var totalFiles = paths.Count;
            for (int i = 0; i < totalFiles; i++)
            {
                var path = paths[i];
                try
                {
                    if (!path.EndsWith(".bundle")) continue;

                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new CustomStreamReader(fs);

                    if (fs.Length >= 3)
                    {
                        var header = reader.ReadBytes(3);
                        ((IReader)reader).Seek(0);
                        if (header[0] == 'G' && header[1] == 'F' && header[2] == 'F') continue;
                    }

                    while (reader.Position < reader.Length)
                    {
                        var file = ParseEntry(path, reader);
                        allFiles.Add(file);
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(path, ex, $"Failed to load GF file {path}: {ex.Message}");
                }
                progress?.Report(new LoadProgress($"Processing {Path.GetFileName(path)}", totalFiles, i));
            }

            LoadedFiles = allFiles;
            return LoadedFiles;
        });
    }

    private GfVirtualFile ParseEntry(string physicalPath, IReader reader)
    {
        var entryOffset = reader.Position;
        var key = reader.ReadBytes(0x10);
        long fileSize = 0;
        bool isEncrypted = !key.SequenceEqual(OriginalHeader);

        if (!isEncrypted)
        {
            reader.Seek(entryOffset + 30);
            fileSize = BinaryPrimitives.ReadInt64BigEndian(reader.ReadBytes(8));
        }
        else
        {
            var keySpan = key.AsSpan();
            for (int i = 0; i < key.Length; i++)
            {
                keySpan[i] = (byte)(keySpan[i] ^ OriginalHeader[i]);
            }

            reader.Seek(entryOffset + 30);
            var fileSizeData = reader.ReadBytes(8);
            for (int i = 0; i < 8; i++)
            {
                fileSizeData[i] ^= key[(i + 30) % 0x10];
            }
            fileSize = BinaryPrimitives.ReadInt64BigEndian(fileSizeData);
        }

        if (entryOffset + fileSize > reader.Length)
        {
            throw new Exception($"Invalid file size({fileSize:X8}) at offset {entryOffset:X8} in {physicalPath}, exceeds file length({reader.Length:X8}).");
        }

        var virtualFileName = $"{Path.GetFileName(physicalPath)}_{entryOffset:X8}";
        var virtualFile = new GfVirtualFile(physicalPath, virtualFileName, entryOffset, fileSize, key, isEncrypted);

        reader.Seek(entryOffset + fileSize);

        return virtualFile;
    }

    public void Clear()
    {
        LoadedFiles.Clear();
    }

    public void Dispose()
    {
        Clear();
    }
}
