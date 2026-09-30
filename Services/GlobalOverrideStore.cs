using Mana.Models;
using Microsoft.Win32;

namespace Mana.Services;

public static class GlobalOverrideStore
{
    private const string KeyPath = @"SYSTEM\CurrentControlSet\Services\i8042prt\Parameters";
    private const string TypeValueName = "OverrideKeyboardType";
    private const string SubtypeValueName = "OverrideKeyboardSubtype";
    private const string IdentifierValueName = "OverrideKeyboardIdentifier";
    private const string LayerDriverJpnValueName = "LayerDriver JPN";
    private const string LayerDriverKorValueName = "LayerDriver KOR";

    public static GlobalOverrideValues Read()
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath, writable: false);
        if (key is null)
        {
            return new GlobalOverrideValues();
        }

        return new GlobalOverrideValues
        {
            Type = ReadDword(key, TypeValueName),
            Subtype = ReadDword(key, SubtypeValueName),
            Identifier = key.GetValue(IdentifierValueName) as string,
            LayerDriverJpn = key.GetValue(LayerDriverJpnValueName) as string,
            LayerDriverKor = key.GetValue(LayerDriverKorValueName) as string
        };
    }

    public static void Write(GlobalOverrideValues values)
    {
        using var key = Registry.LocalMachine.CreateSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open i8042prt Parameters.");

        WriteOrDeleteDword(key, TypeValueName, values.Type);
        WriteOrDeleteDword(key, SubtypeValueName, values.Subtype);
        WriteOrDeleteString(key, IdentifierValueName, values.Identifier);
        WriteOrDeleteString(key, LayerDriverJpnValueName, values.LayerDriverJpn);
        WriteOrDeleteString(key, LayerDriverKorValueName, values.LayerDriverKor);
    }

    public static void ApplyPreset(LayoutPreset preset) => Write(BuildPresetValues(preset, Read()));

    public static GlobalOverrideValues BuildPresetValues(LayoutPreset preset, GlobalOverrideValues current)
    {
        var next = new GlobalOverrideValues
        {
            Type = preset.Type,
            Subtype = preset.Subtype,
            Identifier = current.Identifier,
            LayerDriverJpn = current.LayerDriverJpn,
            LayerDriverKor = current.LayerDriverKor
        };

        if (preset.ClearGlobalExtras)
        {
            next.Identifier = null;
            next.LayerDriverJpn = null;
            next.LayerDriverKor = null;
        }
        else
        {
            if (!string.IsNullOrEmpty(preset.Identifier))
            {
                next.Identifier = preset.Identifier;
            }

            if (!string.IsNullOrEmpty(preset.LayerDriverValueName) &&
                !string.IsNullOrEmpty(preset.LayerDriverValue))
            {
                if (string.Equals(preset.LayerDriverValueName, LayerDriverJpnValueName, StringComparison.OrdinalIgnoreCase))
                {
                    next.LayerDriverJpn = preset.LayerDriverValue;
                }
                else if (string.Equals(preset.LayerDriverValueName, LayerDriverKorValueName, StringComparison.OrdinalIgnoreCase))
                {
                    next.LayerDriverKor = preset.LayerDriverValue;
                }
            }
        }

        return next;
    }

    public static void ClearAll()
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        TryDeleteValue(key, TypeValueName);
        TryDeleteValue(key, SubtypeValueName);
        TryDeleteValue(key, IdentifierValueName);
        TryDeleteValue(key, LayerDriverJpnValueName);
        TryDeleteValue(key, LayerDriverKorValueName);
    }

    private static int? ReadDword(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        return value switch
        {
            int i => i,
            long l => (int)l,
            _ => null
        };
    }

    private static void WriteOrDeleteDword(RegistryKey key, string name, int? value)
    {
        if (value.HasValue)
        {
            key.SetValue(name, value.Value, RegistryValueKind.DWord);
        }
        else
        {
            TryDeleteValue(key, name);
        }
    }

    private static void WriteOrDeleteString(RegistryKey key, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            key.SetValue(name, value, RegistryValueKind.String);
        }
        else
        {
            TryDeleteValue(key, name);
        }
    }

    private static void TryDeleteValue(RegistryKey key, string name)
    {
        try
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (ArgumentException)
        {
        }
    }
}
