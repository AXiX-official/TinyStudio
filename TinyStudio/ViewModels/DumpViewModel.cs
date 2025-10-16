using System;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using TinyStudio.Models;

namespace TinyStudio.ViewModels;

public partial class DumpViewModel : ObservableObject
{
    [ObservableProperty]
    private TextDocument _dumpDocument = new();

    public void SetText(string text)
    {
        DumpDocument = new TextDocument(text);
        DumpDocument.UndoStack.SizeLimit = 0;
    }
    
    public void UpdateDumpContent(AssetWrapper? asset)
    {
        if (asset == null)
        {
            DumpDocument = new("Select an asset to view its content");
            DumpDocument.UndoStack.SizeLimit = 0;
            return;
        }

        Task.Run(() => asset.ToDump)
            .ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    DumpDocument = new(task.Result);
                }
                else
                {
                    DumpDocument = new($"Error dumping asset: {task.Exception?.Message}");
                }
                DumpDocument.UndoStack.SizeLimit = 0;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        GC.Collect();
    }
}