# 💡 LumenWiZ

Native Windows (x64) desktop application designed for local, ultra-fast, direct control of WiZ Connected smart bulbs and LED strips.

---

## 📌 What is LumenWiZ?

**LumenWiZ** is a desktop application for Windows 10 and Windows 11 that lets you control and manage your WiZ smart lighting directly from your computer.

It operates **100% locally** on your Wi-Fi network: no user accounts, no third-party cloud services, and zero internet latency. It integrates seamlessly into Windows with a quick-access System Tray popover and a full management studio.

---

## ⚙️ What Does It Do?

* **Automatic Device Discovery**: Automatically scans and pairs all WiZ smart bulbs and LED strips on your local network within milliseconds using parallel subnet discovery.
* **Room & Individual Light Control**:
  * Master room switch and per-device power toggles.
  * Smooth brightness slider (1% to 100%).
  * White color temperature adjustments (Kelvin) and real-time RGB chromatic color picker.
  * Create, organize, and assign lights to custom rooms.
* **Audio Reactive Synchronization (WASAPI Loopback)**:
  * Lights react in real time to music, games, movies, or any sound played by Windows.
  * Direct digital audio capture: zero microphones, completely immune to ambient noise.
  * 3-band frequency analyzer (Bass, Mids, Treble) assignable to specific lights.
* **Lighting Scene Management**:
  * **Factory Predefined Scenes**: Direct hardware activation of built-in WiZ chip dynamic effects (Ocean, Fireplace, Sunset, Forest, Party, Candlelight, etc.).
  * **Saved Custom Scenes**: Instant snapshot of your entire room's light configuration with one click, ready to recall at any time.
  * **Quick Menu (6 Slots)**: Customizable quick-access slots in the system tray popover.
* **Stream Deck & Local REST API**:
  * Embedded local HTTP server (port `38898`).
  * REST endpoints to toggle power, set brightness, or trigger scenes from Elgato Stream Deck, Windows shortcuts, or home automation scripts.
* **System Settings**:
  * Bilingual interface with live language switching (**English** and **Spanish**).
  * Auto-start with Windows and start minimized to tray options.

---

## 🔬 How Does It Work?

LumenWiZ runs on a low-latency, local-first architecture:

```
┌──────────────────┐       UDP Broadcast / Unicast (Port 38899)       ┌────────────────────────┐
│  LumenWiZ (PC)   │ ───────────────────────────────────────────────> │ WiZ Bulbs & LED Strips │
│                  │ <─────────────────────────────────────────────── │ (Response time < 5 ms) │
└────────┬─────────┘                      JSON-RPC                    └────────────────────────┘
         │
         ├─── WASAPI Audio Loopback ──> FFT Spectrum Analyzer (3 bands) ──> Rate limiter (25 FPS)
         │
         ├─── Embedded HTTP Server (Port 38898) <── Stream Deck / Local automation
         │
         └─── Local Configuration Storage ──> %APPDATA%\LumenWiZ\config.json
```

1. **Native WiZ UDP Protocol (Port 38899)**:
   * Communicates directly with WiZ firmware via UDP datagrams using JSON-RPC commands (`setPilot`, `getPilot`).
   * Ultra-low latency (< 5 ms) and completely functional even during internet outages.
2. **Digital Audio Loopback (WASAPI)**:
   * Captures the audio stream directly from Windows' active audio output buffer.
   * Splits audio into Bass (20–150 Hz), Mids (150–2500 Hz), and Treble (2.5–16 kHz).
   * A rate limiter caps updates to 25 FPS to ensure smooth light transitions without overloading bulb ESP microcontrollers.
3. **Local REST API Server (Port 38898)**:
   * Hosts a lightweight HTTP listener accepting `GET` and `POST` requests.
   * Allows external integration with Elgato Stream Deck, curl, or custom macros.
4. **Local Configuration**:
   * All preferences, device custom names, ordering, room associations, and custom scenes are saved locally in `%APPDATA%\LumenWiZ\config.json`.

---

## 📸 Screenshots

| Rooms & RGB Color Picker | Audio Reactive Sync (WASAPI) |
|:---:|:---:|
| ![Rooms Control](docs/screenshots/01-control-habitaciones.png) | ![Audio Reactive](docs/screenshots/02-audio-reactivo.png) |

| Predefined Factory Scenes | Saved Custom Scenes |
|:---:|:---:|
| ![Predefined Scenes](docs/screenshots/03-escenas-predefinidas.png) | ![Saved Scenes](docs/screenshots/04-escenas-guardadas.png) |

| General Settings & Local REST API |
|:---:|
| ![Settings and API](docs/screenshots/05-ajustes-y-api.png) |

---

## 📥 Downloads & Installation

Ready-to-use binaries are available on the official **[Releases Page](https://github.com/Mauri9291/LumenWiZ/releases/latest)**:

* 🚀 **[Download Installer (LumenWiZ-Setup-v1.0.0.exe)](https://github.com/Mauri9291/LumenWiZ/releases/download/v1.0.0/LumenWiZ-Setup-v1.0.0.exe)**  
  *(Recommended)* Guided Windows installer with Start Menu/Desktop shortcuts and automatic start with Windows minimized to tray.

* 📦 **[Download Portable Version (.zip)](https://github.com/Mauri9291/LumenWiZ/releases/download/v1.0.0/LumenWiZ-v1.0.0-Portable-win-x64.zip)**  
  *(Zero install)* Extract and run `LumenWiZ.UI.exe` directly. Self-contained; does not require .NET to be pre-installed.

---

## 🔌 Local REST API for Stream Deck

Endpoints available at `http://localhost:38898`:

| Action | Method | Endpoint |
|---|---|---|
| **Get status & rooms** | `GET` | `/api/rooms` |
| **Toggle room power** | `POST` | `/api/rooms/{roomId}/toggle?state=true` |
| **Set room brightness** | `POST` | `/api/rooms/{roomId}/brightness?value=80` |
| **Activate scene** | `POST` | `/api/scenes/activate?name={sceneName}` |

---

## 🙏 Credits & Acknowledgments

* **WiZ Local Protocol Research**: Heartfelt thanks to the open-source community that reverse-engineered and documented the local UDP protocol used by WiZ lights, especially the developers behind **[pywizlight](https://github.com/sbidy/pywizlight)** (Stephan Rinkes) and Home Assistant contributors.
* **[WPF-UI](https://github.com/lepoco/wpfui)**: Modern Fluent Windows 11 controls by Lepoco.
* **[Hardcodet NotifyIcon for WPF](https://github.com/hardcodet/wpf-notifyicon)**: System tray icon framework by Philipp Sumi.
* **[NAudio](https://github.com/naudio/NAudio)**: Windows audio WASAPI loopback capture library by Mark Heath.

---

## 📄 License & Disclaimer

This project is licensed under the MIT License.

*WiZ Connected is a registered trademark of Signify N.V. LumenWiZ is an independent open-source project and is not affiliated with, endorsed by, or associated with Signify.*
