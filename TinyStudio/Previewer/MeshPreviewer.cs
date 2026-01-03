using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using TinyStudio.Models;
using TinyStudio.Views;
using UnityAsset.NET;
using UnityAsset.NET.AssetHelper;
using UnityAsset.NET.Enums;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace TinyStudio.Previewer;

public class MeshPreviewer : IPreviewer
{
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
        var preview = new MeshPreview();
        var mesh = GetMeshData(assetManager, (IMesh)asset.Value, asset.Endianness);
        preview.SetMeshData(mesh);
        return preview;
    }

    public static MeshData GetMeshData(AssetManager assetManager, IMesh mesh, Endianness endianness)
    {
        var processedMesh = MeshHelper.GetProcessedMesh(assetManager, mesh, endianness, 1);
        
        if (processedMesh.m_Vertices == null || processedMesh.m_Normals == null || processedMesh.m_Indices == null)
            throw new Exception("Mesh is missing required data.");
        
        float[] vertices = processedMesh.m_Vertices.Chunk(3)
            .Zip(processedMesh.m_Normals.Chunk(3))
            .SelectMany(tuple => tuple.First.Concat(tuple.Second))
            .ToArray();
        
        var vbData = MemoryMarshal.AsBytes(vertices.ToArray().AsSpan()).ToArray();
        var ibData = MemoryMarshal.AsBytes(processedMesh.m_Indices.ToArray().AsSpan()).ToArray();
        
        var layout = new VertexLayout([
            new VertexElement(VertexSemantic.Position,  VertexFormat.Float3, 0),
            new VertexElement(VertexSemantic.Normal,    VertexFormat.Float3, 3 * sizeof(float))
        ]);
        
        return new MeshData(
            layout,
            new VertexBuffer(vbData, vertices.Length),
            new IndexBuffer(ibData, processedMesh.m_Indices.Count),
            [ new SubMesh(0, processedMesh.m_Indices.Count, 0) ]
        );
    }
}