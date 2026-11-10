using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityAsset.NET;
using UnityAsset.NET.FileSystem;

namespace TinyStudio.Games.GF2;

public sealed class Gf2Transform : VirtualFileTransform
{
    private static readonly byte[] OriginalHeader =
        [0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x00, 0x00, 0x00, 0x00, 0x07, 0x35, 0x2E, 0x78, 0x2E];

    private const string ContainerExtension = ".bundle";

    /// <summary>Three bytes of the alternate format some tools ship next to the bundles; it is not split here.</summary>
    private static readonly byte[] OtherSignature = "GFF"u8.ToArray();

    /// <summary>Key (0x10), then a 30-byte head, then the size (8): everything a size check needs to be reachable.</summary>
    private const int EntryHeaderSize = 38;

    public override string Name => "GF2";

    public override bool IsInterested(string name, long length, FileType fileType)
        => fileType != FileType.Unknown || IsContainerName(name);

    public override Task<List<IVirtualFileInfo>> TransformAsync(List<IVirtualFileInfo> files,
        IProgress<LoadProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            var allFiles = new List<IVirtualFileInfo>();
            var totalFiles = files.Count;

            for (var i = 0; i < totalFiles; i++)
            {
                var physical = files[i];
                var parsed = new List<IVirtualFileInfo>();

                try
                {
                    if (!IsContainerName(physical.Name))
                    {
                        // Not a container of this game: keep it, because a later stage or the session itself may still
                        // read it as the serialized file or bundle it is.
                        allFiles.Add(physical);
                    }
                    else
                    {
                        var file = physical.GetFile();
                        if (StartsWith(file, OtherSignature))
                        {
                            // A different format that happens to share the extension: kept so that the caller can see
                            // it rather than have it silently disappear.
                            allFiles.Add(physical);
                        }
                        else
                        {
                            while (file.Position < file.Length)
                                parsed.Add(ParseEntry(physical, file));

                            allFiles.AddRange(parsed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(physical.Path, ex, $"Failed to load GF file {physical.Path}: {ex.Message}");

                    // A container that stops halfway keeps what was decoded and is itself kept only when nothing was:
                    // the failure then shows up as this file in the list instead of as a file that vanished.
                    allFiles.AddRange(parsed);
                    if (parsed.Count == 0)
                        allFiles.Add(physical);
                }

                progress?.Report(new LoadProgress($"GF2: {physical.Name}", totalFiles, i + 1));
            }

            return allFiles;
        });
    }

    private static bool IsContainerName(string name)
        => name.EndsWith(ContainerExtension, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWith(IVirtualFile file, byte[] signature)
    {
        if (file.Length < signature.Length)
            return false;

        var position = file.Position;
        var head = file.ReadBytes((uint)signature.Length);
        file.Position = position;
        return head.AsSpan().SequenceEqual(signature);
    }

    /// <summary>
    /// Reads one entry header and advances the cursor past the entry's payload. Every refusal is an exception: the caller
    /// reports it against the container and keeps whatever the entries before it already produced.
    /// </summary>
    private Gf2VirtualFileInfo ParseEntry(IVirtualFileInfo physical, IVirtualFile file)
    {
        var entryOffset = file.Position;
        if (file.Length - entryOffset < EntryHeaderSize)
            throw new Exception(
                $"Truncated entry header at offset {entryOffset:X8} in {physical.Path}, {file.Length - entryOffset} bytes left.");

        var key = file.ReadBytes(0x10);
        var isEncrypted = !key.AsSpan().SequenceEqual(OriginalHeader);
        long fileSize;

        if (!isEncrypted)
        {
            file.Position = entryOffset + 30;
            fileSize = BinaryPrimitives.ReadInt64BigEndian(file.ReadBytes(8));
        }
        else
        {
            for (var i = 0; i < key.Length; i++)
                key[i] ^= OriginalHeader[i];

            file.Position = entryOffset + 30;
            var fileSizeData = file.ReadBytes(8);
            for (var i = 0; i < 8; i++)
                fileSizeData[i] ^= key[(i + 30) % 0x10];

            fileSize = BinaryPrimitives.ReadInt64BigEndian(fileSizeData);
        }

        // A size that is not positive would leave the cursor where it is and the loop above would never end. That is the
        // failure mode a wrongly keyed key produces: the ciphertext of a size it did not encrypt.
        if (fileSize <= 0 || entryOffset + fileSize > file.Length)
        {
            throw new Exception(
                $"Invalid file size({fileSize:X8}) at offset {entryOffset:X8} in {physical.Path}, " +
                $"file length is {file.Length:X8}.");
        }

        var virtualFileName = $"{Path.GetFileName(physical.Name)}_{entryOffset:X8}";
        var virtualFile = new Gf2VirtualFileInfo(file, physical.Path, virtualFileName, entryOffset, fileSize, key,
            isEncrypted);

        file.Position = entryOffset + fileSize;

        return virtualFile;
    }
}