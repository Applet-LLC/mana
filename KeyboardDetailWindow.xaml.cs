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
        MemoPivot.SelectedIndex = 0;

        Activated += async (_, _) => await EnsureWebViewAsync();
        AppWindow.Closing += AppWindow_Closing;
        Closed += KeyboardDetailWindow_Closed;
    }

    private void ApplyLocalizedChrome()
    {
        DriverStackLabel.Text = Localization.Get("Label_DriverStack");
        MemoLabel.Text = Localization.Get("Label_Memo");
        ViewPivotItem.Header = Localization.Get("Tab_MemoView");
        EditPivotItem.Header = Localization.Get("Tab_MemoEdit");
        OpenFolderButton.Content = Localization.Get("Button_OpenFolder");
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

    private async Task EnsureWebViewAsync()
    {
        if (_webViewReady)
        {
            await RenderMemoViewAsync();
            return;
        }

        try
        {
            await MemoWebView.EnsureCoreWebView2Async();
            _webViewReady = true;
            await RenderMemoViewAsync();
        }
        catch
        {
            // WebView2 runtime missing: leave blank; edit tab still works.
        }
    }

    private async Task RenderMemoViewAsync()
    {
        if (!_webViewReady || MemoWebView.CoreWebView2 is null)
        {
            return;
        }

        var markdown = MemoEditBox.Text ?? string.Empty;
        var htmlBody = Markdown.ToHtml(markdown, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        var html =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>" +
            "<style>" +
            "body { font-family: 'Segoe UI', sans-serif; font-size: 14px; margin: 12px; color: #1a1a1a; background: #fafafa; }" +
            "pre { background: #f0f0f0; padding: 8px; overflow-x: auto; }" +
            "code { font-family: Consolas, monospace; }" +
            "a { color: #0067c0; }" +
            "</style></head><body>" + htmlBody + "</body></html>";
        MemoWebView.NavigateToString(html);
        await Task.CompletedTask;
    }

    private async void MemoPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MemoPivot.SelectedIndex == 0)
        {
            await EnsureWebViewAsync();
        }
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
    }

    private void PersistMemoIfNeeded()
    {
        if (_closingHandled)
        {
            return;
        }

        _closingHandled = true;
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
