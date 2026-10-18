// ============================================================================
// 模块名称: MacroEngine.cs
// 所属区域: Services (后台长效服务层)
// 核心职责:
//   主机侧宏引擎。ASUS 设备不存储宏（逆向结论见 rog-reverse 12 章），
//   官方由 AsusDialAgent / Armoury Crate 在主机侧执行；本模块以同样的
//   思路在 FerrisPulse 内实现宏的录制、存储与回放。
//
//   组成:
//     MacroStep  : 单步动作（按下/抬起/延迟）
//     Macro      : 一组步骤
//     MacroEngine: 录制（低级键盘钩子）、播放（SendInput）、持久化（注册表）
//
// ⚠️ 风险提示: 宏回放属于模拟输入，部分竞技游戏反作弊会检测，
//   默认关闭，由用户显式启用。
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 修改逻辑时【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace RazerBatteryTray
{
    public enum MacroStepKind
    {
        KeyDown = 0,
        KeyUp = 1,
        Delay = 2
    }

    public class MacroStep
    {
        public MacroStepKind Kind { get; set; }
        public ushort ScanCode { get; set; }
        public int DelayMs { get; set; }

        public override string ToString()
        {
            if (Kind == MacroStepKind.Delay) return "Delay " + DelayMs + "ms";
            return (Kind == MacroStepKind.KeyDown ? "Down 0x" : "Up 0x") + ScanCode.ToString("X2");
        }
    }

    public class Macro
    {
        public string Name { get; set; }
        public List<MacroStep> Steps { get; set; }

        public Macro()
        {
            Steps = new List<MacroStep>();
        }

        public Macro(string name)
        {
            Name = name;
            Steps = new List<MacroStep>();
        }
    }

    public static class MacroEngine
    {
        private static readonly string RegKey = @"Software\RazerBatteryTray\Macros";
        private static readonly object sync = new object();

        public static bool IsRecording { get; private set; }
        public static bool ListenerActive { get; private set; }
        public static ushort TriggerScanCode = 0x6F; // F24
        public static string TriggerMacroName = null;
        public static bool IsPlaying { get; private set; }
        public static List<Macro> Macros = new List<Macro>();

        // ---- Win32 ----
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT
        {
            [FieldOffset(0)]
            public uint type;
            [FieldOffset(4)]
            public KEYBDINPUT ki;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private static IntPtr hookHandle = IntPtr.Zero;
        private static LowLevelKeyboardProc hookProc;
        private static Macro recording;
        private static Stopwatch stepWatch = new Stopwatch();

        /// <summary>启动常驻触发监听（捕获触发键后播放指定宏）。</summary>
        public static bool StartListener(ushort triggerScanCode, string macroName)
        {
            lock (sync)
            {
                TriggerScanCode = triggerScanCode;
                TriggerMacroName = macroName;
                if (hookHandle == IntPtr.Zero)
                {
                    hookProc = HookCallback;
                    hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(null), 0);
                }
                ListenerActive = hookHandle != IntPtr.Zero && macroName != null;
                return ListenerActive;
            }
        }

        /// <summary>停止触发监听。</summary>
        public static void StopListener()
        {
            lock (sync)
            {
                ListenerActive = false;
                TriggerMacroName = null;
                if (!IsRecording && hookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookHandle);
                    hookHandle = IntPtr.Zero;
                }
            }
        }

        /// <summary>开始录制，返回是否成功。</summary>
        public static bool StartRecording(string name)
        {
            lock (sync)
            {
                if (IsRecording) return false;
                recording = new Macro(name);
                stepWatch.Restart();
                hookProc = HookCallback;
                hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(null), 0);
                IsRecording = hookHandle != IntPtr.Zero;
                return IsRecording;
            }
        }

        /// <summary>停止录制并保存，返回录到的步骤数（0 表示无内容）。</summary>
        public static int StopRecording()
        {
            lock (sync)
            {
                if (!IsRecording) return 0;
                IsRecording = false;
                if (!ListenerActive && hookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookHandle);
                    hookHandle = IntPtr.Zero;
                }
                int count = recording != null ? recording.Steps.Count : 0;
                if (count > 0)
                {
                    Macros.Add(recording);
                    Save();
                }
                recording = null;
                return count;
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsRecording && recording != null)
            {
                KBDLLHOOKSTRUCT data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();

                // 跳过自身回放注入的事件（LLKHF_INJECTED = 0x10）
                if ((data.flags & 0x10) != 0) return CallNextHookEx(hookHandle, nCode, wParam, lParam);

                MacroStepKind kind;
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) kind = MacroStepKind.KeyDown;
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP) kind = MacroStepKind.KeyUp;
                else return CallNextHookEx(hookHandle, nCode, wParam, lParam);

                // 触发模式：命中触发键且是按下，则播放绑定的宏
                if (!IsRecording && ListenerActive
                    && (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    && data.scanCode == TriggerScanCode
                    && TriggerMacroName != null)
                {
                    Macro target = null;
                    foreach (Macro m in Macros)
                    {
                        if (m.Name == TriggerMacroName) { target = m; break; }
                    }
                    if (target != null)
                    {
                        ThreadPool.QueueUserWorkItem(delegate { Play(target); });
                        return (IntPtr)1; // 吞掉触发键，避免传给前台程序
                    }
                }

                lock (sync)
                {
                    long elapsed = stepWatch.ElapsedMilliseconds;
                    if (elapsed > 0 && elapsed < 60000)
                    {
                        recording.Steps.Add(new MacroStep { Kind = MacroStepKind.Delay, DelayMs = (int)elapsed });
                    }
                    stepWatch.Restart();
                    recording.Steps.Add(new MacroStep { Kind = kind, ScanCode = (ushort)data.scanCode });
                }
            }
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        /// <summary>播放一个宏（阻塞到完成）。</summary>
        public static void Play(Macro macro)
        {
            if (macro == null || macro.Steps.Count == 0) return;
            lock (sync)
            {
                if (IsPlaying) return;
                IsPlaying = true;
            }
            try
            {
                foreach (MacroStep step in macro.Steps)
                {
                    if (step.Kind == MacroStepKind.Delay)
                    {
                        Thread.Sleep(Math.Max(0, step.DelayMs));
                        continue;
                    }
                    SendKey(step.ScanCode, step.Kind == MacroStepKind.KeyDown);
                    Thread.Sleep(8);
                }
            }
            finally
            {
                lock (sync) { IsPlaying = false; }
            }
        }

        private static void SendKey(ushort scanCode, bool down)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].ki.wVk = 0;
            inputs[0].ki.wScan = scanCode;
            inputs[0].ki.dwFlags = KEYEVENTF_SCANCODE | (down ? 0u : KEYEVENTF_KEYUP);
            inputs[0].ki.time = 0;
            inputs[0].ki.dwExtraInfo = new IntPtr(0x52505443); // 'RPTC' 标记，用于识别自身注入
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        // ---- 持久化（简单文本，避免引入 JSON 依赖）----
        public static void Load()
        {
            Macros = new List<Macro>();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    if (key == null) return;
                    string[] names = key.GetValueNames();
                    foreach (string name in names)
                    {
                        string raw = key.GetValue(name) as string;
                        if (string.IsNullOrEmpty(raw)) continue;
                        Macro m = new Macro(name);
                        string[] parts = raw.Split(';');
                        foreach (string p in parts)
                        {
                            if (string.IsNullOrEmpty(p)) continue;
                            string[] f = p.Split(',');
                            if (f.Length < 3) continue;
                            MacroStep s = new MacroStep();
                            s.Kind = (MacroStepKind)int.Parse(f[0]);
                            s.ScanCode = ushort.Parse(f[1]);
                            s.DelayMs = int.Parse(f[2]);
                            m.Steps.Add(s);
                        }
                        Macros.Add(m);
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    if (key == null) return;
                    foreach (string name in key.GetValueNames())
                    {
                        key.DeleteValue(name, false);
                    }
                    foreach (Macro m in Macros)
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (MacroStep s in m.Steps)
                        {
                            if (sb.Length > 0) sb.Append(';');
                            sb.Append((int)s.Kind).Append(',').Append(s.ScanCode).Append(',').Append(s.DelayMs);
                        }
                        key.SetValue(m.Name, sb.ToString());
                    }
                }
            }
            catch { }
        }

        public static void Delete(string name)
        {
            for (int i = Macros.Count - 1; i >= 0; i--)
            {
                if (Macros[i].Name == name) Macros.RemoveAt(i);
            }
            Save();
        }
    }
}
