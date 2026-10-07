using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Mana.Models;
using Mana.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Mana.ViewModels;

public sealed class KeyboardItemViewModel : INotifyPropertyChanged
{
    private bool _isHidden;
    private int? _editType;
    private int? _editSubtype;
    private LayoutPreset? _selectedPreset;
    private string _layoutDisplayName = string.Empty;
    private string _sourceDisplayName = string.Empty;
    private OverrideSource _effectiveSource = OverrideSource.AutoDetect;

    public KeyboardItemViewModel(KeyboardDeviceInfo info, GlobalOverrideValues global, bool isHidden)
    {
        InstancePath = info.InstancePath;
        FriendlyName = info.FriendlyName;
        ShellFriendlyName = info.ShellFriendlyName ?? string.Empty;
        HasShellFriendlyName = !string.IsNullOrWhiteSpace(info.ShellFriendlyName);
        HardwareIds = info.HardwareIds ?? string.Empty;
        InstanceKeyHashText = info.InstanceKeyHashText;
        ModelKeyHashText = info.ModelKeyHashText;
        Manufacturer = info.Manufacturer ?? string.Empty;
        DeviceType = info.DeviceOverride.Type;
        DeviceSubtype = info.DeviceOverride.Subtype;
        _isHidden = isHidden;
        _editType = DeviceType;
        _editSubtype = DeviceSubtype;

        var thumb = KeyboardNoteStore.FindFirstImageForDevice(InstancePath);
        ThumbnailPath = thumb;
        HasThumbnail = !string.IsNullOrWhiteSpace(thumb) && File.Exists(thumb);
        if (HasThumbnail)
        {
            ThumbnailImage = new BitmapImage(new Uri(thumb!));
        }

        RefreshEffective(global);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string InstancePath { get; }
    public string FriendlyName { get; }
    public string ShellFriendlyName { get; }
    public bool HasShellFriendlyName { get; }
    public string HardwareIds { get; }
    public string? InstanceKeyHashText { get; }
    public string? ModelKeyHashText { get; }
    public string Manufacturer { get; }
    public string? ThumbnailPath { get; }
    public BitmapImage? ThumbnailImage { get; }
    public bool HasThumbnail { get; }
    public Visibility ThumbnailImageVisibility => HasThumbnail ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoThumbnailLabelVisibility => HasThumbnail ? Visibility.Collapsed : Visibility.Visible;
    public string HideTooltip => Localization.Get("Tooltip_HideDevice");
    public string NoImageLabel => Localization.Get("Stack_NoImage");
    public string OpenDetailTooltip => Localization.Get("Tooltip_OpenDeviceDetail");
    public int? DeviceType { get; private set; }
    public int? DeviceSubtype { get; private set; }
    public bool HasDeviceOverride => DeviceType.HasValue || DeviceSubtype.HasValue;

    /// <summary>
    /// ACPI 経由の PS/2 キーボード。配列がグローバル設定だけで決まり、
    /// デバイスごとの設定が効かないことがある。
    /// </summary>
    public bool IsAcpiPs2 => InstancePath.StartsWith(@"ACPI\", StringComparison.OrdinalIgnoreCase);

    public bool IsHidden
    {
        get => _isHidden;
        set => SetField(ref _isHidden, value);
    }

    public int? EditType
    {
        get => _editType;
        set
        {
            if (SetField(ref _editType, value))
            {
                SyncPresetFromValues();
            }
        }
    }

    public int? EditSubtype
    {
        get => _editSubtype;
        set
        {
            if (SetField(ref _editSubtype, value))
            {
                SyncPresetFromValues();
            }
        }
    }

    public LayoutPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetField(ref _selectedPreset, value) && value is not null && !value.IsCustom)
            {
                _editType = value.Type;
                _editSubtype = value.Subtype;
                OnPropertyChanged(nameof(EditType));
                OnPropertyChanged(nameof(EditSubtype));
            }
        }
    }

    public string LayoutDisplayName
    {
        get => _layoutDisplayName;
        private set => SetField(ref _layoutDisplayName, value);
    }

    public string SourceDisplayName
    {
        get => _sourceDisplayName;
        private set => SetField(ref _sourceDisplayName, value);
    }

    /// <summary>一覧の種別・ソース表示（間の空白も含めて下線を引く用）。</summary>
    public string LayoutSourceDisplayName => $"{LayoutDisplayName}\u3000\u3000{SourceDisplayName}";

    public OverrideSource EffectiveSource
    {
        get => _effectiveSource;
        private set => SetField(ref _effectiveSource, value);
    }

    public int? EffectiveType { get; private set; }
    public int? EffectiveSubtype { get; private set; }

    public void RefreshEffective(GlobalOverrideValues global)
    {
        // グローバル Type/Subtype があると、デバイスごとの値はレジストリに書けても反映されない。
        if (global.Type.HasValue || global.Subtype.HasValue)
        {
            EffectiveSource = OverrideSource.Global;
            EffectiveType = global.Type;
            EffectiveSubtype = global.Subtype;
        }
        else if (HasDeviceOverride)
        {
            EffectiveSource = OverrideSource.Device;
            EffectiveType = DeviceType;
            EffectiveSubtype = DeviceSubtype;
        }
        else
        {
            EffectiveSource = OverrideSource.AutoDetect;
            EffectiveType = null;
            EffectiveSubtype = null;
        }

        LayoutDisplayName = LayoutCatalog.ResolveDisplayName(EffectiveType, EffectiveSubtype, Localization.Get);
        SourceDisplayName = EffectiveSource switch
        {
            OverrideSource.Device => Localization.Get("Source_Device"),
            OverrideSource.Global => Localization.Get("Source_Global"),
            _ => Localization.Get("Source_Auto")
        };

        SyncPresetFromValues();
        OnPropertyChanged(nameof(EffectiveType));
        OnPropertyChanged(nameof(EffectiveSubtype));
        OnPropertyChanged(nameof(LayoutDisplayName));
        OnPropertyChanged(nameof(SourceDisplayName));
        OnPropertyChanged(nameof(LayoutSourceDisplayName));
        OnPropertyChanged(nameof(EffectiveSource));
    }

    public void ApplyDeviceValues(int? type, int? subtype, GlobalOverrideValues global)
    {
        DeviceType = type;
        DeviceSubtype = subtype;
        EditType = type;
        EditSubtype = subtype;
        OnPropertyChanged(nameof(DeviceType));
        OnPropertyChanged(nameof(DeviceSubtype));
        OnPropertyChanged(nameof(HasDeviceOverride));
        RefreshEffective(global);
    }

    private void SyncPresetFromValues()
    {
        var match = LayoutCatalog.FindByTypeSubtype(EditType, EditSubtype);
        _selectedPreset = match ?? LayoutCatalog.Presets.First(p => p.IsCustom);
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private KeyboardItemViewModel? _selectedDevice;
    private GlobalOverrideValues _global = new();
    private bool _showHidden;
    private string _statusMessage = string.Empty;
    private string? _globalTypeText;
    private string? _globalSubtypeText;
    private string? _globalIdentifier;
    private string? _globalLayerDriverJpn;
    private string? _globalLayerDriverKor;
    private LayoutPreset? _selectedGlobalPreset;
    private HashSet<string> _hiddenPaths = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel()
    {
        Devices = [];
        VisibleDevices = [];
        Presets = new ObservableCollection<LayoutPreset>(LayoutCatalog.Presets);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<KeyboardItemViewModel> Devices { get; }
    public ObservableCollection<KeyboardItemViewModel> VisibleDevices { get; }
    public ObservableCollection<LayoutPreset> Presets { get; }

    public KeyboardItemViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set => SetField(ref _selectedDevice, value);
    }

    public bool ShowHidden
    {
        get => _showHidden;
        set
        {
            if (SetField(ref _showHidden, value))
            {
                RebuildVisible();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string? GlobalTypeText
    {
        get => _globalTypeText;
        set => SetField(ref _globalTypeText, value);
    }

    public string? GlobalSubtypeText
    {
        get => _globalSubtypeText;
        set => SetField(ref _globalSubtypeText, value);
    }

    public string? GlobalIdentifier
    {
        get => _globalIdentifier;
        set => SetField(ref _globalIdentifier, value);
    }

    public string? GlobalLayerDriverJpn
    {
        get => _globalLayerDriverJpn;
        set => SetField(ref _globalLayerDriverJpn, value);
    }

    public string? GlobalLayerDriverKor
    {
        get => _globalLayerDriverKor;
        set => SetField(ref _globalLayerDriverKor, value);
    }

    public LayoutPreset? SelectedGlobalPreset
    {
        get => _selectedGlobalPreset;
        set
        {
            if (SetField(ref _selectedGlobalPreset, value) && value is not null && !value.IsCustom)
            {
                ApplyGlobalPresetToEditors(value);
            }
        }
    }

    public string LanguageButtonLabel => LanguageService.ToggleButtonLabel;

    /// <summary>グローバル Type/Subtype が設定済み（デバイスごとの設定は反映されない状態）。</summary>
    public bool IsGlobalLayoutActive => _global.Type.HasValue || _global.Subtype.HasValue;

    public void Reload()
    {
        _hiddenPaths = HiddenDeviceStore.Load();
        _global = GlobalOverrideStore.Read();
        LoadGlobalEditorsFromStore();

        Devices.Clear();
        var enumerated = KeyboardEnumerator.Enumerate();
        KeyboardMetaStore.UpsertMany(enumerated);
        foreach (var info in enumerated)
        {
            Devices.Add(new KeyboardItemViewModel(info, _global, _hiddenPaths.Contains(info.InstancePath)));
        }

        RebuildVisible();
        SelectedDevice = VisibleDevices.FirstOrDefault() ?? Devices.FirstOrDefault();
        StatusMessage = string.Format(Localization.Get("Status_LoadedFormat"), Devices.Count);
        OnPropertyChanged(nameof(LanguageButtonLabel));
    }

    public void RefreshLocalization()
    {
        foreach (var device in Devices)
        {
            device.RefreshEffective(_global);
        }

        OnPropertyChanged(nameof(LanguageButtonLabel));
    }

    public void ToggleHiddenFilter()
    {
        ShowHidden = !ShowHidden;
    }

    public void PersistHiddenFlags()
    {
        var hidden = Devices.Where(d => d.IsHidden).Select(d => d.InstancePath);
        HiddenDeviceStore.Save(hidden);
        _hiddenPaths = HiddenDeviceStore.Load();
        RebuildVisible();
    }

    public void ApplySelectedDevice()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        try
        {
            DeviceOverrideStore.Write(SelectedDevice.InstancePath, SelectedDevice.EditType, SelectedDevice.EditSubtype);
            SelectedDevice.ApplyDeviceValues(SelectedDevice.EditType, SelectedDevice.EditSubtype, _global);
            AppSessionState.RegistryChanged = true;
            StatusMessage = IsGlobalLayoutActive
                ? Localization.Get("Status_DeviceAppliedGlobalActive")
                : Localization.Get("Status_DeviceApplied");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
        }
    }

    public void ClearSelectedDevice()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        try
        {
            DeviceOverrideStore.Clear(SelectedDevice.InstancePath);
            SelectedDevice.ApplyDeviceValues(null, null, _global);
            AppSessionState.RegistryChanged = true;
            StatusMessage = Localization.Get("Status_DeviceCleared");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
        }
    }

    public void ApplyGlobalPreset()
    {
        if (SelectedGlobalPreset is null || SelectedGlobalPreset.IsCustom)
        {
            ApplyGlobalIndividual();
            return;
        }

        try
        {
            GlobalOverrideStore.ApplyPreset(SelectedGlobalPreset);
            _global = GlobalOverrideStore.Read();
            LoadGlobalEditorsFromStore();
            RefreshAllEffective();
            AppSessionState.RegistryChanged = true;
            StatusMessage = Localization.Get("Status_GlobalPresetApplied");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
        }
    }

    /// <summary>
    /// ボタン操作後に書き込まれるグローバル値を予測する（書き込みはしない）。
    /// presetMode = true は「グローバルプリセット適用」、false は「グローバル値を保存」。
    /// </summary>
    public GlobalOverrideValues BuildPendingGlobalValues(bool presetMode)
    {
        if (presetMode && SelectedGlobalPreset is { IsCustom: false } preset)
        {
            return GlobalOverrideStore.BuildPresetValues(preset, GlobalOverrideStore.Read());
        }

        return new GlobalOverrideValues
        {
            Type = ParseNullableInt(GlobalTypeText),
            Subtype = ParseNullableInt(GlobalSubtypeText),
            Identifier = NullIfWhiteSpace(GlobalIdentifier),
            LayerDriverJpn = NullIfWhiteSpace(GlobalLayerDriverJpn),
            LayerDriverKor = NullIfWhiteSpace(GlobalLayerDriverKor)
        };
    }

    public void ApplyGlobalIndividual()
    {
        try
        {
            var values = BuildPendingGlobalValues(presetMode: false);
            GlobalOverrideStore.Write(values);
            _global = GlobalOverrideStore.Read();
            LoadGlobalEditorsFromStore();
            RefreshAllEffective();
            AppSessionState.RegistryChanged = true;
            StatusMessage = Localization.Get("Status_GlobalApplied");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
        }
    }

    public void ClearGlobal()
    {
        try
        {
            GlobalOverrideStore.ClearAll();
            _global = GlobalOverrideStore.Read();
            LoadGlobalEditorsFromStore();
            RefreshAllEffective();
            AppSessionState.RegistryChanged = true;
            StatusMessage = Localization.Get("Status_GlobalCleared");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
        }
    }

    private void ApplyGlobalPresetToEditors(LayoutPreset preset)
    {
        GlobalTypeText = preset.Type.ToString();
        GlobalSubtypeText = preset.Subtype.ToString();
        if (preset.ClearGlobalExtras)
        {
            GlobalIdentifier = string.Empty;
            GlobalLayerDriverJpn = string.Empty;
            GlobalLayerDriverKor = string.Empty;
            return;
        }

        if (!string.IsNullOrEmpty(preset.Identifier))
        {
            GlobalIdentifier = preset.Identifier;
        }

        if (string.Equals(preset.LayerDriverValueName, "LayerDriver JPN", StringComparison.OrdinalIgnoreCase))
        {
            GlobalLayerDriverJpn = preset.LayerDriverValue;
        }
        else if (string.Equals(preset.LayerDriverValueName, "LayerDriver KOR", StringComparison.OrdinalIgnoreCase))
        {
            GlobalLayerDriverKor = preset.LayerDriverValue;
        }
    }

    private void LoadGlobalEditorsFromStore()
    {
        GlobalTypeText = _global.Type?.ToString() ?? string.Empty;
        GlobalSubtypeText = _global.Subtype?.ToString() ?? string.Empty;
        GlobalIdentifier = _global.Identifier ?? string.Empty;
        GlobalLayerDriverJpn = _global.LayerDriverJpn ?? string.Empty;
        GlobalLayerDriverKor = _global.LayerDriverKor ?? string.Empty;
        SelectedGlobalPreset = LayoutCatalog.FindByTypeSubtype(_global.Type, _global.Subtype)
            ?? LayoutCatalog.Presets.First(p => p.IsCustom);
    }

    private void RefreshAllEffective()
    {
        foreach (var device in Devices)
        {
            device.RefreshEffective(_global);
        }
    }

    private void RebuildVisible()
    {
        VisibleDevices.Clear();
        foreach (var device in Devices.Where(d => ShowHidden || !d.IsHidden))
        {
            VisibleDevices.Add(device);
        }

        if (SelectedDevice is not null && !VisibleDevices.Contains(SelectedDevice))
        {
            SelectedDevice = VisibleDevices.FirstOrDefault();
        }
    }

    private static int? ParseNullableInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return int.TryParse(text.Trim(), out var value) ? value : throw new FormatException($"Invalid number: {text}");
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
