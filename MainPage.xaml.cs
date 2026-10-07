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
    private bool _suppressLocaleEvents;
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
        PopulateLocaleLayoutEditors(selectDefaultForUiLanguage: true);
        DeviceList.ItemsSource = _vm.VisibleDevices;
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.StatusMessage))
            {
                StatusText.Text = _vm.StatusMessage;
            }
        };

        EnsureStorageRootInitialized();
        _ = RunBusyAsync(() =>
        {
            _vm.Reload();
            SyncGlobalEditorsFromVm();
            BindSelectedDeviceEditors();
            UpdateGuidanceText();
            var elevation = ElevationService.IsElevated()
                ? Localization.Get("Status_Elevated")
                : Localization.Get("Status_NotElevated");
            StatusText.Text = string.IsNullOrWhiteSpace(_vm.StatusMessage)
                ? elevation
                : $"{_vm.StatusMessage}  {elevation}";
        });
    }

    public void PersistHiddenOnExit()
    {
        try
        {
            _vm.PersistHiddenFlags();
        }
        catch
        {
        }
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

    private async Task ShowAppSettingsDialogAsync()
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

            var languageButton = new Button { Content = LanguageService.ToggleButtonLabel };
            languageButton.Click += (_, _) =>
            {
                LanguageService.Toggle();
                languageButton.Content = LanguageService.ToggleButtonLabel;
                App.ReloadUiLanguage();
            };

            var showHiddenToggle = new ToggleSwitch
            {
                IsOn = _vm.ShowHidden,
                OnContent = Localization.Get("Button_ShowHidden"),
                OffContent = Localization.Get("Button_HideHidden")
            };

            var globalUi = GlobalMemoUiStore.Load();
            var resolvedFamily = string.IsNullOrWhiteSpace(globalUi.FontFamily)
                ? KeyboardMemoUiStore.DefaultFontFamily
                : globalUi.FontFamily!;
            var resolvedSize = globalUi.FontSize ?? KeyboardMemoUiStore.DefaultFontSize;

            var familyCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEditable = true,
                ItemsSource = KeyboardMemoUiStore.GetInstalledFontFamilies(),
                Text = resolvedFamily
            };
            if (familyCombo.Items.Contains(resolvedFamily))
            {
                familyCombo.SelectedItem = resolvedFamily;
            }

            var sizeBox = new NumberBox
            {
                Minimum = 8,
                Maximum = 48,
                SmallChange = 1,
                LargeChange = 2,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Value = resolvedSize
            };

            var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
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

            panel.Children.Add(new TextBlock
            {
                Text = Localization.Get("Label_UiLanguage"),
                Opacity = 0.75,
                Margin = new Thickness(0, 8, 0, 0)
            });
            panel.Children.Add(languageButton);

            panel.Children.Add(new TextBlock
            {
                Text = Localization.Get("Label_ShowHiddenDevices"),
                Opacity = 0.75,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(showHiddenToggle);

            panel.Children.Add(new TextBlock
            {
                Text = Localization.Get("Label_GlobalMemoFont"),
                Opacity = 0.75,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock { Text = Localization.Get("Label_MemoFontFamily") });
            panel.Children.Add(familyCombo);
            panel.Children.Add(new TextBlock { Text = Localization.Get("Label_MemoFontSize") });
            panel.Children.Add(sizeBox);

            var dialog = new ContentDialog
            {
                Title = Localization.Get("Dialog_StorageTitle"),
                Content = new ScrollViewer
                {
                    Content = panel,
                    MaxHeight = 520,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                PrimaryButtonText = Localization.Get("Button_StorageOk"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();

            if (showHiddenToggle.IsOn != _vm.ShowHidden)
            {
                _vm.ShowHidden = showHiddenToggle.IsOn;
            }

            var family = familyCombo.SelectedItem as string ?? familyCombo.Text;
            if (string.IsNullOrWhiteSpace(family))
            {
                family = KeyboardMemoUiStore.DefaultFontFamily;
            }

            var size = double.IsNaN(sizeBox.Value) ? KeyboardMemoUiStore.DefaultFontSize : sizeBox.Value;
            GlobalMemoUiStore.Save(new KeyboardMemoUiSettings
            {
                FontFamily = family.Trim(),
                FontSize = size
            });

            _vm.PersistHiddenFlags();
            StorageRootStore.EnsureDbDirectory();
            StatusText.Text = string.Format(Localization.Get("Status_StorageSetFormat"), StorageRootStore.GetRootPath());
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
        }
    }

    private void ShowVersionDialog()
    {
        try
        {
            var exePath = Environment.ProcessPath
                          ?? Path.Combine(AppContext.BaseDirectory, "mana.exe");
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            var version = !string.IsNullOrWhiteSpace(versionInfo.ProductVersion)
                ? versionInfo.ProductVersion
                : (versionInfo.FileVersion ?? Localization.Get("AppTitle"));
            var binaryName = Path.GetFileName(exePath);
            var installPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var panel = new StackPanel { Spacing = 8, MinWidth = 420 };
            void AddRow(string label, string value)
            {
                panel.Children.Add(new TextBlock { Text = label, Opacity = 0.75 });
                panel.Children.Add(new TextBlock
                {
                    Text = value,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                });
            }

            AddRow(Localization.Get("Label_VersionAppName"), Localization.Get("AppTitle"));
            AddRow(Localization.Get("Label_VersionBinary"), binaryName);
            AddRow(Localization.Get("Label_VersionNumber"), version ?? string.Empty);
            AddRow(Localization.Get("Label_VersionInstallPath"), installPath);
            panel.Children.Add(new TextBlock
            {
                Text = Localization.Get("Label_VersionWebsite"),
                Opacity = 0.75
            });
            panel.Children.Add(new HyperlinkButton
            {
                Content = "https://appletllc.com/",
                NavigateUri = new Uri("https://appletllc.com/")
            });

            var dialog = new ContentDialog
            {
                Title = Localization.Get("Dialog_VersionTitle"),
                Content = panel,
                CloseButtonText = Localization.Get("Button_Close"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            _ = dialog.ShowAsync();
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
        SettingsButton.Content = Localization.Get("Button_StorageSettings");
        VersionButton.Content = Localization.Get("Button_Version");
        OpenDeviceRegistryButton.Content = Localization.Get("Button_OpenDeviceRegistry");
        OpenGlobalRegistryButton.Content = Localization.Get("Button_OpenGlobalRegistry");
        OpenLanguageSettingsButton.Content = Localization.Get("Button_OpenSettings");
        DevicesHeader.Text = Localization.Get("Section_Devices");
        DeviceDetailHeader.Text = Localization.Get("Section_DeviceDetail");
        LocaleLayoutHeader.Text = Localization.Get("Section_LocaleLayout");
        LocaleLabel.Text = Localization.Get("Label_Locale");
        LayoutFileLabel.Text = Localization.Get("Label_LayoutFile");
        ApplyLocaleLayoutButton.Content = Localization.Get("Button_ApplyLocaleLayout");
        OpenLocaleLayoutRegistryButton.Content = Localization.Get("Button_OpenLocaleLayoutRegistry");
        GlobalHeader.Text = Localization.Get("Section_Global");
        UpdateDeviceIdLabels(_vm.SelectedDevice);
        ShellNameLabel.Text = Localization.Get("Label_ShellName");
        DeviceRegistryPathLabel.Text = Localization.Get("Label_RegistryPath");
        GlobalRegistryPathText.Text = RegistryLauncher.GlobalParametersPath;
        ToolTipService.SetToolTip(GlobalRegistryPathText, RegistryLauncher.GlobalParametersPath);
        RefreshLocaleLayoutPathLabel();
        RefreshLocaleComboLabels();
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
        ElevateButton.Content = Localization.Get("Button_RestartElevated");
        GlobalActiveWarningButton.Content = Localization.Get("Button_WarnGlobalActive");
        AcpiPs2WarningButton.Content = Localization.Get("Button_WarnAcpiPs2");
        ElevateButton.Visibility = ElevationService.IsElevated()
            ? Visibility.Collapsed
            : Visibility.Visible;
        ApplyGlobalPresetButton.Content = Localization.Get("Button_ApplyGlobalPreset");
        ApplyGlobalButton.Content = Localization.Get("Button_ApplyGlobal");
        ClearGlobalButton.Content = Localization.Get("Button_ClearGlobal");
    }

    private void PopulateLocaleLayoutEditors(bool selectDefaultForUiLanguage)
    {
        _suppressLocaleEvents = true;
        try
        {
            LayoutFileCombo.Items.Clear();
            foreach (var candidate in LocaleLayoutStore.LayoutFileCandidates)
            {
                LayoutFileCombo.Items.Add(candidate);
            }

            var preferredId = selectDefaultForUiLanguage
                ? LocaleLayoutStore.GetDefaultLocaleId()
                : GetSelectedLocaleId() ?? LocaleLayoutStore.GetDefaultLocaleId();

            LocaleCombo.Items.Clear();
            ComboBoxItem? selected = null;
            foreach (var locale in LocaleLayoutStore.Locales)
            {
                var item = new ComboBoxItem
                {
                    Content = Localization.Get(locale.DisplayNameResourceKey),
                    Tag = locale.LocaleId
                };
                LocaleCombo.Items.Add(item);
                if (string.Equals(locale.LocaleId, preferredId, StringComparison.OrdinalIgnoreCase))
                {
                    selected = item;
                }
            }

            LocaleCombo.SelectedItem = selected ?? LocaleCombo.Items.OfType<ComboBoxItem>().FirstOrDefault();
            LoadLayoutFileEditorForSelectedLocale();
            RefreshLocaleLayoutPathLabel();
        }
        finally
        {
            _suppressLocaleEvents = false;
        }
    }

    private void RefreshLocaleComboLabels()
    {
        var selectedId = GetSelectedLocaleId();
        _suppressLocaleEvents = true;
        try
        {
            foreach (var item in LocaleCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is string localeId)
                {
                    var info = LocaleLayoutStore.Locales.FirstOrDefault(l =>
                        string.Equals(l.LocaleId, localeId, StringComparison.OrdinalIgnoreCase));
                    if (info is not null)
                    {
                        item.Content = Localization.Get(info.DisplayNameResourceKey);
                    }
                }
            }

            if (!string.IsNullOrEmpty(selectedId))
            {
                LocaleCombo.SelectedItem = LocaleCombo.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(i => string.Equals(i.Tag as string, selectedId, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            _suppressLocaleEvents = false;
        }

        RefreshLocaleLayoutPathLabel();
    }

    private string? GetSelectedLocaleId() =>
        (LocaleCombo.SelectedItem as ComboBoxItem)?.Tag as string;

    private void RefreshLocaleLayoutPathLabel()
    {
        var localeId = GetSelectedLocaleId() ?? LocaleLayoutStore.GetDefaultLocaleId();
        var path = LocaleLayoutStore.GetHivePath(localeId);
        LocaleLayoutRegistryPathText.Text = path;
        ToolTipService.SetToolTip(LocaleLayoutRegistryPathText, path);
    }

    private void LoadLayoutFileEditorForSelectedLocale()
    {
        var localeId = GetSelectedLocaleId();
        if (string.IsNullOrEmpty(localeId))
        {
            LayoutFileCombo.Text = string.Empty;
            return;
        }

        var current = LocaleLayoutStore.ReadLayoutFile(localeId) ?? string.Empty;
        LayoutFileCombo.Text = current;
        if (!string.IsNullOrWhiteSpace(current))
        {
            var match = LayoutFileCombo.Items.OfType<string>()
                .FirstOrDefault(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                LayoutFileCombo.SelectedItem = match;
                LayoutFileCombo.Text = match;
            }
        }
    }

    private void LocaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLocaleEvents)
        {
            return;
        }

        RefreshLocaleLayoutPathLabel();
        LoadLayoutFileEditorForSelectedLocale();
    }

    private void ApplyLocaleLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureCanWriteRegistry())
        {
            return;
        }

        var localeId = GetSelectedLocaleId();
        if (string.IsNullOrEmpty(localeId))
        {
            return;
        }

        var layoutFile = LayoutFileCombo.SelectedItem as string ?? LayoutFileCombo.Text;
        if (string.IsNullOrWhiteSpace(layoutFile))
        {
            StatusText.Text = Localization.Get("Status_LocaleLayoutMissing");
            return;
        }

        _ = RunBusyAsync(() =>
        {
            try
            {
                LocaleLayoutStore.WriteLayoutFile(localeId, layoutFile);
                AppSessionState.RegistryChanged = true;
                LoadLayoutFileEditorForSelectedLocale();
                StatusText.Text = Localization.Get("Status_LocaleLayoutSaved");
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Localization.Get("Status_ErrorFormat"), ex.Message);
            }
        });
    }

    private void OpenLocaleLayoutRegistryButton_Click(object sender, RoutedEventArgs e)
    {
        var localeId = GetSelectedLocaleId() ?? LocaleLayoutStore.GetDefaultLocaleId();
        var path = LocaleLayoutStore.GetHivePath(localeId);
        var (_, message) = RegistryLauncher.OpenKey(path);
        StatusText.Text = message;
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
            LoadLayoutFileEditorForSelectedLocale();
        });
    }

    private void PastKeyboardsButton_Click(object sender, RoutedEventArgs e)
    {
        var connected = _vm.Devices
            .Select(d => d.InstancePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        KeyboardDetailWindowManager.OpenPastList(connected);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ShowAppSettingsDialogAsync();
    }

    private void VersionButton_Click(object sender, RoutedEventArgs e)
    {
        ShowVersionDialog();
    }

    private void DeviceThumbnail_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        OpenDeviceDetail((sender as FrameworkElement)?.Tag as KeyboardItemViewModel
                         ?? (sender as FrameworkElement)?.DataContext as KeyboardItemViewModel);
    }

    private void DeviceName_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        OpenDeviceDetail((sender as FrameworkElement)?.Tag as KeyboardItemViewModel
                         ?? (sender as FrameworkElement)?.DataContext as KeyboardItemViewModel);
    }

    private static void OpenDeviceDetail(KeyboardItemViewModel? item)
    {
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

    private async void OpenLanguageSettingsButton_Click(object sender, RoutedEventArgs e)
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

    private void UpdateDeviceIdLabels(KeyboardItemViewModel? device)
    {
        InstancePathLabel.Text = FormatLabelWithHash(
            Localization.Get("Label_InstancePath"),
            device?.InstanceKeyHashText);
        HardwareIdsLabel.Text = FormatLabelWithHash(
            Localization.Get("Label_HardwareIds"),
            device?.ModelKeyHashText);
    }

    private static string FormatLabelWithHash(string label, string? hashText) =>
        string.IsNullOrEmpty(hashText)
            ? label
            : string.Format(Localization.Get("Label_WithHashFormat"), label, hashText);

    private void BindSelectedDeviceEditors()
    {
        var device = _vm.SelectedDevice;
        UpdateDeviceIdLabels(device);
        if (device is null)
        {
            InstancePathBox.Text = string.Empty;
            HardwareIdsBox.Text = string.Empty;
            ShellNameBox.Text = string.Empty;
            DeviceRegistryPathBox.Text = string.Empty;
            EffectiveValueText.Text = string.Empty;
            DeviceTypeBox.Text = string.Empty;
            DeviceSubtypeBox.Text = string.Empty;
            GlobalActiveWarningButton.Visibility = Visibility.Collapsed;
            AcpiPs2WarningButton.Visibility = Visibility.Collapsed;
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
        GlobalActiveWarningButton.Visibility = _vm.IsGlobalLayoutActive ? Visibility.Visible : Visibility.Collapsed;
        AcpiPs2WarningButton.Visibility = device.IsAcpiPs2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GlobalActiveWarningButton_Click(object sender, RoutedEventArgs e)
    {
        var message = Localization.Get("Dialog_WarnGlobalActiveMessage");
        if (_vm.SelectedDevice is { HasDeviceOverride: true } device)
        {
            message += string.Format(
                Localization.Get("Dialog_WarnGlobalActiveDeviceFormat"),
                device.DeviceType?.ToString() ?? "-",
                device.DeviceSubtype?.ToString() ?? "-");
        }

        ShowWarningDialog(Localization.Get("Dialog_WarnGlobalActiveTitle"), message, showWebsite: false);
    }

    private void AcpiPs2WarningButton_Click(object sender, RoutedEventArgs e)
    {
        ShowWarningDialog(
            Localization.Get("Dialog_WarnAcpiPs2Title"),
            Localization.Get("Dialog_WarnAcpiPs2Message"),
            showWebsite: true);
    }

    private void ShowWarningDialog(string title, string message, bool showWebsite)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        if (showWebsite)
        {
            panel.Children.Add(new HyperlinkButton
            {
                Content = "https://appletllc.com/",
                NavigateUri = new Uri("https://appletllc.com/")
            });
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer { Content = panel },
            CloseButtonText = Localization.Get("Button_Close"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        _ = dialog.ShowAsync();
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
