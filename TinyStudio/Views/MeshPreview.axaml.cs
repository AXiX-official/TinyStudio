using Avalonia.Controls;
using TinyStudio.Previewer;
using TinyStudio.Previewer.Mesh;
using UnityAsset.NET.AssetHelper;

namespace TinyStudio.Views;

public partial class MeshPreview : UserControl
{
    private readonly GlMeshView _meshView;
        
    public MeshPreview()
    {
        InitializeComponent();
        _meshView = this.FindControl<GlMeshView>("MeshView")!;
    }
    
    public void SetMeshData(MeshData meshData)
    {
        _meshView.MeshData = meshData;
    }
}