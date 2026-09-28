using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TinyStudio.Previewer.Mesh;

public struct BoundingBox
{
    public Vector3 Min;
    public Vector3 Max;

    public static BoundingBox FromVertices(float[] vertices, int step = 3)
    {
        if (vertices.Length < step)
            return new BoundingBox { Min = Vector3.Zero, Max = Vector3.Zero };

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (var i = 0; i + step - 1 < vertices.Length; i += step)
        {
            var v = new Vector3(vertices[i], vertices[i + 1], vertices[i + 2]);
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        return new BoundingBox { Min = min, Max = max };
    }

    public Vector3 Center => (Min + Max) * 0.5f;

    public Vector3 Size => Max - Min;

    /// <summary>Radius of the bounding sphere enclosing the box.</summary>
    public float Radius => Size.Length() * 0.5f;
}

public enum VertexSemantic
{
    Position,
    Normal,
    Tangent,
    TexCoord,
    Color
}

public enum VertexFormat
{
    Float2,
    Float3,
    Float4
}

public sealed class VertexElement
{
    public VertexSemantic Semantic { get; }
    public VertexFormat Format { get; }
    public int Offset { get; }

    public int SizeInBytes => Format switch
    {
        VertexFormat.Float2 => 2 * sizeof(float),
        VertexFormat.Float3 => 3 * sizeof(float),
        VertexFormat.Float4 => 4 * sizeof(float),
        _ => throw new NotSupportedException()
    };

    public VertexElement(VertexSemantic semantic, VertexFormat format, int offset)
    {
        Semantic = semantic;
        Format = format;
        Offset = offset;
    }
}

public sealed class VertexLayout
{
    public IReadOnlyList<VertexElement> Elements { get; }
    public int Stride { get; }

    public VertexLayout(IEnumerable<VertexElement> elements)
    {
        Elements = elements.ToList();
        Stride = Elements.Sum(e => e.SizeInBytes);
    }
}

public sealed class VertexBuffer
{
    public byte[] Data { get; }
    public int VertexCount { get; }

    public VertexBuffer(byte[] data, int vertexCount)
    {
        Data = data;
        VertexCount = vertexCount;
    }
}

public sealed class IndexBuffer
{
    public byte[] Data { get; }
    public int IndexCount { get; }

    public IndexBuffer(byte[] data, int indexCount)
    {
        Data = data;
        IndexCount = indexCount;
    }
}

public sealed class SubMesh
{
    public int IndexStart { get; }
    public int IndexCount { get; }
    public int MaterialSlot { get; }

    public SubMesh(int start, int count, int materialSlot)
    {
        IndexStart = start;
        IndexCount = count;
        MaterialSlot = materialSlot;
    }
}


public sealed class MeshData
{
    public VertexLayout Layout { get; }
    public VertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }
    public IReadOnlyList<SubMesh> SubMeshes { get; }

    /// <summary>Bounds of the mesh in the coordinate space used for rendering.</summary>
    public BoundingBox Bounds { get; }

    public MeshData(
        VertexLayout layout,
        VertexBuffer vb,
        IndexBuffer ib,
        IReadOnlyList<SubMesh> subMeshes,
        BoundingBox bounds = default)
    {
        Layout = layout;
        VertexBuffer = vb;
        IndexBuffer = ib;
        SubMeshes = subMeshes;
        Bounds = bounds;
    }
}