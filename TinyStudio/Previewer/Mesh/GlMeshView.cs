using System;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using UnityAsset.NET.AssetHelper;

namespace TinyStudio.Previewer.Mesh;

public sealed class GlMeshView : OpenGlControlBase, ICustomHitTest
{
    private MeshData? _meshData;
    
    public MeshData MeshData
    {
        get => _meshData!;
        set
        {
            _meshData = value;
            if (_renderer == null)
                return;
            _renderer.Mesh = value;
        }
    }
    
    private MeshDataRenderer? _renderer;

    private Vector3 _cameraPos = Vector3.Zero;
    
    private Vector3 _cameraTarget = Vector3.Zero;
    
    private Vector2 _cameraAngles = new(0f, -1f);
    
    private float _cameraDistance = 8f;

    private Vector2 _lastPos = new(-1f, -1f);

    const float PIH_MINUS_EPSILON = (MathF.PI / 2) - 0.0001f;
    
    public GlMeshView()
    {
        PointerPressed += MeshPreviewerControl_PointerPressed;
        PointerReleased += MeshPreviewerControl_PointerReleased;
        PointerMoved += MeshPreviewerControl_PointerMoved;
        PointerWheelChanged += MeshPreviewerControl_PointerWheelChanged;
        
        RecalculateCamera();
    }
    
    private void MeshPreviewerControl_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        var curPos = e.GetPosition(this);
        
        if (properties.IsLeftButtonPressed)
        {
            _lastPos.X = (float)curPos.X;
            _lastPos.Y = (float)curPos.Y;
        }
        
        if (properties.IsMiddleButtonPressed)
        {
            _lastPos.X = (float)curPos.X;
            _lastPos.Y = (float)curPos.Y;
        }
    }

    private void MeshPreviewerControl_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            _lastPos.X = -1f;
            _lastPos.Y = -1f;
        }
        
        if (e.InitialPressMouseButton == MouseButton.Middle)
        {
            _lastPos.X = -1f;
            _lastPos.Y = -1f;
        }
    }

    private void MeshPreviewerControl_PointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var props = point.Properties;

        if (!props.IsLeftButtonPressed && !props.IsMiddleButtonPressed)
            return;

        if (_lastPos.X < 0)
            return;

        var cur = e.GetPosition(this);
        var dx = (float)(cur.X - _lastPos.X);
        var dy = (float)(cur.Y - _lastPos.Y);

        if (props.IsLeftButtonPressed)
        {
            // Orbit
            _cameraAngles.X -= dx * 0.006f;
            _cameraAngles.Y += dy * 0.006f;

            _cameraAngles.Y = MathF.Max(
                -PIH_MINUS_EPSILON,
                MathF.Min(_cameraAngles.Y, PIH_MINUS_EPSILON)
            );

        }
        else if (props.IsMiddleButtonPressed)
        {
            // Pan（见下一节）
            Pan(dx, dy);
        }

        RecalculateCamera();

        _lastPos = new Vector2((float)cur.X, (float)cur.Y);
    }
    
    private float _fovY = MathF.PI / 3f; // 60°

    private void Pan(float dx, float dy)
    {
        var forward = Vector3.Normalize(_cameraTarget - _cameraPos);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        float worldPerPixel =
            2f * _cameraDistance * MathF.Tan(_fovY * 0.5f)
            / (float)Bounds.Height;

        _cameraTarget -= right * dx * worldPerPixel;
        _cameraTarget += up * dy * worldPerPixel;
    }

    private void MeshPreviewerControl_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _cameraDistance *= 1f - (float)e.Delta.Y * 0.1f;
        _cameraDistance = MathF.Max(_cameraDistance, 0.1f);

        RecalculateCamera();
    }
    
    private void RecalculateCamera()
    {
        var yaw = _cameraAngles.X;
        var pitch = _cameraAngles.Y;

        var offset = new Vector3(
            _cameraDistance * MathF.Cos(pitch) * MathF.Sin(yaw),
            _cameraDistance * MathF.Sin(pitch),
            _cameraDistance * MathF.Cos(pitch) * MathF.Cos(yaw)
        );

        _cameraPos = _cameraTarget + offset;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        //var obj = ObjParser.Load(@"C:\Users\28797\Desktop\Mesh\R2BazhiMd019091ClothVC.obj");
        //var mesh = ObjToMeshData.Convert(obj);
        _renderer = new MeshDataRenderer(gl);
        if (_meshData != null)
            _renderer.Mesh = _meshData;
        //_renderer = new MeshDataRenderer(gl, _meshData);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
        _renderer?.Render(size, _cameraPos, _cameraTarget);
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer?.Dispose();
        _renderer = null;
    }
    
    public bool HitTest(Point point)
    {
        return true;
    }
}