using Mana.Models;
using Microsoft.Win32;

namespace Mana.Services;

public static class DeviceOverrideStore
{
    private const string TypeValueName = "KeyboardTypeOverride";
    private const string SubtypeValueName = "KeyboardSubtypeOverride";

    public static DeviceOverrideValues Read(string instancePath)
    {
        using var key = OpenDeviceParameters(instancePath, writable: false);
        if (key is null)
        {
            return new DeviceOverrideValues();
        }

        return new DeviceOverrideValues
        {
            Type = ReadDword(key, TypeValueName),
            Subtype = ReadDword(key, SubtypeValueName)
        };
    }

    public static void Write(string instancePath, int? type, int? subtype)
    {
        using var key = OpenDeviceParameters(instancePath, writable: true)
            ?? throw new InvalidOperationException($"Unable to open Device Parameters for: {instancePath}");

        WriteOrDeleteDword(key, TypeValueName, type);
        WriteOrDeleteDword(key, SubtypeValueName, subtype);
    }

    public static void Clear(string instancePath)
    {
        using var key = OpenDeviceParameters(instancePath, writable: true);
        if (key is null)
        {
            return;
        }

        TryDeleteValue(key, TypeValueName);
        TryDeleteValue(key, SubtypeValueName);
    }

    private static RegistryKey? OpenDeviceParameters(string instancePath, bool writable)
    {
        var path = $@"SYSTEM\CurrentControlSet\Enum\{instancePath}\Device Parameters";
        if (writable)
        {
            var key = Registry.LocalMachine.OpenSubKey(path, writable: true);
            if (key is not null)
            {
                return key;
            }

            return Registry.LocalMachine.CreateSubKey(path, writable: true);
        }

        return Registry.LocalMachine.OpenSubKey(path, writable: false);
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

    private static void TryDeleteValue(RegistryKey key, string name)
    {
        try
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (ArgumentException)
        {
            // Missing value is fine.
        }
    }
}
