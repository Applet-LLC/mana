using Mana.Models;
using Mana.Services;
using Mana.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Mana;

public sealed partial class MainPage : Page
{
    private readonly MainViewModel _vm = new();
    private bool _suppressPresetEvents;
    private bool _busy;
    private InputCursor? _previousCursor;

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLocalizedChrome();
        PopulatePresetCombos();
        DeviceList.ItemsSource = _vm.VisibleDevices;
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.StatusMessage))
            {
                StatusText.Text = _vm.StatusMessage;
            }
            else if (args.PropertyName is nameof(MainViewModel.LanguageButtonLabel))
            {
                LanguageButton.Content = _vm.LanguageButtonLabel;
            }
            else if (args.PropertyName is nameof(MainViewModel.ShowHidden))
            {
                UpdateShowHiddenButton();
            }
        };

        EnsureStorageRootInitialized();
        _ = RunBusyAsync(() =>
        {
            _vm.Reload();
            SyncGlobalEditorsFromVm();
            BindSelectedDeviceEditors();
            UpdateShowHiddenButton();
            UpdateGuidanceText();
            var elevation = ElevationService.IsElevated()
                ? Localization.Get("Status_Elevated")
                : Localization.Get("Status_NotElevated");
            StatusText.Text = string.IsNullOrWhiteSpace(_vm.StatusMessage)
                ? elevation
                : $"{_vm.StatusMessage}  {elevation}";
        });
    }

    private static void EnsureStorageRootInitialized()
    {
        try
        {
            if (!File.Exists(Path.Combine(StorageRootStore.AppSettingsDirectory, "storage-root.json")))
            {
                StorageRootStore.SetRootPath(StorageRootStore.DefaultRootPath);
            }
            else
            {
                StorageRootStore.EnsureDbDirectory();
            }
        }
        catch
        {
            try
            {
                StorageRootStore.SetRootPath(StorageRootStore.DefaultRootPath);
            }
            catch
            {
            }
        }
    }

    private async Task ShowStorageRootDialogAsync()
    {
        try
        {
            EnsureStorageRootInitialized();
            var pathText = new TextBlock
            {
                Text = StorageRootStore.GetRootPath(),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };

            var changeButton = new Button { Content = Localization.Get("Button_ChangeStorage") };
            changeButton.Click += async (_, _) =>
            {
                var picked = await PickStorageFolderAsync();
                if (!string.IsNullOrWhiteSpace(picked))
                {
                    StorageRootStore.SetRootPath(picked);
                    pathText.Text = StorageRootStore.GetRootPath();
                }
            };

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = Localization.Get("Dialog_StorageMessage"),
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = Localization.Get("Dialog_StoragePathLabel"),
                Opacity = 0.75
            });
            panel.Children.Add(pathText);
            panel.Children.Add(changeButton);

            var dialog = new ContentDialog
            {
                Title = Localization.Get("Dialog_StorageTitle"),
                Content = panel,
                PrimaryButtonText = Localization.Get("Button_StorageOk"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
            StorageRootStore.EnsureDbDirectory();
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
        }
    }

    private async Task<string?> PickStorageFolderAsync()
    {
        try
        {
            var window = App.MainAppWindow;
            if (window is null)
            {
                return null;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var picker = new Windows.Storage.Pickers.FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyLocalizedChrome()
    {
        GuidanceText.Text = ConnectedKeyboardLayoutStatus.GetGuidanceText();
        ReloadButton.Content = Localization.Get("Button_Reload");
        PastKeyboardsButton.Content = Localization.Get("Button_PastKeyboards");
        StorageSettingsButton.Content = Localization.Get("Button_StorageSettings");
        LanguageButton.Content = LanguageService.ToggleButtonLabel;
        OpenSettingsButton.Content = Localization.Get("Button_OpenSettings");
        OpenDeviceRegistryButton.Content = Localization.Get("Button_OpenDeviceRegistry");
        OpenGlobalRegistryButton.Content = Localization.Get("Button_OpenGlobalRegistry");
        SaveHiddenButton.Content = Localization.Get("Button_SaveHidden");
        ElevateButton.Content = Localization.Get("Button_RestartElevated");
        ElevateButton.Visibility = ElevationService.IsElevated()
            ? Visibility.Collapsed
            : Visibility.Visible;
        DevicesHeader.Text = Localization.Get("Section_Devices");
        DeviceDetailHeader.Text = Localization.Get("Section_DeviceDetail");
        GlobalHeader.Text = Localization.Get("Section_Global");
        InstancePathLabel.Text = Localization.Get("Label_InstancePath");
        HardwareIdsLabel.Text = Localization.Get("Label_HardwareIds");
        ShellNameLabel.Text = Localization.Get("Label_ShellName");
        DeviceRegistryPathLabel.Text = Localization.Get("Label_RegistryPath");
        GlobalRegistryPathText.Text = RegistryLauncher.GlobalParametersPath;
        ToolTipService.SetToolTip(GlobalRegistryPathText, RegistryLauncher.GlobalParametersPath);
        EffectiveLabel.Text = Localization.Get("Label_Effective");
        DevicePresetLabel.Text = Localization.Get("Label_Preset");
        DeviceTypeLabel.Text = Localization.Get("Label_Type");
        DeviceSubtypeLabel.Text = Localization.Get("Label_Subtype");
        GlobalPresetLabel.Text = Localization.Get("Label_Preset");
        GlobalTypeLabel.Text = Localization.Get("Label_Type");
        GlobalSubtypeLabel.Text = Localization.Get("Label_Subtype");
        GlobalIdentifierLabel.Text = Localization.Get("Label_Identifier");
        GlobalLayerJpnLabel.Text = Localization.Get("Label_LayerDriverJpn");
        GlobalLayerKorLabel.Text = Localization.Get("Label_LayerDriverKor");
        ApplyDeviceButton.Content = Localization.Get("Button_ApplyDevice");
        ClearDeviceButton.Content = Localization.Get("Button_ClearDevice");
        ApplyGlobalPresetButton.Content = Localization.Get("Button_ApplyGlobalPreset");
        ApplyGlobalButton.Content = Localization.Get("Button_ApplyGlobal");
        ClearGlobalButton.Content = Localization.Get("Button_ClearGlobal");
        UpdateShowHiddenButton();
    }

    private void UpdateShowHiddenButton()
    {
        ShowHiddenButton.Content = _vm.ShowHidden
            ? Localization.Get("Button_HideHidden")
            : Localization.Get("Button_ShowHidden");
    }

    private void PopulatePresetCombos()
    {
        _suppressPresetEvents = true;
        DevicePresetCombo.Items.Clear();
        GlobalPresetCombo.Items.Clear();
        foreach (var preset in LayoutCatalog.Presets)
        {
            var item = new ComboBoxItem
            {
                Content = Localization.Get(preset.DisplayNameResourceKey),
                Tag = preset
            };
            DevicePresetCombo.Items.Add(item);

            var globalItem = new ComboBoxItem
            {
                Content = Localization.Get(preset.DisplayNameResourceKey),
                Tag = preset
            };
            GlobalPresetCombo.Items.Add(globalItem);
        }

        _suppressPresetEvents = false;
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        _ = RunBusyAsync(() =>
        {
            _vm.Reload();
            SyncGlobalEditorsFromVm();
            BindSelectedDeviceEditors();
            SelectComboPreset(DevicePresetCombo, _vm.SelectedDevice?.SelectedPreset);
            SelectComboPreset(GlobalPresetCombo, _vm.SelectedGlobalPreset);
        });
    }

    private void PastKeyboardsButton_Click(object sender, RoutedEventArgs e)
    {
        var connected = _vm.Devices
            .Select(d => d.InstancePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        KeyboardDetailWindowManager.OpenPastList(connected);
    }

    private void StorageSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ShowStorageRootDialogAsync();
    }

    private void DeviceName_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        var item = (sender as FrameworkElement)?.Tag as KeyboardItemViewModel
                   ?? (sender as FrameworkElement)?.DataContext as KeyboardItemViewModel;
        if (item is null)
        {
            return;
        }

        var displayName = !string.IsNullOrWhiteSpace(item.ShellFriendlyName)
            ? item.ShellFriendlyName
            : item.FriendlyName;
        KeyboardDetailWindowManager.OpenDetail(
            item.InstancePath,
            displayName,
            item.HasShellFriendlyName ? item.ShellFriendlyName : null,
            showDriverStack: true);
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        LanguageService.Toggle();
        App.ReloadUiLanguage();
    }

    private void ShowHiddenButton_Click(object sender, RoutedEventArgs e)
    {
        _ = RunBusyAsync(() =>
        {
            _vm.ToggleHiddenFilter();
            UpdateShowHiddenButton();
        });
    }

    private void SaveHiddenButton_Click(object sender, RoutedEventArgs e)
    {
        _ = RunBusyAsync(() =>
        {
            _vm.PersistHiddenFlags();
            StatusText.Text = _vm.StatusMessage;
        });
    }

    private async void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync(async () =>
        {
            var (_, message) = await SettingsLauncher.OpenLanguageSettingsAsync();
            StatusText.Text = message;
        });
    }

    private void OpenDeviceRegistryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedDevice is null)
        {
            StatusText.Text = Localization.Get("Status_NoDeviceSelected");
            return;
        }

        var path = RegistryLauncher.GetDeviceParametersPath(_vm.SelectedDevice.InstancePath);
        var (_, message) = RegistryLauncher.OpenKey(path);
        StatusText.Text = message;
    }

    private void OpenGlobalRegistryButton_Click(object sender, RoutedEventArgs e)
    {
        var (_, message) = RegistryLauncher.OpenKey(RegistryLauncher.GlobalParametersPath);
        StatusText.Text = message;
    }

    private void ElevateButton_Click(object sender, RoutedEventArgs e)
    {
        AppSessionState.SuppressExitPrompt = true;
        if (ElevationService.RelaunchElevated())
        {
            Application.Current.Exit();
            return;
        }

        AppSessionState.SuppressExitPrompt = false;
        StatusText.Text = Localization.Get("Status_ElevationCancelled");
    }

    private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        _ = RunBusyAsync(() =>
        {
            _vm.SelectedDevice = DeviceList.SelectedItem as KeyboardItemViewModel;
            BindSelectedDeviceEditors();
        });
    }

    private void BindSelectedDeviceEditors()
    {
        var device = _vm.SelectedDevice;
        if (device is null)
        {
            InstancePathBox.Text = string.Empty;
            HardwareIdsBox.Text = string.Empty;
            ShellNameBox.Text = string.Empty;
            DeviceRegistryPathBox.Text = string.Empty;
            EffectiveValueText.Text = string.Empty;
            DeviceTypeBox.Text = string.Empty;
            DeviceSubtypeBox.Text = string.Empty;
            return;
        }

        if (!ReferenceEquals(DeviceList.SelectedItem, device) && _vm.VisibleDevices.Contains(device))
        {
            DeviceList.SelectedItem = device;
        }

        InstancePathBox.Text = device.InstancePath;
        HardwareIdsBox.Text = device.HardwareIds;
        ShellNameBox.Text = device.HasShellFriendlyName
            ? device.ShellFriendlyName
            : device.FriendlyName;
        DeviceRegistryPathBox.Text = RegistryLauncher.GetDeviceParametersPath(device.InstancePath);
        EffectiveValueText.Text =
            $"{device.LayoutDisplayName} / {device.SourceDisplayName} (Type={device.EffectiveType?.ToString() ?? "-"}, Subtype={device.EffectiveSubtype?.ToString() ?? "-"})";
        DeviceTypeBox.Text = device.EditType?.ToString() ?? string.Empty;
        DeviceSubtypeBox.Text = device.EditSubtype?.ToString() ?? string.Empty;
        SelectComboPreset(DevicePresetCombo, device.SelectedPreset);
    }

    private void SyncGlobalEditorsFromVm()
    {
        GlobalTypeBox.Text = _vm.GlobalTypeText ?? string.Empty;
        GlobalSubtypeBox.Text = _vm.GlobalSubtypeText ?? string.Empty;
        GlobalIdentifierBox.Text = _vm.GlobalIdentifier ?? string.Empty;
        GlobalLayerJpnBox.Text = _vm.GlobalLayerDriverJpn ?? string.Empty;
        GlobalLayerKorBox.Text = _vm.GlobalLayerDriverKor ?? string.Empty;
        SelectComboPreset(GlobalPresetCombo, _vm.SelectedGlobalPreset);
        StatusText.Text = _vm.StatusMessage;
    }

    private void DevicePresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPresetEvents || _vm.SelectedDevice is null)
        {
            return;
        }

        if (DevicePresetCombo.SelectedItem is ComboBoxItem { Tag: LayoutPreset preset })
        {
            _vm.SelectedDevice.SelectedPreset = preset;
            if (!preset.IsCustom)
            {
                DeviceTypeBox.Text = preset.Type.ToString();
                DeviceSubtypeBox.Text = preset.Subtype.ToString();
            }
        }
    }

    private void GlobalPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPresetEvents)
        {
            return;
        }

        if (GlobalPresetCombo.SelectedItem is ComboBoxItem { Tag: LayoutPreset preset })
        {
            _vm.SelectedGlobalPreset = preset;
            SyncGlobalEditorsFromVm();
        }
    }

    private void ApplyDeviceButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanWriteRegistry())
        {
            return;
        }

        if (_vm.SelectedDevice is null)
        {
            return;
        }

        _ = RunBusyAsync(() =>
        {
            try
            {
                _vm.SelectedDevice.EditType = ParseNullableInt(DeviceTypeBox.Text);
                _vm.SelectedDevice.EditSubtype = ParseNullableInt(DeviceSubtypeBox.Text);
                _vm.ApplySelectedDevice();
                BindSelectedDeviceEditors();
                StatusText.Text = _vm.StatusMessage;
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
            }
        });
    }

    private void ClearDeviceButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanWriteRegistry())
        {
            return;
        }

        _ = RunBusyAsync(() =>
        {
            _vm.ClearSelectedDevice();
            BindSelectedDeviceEditors();
            StatusText.Text = _vm.StatusMessage;
        });
    }

    private async void ApplyGlobalPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanWriteRegistry())
        {
            return;
        }

        PushGlobalEditorsToVm();
        if (!await ConfirmGlobalChangeAsync(presetMode: true))
        {
            return;
        }

        _ = RunBusyAsync(() =>
        {
            _vm.ApplyGlobalPreset();
            SyncGlobalEditorsFromVm();
            BindSelectedDeviceEditors();
            UpdateGuidanceText();
        });
    }

    private async void ApplyGlobalButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanWriteRegistry())
        {
            return;
        }

        PushGlobalEditorsToVm();
        if (!await ConfirmGlobalChangeAsync(presetMode: false))
        {
            return;
        }

        _ = RunBusyAsync(() =>
        {
            try
            {
                _vm.ApplyGlobalIndividual();
                SyncGlobalEditorsFromVm();
                BindSelectedDeviceEditors();
                UpdateGuidanceText();
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
            }
        });
    }

    private async void ClearGlobalButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanWriteRegistry())
        {
            return;
        }

        if (!await ConfirmClearPrimaryValuesAsync(GlobalOverrideStore.Read(), new GlobalOverrideValues()))
        {
            return;
        }

        _ = RunBusyAsync(() =>
        {
            _vm.ClearGlobal();
            SyncGlobalEditorsFromVm();
            BindSelectedDeviceEditors();
            UpdateGuidanceText();
        });
    }

    private void UpdateGuidanceText()
    {
        GuidanceText.Text = ConnectedKeyboardLayoutStatus.GetGuidanceText();
    }

    private async Task<bool> ConfirmGlobalChangeAsync(bool presetMode)
    {
        GlobalOverrideValues next;
        try
        {
            next = _vm.BuildPendingGlobalValues(presetMode);
        }
        catch (FormatException ex)
        {
            StatusText.Text = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
            return false;
        }

        var current = GlobalOverrideStore.Read();
        if (!await ConfirmClearPrimaryValuesAsync(current, next))
        {
            return false;
        }

        return await ConfirmDisableConnectedLayoutAsync(current, next);
    }

    /// <summary>
    /// Type / Subtype / Identifier のいずれかを削除する操作のときだけ警告する。
    /// LayerDriver JPN / KOR はこれらに従属するため、単独の削除では警告しない。
    /// </summary>
    private async Task<bool> ConfirmClearPrimaryValuesAsync(GlobalOverrideValues current, GlobalOverrideValues next)
    {
        var cleared = GlobalOverrideValues.GetClearedPrimaryValueNames(current, next);
        if (cleared.Count == 0)
        {
            return true;
        }

        var message = string.Format(
            Localization.Get("Dialog_ClearPrimaryMessageFormat"),
            string.Join("\n", cleared.Select(name => "・" + name)));
        if (!next.Type.HasValue && !next.Subtype.HasValue)
        {
            message += Localization.Get("Dialog_ClearPrimaryConnectedNote");
        }

        return await ShowConfirmDialogAsync(Localization.Get("Dialog_ClearPrimaryTitle"), message);
    }

    private async Task<bool> ConfirmDisableConnectedLayoutAsync(GlobalOverrideValues current, GlobalOverrideValues next)
    {
        // Only warn when this write turns a connected-layout state into a fixed global Type/Subtype.
        var currentlyConnected = !current.Type.HasValue && !current.Subtype.HasValue;
        var willFix = next.Type.HasValue || next.Subtype.HasValue;
        if (!currentlyConnected || !willFix)
        {
            return true;
        }

        return await ShowConfirmDialogAsync(
            Localization.Get("Dialog_DisableConnectedTitle"),
            Localization.Get("Dialog_DisableConnectedMessage"));
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = Localization.Get("Dialog_DisableConnectedContinue"),
            CloseButtonText = Localization.Get("Dialog_DisableConnectedCancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private bool EnsureCanWriteRegistry()
    {
        if (ElevationService.IsElevated())
        {
            return true;
        }

        StatusText.Text = Localization.Get("Status_ElevationNeeded");
        return false;
    }

    private void PushGlobalEditorsToVm()
    {
        _vm.GlobalTypeText = GlobalTypeBox.Text;
        _vm.GlobalSubtypeText = GlobalSubtypeBox.Text;
        _vm.GlobalIdentifier = GlobalIdentifierBox.Text;
        _vm.GlobalLayerDriverJpn = GlobalLayerJpnBox.Text;
        _vm.GlobalLayerDriverKor = GlobalLayerKorBox.Text;
    }

    private void SelectComboPreset(ComboBox combo, LayoutPreset? preset)
    {
        _suppressPresetEvents = true;
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is LayoutPreset p &&
                ((preset is null && p.IsCustom) ||
                 (preset is not null && p.Id == preset.Id)))
            {
                combo.SelectedItem = item;
                _suppressPresetEvents = false;
                return;
            }
        }

        combo.SelectedIndex = combo.Items.Count - 1;
        _suppressPresetEvents = false;
    }

    private async Task RunBusyAsync(Action action)
    {
        await RunBusyAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (_busy)
        {
            await action();
            return;
        }

        _busy = true;
        BeginBusyCursor();
        DeviceList.IsEnabled = false;
        try
        {
            // Let the wait cursor paint before doing UI-heavy work.
            await Task.Yield();
            await action();
            await Task.Yield();
        }
        finally
        {
            DeviceList.IsEnabled = true;
            EndBusyCursor();
            _busy = false;
        }
    }

    private void BeginBusyCursor()
    {
        try
        {
            _previousCursor = ProtectedCursor;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Wait);
        }
        catch
        {
        }
    }

    private void EndBusyCursor()
    {
        try
        {
            ProtectedCursor = _previousCursor ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            _previousCursor = null;
        }
        catch
        {
        }
    }

    private static int? ParseNullableInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!int.TryParse(text.Trim(), out var value))
        {
            throw new FormatException(text);
        }

        return value;
    }
}
