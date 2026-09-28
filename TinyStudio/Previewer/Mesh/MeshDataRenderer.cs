using System;
using System.Numerics;
using Avalonia;
using Avalonia.OpenGL;
using static Avalonia.OpenGL.GlConsts;

namespace TinyStudio.Previewer.Mesh;

public sealed unsafe class MeshDataRenderer : IDisposable
{
    private const int GL_DYNAMIC_DRAW = 0x88E8;
    private readonly GlInterface _gl;
    private MeshData? _mesh;
    private bool _dirtyMark = false;

    public MeshData? Mesh
    {
        set 
        {
            if (_mesh != value)
            {
                _mesh = value;
                if (value != null)
                    _dirtyMark = true;
                    //UploadMesh(value);
            }
        }
    }
    
    private int _vao;
    private int _vbo;
    private int _ebo;
    private int _vertexShader;
    private int _fragmentShader;
    private int _shaderProgram;
    private int _modelLoc;
    private int _viewLoc;
    private int _projectionLoc;
    private int _cameraPosXLoc;
    private int _cameraPosYLoc;
    private int _cameraPosZLoc;
    
    public MeshDataRenderer(GlInterface gl, MeshData? mesh = null)
    {
        _gl = gl;
        
        InitGL();
        
        if (mesh != null)
            Mesh = mesh;
    }
    
    private void InitGL()
    {
        _gl.Enable(GL_DEPTH_TEST);
        _gl.Disable(GL_CULL_FACE);
        _gl.Disable(GL_SCISSOR_TEST);
        _gl.DepthFunc(GL_LESS);
        _gl.DepthMask(1);
        
        //Console.WriteLine($"Renderer: {_gl.GetString(GL_RENDERER)} Version: {_gl.GetString(GL_VERSION)}");

        CreateShaderProgram();
    }
    
    private void CreateShaderProgram()
    {
        const string vs = """
                              #version 300 es
                              precision mediump float;
                              
                              layout (location = 0) in vec3 aPos;
                              layout (location = 1) in vec3 aNormal;
                              
                              uniform mat4 uModel;
                              uniform mat4 uProjection;
                              uniform mat4 uView;
                              
                              out vec3 FragNormal;
                              out vec3 FragPos;

                              void main()
                              {
                                  vec4 worldPos = uModel * vec4(aPos, 1.0);
                                  gl_Position = uProjection * uView * worldPos;
                                  FragNormal = mat3(uModel) * aNormal;
                                  FragPos = worldPos.xyz;
                              }
                          """;

        const string fs = """
                              #version 300 es
                              precision mediump float;
                              
                              in vec3 FragNormal;
                              in vec3 FragPos;
                              
                              out vec4 FragColor;
                              
                              uniform float uDirectionalLightDirX;
                              uniform float uDirectionalLightDirY;
                              uniform float uDirectionalLightDirZ;
                              uniform float uSpecularStrength;
                              uniform float uShininess;
                              uniform float uCameraPosX;
                              uniform float uCameraPosY;
                              uniform float uCameraPosZ;

                              void main()
                              {
                                  vec3 lightDirection = normalize(vec3(
                                      uDirectionalLightDirX,
                                      uDirectionalLightDirY,
                                      uDirectionalLightDirZ));
                                  vec3 cameraPos = vec3(uCameraPosX, uCameraPosY, uCameraPosZ);

                                  vec3 normal = normalize(FragNormal);
                                  vec3 viewDir = normalize(cameraPos - FragPos);

                                  float diff = max(dot(normal, lightDirection), 0.0);

                                  // Half vector between light and view: this is what makes the
                                  // highlight slide across the surface as the view angle changes.
                                  vec3 halfDir = normalize(lightDirection + viewDir);
                                  float spec = pow(max(dot(normal, halfDir), 0.0), uShininess);

                                  // Keep the highlight off geometry that faces away from the light.
                                  spec *= step(0.0001, diff);

                                  vec3 ambient = vec3(0.30);
                                  vec3 diffuse = vec3(0.55) * diff;
                                  vec3 specular = vec3(uSpecularStrength) * spec;

                                  FragColor = vec4(ambient + diffuse + specular, 1.0);
                              }
                          """;

        _vertexShader = _gl.CreateShader(GL_VERTEX_SHADER);
        var err = _gl.CompileShaderAndGetError(_vertexShader, vs);
        if (!string.IsNullOrWhiteSpace(err))
            Console.WriteLine(err);
        _fragmentShader = _gl.CreateShader(GL_FRAGMENT_SHADER);
        err = _gl.CompileShaderAndGetError(_fragmentShader, fs);
        if (!string.IsNullOrWhiteSpace(err))
            Console.WriteLine(err);

        _shaderProgram  = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram , _vertexShader);
        _gl.AttachShader(_shaderProgram , _fragmentShader);

        _gl.LinkProgram(_shaderProgram);
        
        _gl.DeleteShader(_vertexShader);
        _gl.DeleteShader(_fragmentShader);

        _modelLoc = _gl.GetUniformLocationString(_shaderProgram, "uModel");
        _viewLoc = _gl.GetUniformLocationString(_shaderProgram, "uView");
        _projectionLoc = _gl.GetUniformLocationString(_shaderProgram, "uProjection");
        _cameraPosXLoc = _gl.GetUniformLocationString(_shaderProgram, "uCameraPosX");
        _cameraPosYLoc = _gl.GetUniformLocationString(_shaderProgram, "uCameraPosY");
        _cameraPosZLoc = _gl.GetUniformLocationString(_shaderProgram, "uCameraPosZ");

        // Fixed light in object space, so the shading stays attached to the mesh while it turns.
        _gl.UseProgram(_shaderProgram);
        _gl.Uniform1f(_gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightDirX"), -1.0f);
        _gl.Uniform1f(_gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightDirY"), -1.0f);
        _gl.Uniform1f(_gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightDirZ"), -0.7f);
        _gl.Uniform1f(_gl.GetUniformLocationString(_shaderProgram, "uSpecularStrength"), 0.6f);
        _gl.Uniform1f(_gl.GetUniformLocationString(_shaderProgram, "uShininess"), 32.0f);
    }
    
    private void UploadMesh(MeshData mesh)
    {
        _vbo = _gl.GenBuffer();
        fixed (byte* v = mesh.VertexBuffer.Data)
        {
            _gl.BindBuffer(GL_ARRAY_BUFFER, _vbo);
            _gl.BufferData(
                GL_ARRAY_BUFFER,
                mesh.VertexBuffer.Data.Length,
                (IntPtr)v,
                GL_STATIC_DRAW);
        }

        _ebo = _gl.GenBuffer();
        fixed (byte* i = mesh.IndexBuffer.Data)
        {
            _gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ebo);
            _gl.BufferData(
                GL_ELEMENT_ARRAY_BUFFER,
                mesh.IndexBuffer.Data.Length,
                (IntPtr)i,
                GL_STATIC_DRAW);
        }
        
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);
        var elements = mesh.Layout.Elements;
        for (int slot = 0; slot < elements.Count; slot++)
        {
            var elem = elements[slot];
            _gl.VertexAttribPointer(
                slot,
                ComponentCount(elem.Format),
                GL_FLOAT,
                0,
                mesh.Layout.Stride,
                elem.Offset);
            _gl.EnableVertexAttribArray(slot);
            CheckError(_gl);
        }
        
        _dirtyMark = false;
    }
    
    private static void CheckError(GlInterface gl)
    {
        int err;
        while ((err = gl.GetError()) != GL_NO_ERROR)
            Console.WriteLine(err);
    }

    private static int ComponentCount(VertexFormat fmt) => fmt switch
    {
        VertexFormat.Float2 => 2,
        VertexFormat.Float3 => 3,
        VertexFormat.Float4 => 4,
        _ => throw new NotSupportedException()
    };
    
    /// <summary>
    /// Draws the mesh with a fixed camera: <paramref name="model"/> carries the user's orbit
    /// rotation, so the mesh spins in place and the shading turns with it instead of the viewpoint
    /// sliding around a world-locked light.
    /// </summary>
    public void Render(PixelSize size, Matrix4x4 model, Vector3 cameraPos, Vector3 cameraTarget, float fovY = MathF.PI / 4f)
    {
        if (_dirtyMark)
            UploadMesh(_mesh!);
        
        _gl.Viewport(0, 0, size.Width, size.Height);
        
        _gl.ClearDepth(1);

        _gl.ClearColor(0.1f, 0.1f, 0.1f, 1f);
        _gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        
        _gl.BindBuffer(GL_ARRAY_BUFFER, _vbo);
        _gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ebo);
        _gl.BindVertexArray(_vao);
        
        _gl.UseProgram(_shaderProgram);
        
        CheckError(_gl);
        
        var aspect = size.Height > 0 ? size.Width / (float)size.Height : 1f;
        var distance = Vector3.Distance(cameraPos, cameraTarget);
        var near = MathF.Max(distance * 0.01f, 0.001f);
        var far = MathF.Max(distance * 100f, near * 1000f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            Math.Clamp(fovY, 0.05f, MathF.PI - 0.05f), aspect, near, far);

        var view = Matrix4x4.CreateLookAt(cameraPos, cameraTarget, Vector3.UnitY);
        _gl.UniformMatrix4fv(_modelLoc, 1, false, &model);
        _gl.UniformMatrix4fv(_viewLoc, 1, false, &view);
        _gl.UniformMatrix4fv(_projectionLoc, 1, false, &projection);

        // Needed by the specular term: the highlight depends on where the eye is, not just the light.
        _gl.Uniform1f(_cameraPosXLoc, cameraPos.X);
        _gl.Uniform1f(_cameraPosYLoc, cameraPos.Y);
        _gl.Uniform1f(_cameraPosZLoc, cameraPos.Z);
        
        CheckError(_gl);
        
        if (_mesh == null)
            return;
        
        foreach (var sm in _mesh.SubMeshes)
        {
            _gl.DrawElements(
                GL_TRIANGLES,
                sm.IndexCount,
                GL_UNSIGNED_SHORT,
                sm.IndexStart * 2);
            CheckError(_gl);
        }
    }
    
    public void Dispose()
    {
        _gl.BindBuffer(GL_ARRAY_BUFFER, 0);
        _gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0);
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
        
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_shaderProgram );
        _gl.DeleteShader(_fragmentShader);
        _gl.DeleteShader(_vertexShader);
    }
}