namespace Mana.Models;

public sealed class KeyboardDeviceInfo
{
    public required string InstancePath { get; init; }
    public required string FriendlyName { get; init; }
    /// <summary>Devices and Printers 寄りの表示名（取得できない／同一の場合は null）。</summary>
    public string? ShellFriendlyName { get; init; }
    public string? HardwareIds { get; init; }
    public string? Manufacturer { get; init; }
    public DeviceOverrideValues DeviceOverride { get; init; } = new();
}
