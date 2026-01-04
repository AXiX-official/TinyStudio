using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using TinyStudio.Models;
using TinyStudio.Previewer.Mesh;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.AssetHelper;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace TinyStudio.Previewer;

public class MeshPreviewer : IPreviewer
{
    private MeshPreview? _meshPreview;
    
    public bool CanHandle(AssetWrapper asset)
    {
        return asset.Type == "Mesh";
    }

    public Control CreatePreview(AssetWrapper asset, AssetManager assetManager)
    {
        if (!CanHandle(asset))
        {
            return new TextBlock { Text = "Not a mesh." };
        }
        _meshPreview ??= new MeshPreview();
        var processedMesh = MeshHelper.GetProcessedMesh(assetManager, (IMesh)asset.Value, asset.Endianness);
        _meshPreview.MeshData = processedMesh;
        return _meshPreview;
    }

    public void CleanContext()
    {
        if (_meshPreview != null)
            _meshPreview.MeshData = null;
    }

    public static MeshData GetMeshData(MeshHelper.ProcessedMesh processedMesh)
    {
        var indices = processedMesh.m_SubMeshes.SelectMany(subMesh => subMesh.m_Indices).ToList();
        
        if (processedMesh.m_Vertices.Count == 0 || processedMesh.m_Normals.Count == 0 || indices.Count == 0)
            throw new Exception("Mesh is missing required data.");
        
        float[] vertices = processedMesh.m_Vertices.Chunk(3)
            .Zip(processedMesh.m_Normals.Chunk(3))
            .SelectMany(tuple => tuple.First.Concat(tuple.Second))
            .ToArray();
        
        var vbData = MemoryMarshal.AsBytes(vertices.ToArray().AsSpan()).ToArray();
        var ibData = MemoryMarshal.AsBytes(indices.ToArray().AsSpan()).ToArray();
        
        var layout = new VertexLayout([
            new VertexElement(VertexSemantic.Position,  VertexFormat.Float3, 0),
            new VertexElement(VertexSemantic.Normal,    VertexFormat.Float3, 3 * sizeof(float))
        ]);
        
        return new MeshData(
            layout,
            new VertexBuffer(vbData, processedMesh.m_VertexCount),
            new IndexBuffer(ibData, indices.Count),
            [ new SubMesh(0, indices.Count, 0) ]
        );
    }
}