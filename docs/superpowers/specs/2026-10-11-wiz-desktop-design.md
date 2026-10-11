# Especificación de Diseño: WiZ Desktop (LumenWiZ)

**Fecha:** 11 de Octubre de 2026  
**Estado:** Propuesta para Aprobación  
**Tipo de Proyecto:** Aplicación de Escritorio 100% Nativa para Windows (Código Abierto)

---

## 1. Intención y Propósito ("The Why")

No existe una aplicación de escritorio oficial ni moderna para controlar luces WiZ (WiZ Connected by Signify) en Windows. Los usuarios se ven obligados a usar comandos de consola o emuladores de Android.

El objetivo de este proyecto es construir **LumenWiZ**: una aplicación de escritorio 100% nativa para Windows, ultraligera, con diseño de grado industrial (inspirada en la estética minimalista de la app de control de aire de Ilter Canberk) y de muy bajo consumo de recursos en segundo plano (< 15-20 MB de RAM).

La aplicación resuelve tres necesidades clave:
1. **Control Rápido Táctil:** Acceso instantáneo desde la bandeja del sistema (System Tray, al lado del reloj) mediante un popover flotante sin abrir ventanas pesadas.
2. **Control Completo por Habitación y Foco Individual:** Gestión de escenarios/habitaciones y calibración fina de cada dispositivo (bombillas individuales y tiras LED).
3. **Funcionalidades Únicas de Escritorio:**
   - **Sincronización de Audio (WASAPI Loopback):** Luces reactivas a la música (Spotify/Gaming/YouTube) capturando el audio digital interno de Windows sin depender del micrófono.
   - **Captura Local de Escenas:** Guardado instantáneo de la escena activa en la habitación mediante lectura UDP local en 1 milisegundo (privacidad total, sin requerir credenciales en la nube).
   - **Módulo de Sincronización en la Nube (Opcional):** Arquitectura preparada para vincular cuenta WiZ Cloud para quienes deseen descargar sus escenas ya creadas en la app móvil.
   - **Soporte para Stream Deck y Atajos:** API local ligera para control con teclado o botones macro.

---

## 2. Metas, No-Metas y Anti-Metas

### Metas (Goals)
- **100% Nativa en Windows:** C# / .NET 9 con interfaz XAML nativa (WPF / Windows 11 Fluent Design con desenfoque Mica/Acrylic). Cero Electron, cero Chromium/WebView.
- **Comunicación Local por UDP (Puerto 38899):** Envío y recepción de JSON-RPC a las bombillas en la red local. Respuesta instantánea (< 5 ms).
- **Auto-Detección en Red:** Escaneo por broadcast UDP para encontrar automáticamente todas las bombillas y tiras LED y agruparlas por `roomId`.
- **Arquitectura de Interfaz en 2 Niveles:**
  - *Nivel 1 (Popover de Barra):* Widget compacto oscuro pegado al reloj con slider de brillo para todo el cuarto, pastillas de escenas rápidas y botón de configuración.
  - *Nivel 2 (Ventana de Gestión):* Ventana completa para ver cada foco por separado, calibrar colores individuales, gestionar habitaciones y configurar la sincronización de audio.
- **Audio Reactivo Real:** Captura de audio de salida de Windows mediante WASAPI Loopback con análisis de frecuencias (FFT) para graves y agudos.
- **Captura de Escenas Local:** Botón para tomar un "snapshot" de los valores de color y brillo de todas las luces del cuarto y guardarlo como escena personalizada.

### No-Metas (Non-Goals)
- No busca reemplazar la configuración inicial de Wi-Fi de las bombillas (esa configuración inicial de emparejamiento con el router se hace una sola vez en la app de celular o por Wi-Fi AP).
- No requiere conexión a internet obligatoria para funcionar (el 100% de las funciones de control, escenas y música funcionan en red local sin internet).

### Anti-Metas (Anti-Goals - Lo que NO debe ser)
- **NO debe ser una Web App empaquetada:** Prohibido usar Electron, Tauri con WebView pesado, o navegadores embebidos que consuman cientos de megabytes de RAM.
- **NO debe obligar al usuario a meter sus credenciales:** La privacidad y el funcionamiento local son la prioridad #1. El login en la nube de WiZ será estrictamente opcional.
- **NO debe tener diseño genérico de IA:** Nada de plantillas estándar con estilos web planos; debe tener la estética pulida, oscura y táctil de Windows 11.

---

## 3. Restricciones Técnicas

1. **Lenguaje y Plataforma:** .NET 9 / C# (Windows 10/11 x64).
2. **Interfaz de Usuario:** WPF con temática Fluent / ModernWpf o Windows App SDK con soporte de transparencias reales del sistema operativo (Mica / Acrylic).
3. **Consumo de Memoria:** < 15-20 MB en segundo plano mientras está en la bandeja del sistema.
4. **Protocolo WiZ:** Paquetes UDP unidireccionales y bidireccionales en el puerto `38899` con formato JSON (`setPilot`, `getPilot`, `getSystemConfig`).
5. **Captura de Audio:** `NAudio` / `CSCore` utilizando `WasapiLoopbackCapture` para obtener el stream de audio maestro de Windows a baja latencia (< 20 ms).

---

## 4. Decisiones Tomadas con el Usuario y Resoluciones Técnicas

- **Icono del Tray:** Un foco elegante.
- **Alcance de Fases (Captura Local vs Nube):**
  - **Fase 1 (Inmediata):** 100% de enfoque en la captura local de escenas (snapshots UDP instantáneos) y control local offline sin credenciales.
  - **Fase 2:** Implementación del proveedor de sincronización en la nube (`ISceneSyncProvider`) conectando con los endpoints de WiZ Cloud investigados.
- **Superficie de Integración para Stream Deck:**
  - Servidor HTTP local ligero embebido en la app (`http://localhost:38898/api/`) con endpoints REST sencillos (`/api/power/toggle`, `/api/scenes/activate?name=...`, `/api/brightness?val=...`), permitiendo que el plugin oficial de Stream Deck de peticiones web o macros lo invoquen al instante.
- **Comportamiento del Popover (Nivel 1):**
  - Controla la habitación activa (donde está el PC o la preferida) con un sutil selector desplegable al tocar el nombre del cuarto si existen varias habitaciones configuradas.
- **Comportamiento de la Ventana (Nivel 2):**
  - Cada foco y tira LED se lista **de forma individual** con su propio control de color, brillo y switch de audio sync.

---

## 5. Decisiones Delegadas al Desarrollador (Builder Calls)

- **Estructura de almacenamiento local:** Archivo `config.json` en `%APPDATA%\LumenWiZ\` para guardar nombres de cuartos, focos y escenas capturadas.
- **Algoritmo de mapeo de audio a luces:** Ecualizador de 3 bandas (Bass: 20-150Hz para pulsos de la tira LED; Mids/Treble: 150-8000Hz para modulación de color y bombillas).
- **Throttling UDP:** Límite de envío de 25 paquetes/segundo por foco durante el audio reactivo para no saturar el buffer del microcontrolador ESP de las bombillas.
- **Tecnología del UI:** .NET 9 con WPF y ModernWpfUI / Native Win32 Acrylic Backdrop para lograr el diseño idéntico a Windows 11 sin frameworks web pesados.
