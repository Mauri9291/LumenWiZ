# LumenWiZ: WiZ Desktop Native App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and compile LumenWiZ, a 100% native Windows 11 desktop app to control WiZ smart lights (UDP 38899), featuring a minimalist System Tray popover, full room and individual device management, instant local scene capture, WASAPI audio reactivity (Spotify loopback), and a local HTTP API for Stream Deck.

**Architecture:** A lightweight .NET 9 solution separated into a core domain (`LumenWiZ.Core`) handling UDP JSON-RPC networking, device discovery, audio FFT analysis, and local REST HTTP endpoints, coupled to a native Windows presentation layer (`LumenWiZ.UI`) with Win32 Mica/Acrylic styling, a tray popover widget, and a full management window.

**Tech Stack:** C# 13, .NET 9, WPF with ModernWpf / Win32 Acrylic backdrop, System.Net.Sockets (UDP), NAudio (WASAPI Loopback Audio Capture), System.Text.Json, xUnit for TDD.

**Spec:** `docs/superpowers/specs/2026-10-11-wiz-desktop-design.md`

## Global Constraints

- **Platform:** Windows 10/11 x64, 100% native (.NET 9), no Electron or Chromium/WebView.
- **Memory Footprint:** < 20 MB RAM in background when idling in the system tray.
- **Network Protocol:** UDP port 38899 raw JSON-RPC (`setPilot`, `getPilot`, `getSystemConfig`).
- **Throttling:** Max 25 UDP packets/second per light during audio reactive synchronization.
- **Audio Capture:** WASAPI Loopback (system digital audio output, no microphone dependencies).
- **Stream Deck API:** Embedded HTTP server on `http://localhost:38898/api/`.
- **Local Storage:** `%APPDATA%\LumenWiZ\config.json`.

## Review Focus

1. **UDP Broadcast Dropped by Router Isolation:** When UDP broadcast (`255.255.255.255`) fails across subnets, app falls back to subnet-directed broadcast (`192.168.0.255`) and manual IP pinging without freezing the UI.
2. **Audio Sync Throttling:** Rapid audio FFT changes must not flood the UDP socket or cause ESP8266/ESP32 WiZ microcontrollers to crash or drop off the Wi-Fi.
3. **Tray Popover Focus Lost:** When clicking outside the Tray Popover widget, it smoothly hides without lingering in the Windows alt-tab switcher.
4. **Scene Capture Reliability:** If 1 of 5 bulbs is powered off at the wall switch, `CaptureCurrentSceneAsync` successfully records the responsive 4 bulbs and flags the offline bulb rather than failing the whole snapshot.
5. **Stream Deck HTTP Port Conflict:** If port 38898 is occupied, app attempts adjacent port 38897 and logs notification without crashing the application.

---

### Task 1: Environment Setup & Solution Scaffolding

**Files:**
- Create: `LumenWiZ.sln`
- Create: `src/LumenWiZ.Core/LumenWiZ.Core.csproj`
- Create: `src/LumenWiZ.UI/LumenWiZ.UI.csproj`
- Create: `tests/LumenWiZ.Tests/LumenWiZ.Tests.csproj`

**Interfaces:**
- Produces: Visual Studio / .NET 9 solution and project references connecting `UI` -> `Core` and `Tests` -> `Core`.

- [ ] **Step 1: Install .NET 9 SDK if not present**
  Run: `winget install Microsoft.DotNet.SDK.9 --accept-package-agreements --accept-source-agreements --silent`
  Verify: `dotnet --version` outputs `9.x.x`

- [ ] **Step 2: Scaffold Solution and Projects**
  ```bash
  dotnet new sln -n LumenWiZ
  dotnet new classlib -o src/LumenWiZ.Core -f net9.0
  dotnet new wpf -o src/LumenWiZ.UI -f net9.0-windows
  dotnet new xunit -o tests/LumenWiZ.Tests -f net9.0
  dotnet sln add src/LumenWiZ.Core src/LumenWiZ.UI tests/LumenWiZ.Tests
  dotnet add src/LumenWiZ.UI reference src/LumenWiZ.Core
  dotnet add tests/LumenWiZ.Tests reference src/LumenWiZ.Core
  ```

- [ ] **Step 3: Add NuGet dependencies**
  ```bash
  dotnet add src/LumenWiZ.Core package NAudio
  dotnet add src/LumenWiZ.UI package ModernWpfUI
  dotnet add src/LumenWiZ.UI package Hardcodet.NotifyIcon.Wpf
  ```

- [ ] **Step 4: Verify initial build**
  Run: `dotnet build`
  Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**
  ```bash
  git add .
  git commit -m "chore: scaffold LumenWiZ .NET 9 solution and project structure"
  ```

---

### Task 2: WiZ UDP Protocol Engine & Models

**Files:**
- Create: `src/LumenWiZ.Core/Models/WizModels.cs`
- Create: `src/LumenWiZ.Core/Protocol/WizProtocol.cs`
- Create: `tests/LumenWiZ.Tests/ProtocolTests.cs`

**Interfaces:**
- Produces:
  - `WizPilotState`: DTO containing `State` (bool), `Dimming` (int 10-100), `R`, `G`, `B` (int 0-255), `Temp` (int 2200-6500), `SceneId` (int).
  - `WizProtocol.CreateSetPilotPayload(WizPilotState state) -> string`
  - `WizProtocol.CreateGetPilotPayload() -> string`
  - `WizProtocol.CreateGetSystemConfigPayload() -> string`
  - `WizProtocol.ParsePilotResponse(string json) -> WizPilotState`
  - `WizProtocol.ParseSystemConfigResponse(string json) -> (string mac, int homeId, int roomId)`

- [ ] **Step 1: Write failing unit test for protocol serialization and parsing**
  In `tests/LumenWiZ.Tests/ProtocolTests.cs`:
  Test `ParsePilotResponse` with sample WiZ bulb payload: `{"method":"getPilot","env":"pro","result":{"mac":"a8bb50","rssi":-60,"state":true,"sceneId":0,"speed":100,"dimming":80,"temp":2700}}`.

- [ ] **Step 2: Run test to verify it fails**
  Run: `dotnet test --filter FullyQualifiedName~ProtocolTests`
  Expected: FAIL (types do not exist yet)

- [ ] **Step 3: Implement `WizModels.cs` and `WizProtocol.cs`**
  Implement JSON serialization and parsing using `System.Text.Json`.

- [ ] **Step 4: Run test to verify it passes**
  Run: `dotnet test --filter FullyQualifiedName~ProtocolTests`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/LumenWiZ.Core/ tests/LumenWiZ.Tests/
  git commit -m "feat(core): implement WiZ JSON-RPC protocol parser and DTO models"
  ```

---

### Task 3: UDP Transport & Device Discovery Service

**Files:**
- Create: `src/LumenWiZ.Core/Network/WizUdpTransport.cs`
- Create: `src/LumenWiZ.Core/Services/DiscoveryService.cs`
- Create: `tests/LumenWiZ.Tests/DiscoveryTests.cs`

**Interfaces:**
- Consumes: `WizProtocol`, `WizPilotState`
- Produces:
  - `WizUdpTransport.SendAsync(IPAddress ip, string json, int timeoutMs = 1500) -> Task<string>`
  - `WizUdpTransport.SendBroadcastAsync(string json, int timeoutMs = 2500) -> Task<List<(IPAddress ip, string json)>>`
  - `DiscoveryService.DiscoverDevicesAsync() -> Task<List<WizDevice>>` (detects bulbs, pulls MAC, homeId, roomId, and initial pilot state)

- [ ] **Step 1: Write unit tests for DiscoveryService parsing multi-device responses**
  In `tests/LumenWiZ.Tests/DiscoveryTests.cs`: test grouping devices by `RoomId`.

- [ ] **Step 2: Run test to verify it fails**
  Run: `dotnet test --filter FullyQualifiedName~DiscoveryTests`
  Expected: FAIL

- [ ] **Step 3: Implement `WizUdpTransport` and `DiscoveryService`**
  Use `System.Net.Sockets.UdpClient` on port 38899. Implement dual broadcast (`255.255.255.255` and local subnet broadcast `192.168.0.255`).

- [ ] **Step 4: Run test to verify it passes**
  Run: `dotnet test --filter FullyQualifiedName~DiscoveryTests`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/LumenWiZ.Core/Network/ src/LumenWiZ.Core/Services/ tests/LumenWiZ.Tests/
  git commit -m "feat(core): implement UDP broadcast discovery and device grouping"
  ```

---

### Task 4: Local Storage & Instant Scene Snapshot Service

**Files:**
- Create: `src/LumenWiZ.Core/Storage/ConfigStorage.cs`
- Create: `src/LumenWiZ.Core/Services/SceneSnapshotService.cs`
- Create: `tests/LumenWiZ.Tests/SceneSnapshotTests.cs`

**Interfaces:**
- Consumes: `WizDevice`, `WizUdpTransport`, `WizProtocol`
- Produces:
  - `SceneSnapshotService.CaptureRoomSceneAsync(WizRoom room, string sceneName, string icon) -> Task<WizCustomScene>`
  - `SceneSnapshotService.ApplySceneAsync(WizCustomScene scene) -> Task`
  - `ConfigStorage.SaveConfigAsync(AppConfig config) -> Task`
  - `ConfigStorage.LoadConfigAsync() -> Task<AppConfig>`

- [ ] **Step 1: Write unit test for scene capture and apply**
  In `tests/LumenWiZ.Tests/SceneSnapshotTests.cs`: mock 5 devices with distinct colors/brightness; test that snapshot captures all 5 and restores them properly.

- [ ] **Step 2: Run test to verify it fails**
  Run: `dotnet test --filter FullyQualifiedName~SceneSnapshotTests`
  Expected: FAIL

- [ ] **Step 3: Implement `ConfigStorage` and `SceneSnapshotService`**
  Save configurations to `%APPDATA%\LumenWiZ\config.json`. Implement snapshot taking by querying each bulb's `getPilot` and saving the dictionary `{ DeviceMac: WizPilotState }`.

- [ ] **Step 4: Run test to verify it passes**
  Run: `dotnet test --filter FullyQualifiedName~SceneSnapshotTests`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/LumenWiZ.Core/Storage/ src/LumenWiZ.Core/Services/ tests/LumenWiZ.Tests/
  git commit -m "feat(core): implement local scene snapshot capture and config storage"
  ```

---

### Task 5: Windows WASAPI Loopback Audio Reactive Engine

**Files:**
- Create: `src/LumenWiZ.Core/Audio/AudioSpectrumAnalyzer.cs`
- Create: `src/LumenWiZ.Core/Audio/AudioSyncService.cs`
- Create: `tests/LumenWiZ.Tests/AudioAnalyzerTests.cs`

**Interfaces:**
- Consumes: `NAudio.Wave.WasapiLoopbackCapture`, `WizUdpTransport`
- Produces:
  - `AudioSpectrumAnalyzer.ProcessSamples(float[] samples) -> (float bass, float mid, float treble)`
  - `AudioSyncService.StartAsync(List<WizDevice> reactiveDevices)`
  - `AudioSyncService.Stop()`
  - Throttling loop: ensures UDP dispatch is capped at 25 FPS per light.

- [ ] **Step 1: Write unit test for AudioSpectrumAnalyzer FFT processing**
  In `tests/LumenWiZ.Tests/AudioAnalyzerTests.cs`: test that low-frequency sine waves trigger high `bass` energy, while high-frequency sine waves trigger `treble` energy.

- [ ] **Step 2: Run test to verify it fails**
  Run: `dotnet test --filter FullyQualifiedName~AudioAnalyzerTests`
  Expected: FAIL

- [ ] **Step 3: Implement `AudioSpectrumAnalyzer` and `AudioSyncService`**
  Implement FFT magnitude grouping (Bass: 20-150Hz, Mids: 150-2500Hz, Treble: 2500-12000Hz). Map bass energy to LED Strip pulse intensity and mid/treble to bulb hue modulation.

- [ ] **Step 4: Run test to verify it passes**
  Run: `dotnet test --filter FullyQualifiedName~AudioAnalyzerTests`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/LumenWiZ.Core/Audio/ tests/LumenWiZ.Tests/
  git commit -m "feat(core): implement WASAPI loopback audio reactive spectrum analyzer"
  ```

---

### Task 6: Embedded Local REST API for Stream Deck & Macros

**Files:**
- Create: `src/LumenWiZ.Core/Server/LocalHttpServer.cs`
- Create: `tests/LumenWiZ.Tests/LocalServerTests.cs`

**Interfaces:**
- Consumes: `DiscoveryService`, `SceneSnapshotService`
- Produces:
  - `LocalHttpServer.Start(int port = 38898)`
  - Routes:
    - `GET /api/status`: returns active lights and rooms.
    - `POST /api/power/toggle?room={roomId}`: toggles room on/off.
    - `POST /api/scenes/activate?name={sceneName}`: triggers a scene.
    - `POST /api/brightness?value={10-100}&room={roomId}`: sets room brightness.

- [ ] **Step 1: Write integration test for HTTP endpoints**
  In `tests/LumenWiZ.Tests/LocalServerTests.cs`: start server on random high port and test `GET /api/status` and `POST /api/power/toggle`.

- [ ] **Step 2: Run test to verify it fails**
  Run: `dotnet test --filter FullyQualifiedName~LocalServerTests`
  Expected: FAIL

- [ ] **Step 3: Implement `LocalHttpServer` using `System.Net.HttpListener`**
  Implement lightweight asynchronous HTTP request handling with JSON responses.

- [ ] **Step 4: Run test to verify it passes**
  Run: `dotnet test --filter FullyQualifiedName~LocalServerTests`
  Expected: PASS

- [ ] **Step 5: Commit**
  ```bash
  git add src/LumenWiZ.Core/Server/ tests/LumenWiZ.Tests/
  git commit -m "feat(core): implement local HTTP REST API for Stream Deck"
  ```

---

### Task 7: Presentation Layer (Nivel 1: System Tray & Popover Flyout)

**Files:**
- Create: `src/LumenWiZ.UI/Views/TrayPopoverWindow.xaml`
- Create: `src/LumenWiZ.UI/Views/TrayPopoverWindow.xaml.cs`
- Create: `src/LumenWiZ.UI/ViewModels/TrayPopoverViewModel.cs`

**Interfaces:**
- Consumes: `DiscoveryService`, `SceneSnapshotService`, `ConfigStorage`
- Produces:
  - Tactile dark-mode floating flyout window anchored directly above the Windows taskbar clock.
  - Smooth slider for room master brightness.
  - Scene pill buttons (Luz Diaria, Chimenea, Océano, Atardecer, Vela, Audio Sync).
  - Master power toggle.
  - Gear button (`⚙️`) to open Level 2 Management Window.

- [ ] **Step 1: Create XAML layout for `TrayPopoverWindow`**
  Implement window with `WindowStyle="None"`, `AllowsTransparency="True"`, Acrylic blur effect, 20px rounded corners, and dark styling matching `exact-vision-v2.html`.

- [ ] **Step 2: Implement Deactivated Auto-Hide Behavior**
  In `TrayPopoverWindow.xaml.cs`: handle `Deactivated` event to hide window when clicking anywhere outside.

- [ ] **Step 3: Hook ViewModel to Core Services**
  Bind brightness slider to room broadcast `setPilot`, scene pills to `ApplySceneAsync`, and gear icon to show `MainWindow`.

- [ ] **Step 4: Test build and UI compilation**
  Run: `dotnet build src/LumenWiZ.UI`
  Expected: Build succeeded with 0 warnings/errors.

- [ ] **Step 5: Commit**
  ```bash
  git add src/LumenWiZ.UI/
  git commit -m "feat(ui): implement Level 1 System Tray Popover with tactile controls"
  ```

---

### Task 8: Presentation Layer (Nivel 2: Full Management Window & Device Cards)

**Files:**
- Create: `src/LumenWiZ.UI/Views/MainWindow.xaml`
- Create: `src/LumenWiZ.UI/Views/MainWindow.xaml.cs`
- Create: `src/LumenWiZ.UI/ViewModels/MainViewModel.cs`
- Create: `src/LumenWiZ.UI/Controls/DeviceCard.xaml`

**Interfaces:**
- Consumes: `DiscoveryService`, `SceneSnapshotService`, `AudioSyncService`
- Produces:
  - Full management window with rooms sidebar.
  - Individual device rows showing each bulb and LED strip individually.
  - Per-device color picker popup, brightness slider, and audio-sync toggle.
  - "📸 Capturar escena actual" button to name and save custom scenes.
  - "🔍 Detectar nuevas luces" button.

- [ ] **Step 1: Create XAML layout for `MainWindow`**
  Implement sidebar navigation (Habitaciones, Audio Sync, Stream Deck) and main content grid matching `exact-vision-v2.html`.

- [ ] **Step 2: Create `DeviceCard` user control**
  Displays device name, IP, current color indicator, brightness slider, and power toggle.

- [ ] **Step 3: Implement Scene Snapshot Modal**
  Add dialog prompt asking for scene name and icon when clicking "Capturar escena actual", and persisting to `ConfigStorage`.

- [ ] **Step 4: Test UI build**
  Run: `dotnet build src/LumenWiZ.UI`
  Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**
  ```bash
  git add src/LumenWiZ.UI/
  git commit -m "feat(ui): implement Level 2 Management Window with individual device controls"
  ```

---

### Task 9: End-to-End Verification & Standalone Native Release Build

**Files:**
- Modify: `src/LumenWiZ.UI/LumenWiZ.UI.csproj` (publish profile for self-contained single `.exe`)

- [ ] **Step 1: Run all unit and integration tests**
  Run: `dotnet test`
  Expected: All tests PASS.

- [ ] **Step 2: Compile native self-contained release executable**
  Run: `dotnet publish src/LumenWiZ.UI/LumenWiZ.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
  Verify: Executable `LumenWiZ.exe` generated in `bin/Release/net9.0-windows/win-x64/publish/`.

- [ ] **Step 3: Verify binary launch and background memory footprint**
  Run test launch of `LumenWiZ.exe` and measure working set memory: verify RAM < 20 MB in tray idle.

- [ ] **Step 4: Commit and finalize v1 tag**
  ```bash
  git add .
  git commit -m "release: LumenWiZ v1.0.0 standalone native Windows executable"
  ```
