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
    
    public MeshData? MeshData
    {
        get => _meshData!;
        set
        {
            if (ReferenceEquals(_meshData, value))
                return;

            _meshData = value;
            
            // Frame the new mesh and drop any leftover rotation/pan/zoom from the previous one.
            ResetCamera();
            
            if (_renderer != null)
                _renderer.Mesh = value;
            
            RequestNextFrameRendering();
        }
    }
    
    private MeshDataRenderer? _renderer;

    /// <summary>The camera never rotates; the mesh is turned about its own centre instead.</summary>
    private Vector3 _cameraPos;
    private readonly (Vector3 Right, Vector3 Up, Vector3 Forward) _cameraBasis;

    /// <summary>Rotation applied to the mesh, accumulated from the drag so it can run past the poles.</summary>
    private Quaternion _modelRotation = Quaternion.Identity;

    private Vector3 _cameraTarget = Vector3.Zero;

    private float _cameraDistance = DefaultDistance;

    private Vector2 _lastPos = new(-1f, -1f);

    private const float DefaultYaw = 0.6f;
    private const float DefaultPitch = 0.35f;
    private const float DefaultDistance = 1f;
    private const float MinDistance = 0.01f;

    /// <summary>Radians of rotation per pixel of drag.</summary>
    private const float OrbitSpeed = 0.006f;

    private const float FovY = MathF.PI / 4f; // 45°

    public GlMeshView()
    {
        _cameraBasis = CreateCameraBasis(DefaultYaw, DefaultPitch);
        _cameraPos = -_cameraBasis.Forward * DefaultDistance;

        PointerPressed += MeshPreviewerControl_PointerPressed;
        PointerReleased += MeshPreviewerControl_PointerReleased;
        PointerMoved += MeshPreviewerControl_PointerMoved;
        PointerWheelChanged += MeshPreviewerControl_PointerWheelChanged;
    }

    /// <summary>
    /// Right/up/forward of a camera looking at the origin from the given yaw and pitch.
    /// The forward direction matches the shader convention of -Z being "into the screen".
    /// </summary>
    private static (Vector3 Right, Vector3 Up, Vector3 Forward) CreateCameraBasis(float yaw, float pitch)
    {
        var offset = new Vector3(
            MathF.Cos(pitch) * MathF.Sin(yaw),
            MathF.Sin(pitch),
            MathF.Cos(pitch) * MathF.Cos(yaw)
        );

        var forward = Vector3.Normalize(-offset);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        var up = Vector3.Cross(forward, right);

        return (right, up, forward);
    }

    /// <summary>Centers the mesh in view, fits it to the viewport and restores its default orientation.</summary>
    public void ResetCamera()
    {
        _modelRotation = Quaternion.Identity;

        // Camera is therefore still exactly where RecalculateCamera put it.
        _cameraTarget = _meshData?.Bounds.Center ?? Vector3.Zero;
        _cameraDistance = DefaultDistance;

        if (_meshData != null)
        {
            // Bounding sphere radius; guard against flat/degenerate meshes.
            var radius = MathF.Max(_meshData.Bounds.Radius, 1e-4f);
            _cameraDistance = FrameDistance(radius);
        }

        RecalculateCamera();
    }

    /// <summary>Distance at which a sphere of <paramref name="radius"/> fits the viewport.</summary>
    private float FrameDistance(float radius)
    {
        var aspect = Bounds.Height > 0 && Bounds.Width > 0
            ? (float)(Bounds.Width / Bounds.Height)
            : 1f;

        // Vertical FOV always applies; horizontally the effective FOV narrows on tall viewports.
        var fov = aspect >= 1f ? FovY : 2f * MathF.Atan(MathF.Tan(FovY * 0.5f) * aspect);
        var fit = radius / MathF.Sin(MathF.Max(fov, 0.05f) * 0.5f);

        return MathF.Max(fit * 1.15f, MinDistance);
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
            Orbit(dx, dy);
        }
        else if (props.IsMiddleButtonPressed)
        {
            Pan(dx, dy);
        }

        _lastPos = new Vector2((float)cur.X, (float)cur.Y);
    }

    /// <summary>
    /// Turns the mesh about the camera's own axes: horizontal drag spins it around the screen
    /// vertical, vertical drag tips it around the screen horizontal.
    /// <para>
    /// Both axes are fixed in screen space, so a drag keeps doing the same thing no matter how far
    /// the mesh has already turned - vertical never drifts into a sideways spin. Composing rotation
    /// onto the current orientation means there is no pole to stop at either.
    /// </para>
    /// </summary>
    private void Orbit(float dx, float dy)
    {
        // Horizontal drag now turns the mesh itself, so the sign is the opposite of the old
        // camera-orbit version in order to keep moving in the direction you drag.
        var yaw = Quaternion.CreateFromAxisAngle(_cameraBasis.Up, dx * OrbitSpeed);
        var pitch = Quaternion.CreateFromAxisAngle(_cameraBasis.Right, -dy * OrbitSpeed);

        _modelRotation = Quaternion.Normalize(pitch * yaw * _modelRotation);
    }
    
    private void Pan(float dx, float dy)
    {
        var worldPerPixel =
            2f * _cameraDistance * MathF.Tan(FovY * 0.5f)
            / (float)Bounds.Height;

        _cameraTarget -= _cameraBasis.Right * dx * worldPerPixel;
        _cameraTarget += _cameraBasis.Up * dy * worldPerPixel;

        RecalculateCamera();
    }

    private void MeshPreviewerControl_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _cameraDistance *= 1f - (float)e.Delta.Y * 0.1f;
        _cameraDistance = MathF.Max(_cameraDistance, MinDistance);

        RecalculateCamera();
    }

    private void RecalculateCamera()
    {
        // The camera only tracks the target (pan/zoom); its orientation stays fixed.
        _cameraPos = _cameraTarget - _cameraBasis.Forward * _cameraDistance;
    }

    /// <summary>Model matrix: spin about the mesh centre, pushed back to where the camera is looking.</summary>
    private Matrix4x4 GetModelMatrix()
    {
        var center = _meshData?.Bounds.Center ?? Vector3.Zero;

        return Matrix4x4.CreateTranslation(-center)
             * Matrix4x4.CreateFromQuaternion(_modelRotation)
             * Matrix4x4.CreateTranslation(center);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _renderer = new MeshDataRenderer(gl);
        if (_meshData != null)
            _renderer.Mesh = _meshData;
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
        _renderer?.Render(size, GetModelMatrix(), _cameraPos, _cameraTarget, FovY);
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
