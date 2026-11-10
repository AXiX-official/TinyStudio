using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityAsset.NET;
using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace TinyStudio.Games.PerpetualNovelty;

public sealed class PerpetualNoveltyTransform : VirtualFileTransform
{
    /// <summary>The lengths seen so far, with room either side for the next build to move it again.</summary>
    private const int MinMaskedLength = 56;
    private const int MaxMaskedLength = 88;

    /// <summary>Used when nothing decodes: the last length this game was known to use.</summary>
    private const int DefaultMaskedLength = 73;

    /// <summary>How many bundles a load has to decode before the length is settled; the corpus is homogeneous.</summary>
    private const int ProbeLimit = 32;

    public override string Name => "PerpetualNovelty";

    /// <summary>
    /// How many bytes this load measured the game to mask. It is per load rather than per file because the answer is a
    /// property of the build, and it is public so a caller can put it in its log next to the revision it saw.
    /// </summary>
    public int MaskedLength { get; private set; } = DefaultMaskedLength;

    public override Task<List<IVirtualFileInfo>> TransformAsync(List<IVirtualFileInfo> files,
        IProgress<LoadProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            var maskedLength = DetectMaskedLength(files);
            MaskedLength = maskedLength;
            var allFiles = new List<IVirtualFileInfo>(files.Count);
            var totalFiles = files.Count;

            for (var i = 0; i < totalFiles; i++)
            {
                var file = files[i];
                try
                {
                    // Only a bundle carries the header region this game masks; a serialized file is read as it is, and
                    // an unrecognised input is left for the filter at the end of the load to decide about.
                    allFiles.Add(file.FileType == FileType.BundleFile
                        ? new PerpetualNoveltyVirtualFileInfo(file, maskedLength)
                        : file);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(file.Path, ex, $"Failed to load PerpetualNovelty file {file.Path}: {ex.Message}");
                    allFiles.Add(file);
                }

                progress?.Report(new LoadProgress($"PerpetualNovelty: {file.Name}", totalFiles, i + 1));
            }

            return allFiles;
        });
    }
    
    private static int DetectMaskedLength(List<IVirtualFileInfo> files)
    {
        var probes = new List<BundleProbe>();
        foreach (var file in files)
        {
            if (probes.Count >= ProbeLimit)
                break;

            if (file.FileType != FileType.BundleFile)
                continue;

            try
            {
                probes.Add(BundleProbe.Read(file));
            }
            catch (Exception)
            {
                // A bundle whose own header will not parse cannot vote on the length; the stage reports it later.
            }
        }

        if (probes.Count == 0)
            return DefaultMaskedLength;

        var best = DefaultMaskedLength;
        var bestScore = 0;

        for (var length = MinMaskedLength; length <= MaxMaskedLength; length++)
        {
            var score = 0;
            foreach (var probe in probes)
            {
                if (probe.CanDecode(length))
                    score++;
            }

            // Every bundle agreeing ends the search: a length that explains the whole corpus cannot be beaten.
            if (score == probes.Count)
                return length;

            if (score > bestScore)
            {
                bestScore = score;
                best = length;
            }
        }

        return best;
    }

    /// <summary>One bundle's header facts and the ciphertext of its blocks-info, kept so the sweep does not re-read them.</summary>
    private sealed class BundleProbe
    {
        private readonly byte[] _blocksInfo;
        private readonly int _uncompressedSize;
        private readonly CompressionType _compressionType;
        private readonly byte _key;

        private BundleProbe(byte[] blocksInfo, int uncompressedSize, CompressionType compressionType, byte key)
        {
            _blocksInfo = blocksInfo;
            _uncompressedSize = uncompressedSize;
            _compressionType = compressionType;
            _key = key;
        }

        public static BundleProbe Read(IVirtualFileInfo file)
        {
            var reader = new CustomFileReader(file);

            // Same parse as the decode itself: the signature is consumed, then the header, then the header's padding.
            var signature = Encoding.UTF8.GetString(reader.ReadBytes(7));
            if (signature != "UnityFS")
                throw new Exception($"Not a UnityFS bundle: {file.Path}");

            var header = Header.Parse(reader);
            BundleFile.AlignAfterHeader(reader, header);

            var offset = reader.Position;
            ((IReader)reader).Seek(1, SeekOrigin.Current);
            var key = reader.ReadByte();

            reader.Position = offset;
            var blocksInfo = reader.ReadBytes((int)header.CompressedBlocksInfoSize);

            return new BundleProbe(blocksInfo, (int)header.UncompressedBlocksInfoSize,
                (CompressionType)(header.Flags & ArchiveFlags.CompressionTypeMask), key);
        }

        public bool CanDecode(int maskedLength)
        {
            var bytes = (byte[])_blocksInfo.Clone();
            for (var i = 0; i < Math.Min(maskedLength, bytes.Length); i++)
                bytes[i] ^= _key;

            var table = new byte[_uncompressedSize];
            try
            {
                Compression.DecompressToBytes(bytes, table, _compressionType);
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                var reader = new MemoryReader(table);
                BlocksAndDirectoryInfo.Parse(reader);

                // A table that parses but leaves bytes over is not the table this bundle describes.
                return reader.Position == table.Length;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}