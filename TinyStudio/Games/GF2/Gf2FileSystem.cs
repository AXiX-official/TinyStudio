using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityAsset.NET;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;

namespace TinyStudio.Games.GF2;

public class Gf2FileSystem : IFileSystem
{
    private static readonly byte[] OriginalHeader = [ 0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x00, 0x00, 0x00, 0x00, 0x07, 0x35, 0x2E, 0x78, 0x2E ];

    public IFileSystem.ErrorHandler? OnError { get; set; }
    public List<IVirtualFileInfo> LoadedFiles { get; private set; } = new();

    public Gf2FileSystem(IFileSystem.ErrorHandler? onError)
    {
        OnError = onError;
    }

    public Task<List<IVirtualFileInfo>> LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            var allFiles = new List<IVirtualFileInfo>();
            var totalFiles = paths.Count;
            for (int i = 0; i < totalFiles; i++)
            {
                var path = paths[i];
                try
                {
                    if (!path.EndsWith(".bundle")) continue;

                    var fileInfo = new DirectFileInfo(path);
                    var file = fileInfo.GetFile();

                    if (file.Length >= 3)
                    {
                        var header = file.ReadBytes(3);
                        file.Position = 0;
                        if (header[0] == 'G' && header[1] == 'F' && header[2] == 'F') continue;
                    }

                    while (file.Position < file.Length)
                    {
                        allFiles.Add(ParseEntry(path, file));
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

    private Gf2VirtualFileInfo ParseEntry(string physicalPath, IVirtualFile file)
    {
        var entryOffset = file.Position;
        var key = file.ReadBytes(0x10);
        long fileSize = 0;
        bool isEncrypted = !key.SequenceEqual(OriginalHeader);

        if (!isEncrypted)
        {
            file.Position = entryOffset + 30;
            fileSize = BinaryPrimitives.ReadInt64BigEndian(file.ReadBytes(8));
        }
        else
        {
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(key[i] ^ OriginalHeader[i]);
            }

            file.Position = entryOffset + 30;
            var fileSizeData = file.ReadBytes(8);
            for (int i = 0; i < 8; i++)
            {
                fileSizeData[i] ^= key[(i + 30) % 0x10];
            }
            fileSize = BinaryPrimitives.ReadInt64BigEndian(fileSizeData);
        }

        if (entryOffset + fileSize > file.Length)
        {
            throw new Exception($"Invalid file size({fileSize:X8}) at offset {entryOffset:X8} in {physicalPath}, exceeds file length({file.Length:X8}).");
        }

        var virtualFileName = $"{Path.GetFileName(physicalPath)}_{entryOffset:X8}";
        var virtualFile = new Gf2VirtualFileInfo(file.Handle, physicalPath, virtualFileName, entryOffset, fileSize, key, isEncrypted);

        file.Position = entryOffset + fileSize;

        return virtualFile;
    }

    public void Clear()
    {
        LoadedFiles = new();
    }
}
