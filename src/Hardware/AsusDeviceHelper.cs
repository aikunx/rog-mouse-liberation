// ============================================================================
// 模块名称: AsusDeviceHelper.cs
// 所属区域: Hardware (硬件协议驱动层)
// 核心职责:
//   ASUS / ROG 鼠标 HID 私有协议直连（VID 0x0B05）。
//   已适配: P520 (ROG STRIX IMPACT III WIRELESS / OMNI 接收器 PID 0x1ACE)。
//   协议来源: g-helper 项目 (GPL v3) 的 AsusMouse.cs 逆向成果，本文件为独立重实现。
//
// 协议要点:
//   64 字节报文, 首字节为 reportId (P520 = 0x03)
//   [1] 0x12 = 读命令族, 0x51 = 写命令族, 0x50 = profile 切换
//   [2] 子命令
//   读电量: 03 12 07 -> 回包 [1]==0x12 && [2]==0x07, [5]=百分比, [10]>0=充电
//   轮询率: 03 12 04 00 -> [13]=速率档位(0=125 ... 3=1000)
//   设轮询: 03 51 31 04 00 rate
//   设 DPI: 03 51 31 (stage-1) 00 lo hi R G B,  lo/hi = (DPI-50)/50 小端
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 任何 AI Agent 在修改本文件逻辑时，【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RazerBatteryTray
{
    public static class AsusDeviceHelper
    {
        public const int ASUS_VID = 0x0B05;
        private const int PACKET_SIZE = 64;
        private const string CacheRegistryKey = @"Software\RazerBatteryTray\AsusCache";

        private class DeviceProfile
        {
            public string Iface;
            public byte ReportId;
            public string Name;
            public bool Wireless;
        }

        // 30 个型号，参数取自 g-helper 的型号定义（PID / 控制接口 / reportId）
        private static readonly Dictionary<int, DeviceProfile> KnownDevices = new Dictionary<int, DeviceProfile>
        {
            { 0x1ACE, new DeviceProfile { Iface = "mi_02&col03", ReportId = 0x03, Wireless = true,  Name = "ROG STRIX IMPACT III WIRELESS" } },
            { 0x1A88, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = false, Name = "ROG STRIX IMPACT III" } },
            { 0x18E1, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = false, Name = "ROG STRIX IMPACT II" } },
            { 0x1949, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG STRIX IMPACT II WIRELESS" } },
            { 0x1847, new DeviceProfile { Iface = "mi_02",       ReportId = 0x00, Wireless = false, Name = "STRIX IMPACT" } },
            { 0x185B, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = false, Name = "STRIX EVOLVE" } },
            { 0x18B4, new DeviceProfile { Iface = "mi_01",       ReportId = 0x00, Wireless = true,  Name = "ROG STRIX CARRY" } },
            { 0x18E5, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG CHAKRAM" } },
            { 0x1958, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = false, Name = "ROG CHAKRAM CORE" } },
            { 0x1A1A, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG CHAKRAM X" } },
            { 0x1877, new DeviceProfile { Iface = "mi_02",       ReportId = 0x00, Wireless = false, Name = "GLADIUS II ORIGIN" } },
            { 0x18A0, new DeviceProfile { Iface = "mi_02",       ReportId = 0x00, Wireless = true,  Name = "GLADIUS II WIRELESS" } },
            { 0x197F, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG GLADIUS III" } },
            { 0x1A72, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG GLADIUS III AIMPOINT" } },
            { 0x1A94, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG HARPE ACE AIM LAB" } },
            { 0x1B63, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = false, Name = "HARPE ACE MINI" } },
            { 0x1C69, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = false, Name = "ROG HARPE II ACE" } },
            { 0x1B16, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG KERIS II ACE" } },
            { 0x1C0C, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG KERIS II ORIGIN" } },
            { 0x1960, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG KERIS" } },
            { 0x1A68, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG KERIS WIRELESS AIMPOINT" } },
            { 0x1A24, new DeviceProfile { Iface = "mi_02",       ReportId = 0x00, Wireless = true,  Name = "ASUS MOUSE MD200" } },
            { 0x1846, new DeviceProfile { Iface = "mi_02",       ReportId = 0x00, Wireless = false, Name = "ROG PUGIO" } },
            { 0x1908, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG PUGIO II" } },
            { 0x1979, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "ROG SPATHA X" } },
            { 0x1C7F, new DeviceProfile { Iface = "mi_01",       ReportId = 0xEC, Wireless = false, Name = "ROG BULWARK DOCK" } },
            { 0x1910, new DeviceProfile { Iface = "mi_01",       ReportId = 0x00, Wireless = false, Name = "TUF GAMING M3" } },
            { 0x1A03, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = false, Name = "TUF GAMING M4 AIR" } },
            { 0x19F4, new DeviceProfile { Iface = "mi_00",       ReportId = 0x00, Wireless = true,  Name = "TUF GAMING M4 WIRELESS" } },
            { 0x1898, new DeviceProfile { Iface = "mi_02",       ReportId = 0x00, Wireless = false, Name = "TUF GAMING M5" } },
        };

        private static string lastDevicePath = null;
        private static byte lastReportId = 0x03;

        private static readonly object hidLock = new object();

        public static int CachedBatteryPercent = 0;
        public static bool CachedIsCharging = false;
        public static int CachedPollingRate = 1000;
        public static DateTime CachedLastUpdated = DateTime.MinValue;

        [StructLayout(LayoutKind.Sequential)]
        private struct OVERLAPPED
        {
            public IntPtr Internal;
            public IntPtr InternalHigh;
            public uint Offset;
            public uint OffsetHigh;
            public IntPtr hEvent;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, ref OVERLAPPED lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, ref OVERLAPPED lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetOverlappedResult(IntPtr hFile, ref OVERLAPPED lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIo(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        private const uint WAIT_OBJECT_0 = 0;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const int ERROR_IO_PENDING = 997;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        private static byte[] BuildPacket(byte[] payload)
        {
            return BuildPacket(payload, lastReportId);
        }

        private static bool TryResolvePid(string path, out int pid)
        {
            pid = 0;
            if (string.IsNullOrEmpty(path)) return false;
            string lower = path.ToLower();
            int pi = lower.IndexOf("pid_");
            if (pi < 0 || lower.Length < pi + 8) return false;
            return int.TryParse(lower.Substring(pi + 4, 4), System.Globalization.NumberStyles.HexNumber, null, out pid);
        }

        private static byte[] BuildPacket(byte[] payload, byte reportId)
        {
            byte[] pkt = new byte[PACKET_SIZE];
            pkt[0] = reportId;
            for (int i = 0; i < payload.Length && i < PACKET_SIZE - 1; i++)
            {
                pkt[1 + i] = payload[i];
            }
            return pkt;
        }

        private static byte[] SendAndReceive(IntPtr handle, IntPtr hEvent, byte[] tx, int timeoutMs)
        {
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return null;

            // 1. 清空残留报告
            byte[] drain = new byte[PACKET_SIZE];
            int guard = 0;
            while (guard++ < 8)
            {
                OVERLAPPED dOl = new OVERLAPPED();
                dOl.hEvent = hEvent;
                uint readBytes;
                bool ok = ReadFile(handle, drain, (uint)drain.Length, out readBytes, ref dOl);
                if (!ok && Marshal.GetLastWin32Error() == ERROR_IO_PENDING)
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

            // 2. 写命令
            OVERLAPPED wOl = new OVERLAPPED();
            wOl.hEvent = hEvent;
            uint written;
            bool wOk = WriteFile(handle, tx, (uint)tx.Length, out written, ref wOl);
            if (!wOk && Marshal.GetLastWin32Error() == ERROR_IO_PENDING)
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
            else if (!wOk)
            {
                return null;
            }

            // 3. 读响应（先给设备一点时间回包，避免读到上一条命令的残留响应）
            Thread.Sleep(25);
            byte[] rx = new byte[PACKET_SIZE];
            OVERLAPPED rOl = new OVERLAPPED();
            rOl.hEvent = hEvent;
            uint rxRead = 0;
            bool rOk = ReadFile(handle, rx, (uint)rx.Length, out rxRead, ref rOl);
            if (!rOk && Marshal.GetLastWin32Error() == ERROR_IO_PENDING)
            {
                if (WaitForSingleObject(hEvent, (uint)timeoutMs) == WAIT_OBJECT_0)
                {
                    GetOverlappedResult(handle, ref rOl, out rxRead, false);
                    return rx;
                }
                CancelIo(handle);
                return null;
            }
            if (rOk) return rx;
            return null;
        }

        public static void LoadHardwareCache()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CacheRegistryKey))
                {
                    if (key == null) return;
                    object b = key.GetValue("BatteryPercent");
                    if (b != null) CachedBatteryPercent = Convert.ToInt32(b);
                    object c = key.GetValue("IsCharging");
                    if (c != null) CachedIsCharging = Convert.ToBoolean(c);
                    object p = key.GetValue("PollingRate");
                    if (p != null) CachedPollingRate = Convert.ToInt32(p);
                }
            }
            catch { }
        }

        public static void SaveHardwareCache()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CacheRegistryKey))
                {
                    if (key == null) return;
                    key.SetValue("BatteryPercent", CachedBatteryPercent);
                    key.SetValue("IsCharging", CachedIsCharging ? 1 : 0);
                    key.SetValue("PollingRate", CachedPollingRate);
                    key.SetValue("LastUpdated", DateTime.Now.ToString("o"));
                }
            }
            catch { }
        }

        /// <summary>调试: 返回所有 VID 0x0B05 的 HID 接口路径。</summary>
        public static List<string> DebugListAsusPaths()
        {
            List<string> paths = new List<string>();
            Guid hidGuid;
            RazerDeviceHelper.HidD_GetHidGuid(out hidGuid);
            IntPtr devInfo = RazerDeviceHelper.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, 0x12);
            if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return paths;
            RazerDeviceHelper.SP_DEVICE_INTERFACE_DATA ifData = new RazerDeviceHelper.SP_DEVICE_INTERFACE_DATA();
            ifData.cbSize = Marshal.SizeOf(ifData);
            uint idx = 0;
            try
            {
                while (RazerDeviceHelper.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, idx++, ref ifData))
                {
                    uint reqSize;
                    RazerDeviceHelper.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                    if (reqSize == 0) continue;
                    IntPtr buf = Marshal.AllocHGlobal((int)reqSize);
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 5);
                    try
                    {
                        if (RazerDeviceHelper.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, buf, reqSize, out reqSize, IntPtr.Zero))
                        {
                            IntPtr pp = new IntPtr(buf.ToInt64() + 4);
                            string p = Marshal.PtrToStringAuto(pp);
                            if (!string.IsNullOrEmpty(p) && p.ToLower().Contains("vid_0b05")) paths.Add(p);
                        }
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
            finally { RazerDeviceHelper.SetupDiDestroyDeviceInfoList(devInfo); }
            return paths;
        }


        /// <summary>打开控制接口并发送一条写命令，成功返回设备回显包。</summary>
        private static byte[] SendWriteCommand(byte[] payload)
        {
            List<string> paths = DebugListAsusPaths();
            foreach (string path in paths)
            {
                int pid;
                DeviceProfile prof;
                if (!TryResolvePid(path, out pid) || !KnownDevices.TryGetValue(pid, out prof)) continue;
                if (lastDevicePath != null && path != lastDevicePath) continue;
                if (path.ToLower().IndexOf(prof.Iface) < 0) continue;
                lastReportId = prof.ReportId;
                IntPtr handle = RazerDeviceHelper.CreateFile(path, GENERIC_READ | GENERIC_WRITE,
                    RazerDeviceHelper.FILE_SHARE_READ | RazerDeviceHelper.FILE_SHARE_WRITE,
                    IntPtr.Zero, RazerDeviceHelper.OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1)) continue;
                IntPtr hEvent = CreateEvent(IntPtr.Zero, true, false, null);
                try
                {
                    return SendAndReceive(handle, hEvent, BuildPacket(payload), 1200);
                }
                finally
                {
                    if (hEvent != IntPtr.Zero) CloseHandle(hEvent);
                    RazerDeviceHelper.CloseHandle(handle);
                }
            }
            return null;
        }

        /// <summary>提交保存：设备只有在收到 03 50 03 后才把设置写入固件并生效。</summary>
        public static bool SaveSettings()
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x50, 0x03 });
                return r != null && r.Length > 1 && r[1] == 0x50;
            }
        }

        /// <summary>设置轮询率（125/250/500/1000/2000/4000/8000 Hz）。</summary>
        public static bool SetPollingRate(int hz)
        {
            int code;
            switch (hz)
            {
                case 125: code = 0; break;
                case 250: code = 1; break;
                case 500: code = 2; break;
                case 1000: code = 3; break;
                case 2000: code = 4; break;
                case 4000: code = 5; break;
                case 8000: code = 6; break;
                default: return false;
            }
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x51, 0x31, 0x04, 0x00, (byte)code });
                bool ok = r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x31;
                if (ok) { CachedPollingRate = hz; SaveHardwareCache(); SaveSettings(); }
                return ok;
            }
        }

        /// <summary>设置该 profile 的 DPI 档数量（1..4）。空白组必须先设档数才能写入 DPI。</summary>
        public static bool SetDpiCount(byte count)
        {
            if (count < 1 || count > 4) return false;
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x51, 0x31, 0x0A, 0x00, count });
                bool ok = r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x31;
                if (ok) SaveSettings();
                return ok;
            }
        }

        /// <summary>设置第 stage 档 DPI（1 起），dpi 需为 50 的倍数且 100..36000。</summary>
        public static bool SetDpiStage(int stage, int dpi, byte r, byte g, byte b)
        {
            if (stage < 1 || stage > 4) return false;
            if (dpi < 100 || dpi > 36000) return false;
            if (dpi % 50 != 0) return false;
            int encoded = (dpi - 50) / 50;
            lock (hidLock)
            {
                byte[] rx = SendWriteCommand(new byte[] {
                    0x51, 0x31, (byte)(stage - 1), 0x00,
                    (byte)(encoded & 0xFF), (byte)((encoded >> 8) & 0xFF),
                    r, g, b });
                bool ok = rx != null && rx.Length > 2 && rx[1] == 0x51 && rx[2] == 0x31;
                if (ok) SaveSettings();
                return ok;
            }
        }

        /// <summary>切换当前生效的 DPI 档（1 起）。</summary>
        public static bool SwitchDpiStage(int stage)
        {
            if (stage < 1 || stage > 4) return false;
            lock (hidLock)
            {
                byte[] rx = SendWriteCommand(new byte[] { 0x51, 0x31, 0x09, 0x00, (byte)stage });
                return rx != null && rx.Length > 2 && rx[1] == 0x51 && rx[2] == 0x31;
            }
        }





        /// <summary>一组板载配置的快照。</summary>
        public class ProfileSnapshot
        {
            public int Profile;
            public int[] Dpi;
            public ushort[] Bindings;
            public byte LightMode;
            public byte LightBrightness;
            public byte R;
            public byte G;
            public byte B;
            public int PollingRate;
            public int PowerOff;
            public int LowBatt;
        }

        /// <summary>读取指定 profile 的完整配置（自动切回原 profile）。</summary>
        public static ProfileSnapshot ReadProfile(int profile)
        {
            lock (hidLock)
            {
                int original = GetCurrentProfile();
                if (profile < 1 || profile > 5) return null;
                if (!SwitchProfile(profile)) return null;
                System.Threading.Thread.Sleep(250);

                ProfileSnapshot snap = new ProfileSnapshot();
                snap.Profile = profile;
                snap.Dpi = GetDpiStages();
                System.Threading.Thread.Sleep(120);
                snap.Bindings = GetButtonBindings();
                System.Threading.Thread.Sleep(120);
                System.Threading.Thread.Sleep(120);
                byte[] lit = GetLightingRaw(0);
                if (lit != null && lit.Length >= 5)
                {
                    snap.LightMode = lit[0];
                    snap.LightBrightness = lit[1];
                    snap.R = lit[2];
                    snap.G = lit[3];
                    snap.B = lit[4];
                }
                int[] power = GetPowerSettings();
                if (power != null) { snap.PowerOff = power[0]; snap.LowBatt = power[1]; }
                byte[] pr = SendWriteCommand(new byte[] { 0x12, 0x04, 0x00 });
                if (pr != null && pr.Length > 13 && pr[1] == 0x12 && pr[2] == 0x04 && pr[3] == 0x00)
                {
                    int code = pr[13];
                    if (code >= 0 && code <= 6) snap.PollingRate = 125 << code;
                }

                if (original >= 1 && original <= 5 && original != profile)
                {
                    SwitchProfile(original);
                    System.Threading.Thread.Sleep(200);
                }
                return snap;
            }
        }

        /// <summary>把快照写入指定 profile（自动切回原 profile）。</summary>
        public static bool WriteProfile(ProfileSnapshot snap)
        {
            if (snap == null) return false;
            lock (hidLock)
            {
                int original = GetCurrentProfile();
                if (!SwitchProfile(snap.Profile)) return false;
                System.Threading.Thread.Sleep(250);

                bool ok = true;
                if (snap.Dpi != null)
                {
                    // 空白组或档数不足时，先补足档数
                    SetDpiCount((byte)Math.Min(4, snap.Dpi.Length));
                    System.Threading.Thread.Sleep(200);
                    ok = SetAllDpiStages(snap.Dpi, snap.R, snap.G, snap.B) && ok;
                    System.Threading.Thread.Sleep(150);
                }
                if (snap.Bindings != null)
                {
                    for (int i = 0; i < snap.Bindings.Length && i < 8; i++)
                    {
                        ok = SetButtonBinding(i, snap.Bindings[i]) && ok;
                    }
                }
                ok = SetLightingFull(snap.LightMode, snap.LightBrightness, snap.R, snap.G, snap.B, 0, false, 0x07, 0) && ok;
                if (snap.PollingRate > 0) ok = SetPollingRate(snap.PollingRate) && ok;
                if (snap.PowerOff >= 0) ok = SetPowerSettings((byte)snap.PowerOff, (byte)snap.LowBatt) && ok;
                SaveSettings();

                if (original >= 1 && original <= 5 && original != snap.Profile)
                {
                    SwitchProfile(original);
                    System.Threading.Thread.Sleep(200);
                }
                return ok;
            }
        }

        /// <summary>把 from 组的全部配置复制到 to 组。</summary>
        public static bool CopyProfile(int from, int to)
        {
            if (from == to) return false;
            ProfileSnapshot snap = ReadProfile(from);
            if (snap == null) return false;
            snap.Profile = to;
            return WriteProfile(snap);
        }

        /// <summary>读当前生效 Profile（1..5），失败返回 -1。</summary>
        public static int GetCurrentProfile()
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x12, 0x00 });
                if (r == null || r.Length < 13 || r[1] != 0x12 || r[2] != 0x00) return -1;
                return r[11];
            }
        }

        /// <summary>切换 Profile（1..5）。</summary>
        public static bool SwitchProfile(int profile)
        {
            if (profile < 1 || profile > 5) return false;
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x50, 0x02, (byte)profile });
                return r != null && r.Length > 2 && r[1] == 0x50;
            }
        }

        /// <summary>读 4 档 DPI（X/Y 交替，encoded=(DPI-50)/50），失败返回 null。</summary>
        /// <summary>读当前生效的 DPI 档（1..4），失败返回 -1。</summary>
        public static int GetCurrentDpiStage()
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x12, 0x04, 0x01 });
                if (r == null || r.Length <= 5) return -1;
                int stage = r[5] + 1;
                return (stage >= 1 && stage <= 4) ? stage : -1;
            }
        }

        public static int[] GetDpiStages()
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x12, 0x04, 0x02 });
                if (r == null || r.Length < 21 || r[1] != 0x12 || r[2] != 0x04 || r[3] != 0x02) return null;
                int[] stages = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    int encoded = r[5 + i * 4] | (r[6 + i * 4] << 8);
                    stages[i] = encoded * 50 + 50;
                }
                return stages;
            }
        }

        /// <summary>设置自动省电（0=1分钟, 1=2, 2=3, 3=5, 4=10, 0xFF=永不）与低电警告阈值。</summary>
        public static bool SetPowerSettings(byte powerOffSetting, byte lowBatteryWarning)
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x51, 0x37, 0x00, 0x00, powerOffSetting, 0x00, lowBatteryWarning });
                bool ok = r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x37;
                if (ok) SaveSettings();
                return ok;
            }
        }

        /// <summary>读电源设置：out 顺序为 [省电档, 低电阈值]。</summary>
        public static int[] GetPowerSettings()
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x12, 0x07 });
                if (r == null || r.Length < 8 || r[1] != 0x12 || r[2] != 0x07) return null;
                return new int[] { r[6], r[7] };
            }
        }


        /// <summary>角度吸附开关。</summary>
        public static bool SetAngleSnapping(bool enabled)
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x51, 0x31, 0x06, 0x00, (byte)(enabled ? 1 : 0) });
                return r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x31;
            }
        }

        /// <summary>角度微调（P520 步进 5）。</summary>
        public static bool SetAngleTuning(int value)
        {
            if (value < 0 || value > 1000) return false;
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] {
                    0x51, 0x31, 0x0B, 0x00,
                    (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) });
                return r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x31;
            }
        }

        /// <summary>去抖时间（档位 0=关，1..7 = 8/12/16/20/24/28/32 ms）。</summary>
        public static bool SetDebounce(byte level)
        {
            if (level > 7) return false;
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x51, 0x31, 0x05, 0x00, level });
                return r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x31;
            }
        }

        /// <summary>抬起距离：0 = Low，1 = High。</summary>
        public static bool SetLiftOffDistance(byte mode)
        {
            if (mode > 1) return false;
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x51, 0x35, 0xFF, 0x00, 0xFF, mode });
                return r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x35;
            }
        }

        /// <summary>一次性写入多个 DPI 档（每档带颜色），任一失败返回 false。</summary>
        public static bool SetAllDpiStages(int[] dpis, byte r, byte g, byte b)
        {
            if (dpis == null || dpis.Length == 0) return false;
            bool all = true;
            for (int i = 0; i < dpis.Length && i < 4; i++)
            {
                all = SetDpiStage(i + 1, dpis[i], r, g, b) && all;
            }
            return all;
        }

        /// <summary>设置 RapidFire 间隔（左/右/滚轮，单位 ms）。</summary>
        public static bool SetRapidFire(ushort left, ushort right, ushort scroll)
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] {
                    0x51, 0x22, 0x00, 0x00,
                    (byte)(left & 0xFF), (byte)((left >> 8) & 0xFF),
                    (byte)(right & 0xFF), (byte)((right >> 8) & 0xFF),
                    (byte)(scroll & 0xFF), (byte)((scroll >> 8) & 0xFF) });
                bool ok = r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x22;
                if (ok) SaveSettings();
                return ok;
            }
        }

        /// <summary>完整灯效写入（含方向/随机/速度）。</summary>
        public static bool SetLightingFull(byte mode, byte brightness, byte r, byte g, byte b,
                                           byte direction, bool randomColor, byte speed, byte zone)
        {
            lock (hidLock)
            {
                byte[] rx = SendWriteCommand(new byte[] {
                    0x51, 0x28, zone, 0x00, mode, brightness, r, g, b,
                    direction,
                    (byte)(randomColor ? 1 : 0),
                    speed });
                bool ok = rx != null && rx.Length > 2 && rx[1] == 0x51 && rx[2] == 0x28;
                if (ok) SaveSettings();
                return ok;
            }
        }

        /// <summary>设置灯效。P520 仅有一个灯区（数组索引 0 = 滚轮）。</summary>
        /// <param name="mode">0 静态 / 1 呼吸 / 2 循环 / 4 反应 / 6 电量 / 0xF0 关</param>
        public static bool SetLighting(byte mode, byte brightness, byte r, byte g, byte b, byte zone = 0, byte speed = 0x07)
        {
            lock (hidLock)
            {
                byte[] rx = SendWriteCommand(new byte[] {
                    0x51, 0x28, zone, 0x00, mode, brightness, r, g, b,
                    0x00,  // 动画方向
                    0x00,  // 随机色
                    speed });
                bool ok = rx != null && rx.Length > 2 && rx[1] == 0x51 && rx[2] == 0x28;
                if (ok) SaveSettings();
                return ok;
            }
        }

        /// <summary>读取当前灯效原始报文（长度 12），失败返回 null。</summary>
        public static byte[] GetLightingRaw(byte zone = 0)
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x12, 0x03, zone });
                if (r == null || r.Length < 8 || r[1] != 0x12 || r[2] != 0x03) return null;
                byte[] v = new byte[12];
                Array.Copy(r, 5, v, 0, Math.Min(12, r.Length - 5));
                return v;
            }
        }

        /// <summary>读取 8 个按键槽位的动作码（小端 ushort），失败返回 null。</summary>
        public static ushort[] GetButtonBindings()
        {
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] { 0x12, 0x05 });
                if (r == null || r.Length < 21 || r[1] != 0x12 || r[2] != 0x05) return null;
                ushort[] bindings = new ushort[8];
                for (int i = 0; i < 8; i++)
                {
                    bindings[i] = (ushort)(r[5 + i * 2] | (r[6 + i * 2] << 8));
                }
                return bindings;
            }
        }

        /// <summary>按键槽位的固定源动作码（设备侧标识）。</summary>
        public static readonly ushort[] ButtonSourceCodes = new ushort[]
        {
            0x01F0, // 0 左键
            0x01F1, // 1 右键
            0x01F2, // 2 滚轮键
            0x01E4, // 3 侧后退
            0x01E5, // 4 侧前进
            0x01E6, // 5 DPI 键
            0x01E8, // 6 滚上
            0x01E9, // 7 滚下
        };

        /// <summary>设置某个按键槽位的目标动作码（标准 HID Usage ID 或媒体码）。</summary>
        public static bool SetButtonBinding(int slot, ushort actionCode)
        {
            if (slot < 0 || slot >= ButtonSourceCodes.Length) return false;
            ushort src = ButtonSourceCodes[slot];
            lock (hidLock)
            {
                byte[] r = SendWriteCommand(new byte[] {
                    0x51, 0x21, 0x00, 0x00,
                    (byte)(src & 0xFF), (byte)((src >> 8) & 0xFF),
                    (byte)(actionCode & 0xFF), (byte)((actionCode >> 8) & 0xFF) });
                bool ok = r != null && r.Length > 2 && r[1] == 0x51 && r[2] == 0x21;
                if (ok) SaveSettings();
                return ok;
            }
        }

        /// <summary>动作码 → 可读名称。</summary>
        public static string ButtonActionName(ushort code)
        {
            switch (code)
            {
                case 0x01F0: return "Left Click";
                case 0x01F1: return "Right Click";
                case 0x01F2: return "Middle Click";
                case 0x01E4: return "Back";
                case 0x01E5: return "Forward";
                case 0x01E6: return "DPI";
                case 0x01E8: return "Scroll Up";
                case 0x01E9: return "Scroll Down";
                case 0x01C0: return "RapidFire (L)";
                case 0x01C1: return "RapidFire (R)";
                case 0x00F6: return "Volume Up";
                case 0x00F5: return "Volume Down";
                case 0x00F4: return "Mute";
                case 0x00F0: return "Play/Pause";
                case 0x00F2: return "Next Track";
                case 0x00F3: return "Previous Track";
                default:
                    if (code >= 0x0004 && code <= 0x00A4) return "HID Usage 0x" + code.ToString("X2");
                    return "Unknown (0x" + code.ToString("X4") + ")";
            }
        }

        /// <summary>扫描并读取 ASUS 鼠标状态，未找到返回 null。</summary>
        public static MouseBatteryInfo QueryAsusDeviceInfo()
        {
            lock (hidLock)
            {
                Guid hidGuid;
                RazerDeviceHelper.HidD_GetHidGuid(out hidGuid);
                IntPtr devInfo = RazerDeviceHelper.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, 0x12);
                if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return null;

                RazerDeviceHelper.SP_DEVICE_INTERFACE_DATA ifData = new RazerDeviceHelper.SP_DEVICE_INTERFACE_DATA();
                MouseBatteryInfo result = null;
                uint memberIdx = 0;

                try
                {
                    ifData.cbSize = Marshal.SizeOf(ifData);
                    while (RazerDeviceHelper.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, memberIdx++, ref ifData))
                    {
                        uint reqSize;
                        RazerDeviceHelper.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                        if (reqSize == 0) continue;

                        IntPtr detailBuffer = Marshal.AllocHGlobal((int)reqSize);
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 5);
                        try
                        {
                            if (!RazerDeviceHelper.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, detailBuffer, reqSize, out reqSize, IntPtr.Zero))
                                continue;

                            int pathOffset = 4;
                            IntPtr pDevicePath = new IntPtr(detailBuffer.ToInt64() + pathOffset);
                            string devicePath = Marshal.PtrToStringAuto(pDevicePath);
                            if (string.IsNullOrEmpty(devicePath)) continue;

                            string lower = devicePath.ToLower();
                            if (!lower.Contains("vid_0b05")) continue;

                            int pid;
                            if (!TryResolvePid(devicePath, out pid)) continue;
                            DeviceProfile prof;
                            if (!KnownDevices.TryGetValue(pid, out prof)) continue;
                            if (lower.IndexOf(prof.Iface) < 0) continue;
                            lastReportId = prof.ReportId;

                            IntPtr handle = RazerDeviceHelper.CreateFile(
                                devicePath,
                                GENERIC_READ | GENERIC_WRITE,
                                RazerDeviceHelper.FILE_SHARE_READ | RazerDeviceHelper.FILE_SHARE_WRITE,
                                IntPtr.Zero,
                                RazerDeviceHelper.OPEN_EXISTING,
                                FILE_FLAG_OVERLAPPED,
                                IntPtr.Zero);

                            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) continue;

                            IntPtr hEvent = CreateEvent(IntPtr.Zero, true, false, null);
                            try
                            {
                                byte[] resp = SendAndReceive(handle, hEvent, BuildPacket(new byte[] { 0x12, 0x07 }), 1200);
                                if (resp != null && resp.Length > 10 && resp[1] == 0x12 && resp[2] == 0x07)
                                {
                                    int pct = resp[5];
                                    bool charging = resp[10] > 0;
                                    if (pct >= 0 && pct <= 100)
                                    {
                                        CachedBatteryPercent = pct;
                                        CachedIsCharging = charging;
                                        CachedLastUpdated = DateTime.Now;
                                    }

                                    // 轮询率
                                    int rate = -1;
                                    byte[] pr = SendAndReceive(handle, hEvent, BuildPacket(new byte[] { 0x12, 0x04, 0x00 }), 800);
                                    if (pr != null && pr.Length > 13 && pr[1] == 0x12 && pr[2] == 0x04 && pr[3] == 0x00)
                                    {
                                        int code = pr[13];
                                        if (code >= 0 && code <= 6) rate = 125 << code;
                                    }
                                    if (rate > 0) CachedPollingRate = rate;

                                    result = new MouseBatteryInfo();
                                    result.DeviceId = "asus_" + prof.Name.ToLower().Replace(' ', '_').Replace("(", "").Replace(")", "");
                                    result.Brand = "ASUS";
                                    result.Category = DeviceCategory.Mouse;
                                    result.DeviceName = prof.Name;
                                    lastDevicePath = devicePath;
                                    result.BatteryPercent = pct;
                                    result.ExactBatteryPercent = (double)pct;
                                    result.IsCharging = charging;
                                    result.IsConnected = pct > 0;
                                    result.IsDonglePresent = true;
                                    result.IsCustom = false;
                                    result.Transport = prof.Wireless ? "2.4G" : "USB";
                                    result.LastUpdated = DateTime.Now;
                                    result.HardwareId = "VID_0B05&PID_" + pid.ToString("X4");
                                    result.HardwareFingerprint = devicePath;
                                    result.PollingRate = CachedPollingRate;
                                    result.DpiStageCount = 4;
                                }
                            }
                            finally
                            {
                                if (hEvent != IntPtr.Zero) CloseHandle(hEvent);
                                RazerDeviceHelper.CloseHandle(handle);
                            }

                            if (result != null) break;
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(detailBuffer);
                        }
                    }
                }
                catch { }
                finally
                {
                    RazerDeviceHelper.SetupDiDestroyDeviceInfoList(devInfo);
                }

                SaveHardwareCache();
                return result;
            }
        }
    }
}
