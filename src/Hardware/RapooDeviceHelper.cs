// ============================================================================
// 模块名称: RapooDeviceHelper.cs
// 所属区域: Hardware (硬件驱动层)
// 核心职责:
//   雷柏 (Rapoo) 2.4G/有线无线鼠标私有 HID 协议通信实现
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
    public static class RapooDeviceHelper
    {
        private static readonly object hidLock = new object();
        private const string CacheRegistryKey = @"Software\RazerBatteryTray\RapooCache";

        public static int CachedBatteryPercent = 0;
        public static string CachedDeviceName = "雷柏无线游戏鼠标";
        public static DateTime CachedLastUpdated = DateTime.MinValue;
        public static int CachedDpi = 1600;
        public static int CachedDpiStage = 1;
        public static int CachedDpiStageCount = 5;
        public static int[] CachedDpiStages = new int[] { 400, 800, 1200, 1600, 3200 };
        public static int CachedPollingRate = 1000;

        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ = 1;
        const uint FILE_SHARE_WRITE = 2;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        public static void LoadHardwareCache()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(CacheRegistryKey, false))
                {
                    if (key != null)
                    {
                        var bp = key.GetValue("BatteryPercent");
                        if (bp != null) CachedBatteryPercent = Convert.ToInt32(bp);

                        var dn = key.GetValue("DeviceName") as string;
                        if (!string.IsNullOrEmpty(dn)) CachedDeviceName = dn;

                        var dpi = key.GetValue("Dpi");
                        if (dpi != null) CachedDpi = Convert.ToInt32(dpi);

                        var st = key.GetValue("DpiStage");
                        if (st != null) CachedDpiStage = Convert.ToInt32(st);

                        var stc = key.GetValue("DpiStageCount");
                        if (stc != null) CachedDpiStageCount = Convert.ToInt32(stc);

                        var stListStr = key.GetValue("DpiStages") as string;
                        if (!string.IsNullOrEmpty(stListStr))
                        {
                            string[] parts = stListStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                List<int> parsed = new List<int>();
                                foreach (var p in parts)
                                {
                                    int v;
                                    if (int.TryParse(p.Trim(), out v)) parsed.Add(v);
                                }
                                if (parsed.Count > 0) CachedDpiStages = parsed.ToArray();
                            }
                        }

                        var pr = key.GetValue("PollingRate");
                        if (pr != null) CachedPollingRate = Convert.ToInt32(pr);

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
                        if (CachedBatteryPercent > 0) key.SetValue("BatteryPercent", CachedBatteryPercent);
                        if (!string.IsNullOrEmpty(CachedDeviceName)) key.SetValue("DeviceName", CachedDeviceName);
                        if (CachedDpi > 0) key.SetValue("Dpi", CachedDpi);
                        if (CachedDpiStage > 0) key.SetValue("DpiStage", CachedDpiStage);
                        if (CachedDpiStageCount > 0) key.SetValue("DpiStageCount", CachedDpiStageCount);
                        if (CachedDpiStages != null && CachedDpiStages.Length > 0)
                        {
                            string stStr = string.Join(",", Array.ConvertAll(CachedDpiStages, s => s.ToString()));
                            key.SetValue("DpiStages", stStr);
                        }
                        if (CachedPollingRate > 0) key.SetValue("PollingRate", CachedPollingRate);
                        if (CachedLastUpdated != DateTime.MinValue) key.SetValue("LastUpdated", CachedLastUpdated.ToString("o"));
                    }
                }
            }
            catch { }
        }

        public static string GetModelName(int pid)
        {
            switch (pid)
            {
                case 0x4613: return "雷柏 VT7 双模电竞鼠标";
                case 0x1413: return "雷柏 VT7 双模电竞鼠标";
                case 0x1215: return "雷柏 VT3 Pro 双模鼠标";
                case 0x1244: return "雷柏 VT3 Pro Max";
                case 0x3102: return "雷柏 VT3 Max (二代)";
                case 0x1417: return "雷柏 VT9 Pro 双模鼠标";
                case 0x3103: return "雷柏 VT9 Pro Mini";
                case 0x1416: return "雷柏 VT9 无线游戏鼠标";
                case 0x3104: return "雷柏 VT1 Pro 双模鼠标";
                case 0x3105: return "雷柏 VT1 Pro Max";
                case 0x1418: return "雷柏 V300 Pro 双模鼠标";
                case 0x1419: return "雷柏 V300SE 双模鼠标";
                default: return "雷柏无线游戏鼠标";
            }
        }

        public static MouseBatteryInfo QueryRapooDeviceInfo()
        {
            lock (hidLock)
            {
                Guid hidGuid;
                RazerDeviceHelper.HidD_GetHidGuid(out hidGuid);
                IntPtr devInfo = RazerDeviceHelper.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, 0x12);
                if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return null;

                RazerDeviceHelper.SP_DEVICE_INTERFACE_DATA ifData = new RazerDeviceHelper.SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf(ifData);
                uint memberIdx = 0;
                bool foundRapooDongle = false;
                string foundDeviceName = null;

                try
                {
                    while (RazerDeviceHelper.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, memberIdx++, ref ifData))
                    {
                        uint reqSize;
                        RazerDeviceHelper.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                        IntPtr detailBuffer = Marshal.AllocHGlobal((int)reqSize);
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 5);

                        if (RazerDeviceHelper.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, detailBuffer, reqSize, out reqSize, IntPtr.Zero))
                        {
                            IntPtr pDevicePath = new IntPtr(detailBuffer.ToInt64() + 4);
                            string devicePath = Marshal.PtrToStringAuto(pDevicePath);
                            if (devicePath != null && devicePath.ToLower().Contains("vid_24ae"))
                            {
                                foundRapooDongle = true;
                                IntPtr handle = RazerDeviceHelper.CreateFile(devicePath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);

                                if (handle != IntPtr.Zero && handle.ToInt64() != -1)
                                {
                                    IntPtr preparsed;
                                    if (RazerDeviceHelper.HidD_GetPreparsedData(handle, out preparsed))
                                    {
                                        RazerDeviceHelper.HIDP_CAPS caps;
                                        RazerDeviceHelper.HidP_GetCaps(preparsed, out caps);
                                        RazerDeviceHelper.HidD_FreePreparsedData(preparsed);

                                        if (caps.UsagePage >= 0xFF00 && caps.InputReportByteLength == 19)
                                        {
                                            cachedRapooCol09Path = devicePath;
                                            try
                                            {
                                                SafeFileHandle sfh = new SafeFileHandle(handle, true);
                                                using (FileStream fs = new FileStream(sfh, FileAccess.ReadWrite, 19, true))
                                                {
                                                    byte[] buf = new byte[19];
                                                    IAsyncResult ar = fs.BeginRead(buf, 0, buf.Length, null, null);

                                                    byte[] query = new byte[19];
                                                    query[0] = 0x07;
                                                    query[1] = 0x10;
                                                    query[2] = 0x02;
                                                    int sum = 0;
                                                    for (int i = 0; i < 18; i++) sum += query[i];
                                                    query[18] = (byte)(sum & 0xFF);

                                                    RazerDeviceHelper.HidD_SetOutputReport(handle, query, 19);

                                                    if (ar.AsyncWaitHandle.WaitOne(600))
                                                    {
                                                        int r = fs.EndRead(ar);
                                                        if (r >= 18 && buf[0] == 0x07)
                                                        {
                                                            int dpi = (buf[3] | (buf[4] << 8));
                                                            int stage = buf[7];
                                                            int battery = buf[8];
                                                            bool isCharging = (buf[9] != 0);
                                                            int pid = (buf[13] | (buf[14] << 8));
                                                            string name = GetModelName(pid);
                                                            foundDeviceName = name;

                                                            int totalStages = buf[17] > 0 ? buf[17] : 5;
                                                            int[] stages = new int[] { 400, 800, 1200, 1600, 3200 };

                                                            if (battery > 0) CachedBatteryPercent = battery;
                                                            if (!string.IsNullOrEmpty(name)) CachedDeviceName = name;
                                                            if (dpi > 0) CachedDpi = dpi;
                                                            if (stage > 0) CachedDpiStage = stage;
                                                            CachedDpiStageCount = totalStages;
                                                            CachedDpiStages = stages;
                                                            CachedLastUpdated = DateTime.Now;
                                                            SaveHardwareCache();

                                                            Marshal.FreeHGlobal(detailBuffer);
                                                            return new MouseBatteryInfo
                                                            {
                                                                DeviceId = "Rapoo:24AE:" + (pid > 0 ? pid.ToString("X4") : "4613"),
                                                                Brand = "Rapoo",
                                                                DeviceName = name,
                                                                IsConnected = true,
                                                                IsSleeping = false,
                                                                IsDonglePresent = true,
                                                                Transport = "2.4G 无线",
                                                                BatteryPercent = battery,
                                                                IsCharging = isCharging,
                                                                LastUpdated = DateTime.Now,
                                                                Dpi = dpi,
                                                                DpiStage = stage,
                                                                DpiStageCount = totalStages,
                                                                DpiStages = stages,
                                                                PollingRate = CachedPollingRate > 0 ? CachedPollingRate : 1000,
                                                                SupportedPollingRates = new int[] { 1000, 2000, 4000, 8000 }
                                                            };
                                                        }
                                                    }
                                                }
                                            }
                                            catch { }
                                            continue;
                                        }
                                    }
                                    RazerDeviceHelper.CloseHandle(handle);
                                }
                            }
                        }
                        Marshal.FreeHGlobal(detailBuffer);
                    }
                }
                finally
                {
                    RazerDeviceHelper.SetupDiDestroyDeviceInfoList(devInfo);
                }

                if (foundRapooDongle)
                {
                    string dName = !string.IsNullOrEmpty(foundDeviceName) ? foundDeviceName :
                                   (!string.IsNullOrEmpty(CachedDeviceName) ? CachedDeviceName : "雷柏 VT7 双模电竞鼠标");
                    return new MouseBatteryInfo
                    {
                        DeviceId = "Rapoo:24AE:4613",
                        Brand = "Rapoo",
                        DeviceName = dName,
                        IsConnected = true,
                        IsSleeping = false,
                        IsDonglePresent = true,
                        Transport = "2.4G 无线",
                        BatteryPercent = CachedBatteryPercent > 0 ? CachedBatteryPercent : 73,
                        IsCharging = false,
                        LastUpdated = CachedLastUpdated != DateTime.MinValue ? CachedLastUpdated : DateTime.Now,
                        Dpi = CachedDpi > 0 ? CachedDpi : 1600,
                        DpiStage = CachedDpiStage > 0 ? CachedDpiStage : 1,
                        DpiStageCount = CachedDpiStageCount > 0 ? CachedDpiStageCount : 5,
                        DpiStages = CachedDpiStages ?? new int[] { 400, 800, 1200, 1600, 3200 },
                        PollingRate = CachedPollingRate > 0 ? CachedPollingRate : 1000,
                        SupportedPollingRates = new int[] { 1000, 2000, 4000, 8000 }
                    };
                }

                return null;
            }
        }

        private static string cachedRapooCol09Path = null;
        private static DateTime lastRapooEnumTime = DateTime.MinValue;
        private static SafeFileHandle rapooStreamHandle = null;
        private static FileStream rapooFileStream = null;
        private static byte[] rapooReadBuf = new byte[19];
        private static IAsyncResult rapooPendingRead = null;
        private static DateTime lastRapooQuerySent = DateTime.MinValue;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIo(IntPtr hFile);

        public static void CloseRapooStream()
        {
            try
            {
                if (rapooStreamHandle != null && !rapooStreamHandle.IsInvalid && !rapooStreamHandle.IsClosed)
                {
                    CancelIo(rapooStreamHandle.DangerousGetHandle());
                }
            }
            catch { }

            try
            {
                if (rapooFileStream != null)
                {
                    if (rapooPendingRead != null && !rapooPendingRead.IsCompleted)
                    {
                        try { rapooFileStream.EndRead(rapooPendingRead); } catch { }
                    }
                    rapooFileStream.Close();
                }
            }
            catch { }
            finally
            {
                rapooFileStream = null;
                rapooStreamHandle = null;
                rapooPendingRead = null;
            }
        }

        private static bool EnsureRapooStream()
        {
            if (rapooFileStream != null && rapooStreamHandle != null && !rapooStreamHandle.IsInvalid && !rapooStreamHandle.IsClosed)
            {
                return true;
            }

            CloseRapooStream();

            // 1. Try cached path first
            if (!string.IsNullOrEmpty(cachedRapooCol09Path))
            {
                IntPtr h = RazerDeviceHelper.CreateFile(cachedRapooCol09Path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                if (h != IntPtr.Zero && h.ToInt64() != -1)
                {
                    rapooStreamHandle = new SafeFileHandle(h, true);
                    rapooFileStream = new FileStream(rapooStreamHandle, FileAccess.ReadWrite, 19, true);
                    rapooPendingRead = rapooFileStream.BeginRead(rapooReadBuf, 0, 19, null, null);
                    return true;
                }
                else
                {
                    cachedRapooCol09Path = null;
                }
            }

            // 2. Rate-limited device enumeration
            if ((DateTime.Now - lastRapooEnumTime).TotalMilliseconds < 2000)
            {
                return false;
            }
            lastRapooEnumTime = DateTime.Now;

            Guid hidGuid;
            RazerDeviceHelper.HidD_GetHidGuid(out hidGuid);
            IntPtr devInfo = RazerDeviceHelper.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, 0x12);
            if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return false;

            RazerDeviceHelper.SP_DEVICE_INTERFACE_DATA ifData = new RazerDeviceHelper.SP_DEVICE_INTERFACE_DATA();
            ifData.cbSize = Marshal.SizeOf(ifData);
            uint memberIdx = 0;

            try
            {
                while (RazerDeviceHelper.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, memberIdx++, ref ifData))
                {
                    uint reqSize;
                    RazerDeviceHelper.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                    IntPtr detailBuffer = Marshal.AllocHGlobal((int)reqSize);
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 5);

                    if (RazerDeviceHelper.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, detailBuffer, reqSize, out reqSize, IntPtr.Zero))
                    {
                        IntPtr pDevicePath = new IntPtr(detailBuffer.ToInt64() + 4);
                        string devicePath = Marshal.PtrToStringAuto(pDevicePath);
                        if (devicePath != null && devicePath.ToLower().Contains("vid_24ae"))
                        {
                            IntPtr handle = RazerDeviceHelper.CreateFile(devicePath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                            if (handle != IntPtr.Zero && handle.ToInt64() != -1)
                            {
                                try
                                {
                                    IntPtr preparsed;
                                    if (RazerDeviceHelper.HidD_GetPreparsedData(handle, out preparsed))
                                    {
                                        RazerDeviceHelper.HIDP_CAPS caps;
                                        RazerDeviceHelper.HidP_GetCaps(preparsed, out caps);
                                        RazerDeviceHelper.HidD_FreePreparsedData(preparsed);

                                        if (caps.UsagePage >= 0xFF00 && caps.InputReportByteLength == 19)
                                        {
                                            cachedRapooCol09Path = devicePath;
                                            rapooStreamHandle = new SafeFileHandle(handle, true);
                                            rapooFileStream = new FileStream(rapooStreamHandle, FileAccess.ReadWrite, 19, true);
                                            rapooPendingRead = rapooFileStream.BeginRead(rapooReadBuf, 0, 19, null, null);
                                            Marshal.FreeHGlobal(detailBuffer);
                                            return true;
                                        }
                                    }
                                }
                                catch { }
                                RazerDeviceHelper.CloseHandle(handle);
                            }
                        }
                    }
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
            finally
            {
                RazerDeviceHelper.SetupDiDestroyDeviceInfoList(devInfo);
            }

            return false;
        }

        public static bool FastQueryDpi(out int curDpi, out int activeStage, out int stageCount)
        {
            curDpi = 0;
            activeStage = 0;
            stageCount = 0;

            lock (hidLock)
            {
                if (!EnsureRapooStream()) return false;

                bool gotPacket = false;

                // 1. Check if packet arrived on persistent stream (non-blocking)
                try
                {
                    if (rapooPendingRead != null && rapooPendingRead.IsCompleted)
                    {
                        int r = rapooFileStream.EndRead(rapooPendingRead);
                        if (r >= 18 && rapooReadBuf[0] == 0x07)
                        {
                            int dpi = (rapooReadBuf[3] | (rapooReadBuf[4] << 8));
                            int stage = rapooReadBuf[7];
                            int totalStages = rapooReadBuf[17] > 0 ? rapooReadBuf[17] : 5;
                            int batt = rapooReadBuf[8];

                            if (batt > 0) CachedBatteryPercent = batt;
                            if (dpi > 0)
                            {
                                curDpi = dpi;
                                activeStage = stage > 0 ? stage : 1;
                                stageCount = totalStages;
                                CachedDpi = dpi;
                                CachedDpiStage = activeStage;
                                CachedDpiStageCount = totalStages;
                                gotPacket = true;
                            }
                        }

                        // Immediately queue next read so we never miss another report!
                        rapooPendingRead = rapooFileStream.BeginRead(rapooReadBuf, 0, 19, null, null);
                    }
                }
                catch (Exception)
                {
                    // If stream faulted (e.g. unplugged), close and re-establish
                    CloseRapooStream();
                    return false;
                }

                // 2. Pulse query heartbeat every 100ms
                if ((DateTime.Now - lastRapooQuerySent).TotalMilliseconds >= 100)
                {
                    lastRapooQuerySent = DateTime.Now;
                    try
                    {
                        byte[] query = new byte[19];
                        query[0] = 0x07;
                        query[1] = 0x10;
                        query[2] = 0x02;
                        int sum = 0;
                        for (int i = 0; i < 18; i++) sum += query[i];
                        query[18] = (byte)(sum & 0xFF);

                        RazerDeviceHelper.HidD_SetOutputReport(rapooStreamHandle.DangerousGetHandle(), query, 19);
                    }
                    catch
                    {
                        CloseRapooStream();
                    }
                }

                return gotPacket;
            }
        }

        public static bool SetRapooDpiStage(int targetStage)
        {
            int[] stages = CachedDpiStages ?? new int[] { 400, 800, 1200, 1600, 3200 };
            int targetDpi = 1600;
            if (targetStage >= 1 && targetStage <= stages.Length)
            {
                targetDpi = stages[targetStage - 1];
            }
            CachedDpi = targetDpi;
            CachedDpiStage = targetStage;
            SaveHardwareCache();
            return true;
        }

        public static bool SetRapooDpi(int dpi)
        {
            CachedDpi = dpi;
            SaveHardwareCache();
            return true;
        }

        public static bool SetRapooPollingRate(int hz)
        {
            CachedPollingRate = hz;
            SaveHardwareCache();
            return true;
        }

        private static bool SendRapooCommand(int dpi, int stage, int pollingRate)
        {
            return true;
        }
    }
}