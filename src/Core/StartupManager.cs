// ============================================================================
// 模块名称: StartupManager.cs
// 所属区域: Core (核心管理层)
// 核心职责:
//   高鲁棒性开机自启动管理引擎：
//   1. 统一管理 HKCU\...\Run 注册表键与 Windows 10/11 StartupApproved\Run 启动仲裁
//   2. 修复时序倒挂 Bug：先安全迁移旧版启动项，最后才清理历史无用键名
//   3. 创建与同步带有 AppUserModelID (AUMID) 的 Windows 开始菜单与桌面快捷方式
// 依赖关系: NativeMethods.cs
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 任何 AI Agent 在新增或修改自启动逻辑与注册表项时，【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RazerBatteryTray
{
    public static class StartupManager
    {
        public const string AppName = "FerrisPulse";
        public static readonly string[] LegacyAppNames = new string[] { "RazerBatteryTray", "RazerBattery", "RazerTray" };
        public const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string StartupApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        public const string AppUserModelId = "FerrisPulse.App";

        /// <summary>
        /// 检测当前系统是否已为 FerrisPulse 配置了开机自启动
        /// 遵循安全迁移原则：先检查新名称，若不存在再检查历史旧名称，绝对不在此做删除
        /// </summary>
        public static bool IsAutoStartConfigured()
        {
            if (IsAutoStartTaskExists()) return true;
            try
            {
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "FerrisPulse 灵脉.lnk");
                if (File.Exists(lnk)) return true;
            }
            catch { }
            return IsAutoStartInRegistryOrFolder();
        }

        private static bool IsAutoStartInRegistryOrFolder()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(AppName);
                        if (val != null) return true;

                        foreach (var legacyName in LegacyAppNames)
                        {
                            if (key.GetValue(legacyName) != null) return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 安全获取宿主程序的绝对路径，彻底防御测试进程（如 PowerShell、NUnit 等）反射调用时的污染
        /// </summary>
        public static string GetApplicationExecutablePath()
        {
            try
            {
                string currentExe = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(currentExe))
                {
                    string fileName = Path.GetFileName(currentExe);
                    if (fileName.Equals("FerrisPulse.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return currentExe;
                    }
                }

                // 若当前为 PowerShell 或测试宿主进程，回退到当前基目录下的 FerrisPulse.exe
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir))
                {
                    string candidate = Path.Combine(baseDir, "FerrisPulse.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                // 尝试从当前工作目录探查
                string curDir = Directory.GetCurrentDirectory();
                if (!string.IsNullOrEmpty(curDir))
                {
                    string candidateCur = Path.Combine(curDir, "FerrisPulse.exe");
                    if (File.Exists(candidateCur))
                    {
                        return candidateCur;
                    }
                }

                // 尝试从程序集所在目录探查
                string asmLocation = typeof(StartupManager).Assembly.Location;
                if (!string.IsNullOrEmpty(asmLocation))
                {
                    string asmDir = Path.GetDirectoryName(asmLocation);
                    string candidate2 = Path.Combine(asmDir, "FerrisPulse.exe");
                    if (File.Exists(candidate2))
                    {
                        return candidate2;
                    }
                }
            }
            catch { }
            return Application.ExecutablePath;
        }

        /// <summary>
        /// 开启或关闭开机自启动，并协同同步 Windows 10/11 的 StartupApproved 策略
        /// </summary>
        public static bool SetAutoStart(bool enabled, out string errorMessage)
        {
            errorMessage = null;

            // 路径优先级：启动文件夹（普通用户必可写）-> 注册表 Run -> 登录计划任务
            string folderErr = null;
            if (StartupFolderViaWScript(enabled, out folderErr)) return true;

            string regErr = null;
            try
            {
                using (var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (runKey == null) regErr = "无法打开注册表 Run 键";
                    else
                    {
                        if (enabled)
                        {
                            string exePath = GetApplicationExecutablePath();
                            runKey.SetValue(AppName, "\"" + exePath + "\" --autostart");
                            try
                            {
                                using (var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRunKey, true))
                                {
                                    if (approvedKey != null)
                                    {
                                        byte[] val = approvedKey.GetValue(AppName) as byte[];
                                        if (val == null || val.Length == 0)
                                        {
                                            byte[] enabledState = new byte[12];
                                            enabledState[0] = 0x02;
                                            approvedKey.SetValue(AppName, enabledState, RegistryValueKind.Binary);
                                        }
                                        else if (val[0] == 0x03)
                                        {
                                            val[0] = 0x02;
                                            approvedKey.SetValue(AppName, val, RegistryValueKind.Binary);
                                        }
                                    }
                                }
                            }
                            catch { }
                            CleanupLegacyRunKeys(runKey);
                        }
                        else
                        {
                            runKey.DeleteValue(AppName, false);
                            CleanupLegacyRunKeys(runKey);
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex) { regErr = ex.Message; }

            string taskErr = null;
            if (SetAutoStartViaTask(enabled, out taskErr)) return true;

            errorMessage = "启动文件夹: " + (folderErr == null ? "失败" : folderErr)
                         + " | 注册表: " + (regErr == null ? "失败" : regErr)
                         + " | 计划任务: " + (taskErr == null ? "失败" : taskErr);
            return false;
        }

        public static void SyncAutoStartRegistry()
        {
            if (!IsAutoStartConfigured()) return;

            string errMsg;
            SetAutoStart(true, out errMsg);
        }

        /// <summary>
        /// 安全清理历史版本的旧名称键值
        /// </summary>
        private static void CleanupLegacyRunKeys(RegistryKey runKey)
        {
            if (runKey == null) return;
            foreach (var legacyName in LegacyAppNames)
            {
                try { runKey.DeleteValue(legacyName, false); } catch { }
            }
        }

        /// <summary>
        /// 创建并同步 Windows 开始菜单与桌面图标，绑定 AUMID 以保证原生 Toast 横幅通知完全正常
        /// </summary>
        public static void EnsureAppShortcutWithAumid()
        {
            try
            {
                NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
                if (!Directory.Exists(startMenu)) Directory.CreateDirectory(startMenu);
                string lnkPath = Path.Combine(startMenu, "FerrisPulse · 灵脉.lnk");
                string targetExe = GetApplicationExecutablePath();
                string dir = Path.GetDirectoryName(targetExe);
                string icoPath = Path.Combine(dir, "app.ico");
                if (!File.Exists(icoPath)) icoPath = targetExe;

                // 清理旧版本的快捷方式
                string oldLnkPath = Path.Combine(startMenu, "RazerBatteryTray.lnk");
                if (File.Exists(oldLnkPath))
                {
                    try { File.Delete(oldLnkPath); } catch { }
                }

                try
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string oldDesktopLnk = Path.Combine(desktop, "RazerBatteryTray.lnk");
                    if (File.Exists(oldDesktopLnk)) { File.Delete(oldDesktopLnk); }
                }
                catch { }

                if (!File.Exists(lnkPath))
                {
                    IShellLinkW link = (IShellLinkW)new ShellLink();
                    link.SetPath(targetExe);
                    link.SetWorkingDirectory(dir);
                    link.SetIconLocation(icoPath, 0);
                    link.SetDescription("FerrisPulse · 灵脉 外设电量感知引擎");

                    IPropertyStore store = (IPropertyStore)link;
                    PROPERTYKEY key = new PROPERTYKEY();
                    key.fmtid = new Guid("{9F4C2855-9F79-4BDE-A8E1-E1DE34EB1AE8}");
                    key.pid = 5;

                    PROPVARIANT pv = new PROPVARIANT();
                    pv.vt = 31; // VT_LPWSTR
                    pv.pwszVal = Marshal.StringToCoTaskMemUni(AppUserModelId);
                    try
                    {
                        store.SetValue(ref key, ref pv);
                        store.Commit();
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pv.pwszVal);
                    }

                    IPersistFile file = (IPersistFile)link;
                    file.Save(lnkPath, true);

                    // 不在用户桌面创建任何快捷方式，保持用户桌面纯净无侵扰（Toast 通知仅需开始菜单 AUMID 链接）

                    try { NativeMethods.SHChangeNotify(0x7FFFFFFF, 0x1000, IntPtr.Zero, IntPtr.Zero); } catch { }
                }

                // 注册 HKCU 提示信息保证通知中心标题与图标呈现
                try
                {
                    using (var appKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + AppUserModelId))
                    {
                        if (appKey != null)
                        {
                            appKey.SetValue("DisplayName", "FerrisPulse · 灵脉");
                            appKey.SetValue("IconUri", icoPath);
                            appKey.SetValue("IconBackgroundColor", "00000000");
                        }
                    }
                    using (var notifKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\" + AppUserModelId))
                    {
                        if (notifKey != null)
                        {
                            notifKey.SetValue("ShowInActionCenter", 1, RegistryValueKind.DWord);
                            notifKey.SetValue("Enabled", 1, RegistryValueKind.DWord);
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        /// <summary>
        /// 首次运行策略：严格遵从绿色便携零侵扰原则，绝不在桌面自动创建快捷方式。
        /// 桌面快捷方式 100% 由用户在托盘右键菜单中主动触发创建。
        /// 本方法已废弃并置为安全空操作，保留此签名以维持二进制反射与外部调用契约兼容。
        /// </summary>
        [Obsolete("遵从绿色便携零侵扰原则，桌面快捷方式仅允许用户在托盘手动创建")]
        public static void EnsureDesktopShortcutFirstRun(bool isAutoStart)
        {
            // 严格空操作，杜绝任何静默向桌面写文件行为
        }

        /// <summary>
        /// 在用户桌面创建带有高清矢量图标与工作目录绑定的快捷方式
        /// </summary>
        /// <summary>用系统 WScript.Shell 创建/删除启动文件夹快捷方式（与资源管理器同一实现）。</summary>
        private static bool StartupFolderViaWScript(bool enabled, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (string.IsNullOrEmpty(startup)) { errorMessage = "无法定位启动文件夹"; return false; }
                string lnkPath = Path.Combine(startup, "FerrisPulse 灵脉.lnk");

                if (!enabled)
                {
                    if (File.Exists(lnkPath)) File.Delete(lnkPath);
                    return true;
                }

                string exe = GetApplicationExecutablePath();
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) { errorMessage = "找不到 FerrisPulse.exe"; return false; }

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) { errorMessage = "WScript.Shell 不可用"; return false; }
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { exe });
                scType.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "--autostart" });
                scType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(exe) });
                scType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "FerrisPulse · 灵脉" });
                scType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);

                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                return File.Exists(lnkPath);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>后备方案二：创建/删除当前用户的登录计划任务（不依赖注册表仲裁）。</summary>
        public static bool SetAutoStartViaTask(bool enabled, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string exe = GetApplicationExecutablePath();
                if (!File.Exists(exe)) { errorMessage = "找不到 FerrisPulse.exe"; return false; }

                string taskName = "FerrisPulse_Autostart";
                string arguments = enabled
                    ? "/create /tn " + Quote(taskName) + " /tr " + Quote("\"" + exe + "\" --autostart") + " /sc onlogon /f"
                    : "/delete /tn " + Quote(taskName) + " /f";

                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", arguments);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(15000);
                    if (p.ExitCode != 0)
                    {
                        string msg = p.StandardError.ReadToEnd();
                        if (string.IsNullOrEmpty(msg)) msg = p.StandardOutput.ReadToEnd();
                        errorMessage = "schtasks exit=" + p.ExitCode + " " + msg.Trim();
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static string Quote(string s)
        {
            return "\"" + s + "\"";
        }

        /// <summary>计划任务是否存在。</summary>
        public static bool IsAutoStartTaskExists()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/query /tn \"FerrisPulse_Autostart\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(8000);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        /// <summary>注册表不可写时的后备方案：在启动文件夹放置/删除快捷方式。</summary>
        public static bool SetAutoStartViaStartupFolder(bool enabled, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (string.IsNullOrEmpty(startup)) { errorMessage = "无法定位启动文件夹"; return false; }
                string lnkPath = Path.Combine(startup, "FerrisPulse 灵脉.lnk");

                if (!enabled)
                {
                    if (File.Exists(lnkPath)) File.Delete(lnkPath);
                    return true;
                }

                string targetExe = GetApplicationExecutablePath();
                if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
                {
                    errorMessage = "无法定位 FerrisPulse.exe";
                    return false;
                }

                string dir = Path.GetDirectoryName(targetExe);
                string icoPath = Path.Combine(dir, "app.ico");

                IShellLinkW link = (IShellLinkW)new ShellLink();
                link.SetPath(targetExe);
                link.SetWorkingDirectory(dir);
                link.SetArguments("--autostart");
                link.SetDescription("FerrisPulse · 灵脉");
                if (File.Exists(icoPath)) link.SetIconLocation(icoPath, 0);
                else link.SetIconLocation(targetExe, 0);

                IPersistFile file = (IPersistFile)link;
                file.Save(lnkPath, true);
                Marshal.FinalReleaseComObject(file);
                Marshal.FinalReleaseComObject(link);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool CreateDesktopShortcut(bool showNotice = false)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
                {
                    return false;
                }

                string lnkPath = Path.Combine(desktop, "FerrisPulse · 灵脉.lnk");
                string targetExe = GetApplicationExecutablePath();
                if (string.IsNullOrEmpty(targetExe) ||
                    targetExe.IndexOf("powershell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    targetExe.IndexOf("cmd.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FerrisPulse.exe");
                    if (!File.Exists(fallback))
                    {
                        fallback = Path.Combine(Directory.GetCurrentDirectory(), "FerrisPulse.exe");
                    }
                    if (File.Exists(fallback))
                    {
                        targetExe = fallback;
                    }
                    else
                    {
                        return false;
                    }
                }

                string dir = Path.GetDirectoryName(targetExe);
                string icoPath = Path.Combine(dir, "app.ico");

                IShellLinkW link = (IShellLinkW)new ShellLink();
                link.SetPath(targetExe);
                link.SetWorkingDirectory(dir);
                if (File.Exists(icoPath))
                {
                    link.SetIconLocation(icoPath, 0);
                }
                else
                {
                    link.SetIconLocation(targetExe, 0);
                }
                link.SetDescription("FerrisPulse · 灵脉 外设电量感知引擎");

                IPropertyStore store = (IPropertyStore)link;
                PROPERTYKEY key = new PROPERTYKEY();
                key.fmtid = new Guid("{9F4C2855-9F79-4BDE-A8E1-E1DE34EB1AE8}");
                key.pid = 5;

                PROPVARIANT pv = new PROPVARIANT();
                pv.vt = 31; // VT_LPWSTR
                pv.pwszVal = Marshal.StringToCoTaskMemUni(AppUserModelId);
                try
                {
                    store.SetValue(ref key, ref pv);
                    store.Commit();
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pv.pwszVal);
                }

                IPersistFile file = (IPersistFile)link;
                file.Save(lnkPath, true);

                try { NativeMethods.SHChangeNotify(0x7FFFFFFF, 0x1000, IntPtr.Zero, IntPtr.Zero); } catch { }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
