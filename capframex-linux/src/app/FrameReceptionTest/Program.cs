using CapFrameX.Shared.IPC;
using CapFrameX.Shared.Models;

Console.WriteLine("=== CapFrameX Frame Reception Test ===");
Console.WriteLine();

using var client = new DaemonClient();
var frameCount = 0;
var lastFps = 0f;
GameInfo? targetGame = null;

// Set up event handlers
client.Connected += (_, _) => Console.WriteLine("[Connected] Connected to daemon");
client.Disconnected += (_, _) => Console.WriteLine("[Disconnected] Lost connection to daemon");

client.GameDetected += (_, game) =>
{
    Console.WriteLine($"[Game Detected] PID={game.Pid}, Name={game.Name}, Launcher={game.Launcher}");

    // Skip Wine helper processes
    var skipNames = new[] { "explorer.exe", "wineserver", "wine-preloader", "wine64-preloader", "services.exe", "plugplay.exe", "svchost.exe" };
    if (skipNames.Any(s => game.Name.Contains(s, StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine($"[Skip] Skipping Wine helper process: {game.Name}");
        return;
    }

    // Auto-subscribe to first real game
    if (targetGame == null)
    {
        targetGame = game;
        Console.WriteLine($"[Subscribe] Subscribing to PID {game.Pid}...");
        client.SendStartCaptureAsync(game.Pid).Wait();
        Console.WriteLine($"[Subscribe] Subscribed! Waiting for frames...");
    }
};

client.GameExited += (_, pid) =>
{
    Console.WriteLine($"[Game Exited] PID={pid}");
    if (targetGame?.Pid == pid)
    {
        Console.WriteLine($"[Info] Target game exited after {frameCount} frames");
        targetGame = null;
    }
};

client.FrameDataReceived += (_, frame) =>
{
    frameCount++;
    lastFps = frame.Fps;

    // Print every 10th frame to avoid spam
    if (frameCount % 10 == 0 || frameCount <= 5)
    {
        Console.WriteLine($"[Frame {frameCount}] PID={frame.Pid}, Frametime={frame.FrametimeMs:F2}ms, FPS={frame.Fps:F1}");
    }
};

// Connect to daemon
Console.WriteLine("Connecting to daemon...");
var connected = await client.ConnectAsync();

if (!connected)
{
    Console.WriteLine("ERROR: Failed to connect to daemon!");
    Console.WriteLine("Make sure capframex-daemon is running.");
    return 1;
}

Console.WriteLine("Connected! Requesting status...");
await client.RequestStatusAsync();

Console.WriteLine();
Console.WriteLine("Waiting for games/layers to connect...");
Console.WriteLine("Run a Vulkan application with ENABLE_CAPFRAMEX_LAYER=1");
Console.WriteLine("Press Ctrl+C to exit");
Console.WriteLine();

// Wait for Ctrl+C
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Expected
}

Console.WriteLine();
Console.WriteLine($"=== Test Complete ===");
Console.WriteLine($"Total frames received: {frameCount}");
Console.WriteLine($"Last FPS: {lastFps:F1}");

await client.DisconnectAsync();
return 0;
