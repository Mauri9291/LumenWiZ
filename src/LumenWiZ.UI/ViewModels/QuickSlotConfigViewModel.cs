using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LumenWiZ.Core.Models;
using LumenWiZ.Core.Storage;
using LumenWiZ.UI.Common;

namespace LumenWiZ.UI.ViewModels;

public class QuickSceneOptionItem
{
    public string DisplayText { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "💡";
    public int? SceneId { get; set; }
    public string? CustomSceneName { get; set; }
    public bool IsAudioSync { get; set; }

    public override string ToString() => DisplayText;
}

public class QuickSlotConfigViewModel : INotifyPropertyChanged
{
    private readonly int _slotIndex;
    private readonly ConfigStorage _configStorage;
    private readonly Action _onChangedCallback;

    private QuickSceneOptionItem? _selectedOption;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SlotNumber => _slotIndex + 1;
    public string SlotTitle => LocalizationService.Instance.CurrentLanguage == "en" ? $"Slot {SlotNumber}" : $"Ranura {SlotNumber}";

    public ObservableCollection<QuickSceneOptionItem> AvailableOptions { get; } = new();

    public QuickSceneOptionItem? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (_selectedOption != value && value != null)
            {
                _selectedOption = value;
                OnPropertyChanged();
                SaveSlotAssignment(value);
            }
        }
    }

    public QuickSlotConfigViewModel(
        int slotIndex,
        ConfigStorage configStorage,
        Action onChangedCallback)
    {
        _slotIndex = slotIndex;
        _configStorage = configStorage;
        _onChangedCallback = onChangedCallback;

        LocalizationService.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(SlotTitle));
        };
    }

    public void RefreshOptions(List<QuickSceneOptionItem> options, QuickSceneItemConfig? currentAssignment)
    {
        AvailableOptions.Clear();
        foreach (var opt in options)
        {
            AvailableOptions.Add(opt);
        }

        if (currentAssignment != null)
        {
            QuickSceneOptionItem? match = null;
            if (currentAssignment.IsAudioSync)
            {
                match = AvailableOptions.FirstOrDefault(o => o.IsAudioSync);
            }
            else if (!string.IsNullOrEmpty(currentAssignment.CustomSceneName))
            {
                match = AvailableOptions.FirstOrDefault(o => o.CustomSceneName == currentAssignment.CustomSceneName);
            }
            else if (currentAssignment.SceneId.HasValue)
            {
                match = AvailableOptions.FirstOrDefault(o => o.SceneId == currentAssignment.SceneId);
            }

            _selectedOption = match ?? AvailableOptions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedOption));
        }
    }

    private async void SaveSlotAssignment(QuickSceneOptionItem chosen)
    {
        try
        {
            var cfg = await _configStorage.LoadConfigAsync();
            while (cfg.QuickScenes.Count <= _slotIndex)
            {
                cfg.QuickScenes.Add(new QuickSceneItemConfig());
            }

            cfg.QuickScenes[_slotIndex] = new QuickSceneItemConfig
            {
                Name = chosen.Name,
                Icon = chosen.Icon,
                SceneId = chosen.SceneId,
                CustomSceneName = chosen.CustomSceneName,
                IsAudioSync = chosen.IsAudioSync
            };

            await _configStorage.SaveConfigAsync(cfg);
            _onChangedCallback?.Invoke();
        }
        catch { }
    }

    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
