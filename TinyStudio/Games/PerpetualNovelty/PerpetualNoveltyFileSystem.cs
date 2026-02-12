using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityAsset.NET;
using UnityAsset.NET.Enums;
using UnityAsset.NET.FileSystem;

namespace TinyStudio.Games.PerpetualNovelty;

public class PerpetualNoveltyFileSystem : IFileSystem
{
    public IFileSystem.ErrorHandler? OnError { get; set; }
    public List<IVirtualFile> LoadedFiles { get; private set; } = new();

    public PerpetualNoveltyFileSystem(IFileSystem.ErrorHandler? onError)
    {
        OnError = onError;
    }
    
    public Task<List<IVirtualFile>> LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            var files = new List<IVirtualFile>();
            var totalFiles = paths.Count;
            for (int i = 0; i < totalFiles; i++)
            {
                var path = paths[i];
                try
                {
                    var file = new PerpetualNoveltyVirtualFile(path);
                    progress?.Report(new LoadProgress($"DirectFileSystem: Loading {file.Name}", totalFiles, i));
                    if (file.FileType != FileType.Unknown)
                        files.Add(file);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(path, ex, $"Fail to load DirectFile {path}: {ex.Message}");
                }
            }

            LoadedFiles = files;
            return LoadedFiles;
        });
    }
    
    public void Clear()
    {
        LoadedFiles = new();
    }
}