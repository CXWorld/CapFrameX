using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Diagnostics;

namespace CapFrameX.TestRenderer;

/// <summary>
/// Simple 3D test application for PresentMon capture testing.
/// Renders a rotating triangle at configurable frame rates.
/// </summary>
class Program
{
    private static IWindow? _window;
    private static IInputContext? _input;
    private static IKeyboard? _keyboard;
    private static GL? _gl;

    // Frame timing control
    private static double _targetFrameTime;
    private static int _targetFps = 60;
    private static readonly Stopwatch _frameTimer = new();

    // Rendering state
    private static uint _vao;
    private static uint _vbo;
    private static uint _shader;
    private static float _rotation = 0.0f;

    static void Main(string[] args)
    {
        // Parse command line arguments
        ParseArguments(args);

        _targetFrameTime = 1000.0 / _targetFps;

        Console.WriteLine($"CapFrameX Test Renderer v1.0");
        Console.WriteLine($"Target FPS: {_targetFps}");
        Console.WriteLine($"Target Frame Time: {_targetFrameTime:F2}ms");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  1-9: Set FPS (30, 60, 90, 120, 144, 165, 240, 300, Unlimited)");
        Console.WriteLine("  ESC/Q: Quit");
        Console.WriteLine();

        // Create window
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1280, 720),
            FramesPerSecond = 0, // Unlimited FPS
            Title = $"CapFrameX Test Renderer - Target: {_targetFps} FPS",
            VSync = false, // Disable VSync for manual frame rate control
            API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3))
        };

        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClose;

        _window.Run();
    }

    private static void ParseArguments(string[] args)
    {
        _targetFps = 60; // Default FPS

        if (args.Length > 0 && int.TryParse(args[0], out int fps))
        {
            _targetFps = Math.Clamp(fps, 1, 1000);
        }
    }

    private static void OnLoad()
    {
        _gl = _window!.CreateOpenGL();
        _input = _window!.CreateInput();
        _keyboard = _input.Keyboards.FirstOrDefault();

        Console.WriteLine($"OpenGL Version: {_gl.GetStringS(StringName.Version)}");
        Console.WriteLine($"Renderer: {_gl.GetStringS(StringName.Renderer)}");
        Console.WriteLine();

        // Create vertex buffer
        float[] vertices =
        [
            // Positions        // Colors
            -0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f,  // Bottom left (red)
             0.5f, -0.5f, 0.0f, 0.0f, 1.0f, 0.0f,  // Bottom right (green)
             0.0f,  0.5f, 0.0f, 0.0f, 0.0f, 1.0f,  // Top (blue)
        ];

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            fixed (float* verticesPtr = vertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)),
                    verticesPtr, BufferUsageARB.StaticDraw);
            }

            // Position attribute
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);

            // Color attribute
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        }

        // Create shader program
        _shader = CreateShaderProgram();
        _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
        _frameTimer.Start();
    }

    private static void OnUpdate(double deltaTime)
    {
        // Check for input
        var input = _keyboard;
        if (input == null) return;

        if (input.IsKeyPressed(Silk.NET.Input.Key.Escape) || input.IsKeyPressed(Silk.NET.Input.Key.Q))
        {
            _window?.Close();
        }

        // FPS presets
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number1)) SetTargetFps(30);
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number2)) SetTargetFps(60);
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number3)) SetTargetFps(90);
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number4)) SetTargetFps(120);
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number5)) SetTargetFps(144);
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number6)) SetTargetFps(165);
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number7)) SetTargetFps(240);
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number8)) SetTargetFps(300);
        if (input.IsKeyPressed(Silk.NET.Input.Key.Number9)) SetTargetFps(10000); // Unlimited

        // Update rotation
        _rotation += (float)deltaTime * 45.0f; // 45 degrees per second
        if (_rotation > 360.0f)
            _rotation -= 360.0f;
    }

    private static void OnRender(double deltaTime)
    {
        // Frame rate limiting
        double elapsed = _frameTimer.Elapsed.TotalMilliseconds;
        if (elapsed < _targetFrameTime)
        {
            // Spin-wait for better precision than Thread.Sleep
            var spinWait = new SpinWait();
            while (_frameTimer.Elapsed.TotalMilliseconds < _targetFrameTime)
            {
                spinWait.SpinOnce();
            }
        }

        _frameTimer.Restart();

        // Render
        _gl!.Clear(ClearBufferMask.ColorBufferBit);
        _gl.UseProgram(_shader);

        // Set rotation uniform
        int rotationLocation = _gl.GetUniformLocation(_shader, "rotation");
        _gl.Uniform1(rotationLocation, _rotation);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }

    private static void OnClose()
    {
        _gl?.DeleteBuffer(_vbo);
        _gl?.DeleteVertexArray(_vao);
        _gl?.DeleteProgram(_shader);
        _gl?.Dispose();
    }

    private static void SetTargetFps(int fps)
    {
        _targetFps = fps;
        _targetFrameTime = 1000.0 / fps;
        _window!.Title = $"CapFrameX Test Renderer - Target: {_targetFps} FPS";
        Console.WriteLine($"Target FPS changed to: {_targetFps}");
    }

    private static uint CreateShaderProgram()
    {
        const string vertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec3 aColor;

            out vec3 color;
            uniform float rotation;

            void main()
            {
                float rad = radians(rotation);
                mat2 rotMat = mat2(cos(rad), -sin(rad), sin(rad), cos(rad));
                vec2 rotPos = rotMat * aPosition.xy;
                gl_Position = vec4(rotPos, aPosition.z, 1.0);
                color = aColor;
            }
        ";

        const string fragmentShaderSource = @"
            #version 330 core
            in vec3 color;
            out vec4 FragColor;

            void main()
            {
                FragColor = vec4(color, 1.0);
            }
        ";

        uint vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

        uint program = _gl!.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);

        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(program);
            throw new Exception($"Shader program linking failed: {infoLog}");
        }

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        return program;
    }

    private static uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl!.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compileStatus);
        if (compileStatus == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(shader);
            throw new Exception($"Shader compilation failed ({type}): {infoLog}");
        }

        return shader;
    }
}