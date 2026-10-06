using Mana.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Mana;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _closePromptInProgress;
    private bool _sizeInitialized;

    public MainWindow()
    {
        InitializeComponent();
        ApplyLocalizedTitle();
        RestoreWindowBounds();

        try
        {
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        }
        catch
        {
        }

        AppWindow.Changed += AppWindow_Changed;
        AppWindow.Closing += AppWindow_Closing;
        RootFrame.Navigate(typeof(MainPage));
    }

    public void ReloadForLanguageChange()
    {
        ApplyLocalizedTitle();
        RootFrame.Navigate(typeof(MainPage));
    }

    private void ApplyLocalizedTitle()
    {
        var title = Localization.Get("AppTitle");
        if (ElevationService.IsElevated())
        {
            title = Localization.Get("Title_AdminPrefix") + title;
        }

        Title = title;
    }

    private void RestoreWindowBounds()
    {
        var state = WindowStateStore.Load();
        try
        {
            AppWindow.Resize(new SizeInt32(state.Width, state.Height));
            if (state.HasPosition)
            {
                AppWindow.Move(new PointInt32(state.X, state.Y));
            }
        }
        catch
        {
            AppWindow.Resize(new SizeInt32(1100, 900));
        }

        _sizeInitialized = true;
    }

    private void PersistWindowBounds()
    {
        if (!_sizeInitialized)
        {
            return;
        }

        try
        {
            WindowStateStore.Save(AppWindow.Size, AppWindow.Position);
        }
        catch
        {
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPositionChange)
        {
            PersistWindowBounds();
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        PersistWindowBounds();
        if (RootFrame.Content is MainPage mainPage)
        {
            mainPage.PersistHiddenOnExit();
        }

        if (_allowClose || AppSessionState.SuppressExitPrompt || !AppSessionState.RegistryChanged)
        {
            return;
        }

        if (_closePromptInProgress)
        {
            args.Cancel = true;
            return;
        }

        args.Cancel = true;
        _closePromptInProgress = true;

        try
        {
            var xamlRoot = (RootFrame.Content as UIElement)?.XamlRoot ?? RootFrame.XamlRoot;
            if (xamlRoot is null)
            {
                _allowClose = true;
                Close();
                return;
            }

            var dialog = new ContentDialog
            {
                Title = Localization.Get("Dialog_ExitTitle"),
                Content = Localization.Get("Dialog_ExitMessage"),
                PrimaryButtonText = Localization.Get("Dialog_ExitReboot"),
                SecondaryButtonText = Localization.Get("Dialog_ExitQuit"),
                CloseButtonText = Localization.Get("Dialog_ExitCancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var (ok, message) = SystemReboot.RequestImmediateReboot();
                if (!ok)
                {
                    StartupCrashLog.ShowNativeMessage(message, Localization.Get("AppTitle"));
                    return;
                }

                AppSessionState.RegistryChanged = false;
                _allowClose = true;
                Close();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                AppSessionState.RegistryChanged = false;
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closePromptInProgress = false;
        }
    }
}
