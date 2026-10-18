// ============================================================================
// 模块名称: Program.cs
// 所属区域: Core (核心管理层)
// 核心职责:
//   应用程序顶层入口中枢：
//   1. 锁定基准工作目录，杜绝开机自启时被重定向至 System32 的路径漂移
//   2. 声明 Windows 硬件高 DPI 感知 (Per-Monitor DPI Aware)
//   3. 系统全局命名互斥体 (Mutex) 单实例保护，多开时发送系统广播唤醒已有实例并安全退出
//   4. 解析 --autostart 命令行参数与未捕获异常统一兜底弹窗
// 依赖关系: NativeMethods.cs, StartupManager.cs, MainForm.cs
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 任何 AI Agent 在新增启动流程或参数时，【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RazerBatteryTray
{
    static class Program
    {
        private const string SingleInstanceMutexName = @"Global\FerrisPulse_SingleInstance_Mutex";
        public static readonly int WM_SHOWME = NativeMethods.RegisterWindowMessage("FerrisPulse_ActivateExistingInstance");

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        private static Mutex appMutex = null;

        [STAThread]
        static void Main(string[] args)
        {
            // 1. 强制将工作目录锚定在程序运行目录，彻底根治开机启动被重定向至 C:\Windows\System32 的路径破坏
            try
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }
            catch { }

            // 1.1 静默清理自更新残留的旧版本备份 (.old)
            try
            {
                string oldBackup = Application.ExecutablePath + ".old";
                if (File.Exists(oldBackup))
                {
                    File.Delete(oldBackup);
                }
            }
            catch { }

            // 2. 声明系统物理级 High-DPI 感知
            try
            {
                NativeMethods.SetProcessDPIAware();
            }
            catch { }

            // 3. 同步开始菜单与 AUMID 快捷方式
            try
            {
                StartupManager.EnsureAppShortcutWithAumid();
            }
            catch { }

            // 4. 全局未捕获异常安全兜底
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => {
                string msg = e.Exception != null ? (e.Exception.Message + "\n\n" + e.Exception.StackTrace) : "未知线程异常";
                MessageBox.Show("程序发生异常: " + msg, "FerrisPulse · 灵脉 - 错误提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                Exception ex = e.ExceptionObject as Exception;
                string msg = ex != null ? (ex.Message + "\n\n" + ex.StackTrace) : "未知系统错误";
                MessageBox.Show("未处理的致命异常: " + msg, "FerrisPulse · 灵脉 - 致命错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // 5. 解析命令行参数
            bool isAutoStart = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] != null && args[i].IndexOf("autostart", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isAutoStart = true;
                        break;
                    }
                }
            }

            // 6. 系统全局命名互斥体单实例管理（取代暴力 Process.Kill）
            bool isNewInstance = false;
            try
            {
                appMutex = new Mutex(true, SingleInstanceMutexName, out isNewInstance);
            }
            catch
            {
                isNewInstance = true;
            }

            if (!isNewInstance)
            {
                // 如果已有实例运行且本次是用户手动双击，则广播激活消息唤醒已有窗体
                if (!isAutoStart)
                {
                    PostMessage((IntPtr)0xffff, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                }
                return;
            }

            // 7. 启动主窗体消息循环
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new MainForm(isAutoStart));
            }
            finally
            {
                if (appMutex != null)
                {
                    try { appMutex.ReleaseMutex(); } catch { }
                    appMutex.Close();
                    appMutex = null;
                }
            }
        }
    }
}
