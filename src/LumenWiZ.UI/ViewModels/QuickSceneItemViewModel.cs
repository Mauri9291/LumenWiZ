using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LumenWiZ.Core.Models;
using LumenWiZ.UI.Common;

namespace LumenWiZ.UI.ViewModels;

public class QuickSceneItemViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _icon = "💡";
    private int? _sceneId;
    private string? _customSceneName;
    private bool _isAudioSync;

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public int? SceneId
    {
        get => _sceneId;
        set { _sceneId = value; OnPropertyChanged(); }
    }

    public string? CustomSceneName
    {
        get => _customSceneName;
        set { _customSceneName = value; OnPropertyChanged(); }
    }

    public bool IsAudioSync
    {
        get => _isAudioSync;
        set { _isAudioSync = value; OnPropertyChanged(); }
    }

    public ICommand ActivateCommand { get; }

    public QuickSceneItemViewModel(QuickSceneItemConfig config, Action<QuickSceneItemViewModel> onActivate)
    {
        _name = config.Name;
        _icon = config.Icon;
        _sceneId = config.SceneId;
        _customSceneName = config.CustomSceneName;
        _isAudioSync = config.IsAudioSync;

        ActivateCommand = new RelayCommand(() => onActivate(this));
    }

    public QuickSceneItemConfig ToConfig() => new()
    {
        Name = Name,
        Icon = Icon,
        SceneId = SceneId,
        CustomSceneName = CustomSceneName,
        IsAudioSync = IsAudioSync
    };

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
