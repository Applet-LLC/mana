using System.Runtime.InteropServices;
using System.Text;
using Mana.Models;
using Microsoft.Win32;

namespace Mana.Services;

public static class KeyboardEnumerator
{
    private static readonly Guid GuidDevClassKeyboard = new("4d36e96b-e325-11ce-bfc1-08002be10318");
    private static readonly Devpropkey DevpkeyDeviceContainerId = new(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);
    private static readonly Devpropkey DevpkeyDeviceBusReportedDeviceDesc = new(new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), 4);
    private static readonly Devpropkey DevpkeyDeviceFriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    private const uint DigcfPresent = 0x00000002;
    private const uint SpdrpFriendlyName = 0x0000000C;
    private const uint SpdrpDeviceDesc = 0x00000000;
    private const uint SpdrpHardwareId = 0x00000001;
    private const uint SpdrpMfg = 0x0000000B;
    private const uint DevpropTypeGuid = 0x0000000D;
    private const uint DevpropTypeString = 0x00000012;
    private const uint DevpropTypeStringList = 0x00002012;
    private const int CrSuccess = 0;
    private const int CrBufferSmall = 0x0000001A;

    private static readonly HashSet<string> GenericDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HID Keyboard Device",
        "HID キーボード デバイス",
        "HID キーボードデバイス",
        "Standard PS/2 Keyboard",
        "標準 PS/2 キーボード",
        "PS/2 Keyboard",
        "Bluetooth HID Device",
        "Bluetooth HID デバイス",
        "Bluetooth Device",
        "Bluetooth LE Device",
        "Bluetooth デバイス",
        "USB Input Device",
        "USB 入力デバイス",
        "USB Composite Device",
        "USB 複合デバイス",
        "HID-compliant consumer control device",
        "HID-compliant vendor-defined device",
        "Keyboard",
        "キーボード"
    };

    public static IReadOnlyList<KeyboardDeviceInfo> Enumerate()
    {
        var bluetoothNames = LoadBluetoothFriendlyNames();
        var results = new List<KeyboardDeviceInfo>();
        var classGuid = GuidDevClassKeyboard;
        var deviceInfoSet = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
        {
            return results;
        }

        try
        {
            var index = 0u;
            var deviceInfoData = new SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SpDevinfoData>() };
            while (SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfoData))
            {
                var instanceId = GetDeviceInstanceId(deviceInfoSet, ref deviceInfoData);
                if (!string.IsNullOrWhiteSpace(instanceId))
                {
                    var friendly = GetRegistryPropertyString(deviceInfoSet, ref deviceInfoData, SpdrpFriendlyName)
                                   ?? GetRegistryPropertyString(deviceInfoSet, ref deviceInfoData, SpdrpDeviceDesc)
                                   ?? instanceId;
                    var hardwareIdList = GetRegistryPropertyMultiStringList(
                        deviceInfoSet,
                        ref deviceInfoData,
                        SpdrpHardwareId);
                    var firstHardwareId = hardwareIdList.Count > 0 ? hardwareIdList[0] : null;
                    var hardwareIds = hardwareIdList.Count > 0
                        ? string.Join("; ", hardwareIdList)
                        : null;
                    var manufacturer = GetRegistryPropertyString(deviceInfoSet, ref deviceInfoData, SpdrpMfg);
                    var deviceOverride = DeviceOverrideStore.Read(instanceId);
                    var deviceName = ResolveDeviceName(
                        deviceInfoSet,
                        ref deviceInfoData,
                        instanceId,
                        friendly,
                        bluetoothNames);

                    results.Add(new KeyboardDeviceInfo
                    {
                        InstancePath = instanceId,
                        FriendlyName = friendly,
                        ShellFriendlyName = deviceName,
                        HardwareIds = hardwareIds,
                        FirstHardwareId = firstHardwareId,
                        InstanceKeyHashText = DeviceIdHash.TryFormatInstanceKey(instanceId),
                        ModelKeyHashText = DeviceIdHash.TryFormatModelKey(firstHardwareId),
                        Manufacturer = manufacturer,
                        DeviceOverride = deviceOverride
                    });
                }

                index++;
                deviceInfoData = new SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SpDevinfoData>() };
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return results
            .OrderBy(d => d.ShellFriendlyName ?? d.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string? ResolveDeviceName(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        string instanceId,
        string deviceManagerName,
        BluetoothNameIndex bluetoothNames)
    {
        var containerId = GetDevicePropertyGuid(deviceInfoSet, ref deviceInfoData, DevpkeyDeviceContainerId);

        // Bluetooth: Enum\BTHENUM / BTHLE の FriendlyName（Devices and Printers と同系統）
        if (containerId.HasValue &&
            bluetoothNames.ByContainerId.TryGetValue(containerId.Value, out var byContainer) &&
            IsUsefulDeviceName(byContainer, deviceManagerName))
        {
            return byContainer;
        }

        foreach (var address in ExtractBluetoothAddresses(instanceId))
        {
            if (bluetoothNames.ByAddress.TryGetValue(address, out var byAddress) &&
                IsUsefulDeviceName(byAddress, deviceManagerName))
            {
                return byAddress;
            }
        }

        var btAncestor = FindBluetoothRegistryFriendlyName(deviceInfoData.DevInst, deviceManagerName);
        if (IsUsefulDeviceName(btAncestor, deviceManagerName))
        {
            return btAncestor;
        }

        // USB: 親の USB\VID_* ノードの BusReportedDeviceDesc = iProduct（USBView と同じ文字列）
        var usbProduct = FindUsbProductName(deviceInfoData.DevInst, deviceManagerName);
        if (IsUsefulDeviceName(usbProduct, deviceManagerName))
        {
            return usbProduct;
        }

        // 最後の手段: 自身の BusReportedDeviceDesc
        var selfBus = GetDevicePropertyString(deviceInfoSet, ref deviceInfoData, DevpkeyDeviceBusReportedDeviceDesc);
        if (IsUsefulDeviceName(selfBus, deviceManagerName))
        {
            return selfBus;
        }

        return null;
    }

    private static string? FindUsbProductName(uint devInst, string deviceManagerName)
    {
        var current = devInst;
        for (var depth = 0; depth < 12; depth++)
        {
            var instanceId = GetInstanceIdFromDevInst(current);
            if (!string.IsNullOrEmpty(instanceId) &&
                instanceId.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase))
            {
                var busReported = GetBusReportedNameFromDevInst(current);
                if (IsUsefulDeviceName(busReported, deviceManagerName))
                {
                    return busReported;
                }

                // まれに FriendlyName に製品名が入る場合
                var friendly = GetFriendlyNameFromDevInst(current);
                if (IsUsefulDeviceName(friendly, deviceManagerName))
                {
                    return friendly;
                }
            }

            if (CM_Get_Parent(out var parent, current, 0) != CrSuccess)
            {
                break;
            }

            current = parent;
        }

        return null;
    }

    private static string? FindBluetoothRegistryFriendlyName(uint devInst, string deviceManagerName)
    {
        var current = devInst;
        for (var depth = 0; depth < 12; depth++)
        {
            var instanceId = GetInstanceIdFromDevInst(current);
            if (!string.IsNullOrEmpty(instanceId) &&
                (instanceId.StartsWith("BTHENUM\\", StringComparison.OrdinalIgnoreCase) ||
                 instanceId.StartsWith("BTHLE\\", StringComparison.OrdinalIgnoreCase) ||
                 instanceId.StartsWith("BTHLEDEVICE\\", StringComparison.OrdinalIgnoreCase)))
            {
                var name = ReadEnumFriendlyName(instanceId);
                if (IsUsefulDeviceName(name, deviceManagerName))
                {
                    return name;
                }
            }

            if (CM_Get_Parent(out var parent, current, 0) != CrSuccess)
            {
                break;
            }

            current = parent;
        }

        return null;
    }

    private static BluetoothNameIndex LoadBluetoothFriendlyNames()
    {
        var index = new BluetoothNameIndex();
        foreach (var root in new[] { "BTHENUM", "BTHLE" })
        {
            ScanBluetoothEnumTree(
                $@"SYSTEM\CurrentControlSet\Enum\{root}",
                index);
        }

        return index;
    }

    private static void ScanBluetoothEnumTree(string relativePath, BluetoothNameIndex index)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(relativePath);
            if (key is null)
            {
                return;
            }

            ScanBluetoothEnumKey(key, relativePath, index);
        }
        catch
        {
        }
    }

    private static void ScanBluetoothEnumKey(RegistryKey key, string relativePath, BluetoothNameIndex index)
    {
        try
        {
            if (key.GetValue("FriendlyName") is string friendly &&
                !string.IsNullOrWhiteSpace(friendly) &&
                !IsGenericDeviceName(friendly))
            {
                foreach (var address in ExtractBluetoothAddresses(relativePath))
                {
                    index.ByAddress[address] = friendly;
                }

                if (key.GetValue("ContainerID") is string containerText &&
                    Guid.TryParse(containerText, out var containerId))
                {
                    index.ByContainerId[containerId] = friendly;
                }
            }
        }
        catch
        {
        }

        foreach (var subName in key.GetSubKeyNames())
        {
            if (subName.Equals("Device Parameters", StringComparison.OrdinalIgnoreCase) ||
                subName.Equals("Properties", StringComparison.OrdinalIgnoreCase) ||
                subName.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                subName.Equals("LogConf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var sub = key.OpenSubKey(subName);
                if (sub is not null)
                {
                    ScanBluetoothEnumKey(sub, relativePath + "\\" + subName, index);
                }
            }
            catch
            {
            }
        }
    }

    private static string? ReadEnumFriendlyName(string instanceId)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + instanceId);
            return key?.GetValue("FriendlyName") as string;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> ExtractBluetoothAddresses(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        // Dev_ED96002D80D3 / Dev_7363b92732d2 / BluetoothDevice_ED96002D80D3
        foreach (var token in text.Split(new[] { '\\', '&', '#', '{', '}', '.' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var value = token;
            if (value.StartsWith("Dev_", StringComparison.OrdinalIgnoreCase))
            {
                value = value[4..];
            }
            else if (value.StartsWith("BluetoothDevice_", StringComparison.OrdinalIgnoreCase))
            {
                value = value["BluetoothDevice_".Length..];
            }

            if (value.Length is >= 8 and <= 16 && IsHex(value))
            {
                yield return value.ToUpperInvariant();
            }
        }
    }

    private static bool IsHex(string value)
    {
        foreach (var ch in value)
        {
            var isHex = (ch >= '0' && ch <= '9') ||
                        (ch >= 'a' && ch <= 'f') ||
                        (ch >= 'A' && ch <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUsefulDeviceName(string? candidate, string deviceManagerName)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.Equals(candidate, deviceManagerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsGenericDeviceName(candidate);
    }

    private static bool IsGenericDeviceName(string name) =>
        GenericDeviceNames.Contains(name.Trim());

    private static string? GetInstanceIdFromDevInst(uint devInst)
    {
        var size = 0;
        var cr = CM_Get_Device_ID_Size(out size, devInst, 0);
        if (cr != CrSuccess || size <= 0)
        {
            return null;
        }

        var buffer = new StringBuilder(size + 2);
        cr = CM_Get_Device_ID(devInst, buffer, buffer.Capacity, 0);
        return cr == CrSuccess ? buffer.ToString() : null;
    }

    private static string? GetFriendlyNameFromDevInst(uint devInst) =>
        GetDevNodeStringProperty(devInst, DevpkeyDeviceFriendlyName)
        ?? GetDevNodeRegistryProperty(devInst, SpdrpFriendlyName)
        ?? GetDevNodeRegistryProperty(devInst, SpdrpDeviceDesc);

    private static string? GetBusReportedNameFromDevInst(uint devInst) =>
        GetDevNodeStringProperty(devInst, DevpkeyDeviceBusReportedDeviceDesc);

    private static string? GetDevNodeStringProperty(uint devInst, Devpropkey propertyKey)
    {
        var key = propertyKey;
        uint propertyType = 0;
        var required = 0u;
        var cr = CM_Get_DevNode_Property(devInst, ref key, out propertyType, null, ref required, 0);
        if ((cr != CrSuccess && cr != CrBufferSmall) || required == 0)
        {
            return null;
        }

        var buffer = new byte[required];
        cr = CM_Get_DevNode_Property(devInst, ref key, out propertyType, buffer, ref required, 0);
        if (cr != CrSuccess)
        {
            return null;
        }

        if (propertyType == DevpropTypeString || propertyType == DevpropTypeStringList)
        {
            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }

        return null;
    }

    private static string? GetDevNodeRegistryProperty(uint devInst, uint property)
    {
        var required = 0u;
        uint regDataType = 0;
        var cr = CM_Get_DevNode_Registry_Property(devInst, property, out regDataType, null, ref required, 0);
        if ((cr != CrSuccess && cr != CrBufferSmall) || required == 0)
        {
            return null;
        }

        var buffer = new byte[required];
        cr = CM_Get_DevNode_Registry_Property(devInst, property, out _, buffer, ref required, 0);
        if (cr != CrSuccess)
        {
            return null;
        }

        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
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

    private static string? GetRegistryPropertyString(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, uint property)
    {
        if (!SetupDiGetDeviceRegistryProperty(
                deviceInfoSet,
                ref deviceInfoData,
                property,
                out _,
                null,
                0,
                out var requiredSize) && requiredSize == 0)
        {
            return null;
        }

        var buffer = new byte[requiredSize];
        if (!SetupDiGetDeviceRegistryProperty(
                deviceInfoSet,
                ref deviceInfoData,
                property,
                out _,
                buffer,
                (uint)buffer.Length,
                out _))
        {
            return null;
        }

        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    private static IReadOnlyList<string> GetRegistryPropertyMultiStringList(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        uint property)
    {
        var value = GetRegistryPropertyString(deviceInfoSet, ref deviceInfoData, property);
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<string>();
        }

        return value.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static Guid? GetDevicePropertyGuid(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, Devpropkey propertyKey)
    {
        var buffer = new byte[16];
        var key = propertyKey;
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
            return null;
        }

        if (propertyType != DevpropTypeGuid)
        {
            return null;
        }

        return new Guid(buffer);
    }

    private static string? GetDevicePropertyString(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, Devpropkey propertyKey)
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
            return null;
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
            return null;
        }

        if (propertyType != DevpropTypeString && propertyType != DevpropTypeStringList)
        {
            return null;
        }

        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    private sealed class BluetoothNameIndex
    {
        public Dictionary<Guid, string> ByContainerId { get; } = new();
        public Dictionary<string, string> ByAddress { get; } = new(StringComparer.OrdinalIgnoreCase);
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
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
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
    private static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

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

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_Size(out int pulLen, uint dnDevInst, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID(uint dnDevInst, StringBuilder buffer, int bufferLen, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_DevNode_Property(
        uint dnDevInst,
        ref Devpropkey propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_DevNode_Registry_Property(
        uint dnDevInst,
        uint property,
        out uint propertyRegDataType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint flags);
}
