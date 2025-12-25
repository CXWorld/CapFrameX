using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D12.D3D12;
using static Vortice.DXGI.DXGI;

namespace CapFrameX.TestRenderer;

/// <summary>
/// Simple DirectX 12 test application for PresentMon capture testing.
/// Renders a rotating triangle at configurable frame rates.
/// </summary>
class Program
{
    private const int FrameCount = 2;
    private const int Width = 1280;
    private const int Height = 720;

    // Pipeline objects
    private static IDXGIFactory4? _factory;
    private static ID3D12Device? _device;
    private static ID3D12CommandQueue? _commandQueue;
    private static IDXGISwapChain3? _swapChain;
    private static ID3D12DescriptorHeap? _rtvHeap;
    private static int _rtvDescriptorSize;
    private static ID3D12Resource[]? _renderTargets;
    private static ID3D12CommandAllocator[]? _commandAllocators;
    private static ID3D12GraphicsCommandList? _commandList;
    private static ID3D12RootSignature? _rootSignature;
    private static ID3D12PipelineState? _pipelineState;

    // Vertex buffer
    private static ID3D12Resource? _vertexBuffer;
    private static VertexBufferView _vertexBufferView;

    // Synchronization objects
    private static ID3D12Fence? _fence;
    private static ulong[]? _fenceValues;
    private static AutoResetEvent? _fenceEvent;
    private static int _frameIndex;

    // Frame timing control
    private static double _targetFrameTime;
    private static int _targetFps = 60;
    private static readonly Stopwatch _frameTimer = new();
    private static readonly Stopwatch _fpsTimer = new();
    private static int _frameCount;
    private static double _currentFps;

    // Rendering state
    private static float _rotation = 0.0f;
    private static DateTime _lastTime = DateTime.Now;

    // Win32 interop
    private static IntPtr _hwnd;
    private static bool _running = true;

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector4 Color;

        public Vertex(Vector3 position, Vector4 color)
        {
            Position = position;
            Color = color;
        }
    }

    static void Main(string[] args)
    {
        ParseArguments(args);

        _targetFrameTime = 1000.0 / _targetFps;

        Console.WriteLine($"CapFrameX Test Renderer v1.0 (DirectX 12)");
        Console.WriteLine($"Target FPS: {_targetFps}");
        Console.WriteLine($"Target Frame Time: {_targetFrameTime:F2}ms");
        Console.WriteLine();
        Console.WriteLine("Commands (press in console window):");
        Console.WriteLine("  1-9: Set FPS (30, 60, 90, 120, 144, 165, 240, 300, Unlimited)");
        Console.WriteLine("  Q: Quit");
        Console.WriteLine();

        // Create window
        CreateWindow();

        // Initialize DirectX 12
        InitializeD3D12();

        // Start timers
        _frameTimer.Start();
        _fpsTimer.Start();

        // Start input thread
        var inputThread = new Thread(InputThread) { IsBackground = true };
        inputThread.Start();

        // Message loop
        while (_running)
        {
            if (ProcessMessages())
            {
                Update();
                Render();
            }
        }

        // Cleanup
        WaitForGpu();
        Cleanup();
    }

    private static void ParseArguments(string[] args)
    {
        // default to 60 FPS
        _targetFps = 60;

        if (args.Length > 0 && int.TryParse(args[0], out int fps))
        {
            _targetFps = Math.Clamp(fps, 1, 10000);
        }
    }

    #region Win32 Window Creation

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static WndProcDelegate? _wndProcDelegate;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle, IntPtr lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public System.Drawing.Point pt;
    }

    private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint CS_HREDRAW = 0x0002;
    private const uint CS_VREDRAW = 0x0001;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint PM_REMOVE = 0x0001;
    private static readonly IntPtr IDC_ARROW = new(32512);

    private static void CreateWindow()
    {
        var hInstance = GetModuleHandleW(null);
        _wndProcDelegate = WndProc;

        var className = "CapFrameXTestRenderer";
        var classNamePtr = Marshal.StringToHGlobalUni(className);

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = CS_HREDRAW | CS_VREDRAW,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = hInstance,
            hCursor = LoadCursorW(IntPtr.Zero, IDC_ARROW),
            lpszClassName = classNamePtr
        };

        RegisterClassExW(ref wc);

        _hwnd = CreateWindowExW(
            0,
            classNamePtr,
            $"CapFrameX Test Renderer (DX12) - Target: {_targetFps} FPS",
            WS_OVERLAPPEDWINDOW | WS_VISIBLE,
            100, 100, Width, Height,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new Exception("Failed to create window");
        }

        ShowWindow(_hwnd, 1);
        UpdateWindow(_hwnd);

        Marshal.FreeHGlobal(classNamePtr);
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_DESTROY:
                _running = false;
                PostQuitMessage(0);
                return IntPtr.Zero;

            case WM_KEYDOWN:
                HandleKeyDown((int)wParam);
                return IntPtr.Zero;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static void HandleKeyDown(int vkCode)
    {
        switch (vkCode)
        {
            case 0x1B: // Escape
            case 0x51: // Q
                _running = false;
                break;
            case 0x31: SetTargetFps(30); break;   // 1
            case 0x32: SetTargetFps(60); break;   // 2
            case 0x33: SetTargetFps(90); break;   // 3
            case 0x34: SetTargetFps(120); break;  // 4
            case 0x35: SetTargetFps(144); break;  // 5
            case 0x36: SetTargetFps(165); break;  // 6
            case 0x37: SetTargetFps(240); break;  // 7
            case 0x38: SetTargetFps(300); break;  // 8
            case 0x39: SetTargetFps(10000); break; // 9 - Unlimited
        }
    }

    private static bool ProcessMessages()
    {
        while (PeekMessageW(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE))
        {
            if (msg.message == 0x0012) // WM_QUIT
            {
                _running = false;
                return false;
            }
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        return _running;
    }

    #endregion

    #region DirectX 12 Initialization

    private static void InitializeD3D12()
    {
#if DEBUG
        // Enable debug layer
        if (D3D12GetDebugInterface(out ID3D12Debug? debugInterface).Success)
        {
            debugInterface!.EnableDebugLayer();
            debugInterface.Dispose();
        }
#endif

        // Create DXGI factory
        CreateDXGIFactory2(false, out _factory).CheckError();

        // Create device
        D3D12CreateDevice(null, FeatureLevel.Level_11_0, out _device).CheckError();

        // Create command queue
        var queueDesc = new CommandQueueDescription(CommandListType.Direct);
        _device!.CreateCommandQueue(queueDesc, out _commandQueue).CheckError();

        // Create swap chain
        var swapChainDesc = new SwapChainDescription1
        {
            Width = Width,
            Height = Height,
            Format = Format.R8G8B8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = FrameCount,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Unspecified,
            Flags = SwapChainFlags.AllowTearing
        };

        using var tempSwapChain = _factory!.CreateSwapChainForHwnd(_commandQueue, _hwnd, swapChainDesc);
        _swapChain = tempSwapChain.QueryInterface<IDXGISwapChain3>();

        // Disable Alt+Enter fullscreen
        _factory.MakeWindowAssociation(_hwnd, WindowAssociationFlags.IgnoreAltEnter);

        _frameIndex = (int)_swapChain.CurrentBackBufferIndex;

        // Create RTV descriptor heap
        var rtvHeapDesc = new DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, FrameCount);
        _device.CreateDescriptorHeap(rtvHeapDesc, out _rtvHeap).CheckError();
        _rtvDescriptorSize = (int)_device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);

        // Create render targets
        _renderTargets = new ID3D12Resource[FrameCount];
        var rtvHandle = _rtvHeap!.GetCPUDescriptorHandleForHeapStart();

        for (int i = 0; i < FrameCount; i++)
        {
            _swapChain.GetBuffer((uint)i, out _renderTargets[i]).CheckError();
            _device.CreateRenderTargetView(_renderTargets[i], null, rtvHandle);
            rtvHandle.Ptr += (nuint)_rtvDescriptorSize;
        }

        // Create command allocators
        _commandAllocators = new ID3D12CommandAllocator[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            _device.CreateCommandAllocator(CommandListType.Direct, out _commandAllocators[i]).CheckError();
        }

        // Create fence
        _fenceValues = new ulong[FrameCount];
        _device.CreateFence(0, FenceFlags.None, out _fence).CheckError();
        _fenceValues[_frameIndex] = 1;
        _fenceEvent = new AutoResetEvent(false);

        // Create root signature and pipeline state
        CreatePipelineState();

        // Create vertex buffer
        CreateVertexBuffer();

        // Create command list
        _device.CreateCommandList(0, CommandListType.Direct, _commandAllocators[_frameIndex], _pipelineState, out _commandList).CheckError();
        _commandList!.Close();

        Console.WriteLine($"DirectX 12 Device Created");
        Console.WriteLine($"Adapter: {GetAdapterDescription()}");
        Console.WriteLine();
    }

    private static string GetAdapterDescription()
    {
        _factory!.EnumAdapters1(0, out var adapter).CheckError();
        using (adapter)
        {
            return adapter.Description1.Description;
        }
    }

    private static void CreatePipelineState()
    {
        // Create root signature with one 32-bit constant for rotation
        var rootParameters = new RootParameter1[]
        {
            new(new RootConstants(0, 0, 1), ShaderVisibility.Vertex) // shaderRegister: 0, registerSpace: 0, num32BitValues: 1
        };

        var rootSignatureDesc = new RootSignatureDescription1(
            RootSignatureFlags.AllowInputAssemblerInputLayout,
            rootParameters);

        _device!.CreateRootSignature(new VersionedRootSignatureDescription(rootSignatureDesc), out _rootSignature).CheckError();

        // Compile shaders
        var vertexShaderBlob = CompileShader(VertexShaderSource, "main", "vs_5_0");
        var pixelShaderBlob = CompileShader(PixelShaderSource, "main", "ps_5_0");

        // Input layout
        var inputElements = new InputElementDescription[]
        {
            new("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)
        };

        // Create pipeline state
        var psoDesc = new GraphicsPipelineStateDescription
        {
            RootSignature = _rootSignature,
            VertexShader = vertexShaderBlob,
            PixelShader = pixelShaderBlob,
            InputLayout = new InputLayoutDescription(inputElements),
            SampleMask = uint.MaxValue,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            RasterizerState = RasterizerDescription.CullNone,
            BlendState = BlendDescription.Opaque,
            DepthStencilState = DepthStencilDescription.None,
            RenderTargetFormats = new[] { Format.R8G8B8A8_UNorm },
            SampleDescription = new SampleDescription(1, 0)
        };

        _device.CreateGraphicsPipelineState(psoDesc, out _pipelineState).CheckError();
    }

    private static byte[] CompileShader(string source, string entryPoint, string target)
    {
        var result = Vortice.D3DCompiler.Compiler.Compile(source, entryPoint, "shader", target);
        if (result.IsEmpty)
        {
            throw new Exception($"Shader compilation failed");
        }
        return result.ToArray();
    }

    private static void CreateVertexBuffer()
    {
        var vertices = new Vertex[]
         {
            new(new Vector3( 0.0f,   0.5f, 0.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)), // Top (red)
            new(new Vector3( 0.5f,  -0.5f, 0.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)), // Bottom right (green)
            new(new Vector3(-0.5f,  -0.5f, 0.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)), // Bottom left (blue)
         };

        int vertexBufferSize = vertices.Length * Unsafe.SizeOf<Vertex>();

        var heapProps = new HeapProperties(HeapType.Upload);
        var bufferDesc = ResourceDescription.Buffer((ulong)vertexBufferSize);

        _device!.CreateCommittedResource(heapProps, HeapFlags.None, bufferDesc,
            ResourceStates.GenericRead, out _vertexBuffer).CheckError();

        // Copy vertex data
        unsafe
        {
            void* dataPtr;
            _vertexBuffer!.Map(0, null, &dataPtr).CheckError();
            var destination = new Span<byte>(dataPtr, vertexBufferSize);
            MemoryMarshal.AsBytes(vertices.AsSpan()).CopyTo(destination);
        }

        _vertexBuffer.Unmap(0);

        _vertexBufferView = new VertexBufferView(
            _vertexBuffer.GPUVirtualAddress,
            (uint)vertexBufferSize,
            (uint)Unsafe.SizeOf<Vertex>());
    }

    private const string VertexShaderSource = @"
        cbuffer Constants : register(b0)
        {
            float rotation;
        };

        struct VSInput
        {
            float3 position : POSITION;
            float4 color : COLOR;
        };

        struct PSInput
        {
            float4 position : SV_POSITION;
            float4 color : COLOR;
        };

        PSInput main(VSInput input)
        {
            PSInput output;
            
            float rad = rotation * 3.14159265f / 180.0f;
            float cosR = cos(rad);
            float sinR = sin(rad);
            
            float2 rotPos;
            rotPos.x = input.position.x * cosR - input.position.y * sinR;
            rotPos.y = input.position.x * sinR + input.position.y * cosR;
            
            output.position = float4(rotPos, input.position.z, 1.0f);
            output.color = input.color;
            
            return output;
        }
    ";

    private const string PixelShaderSource = @"
        struct PSInput
        {
            float4 position : SV_POSITION;
            float4 color : COLOR;
        };

        float4 main(PSInput input) : SV_TARGET
        {
            return input.color;
        }
    ";

    #endregion

    #region Input Thread

    private static void InputThread()
    {
        while (_running)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1: SetTargetFps(30); break;
                    case ConsoleKey.D2: SetTargetFps(60); break;
                    case ConsoleKey.D3: SetTargetFps(90); break;
                    case ConsoleKey.D4: SetTargetFps(120); break;
                    case ConsoleKey.D5: SetTargetFps(144); break;
                    case ConsoleKey.D6: SetTargetFps(165); break;
                    case ConsoleKey.D7: SetTargetFps(240); break;
                    case ConsoleKey.D8: SetTargetFps(300); break;
                    case ConsoleKey.D9: SetTargetFps(10000); break;
                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        _running = false;
                        break;
                }
            }
            Thread.Sleep(50);
        }
    }

    #endregion

    #region Update and Render

    private static void Update()
    {
        var now = DateTime.Now;
        var deltaTime = (now - _lastTime).TotalSeconds;
        _lastTime = now;

        // Update rotation
        _rotation += (float)(deltaTime * 45.0); // 45 degrees per second
        if (_rotation > 360.0f)
            _rotation -= 360.0f;

        // Calculate FPS
        _frameCount++;
        if (_fpsTimer.Elapsed.TotalSeconds >= 1.0)
        {
            _currentFps = _frameCount / _fpsTimer.Elapsed.TotalSeconds;
            _frameCount = 0;
            _fpsTimer.Restart();
            SetWindowTextW(_hwnd, $"CapFrameX Test Renderer (DX12) - Target: {_targetFps} FPS | Actual: {_currentFps:F1} FPS");
        }
    }

    private static void Render()
    {
        // Frame rate limiting
        double elapsed = _frameTimer.Elapsed.TotalMilliseconds;
        if (elapsed < _targetFrameTime)
        {
            // Spin-wait for better precision
            var spinWait = new SpinWait();
            while (_frameTimer.Elapsed.TotalMilliseconds < _targetFrameTime)
            {
                spinWait.SpinOnce();
            }
        }
        _frameTimer.Restart();

        // Record commands
        _commandAllocators![_frameIndex].Reset();
        _commandList!.Reset(_commandAllocators[_frameIndex], _pipelineState);

        // Set viewport and scissor rect
        _commandList.RSSetViewport(new Viewport(0, 0, Width, Height));
        _commandList.RSSetScissorRect(new Vortice.RawRect(0, 0, Width, Height));

        // Transition render target to render target state
        _commandList.ResourceBarrier(new ResourceBarrier(
            new ResourceTransitionBarrier(_renderTargets![_frameIndex],
                ResourceStates.Present, ResourceStates.RenderTarget)));

        var rtvHandle = _rtvHeap!.GetCPUDescriptorHandleForHeapStart();
        rtvHandle.Ptr += (nuint)(_frameIndex * _rtvDescriptorSize);

        // Clear and set render target
        _commandList.OMSetRenderTargets(rtvHandle);
        _commandList.ClearRenderTargetView(rtvHandle, new Color4(0.1f, 0.1f, 0.1f, 1.0f));

        // Draw triangle
        _commandList.SetGraphicsRootSignature(_rootSignature);
        _commandList.SetGraphicsRoot32BitConstant(0, BitConverter.SingleToInt32Bits(_rotation), 0);
        _commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
        _commandList.IASetVertexBuffers(0, _vertexBufferView);
        _commandList.DrawInstanced(3, 1, 0, 0);

        // Transition render target to present state
        _commandList.ResourceBarrier(new ResourceBarrier(
            new ResourceTransitionBarrier(_renderTargets[_frameIndex],
                ResourceStates.RenderTarget, ResourceStates.Present)));

        _commandList.Close();

        // Execute command list
        _commandQueue!.ExecuteCommandList(_commandList);

        // Present - THIS IS THE KEY CALL THAT PRESENTMON CAPTURES
        _swapChain!.Present(0, PresentFlags.AllowTearing);

        // Move to next frame
        MoveToNextFrame();
    }

    private static void MoveToNextFrame()
    {
        var currentFenceValue = _fenceValues![_frameIndex];
        _commandQueue!.Signal(_fence, currentFenceValue);

        _frameIndex = (int)_swapChain!.CurrentBackBufferIndex;

        if (_fence!.CompletedValue < _fenceValues[_frameIndex])
        {
            _fence.SetEventOnCompletion(_fenceValues[_frameIndex], _fenceEvent);
            _fenceEvent!.WaitOne();
        }

        _fenceValues[_frameIndex] = currentFenceValue + 1;
    }

    private static void WaitForGpu()
    {
        _commandQueue!.Signal(_fence, _fenceValues![_frameIndex]);
        _fence!.SetEventOnCompletion(_fenceValues[_frameIndex], _fenceEvent);
        _fenceEvent!.WaitOne();
        _fenceValues[_frameIndex]++;
    }

    #endregion

    #region Cleanup

    private static void Cleanup()
    {
        _vertexBuffer?.Dispose();
        _pipelineState?.Dispose();
        _rootSignature?.Dispose();
        _commandList?.Dispose();

        if (_commandAllocators != null)
        {
            foreach (var allocator in _commandAllocators)
                allocator?.Dispose();
        }

        if (_renderTargets != null)
        {
            foreach (var rt in _renderTargets)
                rt?.Dispose();
        }

        _fence?.Dispose();
        _fenceEvent?.Dispose();
        _rtvHeap?.Dispose();
        _swapChain?.Dispose();
        _commandQueue?.Dispose();
        _device?.Dispose();
        _factory?.Dispose();

        DestroyWindow(_hwnd);
    }

    #endregion

    private static void SetTargetFps(int fps)
    {
        _targetFps = fps;
        _targetFrameTime = 1000.0 / fps;
        Console.WriteLine($"Target FPS changed to: {_targetFps}");
    }
}
