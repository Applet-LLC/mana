using Mana.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace Mana;

public sealed class PastKeyboardItem
{
    public required string InstancePath { get; init; }
    public required string DisplayName { get; init; }
    public BitmapImage ThumbnailSource { get; init; } = new();
    public bool HasThumbnail { get; init; }
    public bool IsConnected { get; init; }
    public string ConnectedLabel { get; init; } = string.Empty;
    public string NoImageLabel { get; init; } = string.Empty;
    public Visibility ConnectedVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ThumbnailVisibility => HasThumbnail ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoThumbnailVisibility => HasThumbnail ? Visibility.Collapsed : Visibility.Visible;
}

public sealed partial class PastKeyboardsWindow : Window
{
    public PastKeyboardsWindow(IReadOnlySet<string> connectedInstancePaths)
    {
        InitializeComponent();
        HeaderText.Text = Localization.Get("Section_PastKeyboards");
        Title = Localization.Get("Section_PastKeyboards");

        try
        {
            AppWindow.Resize(new SizeInt32(720, 560));
        }
        catch
        {
        }

        Refresh(connectedInstancePaths);
    }

    public void Refresh(IReadOnlySet<string> connectedInstancePaths)
    {
        HeaderText.Text = Localization.Get("Section_PastKeyboards");
        Title = Localization.Get("Section_PastKeyboards");
        EmptyText.Text = Localization.Get("Status_NoHistory");
        var connectedLabel = Localization.Get("Badge_Connected");
        var noImageLabel = Localization.Get("Stack_NoImage");

        var items = KeyboardMetaStore.ListEntries(connectedInstancePaths)
            .Select(e =>
            {
                BitmapImage? thumb = null;
                if (!string.IsNullOrWhiteSpace(e.ThumbnailPath) && File.Exists(e.ThumbnailPath))
                {
                    try
                    {
                        thumb = new BitmapImage(new Uri(e.ThumbnailPath));
                    }
                    catch
                    {
                        thumb = null;
                    }
                }

                return new PastKeyboardItem
                {
                    InstancePath = e.InstancePath,
                    DisplayName = e.DisplayName,
                    // null を Image.Source にバインドすると ConvertValue で落ちるため、常に非 null を渡す
                    ThumbnailSource = thumb ?? new BitmapImage(),
                    HasThumbnail = thumb is not null,
                    IsConnected = e.IsConnected,
                    ConnectedLabel = connectedLabel,
                    NoImageLabel = noImageLabel
                };
            })
            .ToList();

        HistoryList.ItemsSource = items;
        var empty = items.Count == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        HistoryList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HistoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PastKeyboardItem item)
        {
            return;
        }

        KeyboardDetailWindowManager.OpenDetail(
            item.InstancePath,
            item.DisplayName,
            shellFriendlyName: null,
            showDriverStack: false);
    }
}
