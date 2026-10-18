// ============================================================================
// 模块名称: LogitechDeviceHelper.cs
// 所属区域: Hardware (硬件驱动层)
// 核心职责:
//   罗技 (Logitech) HID++ 1.0/2.0 私有协议通信与电量检测实现
// 依赖关系: NativeMethods.cs, MouseBatteryInfo.cs
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 任何 AI Agent 在修改本文件逻辑、协议解析或签名时，【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace RazerBatteryTray
{
public static class LogitechDeviceHelper
{
    private static readonly object hidLock = new object();
    private const string CacheRegistryKey = @"Software\RazerBatteryTray\LogitechCache";

    [StructLayout(LayoutKind.Sequential)]
    private struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr hEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid interfaceClassGuid;
        public int flags;
        public IntPtr reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [DllImport("hid.dll", SetLastError = true)]
    public static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetProductString(IntPtr hidDeviceObject, StringBuilder buffer, int bufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, ref OVERLAPPED lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, ref OVERLAPPED lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetOverlappedResult(IntPtr hFile, ref OVERLAPPED lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIo(IntPtr hFile);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    private const uint WAIT_OBJECT_0 = 0;

    public static int CachedBatteryPercent = -1;
    public static bool CachedIsCharging = false;
    public static string CachedDeviceName = "罗技无线游戏鼠标";
    public static DateTime CachedLastUpdated = DateTime.MinValue;
    public static bool HasValidHardwareReading = false;
    private static Dictionary<string, int> deviceBatteryCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public static void LoadHardwareCache()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(CacheRegistryKey, false))
            {
                if (key != null)
                {
                    var bp = key.GetValue("BatteryPercent");
                    var isValidObj = key.GetValue("IsValid");
                    bool isValid = (isValidObj != null && Convert.ToInt32(isValidObj) == 1);

                    if (bp != null)
                    {
                        int val = Convert.ToInt32(bp);
                        if (val > 0 && val <= 100 && isValid)
                        {
                            CachedBatteryPercent = val;
                            HasValidHardwareReading = true;
                        }
                    }

                    var dn = key.GetValue("DeviceName") as string;
                    if (!string.IsNullOrEmpty(dn)) CachedDeviceName = dn;

                    var chg = key.GetValue("IsCharging");
                    if (chg != null) CachedIsCharging = (Convert.ToInt32(chg) != 0);

                    var updatedStr = key.GetValue("LastUpdated") as string;
                    if (!string.IsNullOrEmpty(updatedStr))
                    {
                        DateTime dt;
                        if (DateTime.TryParse(updatedStr, out dt)) CachedLastUpdated = dt;
                    }
                }
            }
        }
        catch { }
    }

    public static void SaveHardwareCache()
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(CacheRegistryKey))
            {
                if (key != null)
                {
                    if (CachedBatteryPercent > 0)
                    {
                        key.SetValue("BatteryPercent", CachedBatteryPercent);
                        key.SetValue("IsValid", HasValidHardwareReading ? 1 : 0);
                    }
                    if (!string.IsNullOrEmpty(CachedDeviceName)) key.SetValue("DeviceName", CachedDeviceName);
                    key.SetValue("IsCharging", CachedIsCharging ? 1 : 0);
                    if (CachedLastUpdated != DateTime.MinValue) key.SetValue("LastUpdated", CachedLastUpdated.ToString("o"));
                }
            }
        }
        catch { }
    }

    public static int GetCachedBatteryForDevice(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return CachedBatteryPercent;
        int val;
        if (deviceBatteryCache.TryGetValue(deviceId, out val) && val > 0)
        {
            return val;
        }
        return (HasValidHardwareReading && CachedBatteryPercent > 0) ? CachedBatteryPercent : -1;
    }

    private static byte[] SendAndReceive(IntPtr handle, IntPtr hEvent, byte[] tx, int inLen, int timeoutMs)
    {
        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return null;

        // Drain pending reports
        byte[] drain = new byte[inLen];
        while (true)
        {
            OVERLAPPED dOl = new OVERLAPPED();
            dOl.hEvent = hEvent;
            uint readBytes;
            bool ok = ReadFile(handle, drain, (uint)drain.Length, out readBytes, ref dOl);
            if (!ok && Marshal.GetLastWin32Error() == 997)
            {
                if (WaitForSingleObject(hEvent, 10) == WAIT_OBJECT_0)
                {
                    GetOverlappedResult(handle, ref dOl, out readBytes, false);
                    continue;
                }
                CancelIo(handle);
            }
            break;
        }

        // Write command
        OVERLAPPED wOl = new OVERLAPPED();
        wOl.hEvent = hEvent;
        uint written;
        bool wOk = WriteFile(handle, tx, (uint)tx.Length, out written, ref wOl);
        if (!wOk && Marshal.GetLastWin32Error() == 997)
        {
            if (WaitForSingleObject(hEvent, 500) == WAIT_OBJECT_0)
            {
                GetOverlappedResult(handle, ref wOl, out written, false);
            }
            else
            {
                CancelIo(handle);
                return null;
            }
        }

        // Read response
        byte[] rx = new byte[inLen];
        OVERLAPPED rOl = new OVERLAPPED();
        rOl.hEvent = hEvent;
        uint rxRead = 0;
        bool rOk = ReadFile(handle, rx, (uint)rx.Length, out rxRead, ref rOl);
        if (!rOk && Marshal.GetLastWin32Error() == 997)
        {
            if (WaitForSingleObject(hEvent, (uint)timeoutMs) == WAIT_OBJECT_0)
            {
                GetOverlappedResult(handle, ref rOl, out rxRead, false);
                return rx;
            }
            else
            {
                CancelIo(handle);
                return null;
            }
        }
        else if (rOk)
        {
            return rx;
        }

        return null;
    }

    public static string GetModelName(int pid, string productString)
    {
        // 1. Specific PID database
        switch (pid)
        {
            case 0xC547:
            case 0x4093: return "Logitech G PRO X SUPERLIGHT 2";
            case 0x4092:
            case 0xC094: return "Logitech G PRO X SUPERLIGHT";
            case 0x4079:
            case 0xC088: return "Logitech G PRO Wireless (GPW)";
            case 0x4087:
            case 0xC08D: return "Logitech G502 LIGHTSPEED";
            case 0x408E:
            case 0xC096: return "Logitech G502 X PLUS / LIGHTSPEED";
            case 0x4074: return "Logitech G304 / G305 LIGHTSPEED";
            case 0x4070: return "Logitech G703 LIGHTSPEED (HERO)";
            case 0x4071: return "Logitech G903 LIGHTSPEED (HERO)";
            case 0x406E: return "Logitech G604 LIGHTSPEED";
            case 0x4068: return "Logitech G603 LIGHTSPEED";
            case 0x4069:
            case 0xB023: return "Logitech MX Master 3";
            case 0x4090: return "Logitech MX Master 3S";
            case 0x408F: return "Logitech MX Anywhere 3";
            case 0x406B: return "Logitech MX Keys 无线键盘";
            case 0x408A: return "Logitech MX Keys Mini";
            case 0x4082: return "Logitech G913 TKL 机械键盘";
            case 0x4086: return "Logitech G913 机械键盘";
            case 0x409B: return "Logitech G PRO X 60 游戏键盘";
            case 0x4088: return "Logitech G733 LIGHTSPEED 耳机";
            case 0x4097: return "Logitech G PRO X 2 LIGHTSPEED 耳机";
            case 0x4085: return "Logitech G435 无线耳机";
            case 0x4080: return "Logitech G533 无线耳机";
            case 0x407B: return "Logitech G935 无线耳机";
        }

        if (!string.IsNullOrEmpty(productString))
        {
            string p = productString.Trim();
            if (p.Length > 2 && 
                !p.Equals("USB Receiver", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("Lightspeed Receiver", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("Logitech Receiver", StringComparison.OrdinalIgnoreCase))
            {
                return p.StartsWith("Logitech", StringComparison.OrdinalIgnoreCase) ? p : ("Logitech " + p);
            }
        }

        // Receiver defaults
        if (pid == 0xC547) return "Logitech G PRO X SUPERLIGHT 2 (Lightspeed)";
        if (pid == 0xC539 || pid == 0xC53A) return "Logitech G 旗舰无线鼠标 (Lightspeed)";
        if (pid == 0xC545) return "Logitech G502 X LIGHTSPEED";

        return "Logitech 罗技无线游戏外设";
    }

    public static DeviceCategory DetectCategory(int pid, string name)
    {
        string text = (name + " " + pid.ToString("X4")).ToUpperInvariant();
        if (text.Contains("KEYBOARD") || text.Contains("键盘") || text.Contains("G913") || text.Contains("G915") || text.Contains("MX KEYS") || text.Contains("PRO X 60"))
        {
            return DeviceCategory.Keyboard;
        }
        if (text.Contains("HEADSET") || text.Contains("HEADPHONE") || text.Contains("耳机") || text.Contains("G733") || text.Contains("G533") || text.Contains("G935") || text.Contains("G435") || text.Contains("PRO X 2"))
        {
            return DeviceCategory.Headset;
        }
        return DeviceCategory.Mouse;
    }

    public static List<MouseBatteryInfo> QueryAllLogitechDevices()
    {
        lock (hidLock)
        {
            List<MouseBatteryInfo> results = new List<MouseBatteryInfo>();
            HashSet<string> processedDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, 0x12);
            if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return results;

            SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
            uint memberIdx = 0;

            try
            {
                ifData.cbSize = Marshal.SizeOf(ifData);
                while (SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, memberIdx++, ref ifData))
                {
                    uint reqSize;
                    SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                    if (reqSize == 0) continue;

                    IntPtr detailBuffer = Marshal.AllocHGlobal((int)reqSize);
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 5);

                    if (SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, detailBuffer, reqSize, out reqSize, IntPtr.Zero))
                    {
                        IntPtr pDevicePath = new IntPtr(detailBuffer.ToInt64() + 4);
                        string devicePath = Marshal.PtrToStringAuto(pDevicePath);
                        if (devicePath != null && devicePath.ToLower().Contains("vid_046d"))
                        {
                            IntPtr handle = CreateFile(
                                devicePath,
                                GENERIC_READ | GENERIC_WRITE,
                                FILE_SHARE_READ | FILE_SHARE_WRITE,
                                IntPtr.Zero,
                                OPEN_EXISTING,
                                FILE_FLAG_OVERLAPPED,
                                IntPtr.Zero);

                            if (handle != IntPtr.Zero && handle.ToInt64() != -1)
                            {
                                try
                                {
                                    int foundVid = 0;
                                    int foundPid = 0;
                                    string foundName = null;

                                    HIDD_ATTRIBUTES attr = new HIDD_ATTRIBUTES();
                                    attr.Size = Marshal.SizeOf(attr);
                                    if (HidD_GetAttributes(handle, ref attr))
                                    {
                                        foundVid = attr.VendorID;
                                        foundPid = attr.ProductID;
                                    }

                                    StringBuilder sb = new StringBuilder(256);
                                    if (HidD_GetProductString(handle, sb, sb.Capacity))
                                    {
                                        string pStr = sb.ToString().Trim();
                                        if (!string.IsNullOrEmpty(pStr) && pStr.Length > 2) foundName = pStr;
                                    }

                                    IntPtr preparsed;
                                    if (HidD_GetPreparsedData(handle, out preparsed))
                                    {
                                        HIDP_CAPS caps;
                                        HidP_GetCaps(preparsed, out caps);
                                        HidD_FreePreparsedData(preparsed);

                                        // Logitech HID++ endpoint detection:
                                        // UsagePage == 0xFF00 || UsagePage == 0xFF43, InputReportByteLength >= 7
                                        bool isHidppEndpoint = (caps.UsagePage == 0xFF00 || caps.UsagePage == 0xFF43) && 
                                                               (caps.InputReportByteLength >= 7 && caps.OutputReportByteLength >= 7);

                                        if (isHidppEndpoint)
                                        {
                                            string devKey = foundVid.ToString("X4") + ":" + foundPid.ToString("X4");
                                            if (!processedDevices.Contains(devKey))
                                            {
                                                processedDevices.Add(devKey);

                                                IntPtr hEvent = CreateEvent(IntPtr.Zero, true, false, null);
                                                if (hEvent != IntPtr.Zero)
                                                {
                                                    try
                                                    {
                                                        // Check target device slots on this receiver/connection:
                                                        // index 0xFF (wired), index 0x01 (paired wireless mouse)
                                                        byte[] targetIndices = new byte[] { 0xFF, 0x01 };
                                                        foreach (byte devIdx in targetIndices)
                                                        {
                                                            bool deviceOnline = false;
                                                            string detectedName = null;
                                                            int batteryPercent = -1;
                                                            bool isCharging = false;
                                                            int detectedPid = foundPid;

                                                            // 1. HID++ 2.0 Ping: Feature 0x0000 ROOT, Func 0x00, ping 0x5A
                                                            byte[] txPing = new byte[20];
                                                            txPing[0] = 0x11; // Long report
                                                            txPing[1] = devIdx;
                                                            txPing[2] = 0x00; // Feature 0x0000
                                                            txPing[3] = 0x00; // Func 0 (Ping)
                                                            txPing[4] = 0x00;
                                                            txPing[5] = 0x00;
                                                            txPing[6] = 0x5A; // Ping token

                                                            byte[] rxPing = SendAndReceive(handle, hEvent, txPing, caps.InputReportByteLength, 350);
                                                            if (rxPing != null && rxPing.Length >= 7 && rxPing[6] == 0x5A)
                                                            {
                                                                deviceOnline = true;
                                                            }
                                                            else
                                                            {
                                                                // Try Short Report ping (Report ID 0x10)
                                                                byte[] txShortPing = new byte[7] { 0x10, devIdx, 0x00, 0x00, 0x00, 0x00, 0x5A };
                                                                byte[] rxShortPing = SendAndReceive(handle, hEvent, txShortPing, caps.InputReportByteLength, 250);
                                                                if (rxShortPing != null && rxShortPing.Length >= 7 && rxShortPing[6] == 0x5A)
                                                                {
                                                                    deviceOnline = true;
                                                                }
                                                            }

                                                            // 2. Discover Features if online
                                                            if (deviceOnline)
                                                            {
                                                                // Feature 0x0005: Device Name Type
                                                                byte[] txFeatName = new byte[20];
                                                                txFeatName[0] = 0x11;
                                                                txFeatName[1] = devIdx;
                                                                txFeatName[2] = 0x00; // Root feature
                                                                txFeatName[3] = 0x00; // getFeature
                                                                txFeatName[4] = 0x00;
                                                                txFeatName[5] = 0x05; // Feature ID: 0x0005
                                                                byte[] rxFeatName = SendAndReceive(handle, hEvent, txFeatName, caps.InputReportByteLength, 250);
                                                                if (rxFeatName != null && rxFeatName.Length >= 5 && rxFeatName[4] > 0)
                                                                {
                                                                    byte nameFeatIdx = rxFeatName[4];
                                                                    // Get Name Count
                                                                    byte[] txNameCount = new byte[20];
                                                                    txNameCount[0] = 0x11;
                                                                    txNameCount[1] = devIdx;
                                                                    txNameCount[2] = nameFeatIdx;
                                                                    txNameCount[3] = 0x00; // Func 0: getCount
                                                                    byte[] rxNameCount = SendAndReceive(handle, hEvent, txNameCount, caps.InputReportByteLength, 200);
                                                                    if (rxNameCount != null && rxNameCount.Length >= 5 && rxNameCount[4] > 0)
                                                                    {
                                                                        int nameLen = rxNameCount[4];
                                                                        // Get Name String (Func 1: 0x10)
                                                                        byte[] txGetName = new byte[20];
                                                                        txGetName[0] = 0x11;
                                                                        txGetName[1] = devIdx;
                                                                        txGetName[2] = nameFeatIdx;
                                                                        txGetName[3] = 0x10; // Func 1: getName
                                                                        txGetName[4] = 0x00;
                                                                        byte[] rxGetName = SendAndReceive(handle, hEvent, txGetName, caps.InputReportByteLength, 250);
                                                                        if (rxGetName != null && rxGetName.Length > 5)
                                                                        {
                                                                            StringBuilder nsb = new StringBuilder();
                                                                            for (int c = 5; c < rxGetName.Length && (c - 5) < nameLen; c++)
                                                                            {
                                                                                if (rxGetName[c] == 0) break;
                                                                                nsb.Append((char)rxGetName[c]);
                                                                            }
                                                                            string str = nsb.ToString().Trim();
                                                                            if (str.Length > 2) detectedName = str;
                                                                        }
                                                                    }
                                                                }

                                                                // Feature 0x1004: Unified Battery
                                                                byte[] txFeat1004 = new byte[20];
                                                                txFeat1004[0] = 0x11;
                                                                txFeat1004[1] = devIdx;
                                                                txFeat1004[2] = 0x00;
                                                                txFeat1004[3] = 0x00;
                                                                txFeat1004[4] = 0x10;
                                                                txFeat1004[5] = 0x04; // 0x1004
                                                                byte[] rxFeat1004 = SendAndReceive(handle, hEvent, txFeat1004, caps.InputReportByteLength, 250);
                                                                if (rxFeat1004 != null && rxFeat1004.Length >= 5 && rxFeat1004[4] > 0)
                                                                {
                                                                    byte batFeatIdx = rxFeat1004[4];
                                                                    // Call getBatteryStatus (Func 1: 0x10)
                                                                    byte[] txBat = new byte[20];
                                                                    txBat[0] = 0x11;
                                                                    txBat[1] = devIdx;
                                                                    txBat[2] = batFeatIdx;
                                                                    txBat[3] = 0x10; // Func 1
                                                                    byte[] rxBat = SendAndReceive(handle, hEvent, txBat, caps.InputReportByteLength, 300);
                                                                    if (rxBat != null && rxBat.Length > 5)
                                                                    {
                                                                        int pct = rxBat[4];
                                                                        if (pct >= 0 && pct <= 100)
                                                                        {
                                                                            batteryPercent = pct;
                                                                            isCharging = (rxBat[5] == 1);
                                                                        }
                                                                    }
                                                                }

                                                                // Feature 0x1000: Standard Battery Status fallback
                                                                if (batteryPercent < 0)
                                                                {
                                                                    byte[] txFeat1000 = new byte[20];
                                                                    txFeat1000[0] = 0x11;
                                                                    txFeat1000[1] = devIdx;
                                                                    txFeat1000[2] = 0x00;
                                                                    txFeat1000[3] = 0x00;
                                                                    txFeat1000[4] = 0x10;
                                                                    txFeat1000[5] = 0x00; // 0x1000
                                                                    byte[] rxFeat1000 = SendAndReceive(handle, hEvent, txFeat1000, caps.InputReportByteLength, 250);
                                                                    if (rxFeat1000 != null && rxFeat1000.Length >= 5 && rxFeat1000[4] > 0)
                                                                    {
                                                                        byte batFeatIdx = rxFeat1000[4];
                                                                        // Call getBatteryLevelStatus (Func 0: 0x00)
                                                                        byte[] txBat = new byte[20];
                                                                        txBat[0] = 0x11;
                                                                        txBat[1] = devIdx;
                                                                        txBat[2] = batFeatIdx;
                                                                        txBat[3] = 0x00; // Func 0
                                                                        byte[] rxBat = SendAndReceive(handle, hEvent, txBat, caps.InputReportByteLength, 300);
                                                                        if (rxBat != null && rxBat.Length > 6)
                                                                        {
                                                                            int pct = rxBat[4];
                                                                            if (pct >= 0 && pct <= 100)
                                                                            {
                                                                                batteryPercent = pct;
                                                                                isCharging = (rxBat[6] == 1);
                                                                            }
                                                                        }
                                                                    }
                                                                }

                                                                // HID++ 1.0 Sub-ID 0x0D Fallback
                                                                if (batteryPercent < 0)
                                                                {
                                                                    byte[] txHidpp1 = new byte[7] { 0x10, devIdx, 0x0D, 0x00, 0x00, 0x00, 0x00 };
                                                                    byte[] rxHidpp1 = SendAndReceive(handle, hEvent, txHidpp1, caps.InputReportByteLength, 250);
                                                                    if (rxHidpp1 != null && rxHidpp1.Length >= 5 && rxHidpp1[2] == 0x0D)
                                                                    {
                                                                        int pct = rxHidpp1[3];
                                                                        if (pct >= 0 && pct <= 100)
                                                                        {
                                                                            batteryPercent = pct;
                                                                            isCharging = (rxHidpp1[4] == 1);
                                                                        }
                                                                    }
                                                                }
                                                            }

                                                            // Determine Device Name and ID
                                                            string finalName = !string.IsNullOrEmpty(detectedName) ? detectedName : GetModelName(detectedPid, foundName);
                                                            DeviceCategory cat = DetectCategory(detectedPid, finalName);
                                                            string deviceId = "Logitech:" + foundVid.ToString("X4") + ":" + detectedPid.ToString("X4") + (devIdx != 0xFF ? (":" + devIdx) : "");

                                                            // If device was online and battery read, update cache
                                                            if (batteryPercent >= 0)
                                                            {
                                                                CachedBatteryPercent = batteryPercent;
                                                                CachedIsCharging = isCharging;
                                                                CachedLastUpdated = DateTime.Now;
                                                                CachedDeviceName = finalName;
                                                                HasValidHardwareReading = true;
                                                                deviceBatteryCache[deviceId] = batteryPercent;
                                                                SaveHardwareCache();
                                                            }

                                                            int displayBattery = (batteryPercent >= 0) ? batteryPercent : GetCachedBatteryForDevice(deviceId);
                                                            bool displayCharging = (batteryPercent >= 0) ? isCharging : CachedIsCharging;

                                                            // Only add if device is online or if dongle is present
                                                            // For dongle receiver, even if mouse is sleeping, provide dongle standby status
                                                            bool isDongle = (foundPid == 0xC547 || foundPid == 0xC539 || foundPid == 0xC53A || 
                                                                             foundPid == 0xC545 || foundPid == 0xC548 || foundPid == 0xC52B || 
                                                                             foundPid == 0xC534 || foundPid == 0xC52F);

                                                            if (deviceOnline || isDongle)
                                                            {
                                                                MouseBatteryInfo info = new MouseBatteryInfo
                                                                {
                                                                    DeviceId = deviceId,
                                                                    HardwareFingerprint = deviceId,
                                                                    Brand = "Logitech",
                                                                    DeviceName = finalName,
                                                                    Category = cat,
                                                                    Transport = (devIdx == 0xFF) ? "USB 有线" : "Lightspeed 无线",
                                                                    IsConnected = true,
                                                                    IsSleeping = !deviceOnline,
                                                                    IsDonglePresent = true,
                                                                    IsCustom = false,
                                                                    BatteryPercent = displayBattery,
                                                                    ExactBatteryPercent = displayBattery,
                                                                    IsCharging = displayCharging,
                                                                    LastUpdated = (batteryPercent >= 0) ? DateTime.Now : (CachedLastUpdated != DateTime.MinValue ? CachedLastUpdated : DateTime.Now),
                                                                    Dpi = 1600,
                                                                    DpiStage = 2,
                                                                    DpiStageCount = 5,
                                                                    DpiStages = new int[] { 400, 800, 1600, 3200, 6400 },
                                                                    PollingRate = 1000,
                                                                    SupportedPollingRates = new int[] { 1000, 2000, 4000, 8000 }
                                                                };
                                                                results.Add(info);
                                                                break; // found active/valid paired slot
                                                            }
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        CloseHandle(hEvent);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    CloseHandle(handle);
                                }
                            }
                        }
                    }
                    Marshal.FreeHGlobal(detailBuffer);
                    ifData.cbSize = Marshal.SizeOf(ifData);
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }

            return results;
        }
    }

        public static MouseBatteryInfo QueryLogitechDeviceInfo(string deviceFingerprint = null)
        {
            var list = QueryAllLogitechDevices();
            if (list == null || list.Count == 0) return null;
            if (!string.IsNullOrEmpty(deviceFingerprint))
            {
                foreach (var d in list)
                {
                    if (d.DeviceId != null && (d.DeviceId.Equals(deviceFingerprint, StringComparison.OrdinalIgnoreCase) ||
                                               (d.HardwareFingerprint != null && d.HardwareFingerprint.Equals(deviceFingerprint, StringComparison.OrdinalIgnoreCase))))
                    {
                        return d;
                    }
                }
            }
            return list[0];
        }
    }
}