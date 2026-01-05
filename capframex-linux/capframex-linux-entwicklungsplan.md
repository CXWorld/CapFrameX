# CapFrameX Linux – Entwicklungsplan

## Projektübersicht

**Ziel**: Eine Linux-native Lösung für Frametime-Capturing und -Analyse, konzeptionell angelehnt an CapFrameX für Windows. 
Im ersten Entwicklungsschritt soll das GUI als Frontend für MangoHud gestaltet werden. CapFrameX soll in der Lage sein, MangoHud zu konfigurieren.

**Architektur**:
```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│     Daemon      │◄───►│  Vulkan Layer   │◄───►│   GUI (C#)      │
│     ( C )       │     │    (C / C++)    │     │   Avalonia UI   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │                       │
        └───────────────────────┴───────────────────────┘
                              IPC
                    (Unix Socket / Shared Memory)
```

---

## Phase 1: Projekt-Setup & Grundstruktur

### 1.1 Repository-Struktur erstellen

Verwende das Unterverzeichnis root\capframex-linux

```
capframex-linux/
├── src/
│   ├── daemon/              # Prozess-Überwachung (C oder Rust)
│   ├── layer/               # Vulkan Layer (C/C++)
│   └── app/                 # Avalonia UI (C#)
│       ├── CapFrameX.App/
│       ├── CapFrameX.Core/
│       └── CapFrameX.Shared/
├── tests/
├── docs/
├── scripts/                 # Build & Install Scripts
└── README.md
```

### 1.2 Build-System konfigurieren

**Aufgaben**:
- [ ] CMake für Daemon und Layer
- [ ] .NET 8 Solution für Avalonia App
- [ ] Meta-Build-Script das alles zusammenbaut
- [ ] GitHub Actions CI/CD Pipeline

### 1.3 Entwicklungsumgebung dokumentieren

**Abhängigkeiten**:
```bash
# Daemon/Layer
sudo apt install build-essential cmake libvulkan-dev

# .NET / Avalonia
# .NET 8 SDK installieren
```

---

## Phase 2: Daemon – Spielerkennung

### 2.1 Prozess-Monitoring implementieren

**Datei**: `src/daemon/process_monitor.c`

**Aufgaben**:
- [ ] Netlink-Socket für Prozess-Events (PROC_EVENT_FORK, PROC_EVENT_EXEC)
- [ ] Alternativ: Polling von /proc mit inotify
- [ ] Prozessbaum-Traversierung (ppid → parent chain)

**Schnittstelle**:
```c
typedef struct {
    pid_t pid;
    char exe_path[PATH_MAX];
    pid_t parent_pid;
    char parent_name[256];
} ProcessInfo;

// Callback wenn neuer Prozess erkannt
typedef void (*process_callback)(ProcessInfo* info);
```

### 2.2 Launcher-Erkennung

**Datei**: `src/daemon/launcher_detect.c`

**Bekannte Launcher**:
```c
const char* KNOWN_LAUNCHERS[] = {
    "steam",
    "lutris",
    "heroic",
    "bottles",
    "gamescope",
    NULL
};
```

**Aufgaben**:
- [ ] Launcher-Prozesse identifizieren und tracken
- [ ] Kindprozesse von Launchern markieren
- [ ] Whitelist/Blacklist-System

### 2.3 IPC-Schnittstelle

**Datei**: `src/daemon/ipc.c`

**Aufgaben**:
- [ ] Unix Socket erstellen (`/run/user/{UID}/capframex.sock`)
- [ ] Shared Memory Segment für aktive PIDs (`/dev/shm/capframex_pids`)
- [ ] Protokoll definieren:

```c
enum MessageType {
    MSG_GAME_STARTED,      // Daemon → Layer/App
    MSG_GAME_STOPPED,      // Daemon → Layer/App
    MSG_START_CAPTURE,     // App → Layer
    MSG_STOP_CAPTURE,      // App → Layer
    MSG_FRAMETIME_DATA,    // Layer → App
};

typedef struct {
    uint32_t type;
    uint32_t payload_size;
    uint8_t payload[];
} Message;
```

### 2.4 Systemd User Service

**Datei**: `scripts/capframex-daemon.service`

```ini
[Unit]
Description=CapFrameX Game Detection Daemon

[Service]
ExecStart=/usr/bin/capframex-daemon
Restart=on-failure

[Install]
WantedBy=default.target
```

---

## Phase 3: Vulkan Layer – Frametime Capture

### 3.1 Layer-Grundgerüst

**Datei**: `src/layer/layer.c`

**Aufgaben**:
- [ ] Vulkan Layer Boilerplate (vkNegotiateLoaderLayerInterfaceVersion)
- [ ] Layer Manifest JSON erstellen
- [ ] Intercept von vkCreateInstance, vkCreateDevice

### 3.2 Swapchain Hooking

**Datei**: `src/layer/swapchain.c`

**Zu interceptende Funktionen**:
```c
// Swapchain-Erstellung erkennen
vkCreateSwapchainKHR

// Frametime messen
vkQueuePresentKHR

// Cleanup
vkDestroySwapchainKHR
```

**Aufgaben**:
- [ ] Beim Create: PID gegen Daemon prüfen (aktiv/inaktiv)
- [ ] Timestamps bei jedem Present speichern
- [ ] Ringbuffer für Frametimes

### 3.3 Frametime-Messung

**Datei**: `src/layer/timing.c`

```c
typedef struct {
    uint64_t timestamp_ns;    // clock_gettime(CLOCK_MONOTONIC)
    uint64_t frame_number;
    float frametime_ms;
} FrameData;

// Ringbuffer für ~60 Sekunden @ 144fps
#define FRAME_BUFFER_SIZE 8640
```

**Aufgaben**:
- [ ] Hochpräzise Zeitmessung (CLOCK_MONOTONIC)
- [ ] Optional: GPU-Timestamps via VK_EXT_calibrated_timestamps
- [ ] Frametime-Berechnung (Delta zwischen Presents)

### 3.4 Daten-Export

**Aufgaben**:
- [ ] Shared Memory für Live-Daten an App
- [ ] CSV-Export bei Capture-Stop
- [ ] Kompatibles Format mit Windows CapFrameX

```csv
MsBetweenPresents,MsUntilRenderComplete,MsUntilDisplayed
6.94,5.21,6.94
7.02,5.18,7.02
```

### 3.5 Layer Installation

**Datei**: `src/layer/capframex_layer.json`

```json
{
    "file_format_version": "1.0.0",
    "layer": {
        "name": "VK_LAYER_capframex_capture",
        "type": "GLOBAL",
        "library_path": "/usr/lib/libcapframex_layer.so",
        "api_version": "1.3.0",
        "implementation_version": "1",
        "description": "CapFrameX Frametime Capture Layer"
    }
}
```

---

## Phase 4: Avalonia App – Grundstruktur

### 4.1 Solution erstellen

```bash
dotnet new sln -n CapFrameX
dotnet new avalonia.app -n CapFrameX.App
dotnet new classlib -n CapFrameX.Core
dotnet new classlib -n CapFrameX.Shared
dotnet sln add **/*.csproj
```

### 4.2 Projekt-Architektur

```
CapFrameX.App/
├── Views/
│   ├── MainWindow.axaml
│   ├── CaptureView.axaml
│   ├── AnalysisView.axaml
│   └── CompareView.axaml
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── CaptureViewModel.cs
│   ├── AnalysisViewModel.cs
│   └── CompareViewModel.cs
└── App.axaml

CapFrameX.Core/
├── Capture/
│   ├── DaemonClient.cs
│   └── FrametimeReceiver.cs
├── Analysis/
│   ├── FrametimeAnalyzer.cs
│   ├── StatisticsCalculator.cs
│   └── ChartDataProvider.cs
└── Data/
    ├── Session.cs
    └── SessionManager.cs

CapFrameX.Shared/
├── Models/
│   ├── FrameData.cs
│   └── CaptureSession.cs
└── IPC/
    ├── Messages.cs
    └── SocketClient.cs
```

### 4.3 MVVM Setup

**Aufgaben**:
- [ ] ReactiveUI oder CommunityToolkit.Mvvm einbinden
- [ ] Navigation zwischen Views
- [ ] Dependency Injection konfigurieren

---

## Phase 5: App – Daemon-Kommunikation

### 5.1 Unix Socket Client

**Datei**: `CapFrameX.Shared/IPC/SocketClient.cs`

```csharp
public class DaemonClient : IDisposable
{
    private readonly Socket _socket;
    private readonly string _socketPath;
    
    public event EventHandler<GameDetectedEventArgs>? GameDetected;
    public event EventHandler<GameExitedEventArgs>? GameExited;
    
    public Task ConnectAsync();
    public Task StartCaptureAsync(int pid);
    public Task StopCaptureAsync();
}
```

### 5.2 Frametime-Empfang

**Datei**: `CapFrameX.Core/Capture/FrametimeReceiver.cs`

**Aufgaben**:
- [ ] Shared Memory lesen (MemoryMappedFile)
- [ ] Live-Update an ViewModel (Observable)
- [ ] Pufferung für Performance

---

## Phase 6: App – Capture-Ansicht

### 6.1 UI-Komponenten

**Aufgaben**:
- [ ] Spielerkennung-Anzeige (Icon, Name, PID)
- [ ] Start/Stop Capture Button
- [ ] Hotkey-Konfiguration
- [ ] Live-Frametime-Graph (Rolling Window)
- [ ] Live-Statistiken (FPS, 1% Low, 0.1% Low)

### 6.2 Hotkey-System

**Datei**: `CapFrameX.App/Services/HotkeyService.cs`

**Aufgaben**:
- [ ] Globale Hotkeys unter X11/Wayland
- [ ] Für X11: XGrabKey
- [ ] Für Wayland: GlobalShortcuts Portal (xdg-desktop-portal)

---

## Phase 7: App – Analyse

### 7.1 Statistik-Berechnung

**Datei**: `CapFrameX.Core/Analysis/StatisticsCalculator.cs`

```csharp
public class FrametimeStatistics
{
    public double Average { get; set; }
    public double Median { get; set; }
    public double P1 { get; set; }          // 1% Percentile (1% Low)
    public double P01 { get; set; }         // 0.1% Percentile
    public double P001 { get; set; }        // 0.01% Percentile
    public double Max { get; set; }
    public double Min { get; set; }
    public double StdDev { get; set; }
}
```

### 7.2 Charts

**Aufgaben**:
- [ ] LiveCharts2 oder ScottPlot für Avalonia einbinden
- [ ] Frametime-Graph
- [ ] FPS-Graph
- [ ] L-Shape Diagramm (Percentile-Kurve)
- [ ] Histogramm

### 7.3 Session-Vergleich

**Aufgaben**:
- [ ] Mehrere Sessions laden
- [ ] Überlagerter Graph
- [ ] Tabellarischer Vergleich
- [ ] Export (PNG, CSV)

---

## Phase 8: Persistenz & Export

### 8.1 Session-Format

**Datei**: `~/.local/share/capframex/sessions/`

**Aufgaben**:
- [ ] JSON-Metadaten + CSV-Frametimes
- [ ] Kompatibilität mit Windows-CapFrameX prüfen
- [ ] Session-Browser in App

### 8.2 Hardware-Info sammeln

**Aufgaben**:
- [ ] CPU: /proc/cpuinfo, lscpu
- [ ] GPU: vulkaninfo, /sys/class/drm
- [ ] RAM: /proc/meminfo
- [ ] Kernel-Version, Distribution

---

## Phase 9: Integration & Polish

### 9.1 Installer

**Aufgaben**:
- [ ] .deb Paket (Debian/Ubuntu)
- [ ] .rpm Paket (Fedora)
- [ ] AUR PKGBUILD (Arch)
- [ ] Flatpak (optional)

### 9.2 Steam Deck Optimierung

**Aufgaben**:
- [ ] Gamescope-Integration testen
- [ ] Touch-freundliche UI-Variante
- [ ] Performance auf APU validieren

### 9.3 Dokumentation

**Aufgaben**:
- [ ] README mit Screenshots
- [ ] Installation Guide
- [ ] Troubleshooting (Vulkan Layer nicht geladen, etc.)
- [ ] Contributing Guide

---

## Meilensteine

| Phase | Beschreibung |
|-------|--------------|
| 1 | Projekt-Setup |
| 2 | Daemon |
| 3 | Vulkan Layer |
| 4 | App Grundstruktur |
| 5 | App ↔ Daemon IPC |
| 6 | Capture UI |
| 7 | Analyse |
| 8 | Persistenz |
| 9 | Polish & Release |

---

## Lizenz
MIT

## Offene Entscheidungen
3. **Branding**: CapFrameX Linux oder neuer Name?
---

## Nächster Schritt

Beginne mit **Phase 1.1**: Repository-Struktur erstellen und Build-System aufsetzen.

```bash
mkdir -p capframex-linux/{src/{daemon,layer,app},tests,docs,scripts}
cd capframex-linux
git init
```
