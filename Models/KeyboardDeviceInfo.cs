namespace Mana.Models;

public sealed class KeyboardDeviceInfo
{
    public required string InstancePath { get; init; }
    public required string FriendlyName { get; init; }
    /// <summary>Devices and Printers 寄りの表示名（取得できない／同一の場合は null）。</summary>
    public string? ShellFriendlyName { get; init; }
    public string? HardwareIds { get; init; }
    /// <summary>HardwareId MULTI_SZ の先頭1件（ModelKey 算出用）。</summary>
    public string? FirstHardwareId { get; init; }
    /// <summary>ドライバ本線の InstanceKey（0xXXXXXXXX）。取得できないときは null。</summary>
    public string? InstanceKeyHashText { get; init; }
    /// <summary>ドライバ本線の ModelKey（0xXXXXXXXX）。取得できないときは null。</summary>
    public string? ModelKeyHashText { get; init; }
    public string? Manufacturer { get; init; }
    public DeviceOverrideValues DeviceOverride { get; init; } = new();
}
