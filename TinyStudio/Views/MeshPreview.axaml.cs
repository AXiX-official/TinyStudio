using Avalonia.Controls;
using TinyStudio.Previewer;
using TinyStudio.Previewer.Mesh;
using UnityAsset.NET.AssetHelper;

namespace TinyStudio.Views;

public partial class MeshPreview : UserControl
{
    private readonly GlMeshView _meshView;

    private MeshHelper.ProcessedMesh? _processedMesh;
    public MeshHelper.ProcessedMesh? MeshData
    {
        get => _processedMesh;
        set
        {
            _processedMesh = value;
            if (value == null)
            {
                _meshView.MeshData = null;
                return;
            }
            var meshData = MeshPreviewer.GetMeshData(_processedMesh!);
            _meshView.MeshData = meshData;
        }
    }
        
    public MeshPreview()
    {
        InitializeComponent();
        _meshView = this.FindControl<GlMeshView>("MeshView")!;
    }
}