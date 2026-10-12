using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace LumenWiZ.UI.Common;

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private string _currentLanguage = "es";
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            string clean = value?.ToLowerInvariant() == "en" ? "en" : "es";
            if (_currentLanguage != clean)
            {
                _currentLanguage = clean;
                ApplyToApplicationResources();
                OnPropertyChanged(string.Empty);
                LanguageChanged?.Invoke();
            }
        }
    }

    public event Action? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Dictionary<string, string> _es = new()
    {
        // TitleBar & App
        ["Loc_AppTitle"] = "LumenWiZ",
        ["Loc_LightsConnected"] = "Luces Conectadas",
        ["Loc_StatusReady"] = "Listo",

        // Navigation Rail
        ["Loc_Nav_MyLights"] = "Mis Luces",
        ["Loc_Nav_AudioReactive"] = "Audio Reactivo",
        ["Loc_Nav_Scenes"] = "Escenas",
        ["Loc_Nav_SettingsApi"] = "Ajustes & API",

        // Rooms Sidebar
        ["Loc_Rooms"] = "HABITACIONES",
        ["Loc_Spaces"] = "ESPACIOS",
        ["Loc_NewRoom"] = "+ Nueva Habitación",
        ["Loc_NewRoomPlaceholder"] = "Nombre de habitación...",
        ["Loc_Create"] = "Crear",
        ["Loc_Cancel"] = "Cancelar",
        ["Loc_DeleteRoom"] = "Eliminar Habitación",
        ["Loc_DeleteRoomTooltip"] = "Eliminar esta habitación",
        ["Loc_GeneralRoom"] = "General",

        // View 0: Mis Luces
        ["Loc_Brightness"] = "Brillo:",
        ["Loc_SearchLights"] = "Buscar Luces",
        ["Loc_RoomPowerTooltip"] = "Encender / Apagar Habitación",
        ["Loc_TurnOnOffRoom"] = "Encender/Apagar toda la habitación",
        ["Loc_LightOn"] = "Luz Encendida",
        ["Loc_LightOff"] = "Luz Apagada",
        ["Loc_Temperature"] = "Temperatura",
        ["Loc_Color"] = "Color",
        ["Loc_Effect"] = "Efecto",
        ["Loc_RenameLight"] = "Renombrar luz",
        ["Loc_Save"] = "Guardar",
        ["Loc_RoomTooltip"] = "Habitación asignada a este foco",

        // View 1: Audio Reactivo
        ["Loc_AudioReactive_Title"] = "Audio Reactivo",
        ["Loc_AudioDevice_Title"] = "Dispositivo de Salida de Audio",
        ["Loc_AudioDevice_Subtitle"] = "Selecciona qué salida de sonido de tu PC escuchará LumenWiZ",
        ["Loc_StartAudioSync"] = "▶ Iniciar Audio Reactivo",
        ["Loc_StopAudioSync"] = "⏹ Detener Audio Reactivo",
        ["Loc_Refresh"] = "🔄 Actualizar",
        ["Loc_BandAssignment"] = "ASIGNACIÓN DE BANDAS DE FRECUENCIA",
        ["Loc_Bass"] = "Graves",
        ["Loc_Mids"] = "Medios",
        ["Loc_Treble"] = "Agudos",
        ["Loc_FullSpectrum"] = "Todo el Espectro",
        ["Loc_IgnoreAudio"] = "Ignorar Audio",

        // View 2: Escenas
        ["Loc_Scenes_Title"] = "Gestor de Escenas",
        ["Loc_Tab_Predefined"] = "Escenas Predeterminadas",
        ["Loc_Tab_Saved"] = "Escenas Guardadas",
        ["Loc_Tab_QuickMenu"] = "Menú Rápido (6 Ranuras)",
        ["Loc_CaptureScene_Title"] = "Capturar Escena Actual",
        ["Loc_CaptureScene_Subtitle"] = "Guarda el color, brillo y estado exacto de todas las luces de esta habitación",
        ["Loc_ChooseSceneIcon"] = "Elegir icono para la escena:",
        ["Loc_CaptureCurrentState"] = "📸 Capturar Estado Actual",
        ["Loc_SavedLightsCount"] = "{0} luces memorizadas",
        ["Loc_SceneNamePlaceholder"] = "Nombre de la escena (ej. Noche de Cine, Relax)...",
        ["Loc_SaveScene"] = "Guardar Escena",
        ["Loc_ActivateScene"] = "Activar Escena",
        ["Loc_Activate"] = "Activar",
        ["Loc_Delete"] = "Eliminar",
        ["Loc_QuickCustomizer_Title"] = "⚡ Personalizador del Menú Rápido de la Barra de Tareas",
        ["Loc_QuickCustomizer_Subtitle"] = "Elige qué 6 escenas quieres tener accesibles con un solo clic en la bandeja de Windows.",
        ["Loc_SlotQuickButton"] = "Botón en menú rápido",

        // View 3: Ajustes Generales & API REST
        ["Loc_GeneralSettings_Title"] = "Ajustes Generales",
        ["Loc_GeneralSettings_Subtitle"] = "Preferencias del sistema, idioma y comportamiento al iniciar Windows",
        ["Loc_Language"] = "Idioma de la Aplicación",
        ["Loc_Language_Help"] = "Cambia la interfaz entre Español e Inglés al instante",
        ["Loc_StartWithWindows"] = "Iniciar con Windows",
        ["Loc_StartWithWindows_Help"] = "Abre LumenWiZ automáticamente cuando inicias sesión en tu PC",
        ["Loc_StartMinimized"] = "Iniciar minimizado en la bandeja",
        ["Loc_StartMinimized_Help"] = "Mantiene la aplicación en segundo plano en la bandeja sin abrir la ventana",
        ["Loc_StreamDeck_Title"] = "Stream Deck & API REST Local",
        ["Loc_StreamDeck_Subtitle"] = "Controla LumenWiZ desde Stream Deck, scripts de PowerShell o atajos mediante HTTP",
        ["Loc_ServerActive"] = "🟢 Servidor REST Local Activo",
        ["Loc_ListeningOn"] = "Escuchando en: http://localhost:38898/api/",
        ["Loc_AvailableEndpoints"] = "ENDPOINTS DISPONIBLES (COMPATIBLE CON STREAM DECK):",
        ["Loc_EndpointStatus"] = "Devuelve el estado JSON de todas las luces y habitaciones descubiertas.",
        ["Loc_EndpointPower"] = "Enciende o apaga todas las luces de la habitación especificada.",
        ["Loc_EndpointBrightness"] = "Ajusta el brillo global de la habitación activa (10 a 100%).",
        ["Loc_EndpointScenes"] = "Aplica al instante una de tus escenas personalizadas guardadas.",

        // Popover
        ["Loc_RoomBrightness"] = "BRILLO HABITACIÓN",
        ["Loc_QuickScenes"] = "ESCENAS RÁPIDAS",
        ["Loc_Edit6Slots"] = "EDITAR 6 RANURAS RÁPIDAS",
        ["Loc_Edit"] = "Editar",
        ["Loc_Done"] = "Listo",
        ["Loc_SaveAndExit"] = "Guardar y Salir",
        ["Loc_OpenFullStudio"] = "⚙️ Abrir WiZ Studio Completo",
        ["Loc_ActiveLights"] = "{0} luces activas",

        // Tray Context Menu
        ["Loc_Tray_OpenStudio"] = "💡 Abrir WiZ Lights Studio",
        ["Loc_Tray_QuickControl"] = "⚡ Control Rápido",
        ["Loc_Tray_Exit"] = "❌ Salir",

        // WiZ Preset Scene Names
        ["Scene_Cozy"] = "Luz Cálida",
        ["Scene_Daylight"] = "Luz Diaria",
        ["Scene_CoolWhite"] = "Luz Fría",
        ["Scene_NightLight"] = "Luz de Noche",
        ["Scene_Fireplace"] = "Chimenea",
        ["Scene_Sunset"] = "Atardecer",
        ["Scene_Party"] = "Fiesta",
        ["Scene_Relax"] = "Relax",
        ["Scene_Focus"] = "Concentración",
        ["Scene_Candlelight"] = "Vela",
        ["Scene_Ocean"] = "Océano",
        ["Scene_Romance"] = "Romance",
        ["Scene_Forest"] = "Bosque"
    };

    private readonly Dictionary<string, string> _en = new()
    {
        // TitleBar & App
        ["Loc_AppTitle"] = "LumenWiZ",
        ["Loc_LightsConnected"] = "Lights Connected",
        ["Loc_StatusReady"] = "Ready",

        // Navigation Rail
        ["Loc_Nav_MyLights"] = "My Lights",
        ["Loc_Nav_AudioReactive"] = "Audio Reactive",
        ["Loc_Nav_Scenes"] = "Scenes",
        ["Loc_Nav_SettingsApi"] = "Settings & API",

        // Rooms Sidebar
        ["Loc_Rooms"] = "ROOMS",
        ["Loc_Spaces"] = "SPACES",
        ["Loc_NewRoom"] = "+ New Room",
        ["Loc_NewRoomPlaceholder"] = "Room name...",
        ["Loc_Create"] = "Create",
        ["Loc_Cancel"] = "Cancel",
        ["Loc_DeleteRoom"] = "Delete Room",
        ["Loc_DeleteRoomTooltip"] = "Delete this room",
        ["Loc_GeneralRoom"] = "General",

        // View 0: My Lights
        ["Loc_Brightness"] = "Brightness:",
        ["Loc_SearchLights"] = "Search Lights",
        ["Loc_RoomPowerTooltip"] = "Turn On / Off Room",
        ["Loc_TurnOnOffRoom"] = "Turn On/Off whole room",
        ["Loc_LightOn"] = "Light On",
        ["Loc_LightOff"] = "Light Off",
        ["Loc_Temperature"] = "Temperature",
        ["Loc_Color"] = "Color",
        ["Loc_Effect"] = "Effect",
        ["Loc_RenameLight"] = "Rename light",
        ["Loc_Save"] = "Save",
        ["Loc_RoomTooltip"] = "Room assigned to this bulb",

        // View 1: Audio Reactive
        ["Loc_AudioReactive_Title"] = "Audio Reactive",
        ["Loc_AudioDevice_Title"] = "Audio Output Device",
        ["Loc_AudioDevice_Subtitle"] = "Select which PC audio output LumenWiZ will listen to",
        ["Loc_StartAudioSync"] = "▶ Start Audio Reactive",
        ["Loc_StopAudioSync"] = "⏹ Stop Audio Reactive",
        ["Loc_Refresh"] = "🔄 Refresh",
        ["Loc_BandAssignment"] = "FREQUENCY BAND ASSIGNMENT",
        ["Loc_Bass"] = "Bass",
        ["Loc_Mids"] = "Mids",
        ["Loc_Treble"] = "Treble",
        ["Loc_FullSpectrum"] = "Full Spectrum",
        ["Loc_IgnoreAudio"] = "Ignore Audio",

        // View 2: Scenes
        ["Loc_Scenes_Title"] = "Scenes Manager",
        ["Loc_Tab_Predefined"] = "Predefined Scenes",
        ["Loc_Tab_Saved"] = "Saved Scenes",
        ["Loc_Tab_QuickMenu"] = "Quick Menu (6 Slots)",
        ["Loc_CaptureScene_Title"] = "Capture Current Scene",
        ["Loc_CaptureScene_Subtitle"] = "Saves the exact color, brightness and state of all lights in this room",
        ["Loc_ChooseSceneIcon"] = "Choose scene icon:",
        ["Loc_CaptureCurrentState"] = "📸 Capture Current State",
        ["Loc_SavedLightsCount"] = "{0} lights memorized",
        ["Loc_SceneNamePlaceholder"] = "Scene name (e.g. Movie Night, Relax)...",
        ["Loc_SaveScene"] = "Save Scene",
        ["Loc_ActivateScene"] = "Activate Scene",
        ["Loc_Activate"] = "Activate",
        ["Loc_Delete"] = "Delete",
        ["Loc_QuickCustomizer_Title"] = "⚡ Taskbar Quick Menu Customizer",
        ["Loc_QuickCustomizer_Subtitle"] = "Choose which 6 scenes you want accessible with one click in the Windows tray.",
        ["Loc_SlotQuickButton"] = "Button in quick menu",

        // View 3: General Settings & REST API
        ["Loc_GeneralSettings_Title"] = "General Settings",
        ["Loc_GeneralSettings_Subtitle"] = "System preferences, language and Windows startup behavior",
        ["Loc_Language"] = "Application Language",
        ["Loc_Language_Help"] = "Switch interface between Spanish and English instantly",
        ["Loc_StartWithWindows"] = "Start with Windows",
        ["Loc_StartWithWindows_Help"] = "Launch LumenWiZ automatically when you log into your PC",
        ["Loc_StartMinimized"] = "Start minimized to tray",
        ["Loc_StartMinimized_Help"] = "Keep the application in the background in tray without opening the window",
        ["Loc_StreamDeck_Title"] = "Stream Deck & Local REST API",
        ["Loc_StreamDeck_Subtitle"] = "Control LumenWiZ from Stream Deck, PowerShell scripts or HTTP shortcuts",
        ["Loc_ServerActive"] = "🟢 Local REST Server Active",
        ["Loc_ListeningOn"] = "Listening on: http://localhost:38898/api/",
        ["Loc_AvailableEndpoints"] = "AVAILABLE ENDPOINTS (STREAM DECK COMPATIBLE):",
        ["Loc_EndpointStatus"] = "Returns the JSON status of all discovered lights and rooms.",
        ["Loc_EndpointPower"] = "Turns on or off all lights in the specified room.",
        ["Loc_EndpointBrightness"] = "Adjusts the master brightness of the active room (10 to 100%).",
        ["Loc_EndpointScenes"] = "Instantly applies one of your saved custom scenes.",

        // Popover
        ["Loc_RoomBrightness"] = "ROOM BRIGHTNESS",
        ["Loc_QuickScenes"] = "QUICK SCENES",
        ["Loc_Edit6Slots"] = "EDIT 6 QUICK SLOTS",
        ["Loc_Edit"] = "Edit",
        ["Loc_Done"] = "Done",
        ["Loc_SaveAndExit"] = "Save and Exit",
        ["Loc_OpenFullStudio"] = "⚙️ Open Full WiZ Studio",
        ["Loc_ActiveLights"] = "{0} active lights",

        // Tray Context Menu
        ["Loc_Tray_OpenStudio"] = "💡 Open WiZ Lights Studio",
        ["Loc_Tray_QuickControl"] = "⚡ Quick Control",
        ["Loc_Tray_Exit"] = "❌ Exit",

        // WiZ Preset Scene Names
        ["Scene_Cozy"] = "Warm White",
        ["Scene_Daylight"] = "Daylight",
        ["Scene_CoolWhite"] = "Cool White",
        ["Scene_NightLight"] = "Night Light",
        ["Scene_Fireplace"] = "Fireplace",
        ["Scene_Sunset"] = "Sunset",
        ["Scene_Party"] = "Party",
        ["Scene_Relax"] = "Relax",
        ["Scene_Focus"] = "Focus",
        ["Scene_Candlelight"] = "Candlelight",
        ["Scene_Ocean"] = "Ocean",
        ["Scene_Romance"] = "Romance",
        ["Scene_Forest"] = "Forest"
    };

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        var dict = _currentLanguage == "en" ? _en : _es;
        if (dict.TryGetValue(key, out var val))
        {
            return val;
        }
        if (_es.TryGetValue(key, out var fallback))
        {
            return fallback;
        }
        return key;
    }

    public static string Tr(string key) => Instance.Get(key);

    public static string TrFormat(string key, params object[] args)
    {
        try
        {
            return string.Format(Instance.Get(key), args);
        }
        catch
        {
            return Instance.Get(key);
        }
    }

    public void ApplyToApplicationResources()
    {
        if (Application.Current == null) return;

        var dict = _currentLanguage == "en" ? _en : _es;
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var kvp in dict)
            {
                Application.Current.Resources[kvp.Key] = kvp.Value;
            }
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
