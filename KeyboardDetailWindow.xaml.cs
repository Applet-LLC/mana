using System.Text;
using Mana.Services;
using Markdig;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Mana;

public sealed partial class KeyboardDetailWindow : Window
{
    private readonly string _instancePath;
    private readonly bool _showDriverStack;
    private string _savedMemo = string.Empty;
    private bool _webViewReady;
    private bool _closingHandled;
    private bool _viewMode = true;
    private string? _memoPreviewHtmlPath;

    public KeyboardDetailWindow(
        string instancePath,
        string displayName,
        string? shellFriendlyName,
        bool showDriverStack)
    {
        InitializeComponent();
        _instancePath = instancePath;
        _showDriverStack = showDriverStack;

        Title = displayName;
        TitleText.Text = displayName;
        if (!string.IsNullOrWhiteSpace(shellFriendlyName) &&
            !string.Equals(shellFriendlyName, displayName, StringComparison.OrdinalIgnoreCase))
        {
            ShellNameText.Text = shellFriendlyName;
            ShellNameText.Visibility = Visibility.Visible;
        }
        else
        {
            ShellNameText.Visibility = Visibility.Collapsed;
        }

        InstancePathBox.Text = instancePath;
        ApplyLocalizedChrome();

        try
        {
            AppWindow.Resize(new SizeInt32(780, 720));
        }
        catch
        {
        }

        if (_showDriverStack)
        {
            BuildStackRow();
        }
        else
        {
            DriverStackLabel.Visibility = Visibility.Collapsed;
            LoadHistoryThumbnail();
        }

        _savedMemo = KeyboardNoteStore.LoadMemo(_instancePath);
        MemoEditBox.Text = _savedMemo;
        ApplyMemoMode(viewMode: true);

        Activated += async (_, _) =>
        {
            if (_viewMode)
            {
                await ShowMemoViewAsync();
            }
        };
        AppWindow.Closing += AppWindow_Closing;
        Closed += KeyboardDetailWindow_Closed;
    }

    private void ApplyLocalizedChrome()
    {
        DriverStackLabel.Text = Localization.Get("Label_DriverStack");
        MemoLabel.Text = Localization.Get("Label_Memo");
        ViewModeButton.Content = Localization.Get("Tab_MemoView");
        EditModeButton.Content = Localization.Get("Tab_MemoEdit");
        OpenFolderButton.Content = Localization.Get("Button_OpenFolder");
    }

    private void ApplyMemoMode(bool viewMode)
    {
        _viewMode = viewMode;
        MemoWebView.Visibility = viewMode ? Visibility.Visible : Visibility.Collapsed;
        MemoViewFallbackBox.Visibility = Visibility.Collapsed;
        MemoEditBox.Visibility = viewMode ? Visibility.Collapsed : Visibility.Visible;
        ViewModeButton.IsEnabled = !viewMode;
        EditModeButton.IsEnabled = viewMode;
    }

    private async void ViewModeButton_Click(object sender, RoutedEventArgs e)
    {
        PersistMemoIfNeeded(forceAllow: true);
        ApplyMemoMode(viewMode: true);
        await ShowMemoViewAsync();
    }

    private void EditModeButton_Click(object sender, RoutedEventArgs e)
    {
        PersistMemoIfNeeded(forceAllow: true);
        ApplyMemoMode(viewMode: false);
    }

    private void BuildStackRow()
    {
        StackPanel.Children.Clear();
        var nodes = new List<UIElement>();

        var imagePath = KeyboardNoteStore.FindFirstImageForDevice(_instancePath);
        nodes.Add(CreateThumbnailElement(imagePath));

        var info = DeviceStackService.GetFilterStack(_instancePath);
        // DEVPKEY_Device_Stack はスタック上位(kbdclass側)が先頭。
        // UI は物理キーボード側(バス寄り)を左にするため逆順で並べ、末尾を kbdclass にする。
        // LowerFilters の空箱は表示しない。
        if (info.Stack.Count > 0)
        {
            foreach (var driver in info.Stack.Reverse())
            {
                nodes.Add(CreateFilterBox(driver, emphasize: DeviceStackService.IsKbdClass(driver)));
            }
        }
        else
        {
            // Stack が取れない場合: バス側(Lower) → UpperFilters → kbdclass（右端）
            // LowerFilters が空でも空箱は出さない
            foreach (var filter in info.LowerFilters)
            {
                nodes.Add(CreateFilterBox(filter));
            }

            foreach (var filter in info.UpperFilters)
            {
                nodes.Add(CreateFilterBox(filter));
            }

            nodes.Add(CreateFilterBox("kbdclass", emphasize: true));
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (i > 0)
            {
                StackPanel.Children.Add(CreateConnector());
            }

            StackPanel.Children.Add(nodes[i]);
        }
    }

    private void LoadHistoryThumbnail()
    {
        StackSection.Visibility = Visibility.Visible;
        DriverStackLabel.Visibility = Visibility.Collapsed;
        StackPanel.Children.Clear();
        var imagePath = KeyboardNoteStore.FindFirstImageForDevice(_instancePath);
        StackPanel.Children.Add(CreateThumbnailElement(imagePath));
    }

    private UIElement CreateThumbnailElement(string? imagePath)
    {
        // 約 2:1 の横長サムネイル枠
        var border = new Border
        {
            Width = 160,
            Height = 80,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                Source = new BitmapImage(new Uri(imagePath))
            };
            border.Child = image;
            border.Tag = imagePath;
            border.PointerPressed += Thumbnail_PointerPressed;
            ToolTipService.SetToolTip(border, Localization.Get("Tooltip_OpenImage"));
        }
        else
        {
            border.Child = new TextBlock
            {
                Text = Localization.Get("Stack_NoImage"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Opacity = 0.7,
                TextWrapping = TextWrapping.WrapWholeWords,
                Margin = new Thickness(4)
            };
        }

        return border;
    }

    private static Border CreateFilterBox(string text, bool emphasize = false, bool subtle = false)
    {
        return new Border
        {
            MinWidth = 80,
            MinHeight = 48,
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(emphasize ? 2 : 1),
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = (Brush)Application.Current.Resources[
                emphasize ? "AccentTextFillColorPrimaryBrush" : "CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources[
                subtle ? "SubtleFillColorSecondaryBrush" : "CardBackgroundFillColorDefaultBrush"],
            Child = new TextBlock
            {
                Text = text,
                FontWeight = emphasize ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Opacity = subtle ? 0.65 : 1,
                TextWrapping = TextWrapping.WrapWholeWords,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private static UIElement CreateConnector()
    {
        var brush = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        var canvas = new Canvas
        {
            Width = 28,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0)
        };
        canvas.Children.Add(new Line
        {
            X1 = 0,
            Y1 = 12,
            X2 = 18,
            Y2 = 12,
            Stroke = brush,
            StrokeThickness = 1.5
        });
        canvas.Children.Add(new Line
        {
            X1 = 12,
            Y1 = 6,
            X2 = 20,
            Y2 = 12,
            Stroke = brush,
            StrokeThickness = 1.5
        });
        canvas.Children.Add(new Line
        {
            X1 = 12,
            Y1 = 18,
            X2 = 20,
            Y2 = 12,
            Stroke = brush,
            StrokeThickness = 1.5
        });
        return canvas;
    }

    private void Thumbnail_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
        {
            KeyboardNoteStore.OpenImage(path);
        }
    }

    private async Task ShowMemoViewAsync()
    {
        var markdown = MemoEditBox.Text ?? string.Empty;
        var htmlBody = Markdown.ToHtml(markdown, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        var html =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>" +
            "<style>" +
            "body { font-family: 'Segoe UI', 'Yu Gothic UI', 'Meiryo UI', sans-serif; font-size: 14px; margin: 12px; color: #1a1a1a; background: #fafafa; }" +
            "pre { background: #f0f0f0; padding: 8px; overflow-x: auto; }" +
            "code { font-family: Consolas, monospace; }" +
            "a { color: #0067c0; }" +
            "</style></head><body>" + htmlBody + "</body></html>";

        try
        {
            // レイアウト確定後に初期化（Visibility 切替直後の 0 サイズを避ける）
            await Task.Delay(1);
            if (MemoWebView.ActualHeight < 1)
            {
                await Task.Delay(50);
            }

            if (!_webViewReady || MemoWebView.CoreWebView2 is null)
            {
                await MemoWebView.EnsureCoreWebView2Async();
                _webViewReady = MemoWebView.CoreWebView2 is not null;
            }

            if (!_webViewReady || MemoWebView.CoreWebView2 is null)
            {
                ShowMemoFallback(markdown);
                return;
            }

            // data URI / NavigateToString より file:// の方が WinUI WebView2 で安定する
            _memoPreviewHtmlPath ??= System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "mana-memo-preview-" + Guid.NewGuid().ToString("N") + ".html");
            await File.WriteAllTextAsync(_memoPreviewHtmlPath, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            MemoWebView.CoreWebView2.Navigate(new Uri(_memoPreviewHtmlPath).AbsoluteUri);
            MemoWebView.Visibility = Visibility.Visible;
            MemoViewFallbackBox.Visibility = Visibility.Collapsed;
        }
        catch
        {
            _webViewReady = false;
            ShowMemoFallback(markdown);
        }
    }

    private void ShowMemoFallback(string markdown)
    {
        MemoWebView.Visibility = Visibility.Collapsed;
        MemoViewFallbackBox.Text = markdown;
        MemoViewFallbackBox.Visibility = Visibility.Visible;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        KeyboardNoteStore.OpenKeyboardFolder(_instancePath);
    }

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        PersistMemoIfNeeded();
    }

    private void KeyboardDetailWindow_Closed(object sender, WindowEventArgs args)
    {
        if (!_closingHandled)
        {
            PersistMemoIfNeeded();
        }

        if (!string.IsNullOrEmpty(_memoPreviewHtmlPath))
        {
            try
            {
                File.Delete(_memoPreviewHtmlPath);
            }
            catch
            {
            }

            _memoPreviewHtmlPath = null;
        }
    }

    private void PersistMemoIfNeeded(bool forceAllow = false)
    {
        if (_closingHandled && !forceAllow)
        {
            return;
        }

        if (!forceAllow)
        {
            _closingHandled = true;
        }

        var current = MemoEditBox.Text ?? string.Empty;
        if (!string.Equals(current, _savedMemo, StringComparison.Ordinal))
        {
            try
            {
                KeyboardNoteStore.SaveMemo(_instancePath, current);
                _savedMemo = current;
            }
            catch
            {
            }
        }
    }
}
