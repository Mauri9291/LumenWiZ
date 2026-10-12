using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Storage;
using LumenWiZ.UI.Common;

namespace LumenWiZ.UI.ViewModels;

public class SceneItemViewModel : INotifyPropertyChanged
{
    private readonly WizCustomScene _scene;
    private readonly ConfigStorage _configStorage;
    private readonly Action<SceneItemViewModel> _applyAction;
    private readonly Action<SceneItemViewModel> _deleteAction;
    private readonly Action _onChangedAction;

    private bool _isEditing;
    private string _editNameText = string.Empty;
    private string _editIconText = "💡";

    public event PropertyChangedEventHandler? PropertyChanged;

    public WizCustomScene Model => _scene;
    public string Id => _scene.Id;
    public int RoomId => _scene.RoomId;
    public int DeviceCount => _scene.DeviceStates.Count;

    public string Name
    {
        get => _scene.Name;
        set
        {
            if (_scene.Name != value)
            {
                _scene.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Icon
    {
        get => string.IsNullOrEmpty(_scene.Icon) ? "💡" : _scene.Icon;
        set
        {
            if (_scene.Icon != value)
            {
                _scene.Icon = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowInQuickScenes
    {
        get => _scene.ShowInQuickScenes;
        set
        {
            if (_scene.ShowInQuickScenes != value)
            {
                _scene.ShowInQuickScenes = value;
                OnPropertyChanged();
                _ = SaveConfigAsync();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    public string EditNameText
    {
        get => _editNameText;
        set { _editNameText = value; OnPropertyChanged(); }
    }

    public string EditIconText
    {
        get => _editIconText;
        set { _editIconText = value; OnPropertyChanged(); }
    }

    public ICommand StartEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand SelectEmojiCommand { get; }
    public ICommand ToggleQuickScenesCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand DeleteCommand { get; }

    public SceneItemViewModel(
        WizCustomScene scene, 
        ConfigStorage configStorage,
        Action<SceneItemViewModel> applyAction,
        Action<SceneItemViewModel> deleteAction,
        Action onChangedAction)
    {
        _scene = scene;
        _configStorage = configStorage;
        _applyAction = applyAction;
        _deleteAction = deleteAction;
        _onChangedAction = onChangedAction;

        _editNameText = scene.Name;
        _editIconText = string.IsNullOrEmpty(scene.Icon) ? "💡" : scene.Icon;

        StartEditCommand = new RelayCommand(() =>
        {
            EditNameText = Name;
            EditIconText = Icon;
            IsEditing = true;
        });

        SaveEditCommand = new RelayCommand(async () =>
        {
            if (!string.IsNullOrWhiteSpace(EditNameText))
            {
                Name = EditNameText.Trim();
                Icon = string.IsNullOrWhiteSpace(EditIconText) ? "💡" : EditIconText.Trim();
                await SaveConfigAsync();
            }
            IsEditing = false;
        });

        CancelEditCommand = new RelayCommand(() =>
        {
            IsEditing = false;
        });

        SelectEmojiCommand = new RelayCommand(param =>
        {
            if (param is string emoji)
            {
                EditIconText = emoji;
            }
        });

        ToggleQuickScenesCommand = new RelayCommand(() =>
        {
            ShowInQuickScenes = !ShowInQuickScenes;
        });

        ApplyCommand = new RelayCommand(() => _applyAction(this));
        DeleteCommand = new RelayCommand(() => _deleteAction(this));
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            var cfg = await _configStorage.LoadConfigAsync();
            var existing = cfg.CustomScenes.FirstOrDefault(s => s.Id == _scene.Id || s.Name == _scene.Name);
            if (existing != null)
            {
                existing.Name = Name;
                existing.Icon = Icon;
                existing.ShowInQuickScenes = ShowInQuickScenes;
            }
            await _configStorage.SaveConfigAsync(cfg);
            _onChangedAction?.Invoke();
        }
        catch { }
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
