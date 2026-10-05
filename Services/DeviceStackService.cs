using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Mana.Services;

public sealed class DeviceFilterStackInfo
{
    public IReadOnlyList<string> UpperFilters { get; init; } = [];
    public IReadOnlyList<string> LowerFilters { get; init; } = [];
    public IReadOnlyList<string> Stack { get; init; } = [];
}

public static class DeviceStackService
{
    private static readonly Guid GuidDevClassKeyboard = new("4d36e96b-e325-11ce-bfc1-08002be10318");
    private static readonly Devpropkey DevpkeyDeviceUpperFilters = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 19);
    private static readonly Devpropkey DevpkeyDeviceLowerFilters = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 20);
    // DEVPKEY_Device_Stack — fmtid は DEVPKEY_Device_BusReportedDeviceDesc と同じカテゴリ (PID 14)
    // 誤って a45c254e / PID 24 (EnumeratorName = HID/ACPI) を指定するとスタックが壊れて見える
    private static readonly Devpropkey DevpkeyDeviceStack = new(new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), 14);

    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint DevpropTypeStringList = 0x00002012;
    private const uint DevpropTypeString = 0x00000012;

    public static DeviceFilterStackInfo GetFilterStack(string instancePath)
    {
        var upper = GetDeviceStringListProperty(instancePath, DevpkeyDeviceUpperFilters);
        var lower = GetDeviceStringListProperty(instancePath, DevpkeyDeviceLowerFilters);
        // DEVPKEY_Device_Stack = 実行時のドライバ並び（同一ドライバの複数回登場も含む）
        var stack = GetDeviceStringListProperty(instancePath, DevpkeyDeviceStack)
            .Select(NormalizeDriverName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (upper.Count == 0)
        {
            upper = ReadRegistryMultiString($@"SYSTEM\CurrentControlSet\Enum\{instancePath}", "UpperFilters");
        }

        if (lower.Count == 0)
        {
            lower = ReadRegistryMultiString($@"SYSTEM\CurrentControlSet\Enum\{instancePath}", "LowerFilters");
        }

        if (upper.Count == 0)
        {
            upper = ReadClassFilters("UpperFilters");
        }

        if (lower.Count == 0)
        {
            lower = ReadClassFilters("LowerFilters");
        }

        upper = upper.Select(NormalizeDriverName).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        lower = lower.Select(NormalizeDriverName).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        return new DeviceFilterStackInfo
        {
            UpperFilters = upper,
            LowerFilters = lower,
            Stack = stack
        };
    }

    public static string NormalizeDriverName(string name)
    {
        var value = name.Trim();
        if (value.StartsWith(@"\Driver\", StringComparison.OrdinalIgnoreCase))
        {
            value = value[@"\Driver\".Length..];
        }

        return value;
    }

    public static bool IsKbdClass(string driverName) =>
        string.Equals(NormalizeDriverName(driverName), "kbdclass", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ReadClassFilters(string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Control\Class\{GuidDevClassKeyboard:B}");
            if (key?.GetValue(valueName) is string[] multi)
            {
                return multi.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }

            if (key?.GetValue(valueName) is string single && !string.IsNullOrWhiteSpace(single))
            {
                return [single];
            }
        }
        catch
        {
        }

        return [];
    }

    private static IReadOnlyList<string> ReadRegistryMultiString(string relativePath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(relativePath);
            if (key?.GetValue(valueName) is string[] multi)
            {
                return multi.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }

            if (key?.GetValue(valueName) is string single && !string.IsNullOrWhiteSpace(single))
            {
                return [single];
            }
        }
        catch
        {
        }

        return [];
    }

    private static IReadOnlyList<string> GetDeviceStringListProperty(string instancePath, Devpropkey propertyKey)
    {
        var deviceInfoSet = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
        {
            return GetDeviceStringListPropertyByEnum(instancePath, propertyKey);
        }

        try
        {
            var deviceInfoData = new SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SpDevinfoData>() };
            if (!SetupDiOpenDeviceInfo(deviceInfoSet, instancePath, IntPtr.Zero, 0, ref deviceInfoData))
            {
                return GetDeviceStringListPropertyByEnum(instancePath, propertyKey);
            }

            return GetDevicePropertyStringList(deviceInfoSet, ref deviceInfoData, propertyKey);
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static IReadOnlyList<string> GetDeviceStringListPropertyByEnum(string instancePath, Devpropkey propertyKey)
    {
        var deviceInfoSet = SetupDiGetClassDevs(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
        {
            return [];
        }

        try
        {
            var index = 0u;
            var deviceInfoData = new SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SpDevinfoData>() };
            while (SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfoData))
            {
                var id = GetDeviceInstanceId(deviceInfoSet, ref deviceInfoData);
                if (string.Equals(id, instancePath, StringComparison.OrdinalIgnoreCase))
                {
                    return GetDevicePropertyStringList(deviceInfoSet, ref deviceInfoData, propertyKey);
                }

                index++;
                deviceInfoData = new SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SpDevinfoData>() };
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return [];
    }

    private static IReadOnlyList<string> GetDevicePropertyStringList(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        Devpropkey propertyKey)
    {
        var key = propertyKey;
        if (!SetupDiGetDeviceProperty(
                deviceInfoSet,
                ref deviceInfoData,
                ref key,
                out _,
                null,
                0,
                out var requiredSize,
                0) && requiredSize == 0)
        {
            return [];
        }

        var buffer = new byte[requiredSize];
        if (!SetupDiGetDeviceProperty(
                deviceInfoSet,
                ref deviceInfoData,
                ref key,
                out var propertyType,
                buffer,
                (uint)buffer.Length,
                out _,
                0))
        {
            return [];
        }

        if (propertyType != DevpropTypeStringList && propertyType != DevpropTypeString)
        {
            return [];
        }

        var text = Encoding.Unicode.GetString(buffer);
        return text.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string? GetDeviceInstanceId(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData)
    {
        var buffer = new StringBuilder(512);
        if (!SetupDiGetDeviceInstanceId(deviceInfoSet, ref deviceInfoData, buffer, buffer.Capacity, out _))
        {
            return null;
        }

        return buffer.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevinfoData
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Devpropkey
    {
        public Guid Fmtid;
        public uint Pid;

        public Devpropkey(Guid fmtid, uint pid)
        {
            Fmtid = fmtid;
            Pid = pid;
        }
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr classGuid, IntPtr hwndParent);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiOpenDeviceInfo(
        IntPtr deviceInfoSet,
        string deviceInstanceId,
        IntPtr hwndParent,
        uint openFlags,
        ref SpDevinfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        IntPtr classGuid,
        IntPtr enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SpDevinfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        StringBuilder deviceInstanceId,
        int deviceInstanceIdSize,
        out int requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceProperty(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        ref Devpropkey propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
}
