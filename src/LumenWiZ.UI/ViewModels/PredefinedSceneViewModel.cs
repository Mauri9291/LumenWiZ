using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LumenWiZ.UI.Common;

namespace LumenWiZ.UI.ViewModels;

public class PredefinedSceneViewModel : INotifyPropertyChanged
{
    private readonly Action<PredefinedSceneViewModel> _applyAction;
    private readonly Action<PredefinedSceneViewModel> _togglePinAction;

    private int _sceneId;
    private string _name = string.Empty;
    private string _icon = "💡";
    private string _description = string.Empty;
    private bool _isAudioSync;
    private bool _isPinned = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SceneId
    {
        get => _sceneId;
        set { _sceneId = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public bool IsAudioSync
    {
        get => _isAudioSync;
        set { _isAudioSync = value; OnPropertyChanged(); }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set { _isPinned = value; OnPropertyChanged(); }
    }

    public ICommand ApplyCommand { get; }
    public ICommand TogglePinCommand { get; }

    public PredefinedSceneViewModel(
        int sceneId,
        string name,
        string icon,
        string description,
        bool isAudioSync,
        bool isPinned,
        Action<PredefinedSceneViewModel> applyAction,
        Action<PredefinedSceneViewModel> togglePinAction)
    {
        _sceneId = sceneId;
        _name = name;
        _icon = icon;
        _description = description;
        _isAudioSync = isAudioSync;
        _isPinned = isPinned;
        _applyAction = applyAction;
        _togglePinAction = togglePinAction;

        ApplyCommand = new RelayCommand(() => _applyAction(this));
        TogglePinCommand = new RelayCommand(() =>
        {
            IsPinned = !IsPinned;
            _togglePinAction(this);
        });
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
