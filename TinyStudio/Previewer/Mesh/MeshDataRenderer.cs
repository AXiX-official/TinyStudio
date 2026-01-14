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

                              void main()
                              {
                                  vec3 fixedPos = aPos;
                                  fixedPos.x = -fixedPos.x;
                                  vec3 fixedNormal = aNormal;
                                  fixedNormal.x = -fixedNormal.x;
                                  gl_Position = uProjection * uView * uModel * vec4(fixedPos, 1.0);
                                  FragNormal = mat3(transpose(inverse(uModel))) * fixedNormal;
                              }
                          """;

        const string fs = """
                              #version 300 es
                              precision mediump float;
                              
                              in vec3 FragNormal;
                              
                              out vec4 FragColor;
                              
                              uniform float uDirectionalLightDirX;
                              uniform float uDirectionalLightDirY;
                              uniform float uDirectionalLightDirZ;
                              
                              uniform float uDirectionalLightColoR;
                              uniform float uDirectionalLightColoG;
                              uniform float uDirectionalLightColoB;

                              void main()
                              {
                                  vec3 uDirectionalLightDir = vec3(uDirectionalLightDirX, uDirectionalLightDirY, uDirectionalLightDirZ);
                                  vec3 uDirectionalLightColor = vec3(uDirectionalLightColoR, uDirectionalLightColoG, uDirectionalLightColoB);
                                  
                                  vec3 normal = normalize(FragNormal);
                                  vec3 lightDirection = normalize(uDirectionalLightDir) * 0.8;
                                  
                                  float diff = max(dot(normal, lightDirection), 0.0);
                                  vec3 diffuse = diff * uDirectionalLightColor + 0.3;
                          
                                  FragColor = vec4(diffuse, 1.0);
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
    
    public void Render(PixelSize size, Vector3 cameraPos, Vector3 cameraTarget)
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
        
        var projection =
            Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 4), size.Width / (float)size.Height,
                0.01f, 1000);


        var view = Matrix4x4.CreateLookAt(cameraPos, cameraTarget, new Vector3(0, 1, 0));
        var model = Matrix4x4.Identity;
        var modelLoc = _gl.GetUniformLocationString(_shaderProgram, "uModel");
        var viewLoc = _gl.GetUniformLocationString(_shaderProgram, "uView");
        var projectionLoc = _gl.GetUniformLocationString(_shaderProgram, "uProjection");
        _gl.UniformMatrix4fv(modelLoc, 1, false, &model);
        _gl.UniformMatrix4fv(viewLoc, 1, false, &view);
        _gl.UniformMatrix4fv(projectionLoc, 1, false, &projection);
        
        // no Uniform3f binding
        var directionalLightDirXLoc = _gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightDirX");
        var directionalLightDirYLoc = _gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightDirY");
        var directionalLightDirZLoc = _gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightDirZ");
        var directionalLightColorRLoc = _gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightColoR");
        var directionalLightColorGLoc = _gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightColoG");
        var directionalLightColorBLoc = _gl.GetUniformLocationString(_shaderProgram, "uDirectionalLightColoB");
        _gl.Uniform1f(directionalLightDirXLoc, -1.0f);
        _gl.Uniform1f(directionalLightDirYLoc, -1.0f);
        _gl.Uniform1f(directionalLightDirZLoc, -0.7f);
        _gl.Uniform1f(directionalLightColorRLoc, 1.0f);
        _gl.Uniform1f(directionalLightColorGLoc, 1.0f);
        _gl.Uniform1f(directionalLightColorBLoc, 1.0f);
        
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