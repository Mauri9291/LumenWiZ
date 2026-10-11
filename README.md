# 💡 LumenWiZ — WiZ Lights Studio para Windows

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Plataforma](https://img.shields.io/badge/Plataforma-Windows%2010%20%2F%2011-blue.svg)](https://microsoft.com)
[![Licencia](https://img.shields.io/badge/Licencia-MIT-green.svg)](LICENSE)
[![Stream Deck](https://img.shields.io/badge/Integración-Stream%20Deck%20Ready-orange.svg)](#-integración-con-stream-deck)

> **Aplicación de escritorio 100% nativa de alto rendimiento para controlar bombillas y tiras LED WiZ Connected (Signify) en tu red local Wi-Fi, sin nube, con respuesta auditiva reactiva interna (Spotify/Juegos) y soporte para Stream Deck.**

---

## ✨ Características Principales

- 🪶 **100% Nativo & Ultraligero:** Construido con .NET 9, C# y WPF/ModernWpf. Sin Electron, sin Chrome embebido, sin WebView2. Consumo de memoria mínimo (< 20 MB en reposo).
- 🔒 **100% Local & Privado:** Comunicación UDP directa por el puerto estándar `38899` en tu red Wi-Fi doméstica. No requiere cuenta de Signify ni conexión a servidores externos.
- 🎛️ **Arquitectura de Dos Niveles:**
  1. **Nivel 1 (Popover en la Bandeja del Sistema):** Un icono de bombilla en la barra de tareas de Windows. Al hacer clic se despliega un panel flotante oscuro con el deslizador táctil del brillo maestro de la habitación, escenas rápidas (*Luz Diaria, Chimenea, Océano, Atardecer, Vela, Audio Sync*), interruptor maestro y botón de configuración rápida.
  2. **Nivel 2 (Estudio de Gestión Completo):** Ventana completa con barra lateral para ver cada bombilla y tira LED de forma individual con control fino de brillo, paleta rápida de colores, interruptor individual y selector de reactividad al sonido.
- 🎵 **Sincronización Reactiva de Audio WASAPI (Spotify & Juegos):**
  - Captura el flujo de audio digital interno de Windows directamente desde el render endpoint (Spotify, YouTube, Discord, videojuegos).
  - **Cero micrófonos:** no se ve afectado por ruido ambiente ni conversaciones.
  - Analizador FFT de 3 bandas (Graves 20-150Hz asignados a Tiras LED, Medios a bombillas principales, Agudos a lámparas ambientales).
  - Bucle de control con limitación estricta a **25 FPS** para proteger los microcontroladores ESP de las luces WiZ contra sobrecargas UDP.
- 📸 **Captura Instantánea de Escenas Locales:** Guarda el estado exacto de todos tus focos en 1 ms con un solo clic y aplícalo cuando quieras sin rehacer la configuración en la app móvil.
- 🎛️ **Servidor REST Local para Stream Deck:** API HTTP ligera en `http://localhost:38898/api/` lista para macros, botones de Stream Deck o atajos de teclado de Windows.

---

## 🚀 Inicio Rápido

### Ejecutables Compilados
En la carpeta de compilación se encuentran dos variantes:
1. **Versión Standalone (Zero Install):**
   - Ruta: `src/LumenWiZ.UI/bin/Release/net9.0-windows/win-x64/publish/LumenWiZ.UI.exe`
   - No requiere tener .NET instalado en el equipo.
2. **Versión Portable Ligera (5.3 MB):**
   - Ruta: `bin/LumenWiZ-Portable/LumenWiZ.UI.exe`
   - Requiere el runtime de .NET 9 en Windows.

### Compilar desde el Código Fuente
```bash
# Clonar el repositorio
git clone https://github.com/tu-usuario/LumenWiZ.git
cd LumenWiZ

# Restaurar dependencias y ejecutar pruebas
dotnet test

# Publicar versión nativa autoejecutable
dotnet publish src/LumenWiZ.UI/LumenWiZ.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 🎛️ Integración con Stream Deck

LumenWiZ incluye un servidor HTTP embebido en segundo plano. Puedes configurar tus botones de Stream Deck con la acción **System > Website / Open URL** o plugins como **API Ninja** / **HTTP Request**:

| Acción | Método HTTP | Endpoint |
|---|---|---|
| **Estado Global** | `GET` | `http://localhost:38898/api/status` |
| **Encender / Apagar Habitación** | `POST` | `http://localhost:38898/api/power/toggle?room=1` |
| **Ajustar Brillo de Habitación** | `POST` | `http://localhost:38898/api/brightness?value=80` |
| **Activar Escena Personalizada** | `POST` | `http://localhost:38898/api/scenes/activate?name=ModoCine` |

---

## 🏛️ Arquitectura del Proyecto

```
LumenWiZ/
├── src/
│   ├── LumenWiZ.Core/            # Lógica central reutilizable e independiente
│   │   ├── Models/              # DTOs WiZ, WizDevice, WizRoom, WizCustomScene
│   │   ├── Protocol/            # Serializador JSON-RPC UDP para WiZ (setPilot, getPilot)
│   │   ├── Network/             # Cliente UDP asíncrono con broadcast automático
│   │   ├── Services/            # DiscoveryService y SceneSnapshotService
│   │   ├── Audio/               # WASAPI Loopback Capture & Analizador FFT Goertzel
│   │   ├── Storage/             # Persistencia JSON local en %APPDATA%\LumenWiZ\
│   │   └── Server/              # Servidor REST HTTP embebido en puerto 38898
│   │
│   └── LumenWiZ.UI/              # Capa de presentación WPF moderna
│       ├── Common/              # RelayCommand y Converters XAML
│       ├── ViewModels/          # TrayPopoverViewModel, MainViewModel, DeviceItemViewModel
│       ├── Controls/            # DeviceCard (tarjeta con slider y selector por foco)
│       └── Views/               # TrayPopoverWindow (popover flotante de bandeja)
│
└── tests/
    └── LumenWiZ.Tests/          # Suite de pruebas unitarias automatizadas (xUnit)
```

---

## 📄 Licencia

Este proyecto está distribuido bajo la licencia MIT. Consulta el archivo `LICENSE` para más detalles.
WiZ Connected es una marca registrada de Signify N.V. Este proyecto es una utilidad de software independiente de código abierto y no está afiliado oficialmente con Signify.
