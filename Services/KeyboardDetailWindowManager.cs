using Microsoft.UI.Xaml;

namespace Mana.Services;

public static class KeyboardDetailWindowManager
{
    private static readonly Dictionary<string, KeyboardDetailWindow> OpenDetails =
        new(StringComparer.OrdinalIgnoreCase);

    private static PastKeyboardsWindow? _pastWindow;

    public static void OpenDetail(
        string instancePath,
        string displayName,
        string? shellFriendlyName,
        bool showDriverStack)
    {
        if (OpenDetails.TryGetValue(instancePath, out var existing))
        {
            try
            {
                existing.Activate();
                existing.AppWindow.MoveInZOrderAtTop();
                return;
            }
            catch
            {
                OpenDetails.Remove(instancePath);
            }
        }

        var window = new KeyboardDetailWindow(instancePath, displayName, shellFriendlyName, showDriverStack);
        OpenDetails[instancePath] = window;
        window.Closed += (_, _) => OpenDetails.Remove(instancePath);
        window.Activate();
    }

    public static void OpenPastList(IReadOnlySet<string> connectedInstancePaths)
    {
        if (_pastWindow is not null)
        {
            try
            {
                _pastWindow.Refresh(connectedInstancePaths);
                _pastWindow.Activate();
                _pastWindow.AppWindow.MoveInZOrderAtTop();
                return;
            }
            catch
            {
                _pastWindow = null;
            }
        }

        _pastWindow = new PastKeyboardsWindow(connectedInstancePaths);
        _pastWindow.Closed += (_, _) => _pastWindow = null;
        _pastWindow.Activate();
    }
}
