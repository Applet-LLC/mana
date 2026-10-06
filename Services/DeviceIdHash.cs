using Microsoft.Win32;

namespace Mana.Services;

/// <summary>
/// LayerDriver / kbdlayer の本線と同じ DeviceId ハッシュ（フォールバックなし）を算出する。
/// 仕様: LayerDriver docs/DEVICE-ID.md
/// </summary>
public static class DeviceIdHash
{
    /// <summary>16bit FNV-1a を上位16bitに置いた32bit値。</summary>
    public static uint Fnv1aHash16Upper(string s)
    {
        ushort h = 0x9DC5;
        foreach (var ch in s)
        {
            h = (ushort)((h ^ (byte)(ch & 0xFF)) * 0x0193);
            h = (ushort)((h ^ (byte)(ch >> 8)) * 0x0193);
        }

        return (uint)h << 16;
    }

    public static string Format(uint hash) => $"0x{hash:X8}";

    /// <summary>
    /// レジストリ Enum のキー名を連結し、ドライバが見る DeviceInstanceId と同じ大文字小文字にする。
    /// </summary>
    public static string? ResolveEnumInstanceId(string instancePath)
    {
        if (string.IsNullOrWhiteSpace(instancePath))
        {
            return null;
        }

        var parts = instancePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        try
        {
            using var enumRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum");
            if (enumRoot is null)
            {
                return null;
            }

            var resolved = new string[parts.Length];
            RegistryKey current = enumRoot;
            RegistryKey? owned = null;
            try
            {
                for (var i = 0; i < parts.Length; i++)
                {
                    string? match = null;
                    foreach (var name in current.GetSubKeyNames())
                    {
                        if (name.Equals(parts[i], StringComparison.OrdinalIgnoreCase))
                        {
                            match = name;
                            break;
                        }
                    }

                    if (match is null)
                    {
                        return null;
                    }

                    resolved[i] = match;

                    if (i == parts.Length - 1)
                    {
                        break;
                    }

                    var next = current.OpenSubKey(match);
                    if (next is null)
                    {
                        return null;
                    }

                    owned?.Dispose();
                    owned = next;
                    current = next;
                }
            }
            finally
            {
                owned?.Dispose();
            }

            return string.Join('\\', resolved);
        }
        catch
        {
            return null;
        }
    }

    public static uint? TryInstanceKey(string instancePath)
    {
        var enumId = ResolveEnumInstanceId(instancePath);
        if (string.IsNullOrEmpty(enumId))
        {
            return null;
        }

        return Fnv1aHash16Upper(enumId);
    }

    /// <summary>HardwareId MULTI_SZ の先頭1件から ModelKey。</summary>
    public static uint? TryModelKey(string? firstHardwareId)
    {
        if (string.IsNullOrEmpty(firstHardwareId))
        {
            return null;
        }

        return Fnv1aHash16Upper(firstHardwareId);
    }

    public static string? TryFormatInstanceKey(string instancePath)
    {
        var hash = TryInstanceKey(instancePath);
        return hash.HasValue ? Format(hash.Value) : null;
    }

    public static string? TryFormatModelKey(string? firstHardwareId)
    {
        var hash = TryModelKey(firstHardwareId);
        return hash.HasValue ? Format(hash.Value) : null;
    }
}
