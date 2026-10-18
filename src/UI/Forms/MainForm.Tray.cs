// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️
// 1. 目标平台与编译规范：严格维持 C# 5.0 语法标准与 .NET Framework 4.0 兼容性。严禁引入任何第三方包或依赖。
// 2. 编码规范：所有文件严格保存为 UTF-8 带签名（BOM）格式，防止中文字符乱码。
// 3. 架构规范：本文件属于 MainForm 的分部类 (partial class) 之一。修改时请保持职责清晰，任何新增方法或逻辑请同步维护 ARCHITECTURE.md。
// 模块描述: MainForm 任务栏托盘生命周期、右键上下文菜单、任务栏重启自愈与 GDI+ 矢量图标绘制
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RazerBatteryTray
{
    public partial class MainForm : Form
    {
        private void ConfigureSubMenuDropDown(ToolStripDropDown dropDown)
        {
            if (dropDown == null) return;
            dropDown.Renderer = contextMenu != null ? contextMenu.Renderer : new ModernDarkMenuRenderer();
            if (dropDown is ToolStripDropDownMenu)
            {
                ToolStripDropDownMenu menu = (ToolStripDropDownMenu)dropDown;
                menu.ShowImageMargin = false;
                menu.ShowCheckMargin = true;
            }
            dropDown.ItemClicked += (s, e) => {
                lastClickedTrayItem = e.ClickedItem;
            };
            dropDown.Closing += (s, e) => {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                {
                    if (ShouldKeepTrayMenuOpen(lastClickedTrayItem))
                    {
                        e.Cancel = true;
                    }
                }
            };
        }

        private void InitializeTray()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.Renderer = new ModernDarkMenuRenderer();
            contextMenu.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            contextMenu.ShowImageMargin = false;
            contextMenu.ShowCheckMargin = true;
            contextMenu.Padding = new Padding(3, 4, 3, 4);

            contextMenu.ItemClicked += (s, e) => {
                lastClickedTrayItem = e.ClickedItem;
            };
            contextMenu.Closing += (s, e) => {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                {
                    if (ShouldKeepTrayMenuOpen(lastClickedTrayItem))
                    {
                        e.Cancel = true;
                    }
                }
            };

            statusMenuItem = new ToolStripMenuItem("正在检测设备...");
            statusMenuItem.Tag = "Header";
            statusMenuItem.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            statusMenuItem.Click += (s, e) => ShowWindow();
            contextMenu.Items.Add(statusMenuItem);

            deviceMenuSep = new ToolStripSeparator();
            contextMenu.Items.Add(deviceMenuSep);

            showItem = new ToolStripMenuItem("打开控制面板", null, (s, e) => ShowWindow());
        asusItem = new ToolStripMenuItem("ROG 鼠标设置", null, (s, e) => {
            try { new AsusMouseForm().ShowDialog(); } catch (Exception ex) { MessageBox.Show(ex.Message); }
        });
            contextMenu.Items.Add(showItem);
                        contextMenu.Items.Add(asusItem);

            refreshItem = new ToolStripMenuItem("立即刷新电量", null, (s, e) => RefreshBatteryStatus(true));
            contextMenu.Items.Add(refreshItem);

            // Submenu: DPI 调节
            dpiMenu = new ToolStripMenuItem("调节 DPI 档位");
            ConfigureSubMenuDropDown(dpiMenu.DropDown);
            for (int i = 0; i < dpiStageValues.Length; i++)
            {
                int val = dpiStageValues[i];
                var sub = new ToolStripMenuItem(val + " DPI", null, (s, e) => {
                    SetDpiFromUI(val);
                    foreach (ToolStripItem it in dpiMenu.DropDownItems)
                    {
                        var tmi = it as ToolStripMenuItem;
                        if (tmi != null && tmi.Tag is int)
                        {
                            tmi.Checked = ((int)tmi.Tag == val);
                        }
                    }
                });
                sub.Tag = val;
                dpiMenu.DropDownItems.Add(sub);
            }
            contextMenu.Items.Add(dpiMenu);

            // Submenu: 回报率调节
            rateMenu = new ToolStripMenuItem("调节回报率");
            ConfigureSubMenuDropDown(rateMenu.DropDown);
            for (int i = 0; i < pollingRateValues.Length; i++)
            {
                int val = pollingRateValues[i];
                var sub = new ToolStripMenuItem(val + " Hz", null, (s, e) => {
                    SetPollingRateFromUI(val);
                    foreach (ToolStripItem it in rateMenu.DropDownItems)
                    {
                        var tmi = it as ToolStripMenuItem;
                        if (tmi != null && tmi.Tag is int)
                        {
                            tmi.Checked = ((int)tmi.Tag == val);
                        }
                    }
                });
                sub.Tag = val;
                rateMenu.DropDownItems.Add(sub);
            }
            contextMenu.Items.Add(rateMenu);

            // Submenu: 全屏游戏自动回报率
            autoGameRateMenu = new ToolStripMenuItem("全屏游戏自动回报率");
            ConfigureSubMenuDropDown(autoGameRateMenu.DropDown);

            autoGameRateToggleItem = new ToolStripMenuItem("启用全屏自动切换", null, (s, e) => {
                SetAutoGameRateEnabled(!autoGameRateEnabled);
                autoGameRateToggleItem.Checked = autoGameRateEnabled;
            });
            autoGameRateToggleItem.Checked = autoGameRateEnabled;
            autoGameRateMenu.DropDownItems.Add(autoGameRateToggleItem);
            autoGameRateMenu.DropDownItems.Add(new ToolStripSeparator());

            var gameRateSub = new ToolStripMenuItem("全屏电竞回报率");
            ConfigureSubMenuDropDown(gameRateSub.DropDown);
            for (int i = 0; i < pollingRateValues.Length; i++)
            {
                int val = pollingRateValues[i];
                var sub = new ToolStripMenuItem(val + " Hz", null, (s, e) => {
                    SetGamePollingRateFromUI(val);
                    foreach (ToolStripItem it in gameRateSub.DropDownItems)
                    {
                        var tmi = it as ToolStripMenuItem;
                        if (tmi != null && tmi.Tag is int)
                        {
                            tmi.Checked = ((int)tmi.Tag == val);
                        }
                    }
                });
                sub.Tag = val;
                gameRateSub.DropDownItems.Add(sub);
            }
            autoGameRateMenu.DropDownItems.Add(gameRateSub);

            var notifySub = new ToolStripMenuItem("切换提醒方式");
            ConfigureSubMenuDropDown(notifySub.DropDown);
            var miOsd = new ToolStripMenuItem("游戏 OSD 浮窗", null, (s, e) => {
                gameRateNotifyOsd = !gameRateNotifyOsd;
                if (chkNotifyOsd != null) chkNotifyOsd.Checked = gameRateNotifyOsd;
                SaveConfig();
                ((ToolStripMenuItem)s).Checked = gameRateNotifyOsd;
            });
            miOsd.Checked = gameRateNotifyOsd;
            notifySub.DropDownItems.Add(miOsd);

            var miToast = new ToolStripMenuItem("Windows 系统通知", null, (s, e) => {
                gameRateNotifyToast = !gameRateNotifyToast;
                if (chkNotifyToast != null) chkNotifyToast.Checked = gameRateNotifyToast;
                SaveConfig();
                ((ToolStripMenuItem)s).Checked = gameRateNotifyToast;
            });
            miToast.Checked = gameRateNotifyToast;
            notifySub.DropDownItems.Add(miToast);

            var miSound = new ToolStripMenuItem("柔和提示音", null, (s, e) => {
                gameRateNotifySound = !gameRateNotifySound;
                if (chkNotifySound != null) chkNotifySound.Checked = gameRateNotifySound;
                SaveConfig();
                ((ToolStripMenuItem)s).Checked = gameRateNotifySound;
            });
            miSound.Checked = gameRateNotifySound;
            notifySub.DropDownItems.Add(miSound);

            autoGameRateMenu.DropDownItems.Add(notifySub);
            contextMenu.Items.Add(autoGameRateMenu);

            nuphySleepMenuItem = new ToolStripMenuItem("自动休眠: 已开启");
            nuphySleepMenuItem.Click += (s, e) => {
                var cur = NuphyDeviceHelper.CachedHardwareSettings ?? new NuphyDeviceHelper.NuphyHardwareSettings();
                bool newEn = !cur.SleepEnabled;
                NuphyDeviceHelper.SetAutoSleep(newEn);
                ApplyNuphySettingsToUI();
                UpdateTrayDeviceMenuItems();
            };
            nuphySleepMenuItem.Visible = false;
            contextMenu.Items.Add(nuphySleepMenuItem);

            nuphyWinLockMenuItem = new ToolStripMenuItem("Win键锁定: 已关闭");
            nuphyWinLockMenuItem.Click += (s, e) => {
                var cur = NuphyDeviceHelper.CachedHardwareSettings ?? new NuphyDeviceHelper.NuphyHardwareSettings();
                bool newLock = !cur.WinLock;
                NuphyDeviceHelper.SetWinLock(newLock);
                ApplyNuphySettingsToUI();
                UpdateTrayDeviceMenuItems();
            };
            nuphyWinLockMenuItem.Visible = false;
            contextMenu.Items.Add(nuphyWinLockMenuItem);

            nuphyBacklightMenu = new ToolStripMenuItem("键盘背光调节");
            ConfigureSubMenuDropDown(nuphyBacklightMenu.DropDown);
            int[] brightPresets = new int[] { 0, 25, 50, 75, 100 };
            for (int i = 0; i < brightPresets.Length; i++)
            {
                int bVal = brightPresets[i];
                var sub = new ToolStripMenuItem(bVal == 0 ? "关闭背光 (0%)" : (bVal + "% 亮度"));
                sub.Click += (s, e) => {
                    NuphyDeviceHelper.SetBacklightBrightness(bVal);
                    ApplyNuphySettingsToUI();
                    UpdateTrayDeviceMenuItems();
                };
                sub.Tag = bVal;
                nuphyBacklightMenu.DropDownItems.Add(sub);
            }
            nuphyBacklightMenu.Visible = false;
            contextMenu.Items.Add(nuphyBacklightMenu);

            nuphySidelightMenuItem = new ToolStripMenuItem("前置灯带: 已开启");
            nuphySidelightMenuItem.Click += (s, e) => {
                var cur = NuphyDeviceHelper.CachedHardwareSettings ?? new NuphyDeviceHelper.NuphyHardwareSettings();
                bool newSide = !(cur.SidelightEnabled && cur.SidelightBrightness > 0);
                NuphyDeviceHelper.SetSidelightEnabled(newSide);
                ApplyNuphySettingsToUI();
                UpdateTrayDeviceMenuItems();
            };
            nuphySidelightMenuItem.Visible = false;
            contextMenu.Items.Add(nuphySidelightMenuItem);

            nuphyTraySep = new ToolStripSeparator();
            nuphyTraySep.Visible = false;
            contextMenu.Items.Add(nuphyTraySep);

            var intervalMenu = new ToolStripMenuItem("自动刷新频率");
            ConfigureSubMenuDropDown(intervalMenu.DropDown);
            int30sMenuItem = new ToolStripMenuItem("30 秒", null, (s, e) => {
                SetInterval(30000, true);
                int30sMenuItem.Checked = true;
                int1mMenuItem.Checked = false;
                int5mMenuItem.Checked = false;
            });
            int1mMenuItem = new ToolStripMenuItem("1 分钟", null, (s, e) => {
                SetInterval(60000, true);
                int30sMenuItem.Checked = false;
                int1mMenuItem.Checked = true;
                int5mMenuItem.Checked = false;
            });
            int5mMenuItem = new ToolStripMenuItem("5 分钟", null, (s, e) => {
                SetInterval(300000, true);
                int30sMenuItem.Checked = false;
                int1mMenuItem.Checked = false;
                int5mMenuItem.Checked = true;
            });
            intervalMenu.DropDownItems.AddRange(new ToolStripItem[] { int30sMenuItem, int1mMenuItem, int5mMenuItem });
            contextMenu.Items.Add(intervalMenu);

            var styleMenu = new ToolStripMenuItem("托盘图标样式");
            ConfigureSubMenuDropDown(styleMenu.DropDown);
            styleCapsuleItem = new ToolStripMenuItem("现代胶囊电池", null, (s, e) => {
                SetTrayStyle(0, true);
                styleCapsuleItem.Checked = true;
                styleNumItem.Checked = false;
            });
            styleNumItem = new ToolStripMenuItem("醒目数字能量表", null, (s, e) => {
                SetTrayStyle(1, true);
                styleCapsuleItem.Checked = false;
                styleNumItem.Checked = true;
            });
            styleCapsuleItem.Checked = (trayStyle == 0);
            styleNumItem.Checked = (trayStyle == 1);
            styleMenu.DropDownItems.AddRange(new ToolStripItem[] { styleCapsuleItem, styleNumItem });
            contextMenu.Items.Add(styleMenu);

            osdStyleMenu = new ToolStripMenuItem("DPI 浮窗样式");
            ConfigureSubMenuDropDown(osdStyleMenu.DropDown);
            osdStyleCapsuleItem = new ToolStripMenuItem("居中电竞胶囊", null, (s, e) => {
                SetOsdStyle(0, true);
                osdStyleCapsuleItem.Checked = true;
                osdStyleGaugeItem.Checked = false;
                osdStyleCompactItem.Checked = false;
            });
            osdStyleGaugeItem = new ToolStripMenuItem("右侧阶梯能量计", null, (s, e) => {
                SetOsdStyle(1, true);
                osdStyleCapsuleItem.Checked = false;
                osdStyleGaugeItem.Checked = true;
                osdStyleCompactItem.Checked = false;
            });
            osdStyleCompactItem = new ToolStripMenuItem("顶置微型指示段", null, (s, e) => {
                SetOsdStyle(2, true);
                osdStyleCapsuleItem.Checked = false;
                osdStyleGaugeItem.Checked = false;
                osdStyleCompactItem.Checked = true;
            });
            osdStyleCapsuleItem.Checked = (osdStyle == 0);
            osdStyleGaugeItem.Checked = (osdStyle == 1);
            osdStyleCompactItem.Checked = (osdStyle == 2);
            osdStyleMenu.DropDownItems.AddRange(new ToolStripItem[] { osdStyleCapsuleItem, osdStyleGaugeItem, osdStyleCompactItem });
            contextMenu.Items.Add(osdStyleMenu);

            contextMenu.Items.Add(new ToolStripSeparator());

            dpiOsdMenuItem = new ToolStripMenuItem("DPI 切换屏幕提示 (OSD)", null, (s, e) => {
                SetDpiOsdEnabled(!dpiOsdEnabled, true);
                dpiOsdMenuItem.Checked = dpiOsdEnabled;
            });
            contextMenu.Items.Add(dpiOsdMenuItem);

            lowBatteryAlertMenuItem = new ToolStripMenuItem("低电量气泡通知 (≤20%)", null, (s, e) => {
                SetLowBatteryAlert(!lowBatteryAlertEnabled, true);
                lowBatteryAlertMenuItem.Checked = lowBatteryAlertEnabled;
            });
            contextMenu.Items.Add(lowBatteryAlertMenuItem);

            autoStartMenuItem = new ToolStripMenuItem("开机自动启动", null, (s, e) => {
                SetAutoStart(!IsAutoStartEnabled(), true);
                autoStartMenuItem.Checked = IsAutoStartEnabled();
            });
            contextMenu.Items.Add(autoStartMenuItem);

            autoStartShowUIMenuItem = new ToolStripMenuItem("开机弹出主窗口", null, (s, e) => {
                SetAutoStartShowUI(!autoStartShowMainWindow, true);
                autoStartShowUIMenuItem.Checked = autoStartShowMainWindow;
            });
            contextMenu.Items.Add(new ToolStripSeparator());

            prioMenuItem = new ToolStripMenuItem("设备优先级管理 (拖拽排序)...", null, (s, e) => OpenPriorityManager());
            contextMenu.Items.Add(prioMenuItem);

            addDevMenuItem = new ToolStripMenuItem("手动添加新设备向导...", null, (s, e) => OpenAddDeviceWizard());
            contextMenu.Items.Add(addDevMenuItem);

            createDesktopLnkItem = new ToolStripMenuItem("创建桌面快捷方式", null, (s, e) => {
                bool ok = StartupManager.CreateDesktopShortcut(true);
                if (ok && trayIcon != null)
                {
                    try { trayIcon.ShowBalloonTip(3000, "桌面快捷方式已创建", "已成功在桌面为您生成 FerrisPulse · 灵脉 快捷方式！", ToolTipIcon.Info); } catch { }
                }
            });
            contextMenu.Items.Add(createDesktopLnkItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            aboutMenuItem = new ToolStripMenuItem("关于 FerrisPulse", null, (s, e) => OpenAboutForm());
            contextMenu.Items.Add(aboutMenuItem);

            checkUpdateMenuItem = new ToolStripMenuItem("检查更新", null, (s, e) => OnTrayUpdateMenuItemClicked());
            contextMenu.Items.Add(checkUpdateMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            exitItem = new ToolStripMenuItem("退出程序", null, (s, e) => ExitApp());
            contextMenu.Items.Add(exitItem);

            foreach (ToolStripItem item in contextMenu.Items)
            {
                item.Padding = new Padding(6, 4, 12, 4);
            }

            contextMenu.Opening += (s, e) => UpdateTrayDeviceMenuItems();

            UpdateManager.OnPendingUpdateChanged -= OnPendingUpdateChangedHandler;
            UpdateManager.OnPendingUpdateChanged += OnPendingUpdateChangedHandler;
            UpdateTrayUpdateMenuItem();

            trayIcon = new NotifyIcon();
            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.Text = "FerrisPulse · 灵脉 (正在检测外设...)";
            if (this.Icon != null)
            {
                trayIcon.Icon = this.Icon;
            }
            else
            {
                UpdateTrayIcon(-1, false, false);
            }
            trayIcon.Visible = true;

            trayIcon.Click += (s, e) => {
                var me = e as MouseEventArgs;
                if (me != null && me.Button == MouseButtons.Left)
                {
                    ToggleWindow();
                }
            };
            trayIcon.DoubleClick += (s, e) => ShowWindow();
        }


        /// <summary>
        /// 当 Windows 任务栏重建 (WM_TASKBARCREATED) 或 Explorer 重启时，重构托盘图标，防止托盘图标丢失
        /// </summary>
        public void ReinitializeTrayIcon()
        {
            try
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    trayIcon = null;
                }
                InitializeTray();
                if (lastInfo != null)
                {
                    UpdateTrayIcon(lastInfo.BatteryPercent, lastInfo.IsCharging, lastInfo.IsConnected, lastInfo.IsSleeping, lastInfo.BrandColor);
                }
            }
            catch { }
        }
        private void UpdateTrayDeviceMenuItems()
        {
            if (contextMenu == null) return;

            foreach (var item in dynamicDeviceMenuItems)
            {
                if (contextMenu.Items.Contains(item))
                {
                    contextMenu.Items.Remove(item);
                }
            }
            dynamicDeviceMenuItems.Clear();

            var primary = GetPrimaryDevice();
            var onlineDevices = new List<MouseBatteryInfo>();
            if (activeDevices != null)
            {
                for (int i = 0; i < activeDevices.Count; i++)
                {
                    if (activeDevices[i].IsConnected && !DeviceManager.IsDeviceHidden(activeDevices[i].DeviceId))
                    {
                        onlineDevices.Add(activeDevices[i]);
                    }
                }
            }

            if (onlineDevices.Count == 0)
            {
                if (statusMenuItem != null)
                {
                    statusMenuItem.Visible = true;
                    statusMenuItem.Text = "暂无在线设备";
                    statusMenuItem.ForeColor = Color.FromArgb(145, 155, 175);
                }
            }
            else
            {
                if (statusMenuItem != null)
                {
                    statusMenuItem.Visible = false;
                }

                int insertIdx = contextMenu.Items.IndexOf(deviceMenuSep);
                if (insertIdx < 0) insertIdx = 0;

                for (int i = 0; i < onlineDevices.Count; i++)
                {
                    var dev = onlineDevices[i];
                    string battStr;
                    if (dev.Brand == "NuPhy" || dev.Category == DeviceCategory.Keyboard)
                    {
                        bool isWired = (dev.Transport != null && (dev.Transport.IndexOf("有线", StringComparison.OrdinalIgnoreCase) >= 0 || dev.Transport.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0) && !dev.Transport.Contains("无线")) ||
                                       (dev.DeviceId != null && dev.DeviceId.IndexOf("pid_a011", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (isWired)
                        {
                            battStr = "供电中";
                        }
                        else if (dev.IsCharging || (dev.Brand == "NuPhy" && dev.BatteryPercent >= 100))
                        {
                            battStr = "充电中";
                        }
                        else
                        {
                            battStr = dev.BatteryPercent > 0 ? (dev.BatteryPercent.ToString() + "%") : "--";
                        }
                    }
                    else
                    {
                        if (dev.IsCharging)
                        {
                            battStr = string.Format("{0}% (充电中)", dev.BatteryPercent > 0 ? dev.BatteryPercent : 0);
                        }
                        else if (dev.IsSleeping)
                        {
                            battStr = string.Format("{0}% (休眠)", dev.BatteryPercent > 0 ? dev.BatteryPercent : 0);
                        }
                        else
                        {
                            battStr = dev.BatteryPercent > 0 ? (dev.BatteryPercent.ToString() + "%") : "--";
                        }
                    }

                    string itemText = dev.DisplayName + "\t" + battStr;
                    var mi = new ToolStripMenuItem(itemText);
                    mi.Checked = false;
                    mi.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
                    mi.ForeColor = dev.BrandColor;
                    mi.Padding = new Padding(6, 4, 12, 4);

                    var targetDev = dev;
                    mi.Click += (s, e) => {
                        ShowWindow(targetDev);
                    };

                    contextMenu.Items.Insert(insertIdx + i, mi);
                    dynamicDeviceMenuItems.Add(mi);
                }
            }

            // Context-sensitive controls based on primary tray device
            if (primary != null && primary.IsConnected)
            {
                if (primary.Brand == "NuPhy" || primary.Category == DeviceCategory.Keyboard)
                {
                    // Primary is NuPhy keyboard: Show NuPhy controls, hide Mouse DPI/Rate
                    if (dpiMenu != null) dpiMenu.Visible = false;
                    if (rateMenu != null) rateMenu.Visible = false;

                    var curSettings = NuphyDeviceHelper.CachedHardwareSettings ?? new NuphyDeviceHelper.NuphyHardwareSettings();

                    if (nuphySleepMenuItem != null)
                    {
                        nuphySleepMenuItem.Visible = true;
                        nuphySleepMenuItem.Text = curSettings.SleepEnabled ? "自动休眠: 已开启" : "自动休眠: 已关闭 (常亮)";
                    }
                    if (nuphyWinLockMenuItem != null)
                    {
                        nuphyWinLockMenuItem.Visible = true;
                        nuphyWinLockMenuItem.Text = curSettings.WinLock ? "Win键锁定: 已开启" : "Win键锁定: 已关闭";
                    }
                    if (nuphyBacklightMenu != null)
                    {
                        nuphyBacklightMenu.Visible = true;
                        ConfigureSubMenuDropDown(nuphyBacklightMenu.DropDown);
                        for (int k = 0; k < nuphyBacklightMenu.DropDownItems.Count; k++)
                        {
                            var sub = nuphyBacklightMenu.DropDownItems[k] as ToolStripMenuItem;
                            if (sub != null && sub.Tag is int)
                            {
                                sub.Checked = ((int)sub.Tag == curSettings.BacklightBrightness);
                            }
                        }
                    }
                    if (nuphySidelightMenuItem != null)
                    {
                        nuphySidelightMenuItem.Visible = true;
                        nuphySidelightMenuItem.Text = (curSettings.SidelightEnabled && curSettings.SidelightBrightness > 0) ? "前置灯带: 已开启" : "前置灯带: 已关闭";
                    }
                    if (nuphyTraySep != null) nuphyTraySep.Visible = true;
                }
                else if (primary.Category == DeviceCategory.Mouse)
                {
                    // Primary is Mouse (Razer, Rapoo, etc.): Show DPI/Rate, hide NuPhy controls
                    if (nuphySleepMenuItem != null) nuphySleepMenuItem.Visible = false;
                    if (nuphyWinLockMenuItem != null) nuphyWinLockMenuItem.Visible = false;
                    if (nuphyBacklightMenu != null) nuphyBacklightMenu.Visible = false;
                    if (nuphySidelightMenuItem != null) nuphySidelightMenuItem.Visible = false;
                    if (nuphyTraySep != null) nuphyTraySep.Visible = false;

                    if (dpiMenu != null)
                    {
                        dpiMenu.Visible = true;
                        dpiMenu.DropDownItems.Clear();
                        ConfigureSubMenuDropDown(dpiMenu.DropDown);
                        if (dpiStageValues != null)
                        {
                            for (int i = 0; i < dpiStageValues.Length; i++)
                            {
                                int val = dpiStageValues[i];
                                var sub = new ToolStripMenuItem(val + " DPI", null, (s, e) => {
                                    SetDpiFromUI(val);
                                    foreach (ToolStripItem it in dpiMenu.DropDownItems)
                                    {
                                        var tmi = it as ToolStripMenuItem;
                                        if (tmi != null && tmi.Tag is int)
                                        {
                                            tmi.Checked = ((int)tmi.Tag == val);
                                        }
                                    }
                                });
                                sub.Tag = val;
                                sub.Checked = (primary.Dpi == val);
                                dpiMenu.DropDownItems.Add(sub);
                            }
                        }
                    }

                    if (rateMenu != null)
                    {
                        rateMenu.Visible = true;
                        ConfigureSubMenuDropDown(rateMenu.DropDown);
                        for (int k = 0; k < rateMenu.DropDownItems.Count; k++)
                        {
                            var sub = rateMenu.DropDownItems[k] as ToolStripMenuItem;
                            if (sub != null && sub.Tag is int)
                            {
                                sub.Checked = ((int)sub.Tag == primary.PollingRate);
                            }
                        }
                    }
                }
                else
                {
                    // Other device (Headset, Gamepad)
                    if (dpiMenu != null) dpiMenu.Visible = false;
                    if (rateMenu != null) rateMenu.Visible = false;
                    if (nuphySleepMenuItem != null) nuphySleepMenuItem.Visible = false;
                    if (nuphyWinLockMenuItem != null) nuphyWinLockMenuItem.Visible = false;
                    if (nuphyBacklightMenu != null) nuphyBacklightMenu.Visible = false;
                    if (nuphySidelightMenuItem != null) nuphySidelightMenuItem.Visible = false;
                    if (nuphyTraySep != null) nuphyTraySep.Visible = false;
                }
            }
            else
            {
                // No connected primary device
                if (dpiMenu != null) dpiMenu.Visible = false;
                if (rateMenu != null) rateMenu.Visible = false;
                if (nuphySleepMenuItem != null) nuphySleepMenuItem.Visible = false;
                if (nuphyWinLockMenuItem != null) nuphyWinLockMenuItem.Visible = false;
                if (nuphyBacklightMenu != null) nuphyBacklightMenu.Visible = false;
                if (nuphySidelightMenuItem != null) nuphySidelightMenuItem.Visible = false;
                if (nuphyTraySep != null) nuphyTraySep.Visible = false;
            }
        }


        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSMICON = 49;
        private int GetTrayIconSize()
        {
            try
            {
                int size = GetSystemMetrics(SM_CXSMICON);
                if (size >= 24) return 24;
                if (size >= 20) return 20;
                return 16;
            }
            catch
            {
                return 16;
            }
        }

        private void UpdateTrayIcon(int percent, bool isCharging, bool isConnected, bool isSleeping = false, Color? brandColor = null)
        {
            int iconSize = GetTrayIconSize();
            Color bCol = brandColor ?? ((lastInfo != null) ? lastInfo.BrandColor : Color.FromArgb(0, 230, 118));
            using (Bitmap bmp = DrawTrayBitmap(percent, isCharging, isConnected, trayStyle, iconSize, isSleeping, bCol))
            {
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        trayIcon.Icon = (Icon)tempIcon.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        private static readonly byte[][] Digits3 = new byte[][] {
            new byte[] { 0x7, 0x5, 0x5, 0x5, 0x7 }, // 0
            new byte[] { 0x1, 0x3, 0x1, 0x1, 0x1 }, // 1 (width 2, or 1 in 100)
            new byte[] { 0x7, 0x1, 0x7, 0x4, 0x7 }, // 2
            new byte[] { 0x7, 0x1, 0x7, 0x1, 0x7 }, // 3
            new byte[] { 0x5, 0x5, 0x7, 0x1, 0x1 }, // 4
            new byte[] { 0x7, 0x4, 0x7, 0x1, 0x7 }, // 5
            new byte[] { 0x7, 0x4, 0x7, 0x5, 0x7 }, // 6
            new byte[] { 0x7, 0x1, 0x1, 0x1, 0x1 }, // 7
            new byte[] { 0x7, 0x5, 0x7, 0x5, 0x7 }, // 8
            new byte[] { 0x7, 0x5, 0x7, 0x1, 0x7 }, // 9
        };

        private static readonly byte[][] Digits4x7 = new byte[][] {
            new byte[] { 0x6, 0x9, 0x9, 0x9, 0x9, 0x9, 0x6 }, // 0
            new byte[] { 0x2, 0x6, 0x2, 0x2, 0x2, 0x2, 0x7 }, // 1 (width 3)
            new byte[] { 0x6, 0x9, 0x1, 0x2, 0x4, 0x8, 0xF }, // 2
            new byte[] { 0xE, 0x1, 0x1, 0x6, 0x1, 0x1, 0xE }, // 3
            new byte[] { 0x9, 0x9, 0x9, 0xF, 0x1, 0x1, 0x1 }, // 4
            new byte[] { 0xF, 0x8, 0xE, 0x1, 0x1, 0x9, 0x6 }, // 5
            new byte[] { 0x6, 0x8, 0xE, 0x9, 0x9, 0x9, 0x6 }, // 6
            new byte[] { 0xF, 0x1, 0x2, 0x2, 0x4, 0x4, 0x4 }, // 7
            new byte[] { 0x6, 0x9, 0x9, 0x6, 0x9, 0x9, 0x6 }, // 8
            new byte[] { 0x6, 0x9, 0x9, 0x7, 0x1, 0x1, 0x6 }, // 9
        };

        // 5x10 Ultra-Bold Font (0-9): 10px tall, 2px thick strokes for maximum clarity & presence
        private static readonly ushort[][] BoldDigits5x10 = new ushort[][] {
            new ushort[] { 0x0E, 0x1F, 0x1B, 0x1B, 0x1B, 0x1B, 0x1B, 0x1B, 0x1F, 0x0E }, // 0
            new ushort[] { 0x06, 0x07, 0x07, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03 }, // 1 (width 3, bold column)
            new ushort[] { 0x0E, 0x1F, 0x13, 0x03, 0x06, 0x0C, 0x18, 0x18, 0x1F, 0x1F }, // 2
            new ushort[] { 0x1E, 0x1F, 0x03, 0x03, 0x0E, 0x0E, 0x03, 0x03, 0x1F, 0x1E }, // 3
            new ushort[] { 0x13, 0x13, 0x13, 0x13, 0x1F, 0x1F, 0x03, 0x03, 0x03, 0x03 }, // 4
            new ushort[] { 0x1F, 0x1F, 0x18, 0x1E, 0x1F, 0x03, 0x03, 0x13, 0x1F, 0x0E }, // 5
            new ushort[] { 0x0E, 0x1F, 0x18, 0x1E, 0x1F, 0x1B, 0x1B, 0x1B, 0x1F, 0x0E }, // 6
            new ushort[] { 0x1F, 0x1F, 0x03, 0x03, 0x06, 0x06, 0x0C, 0x0C, 0x18, 0x18 }, // 7
            new ushort[] { 0x0E, 0x1F, 0x1B, 0x1B, 0x0E, 0x0E, 0x1B, 0x1B, 0x1F, 0x0E }, // 8
            new ushort[] { 0x0E, 0x1F, 0x1B, 0x1B, 0x1F, 0x0F, 0x03, 0x03, 0x1F, 0x0E }, // 9
        };

        // Compact 4x10 '0' for 100% display
        private static readonly ushort[] BoldDigit0_4x10 = new ushort[] {
            0x06, 0x0F, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x0F, 0x06
        };

        // Compact 2x10 '1' for 100% display
        private static readonly ushort[] BoldDigit1_2x10 = new ushort[] {
            0x02, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03
        };


        private static Bitmap DrawTrayBitmap(int percent, bool isCharging, bool isConnected, int style, int iconSize, bool isSleeping = false, Color? brandColor = null)
        {
            if (iconSize >= 24)
            {
                return DrawTrayBitmap24(percent, isCharging, isConnected, style, isSleeping, brandColor);
            }
            else
            {
                return DrawTrayBitmap16(percent, isCharging, isConnected, style, isSleeping, brandColor);
            }
        }

        private static Bitmap DrawTrayBitmap24(int percent, bool isCharging, bool isConnected, int style, bool isSleeping = false, Color? brandColor = null)
        {
            Bitmap bmp = new Bitmap(24, 24);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (!isConnected)
                {
                    if (style == 2) // 极简纯数字: 居中双破折号
                    {
                        Color disCol = Color.FromArgb(160, 172, 192);
                        int y = 11;
                        for (int x = 6; x <= 9; x++) { bmp.SetPixel(x, y, disCol); bmp.SetPixel(x, y + 1, disCol); }
                        for (int x = 14; x <= 17; x++) { bmp.SetPixel(x, y, disCol); bmp.SetPixel(x, y + 1, disCol); }
                        return bmp;
                    }
                    else if (style == 1) // 醒目数字能量表: 严格水平垂直绝对居中
                    {
                        using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 23, 30)))
                            g.FillRectangle(bg, 0, 0, 24, 24);
                        using (Pen border = new Pen(Color.FromArgb(48, 56, 74), 1f))
                            g.DrawRectangle(border, 0, 0, 23, 23);
                        Color slateDash = Color.FromArgb(170, 180, 198);
                        int dy = 8;
                        for (int x = 7; x <= 10; x++) { bmp.SetPixel(x, dy, slateDash); bmp.SetPixel(x, dy + 1, slateDash); }
                        for (int x = 13; x <= 16; x++) { bmp.SetPixel(x, dy, slateDash); bmp.SetPixel(x, dy + 1, slateDash); }
                        using (SolidBrush barB = new SolidBrush(Color.FromArgb(40, 48, 62)))
                            g.FillRectangle(barB, 3, 17, 18, 4);
                        return bmp;
                    }
                    else // 现代胶囊电池: 四角闭合完全连贯密封，无空隙
                    {
                        Color slateBorder = Color.FromArgb(140, 150, 168);
                        Color slateDash = Color.FromArgb(170, 180, 198);
                        using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 24, 32)))
                            g.FillRectangle(bg, 2, 4, 18, 16);
                        using (Pen p = new Pen(slateBorder, 1f))
                        {
                            g.DrawLine(p, 2, 3, 19, 3);
                            g.DrawLine(p, 2, 20, 19, 20);
                            g.DrawLine(p, 1, 4, 1, 19);
                            g.DrawLine(p, 20, 4, 20, 19);
                        }
                        bmp.SetPixel(1, 3, slateBorder);
                        bmp.SetPixel(1, 20, slateBorder);
                        bmp.SetPixel(20, 3, slateBorder);
                        bmp.SetPixel(20, 20, slateBorder);
                        using (SolidBrush cap = new SolidBrush(slateBorder))
                            g.FillRectangle(cap, 21, 8, 2, 8);
                        for (int x = 6; x <= 8; x++) { bmp.SetPixel(x, 11, slateDash); bmp.SetPixel(x, 12, slateDash); }
                        for (int x = 12; x <= 14; x++) { bmp.SetPixel(x, 11, slateDash); bmp.SetPixel(x, 12, slateDash); }
                        return bmp;
                    }
                }

                Color brandAccent = brandColor.HasValue ? brandColor.Value : Color.FromArgb(0, 230, 118);
                Color accentColor = isSleeping ? Color.FromArgb(255, 183, 77) :
                                    (percent < 0 ? brandAccent :
                                    ((percent > 20 || isCharging) ? brandAccent :
                                    ((percent > 10) ? Color.FromArgb(255, 214, 0) : Color.FromArgb(255, 50, 65))));

                if (style == 2) // 极简纯数字
                {
                    DrawPureNumericTrayBitmap24(bmp, percent, isCharging, accentColor);
                }
                else if (style == 1) // 醒目数字能量表 (Centered digits)
                {
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 23, 30)))
                        g.FillRectangle(bg, 0, 0, 24, 24);
                    using (Pen border = new Pen(Color.FromArgb(48, 56, 74), 1f))
                        g.DrawRectangle(border, 0, 0, 23, 23);

                    Color digitCol = (brandAccent == Color.FromArgb(0, 229, 255)) ? brandAccent : Color.White;
                    if (isCharging)
                        DrawBolt24(bmp, 11, 10, Color.White);
                    else
                        DrawDigits24(bmp, percent, 11, 5, digitCol, false);

                    int barW = (percent < 0) ? 18 : Math.Max(2, (int)(18 * (percent / 100.0)));
                    using (SolidBrush barB = new SolidBrush(accentColor))
                        g.FillRectangle(barB, 3, 17, barW, 4);
                }
                else // 现代胶囊电池 (Default, sealed closed white corners)
                {
                    Color whiteCol = Color.White;

                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 22, 30)))
                        g.FillRectangle(bg, 2, 4, 18, 16);

                    int fillW = (percent < 0) ? 18 : Math.Max(1, (int)(18 * (percent / 100.0)));
                    using (SolidBrush fb = new SolidBrush(accentColor))
                        g.FillRectangle(fb, 2, 4, fillW, 16);

                    if (isCharging)
                        DrawBolt24(bmp, 10, 11, Color.White);
                    else
                        DrawDigits24(bmp, percent, 10, 8, Color.White, true);

                    using (Pen p = new Pen(whiteCol, 1f))
                    {
                        g.DrawLine(p, 2, 3, 19, 3);
                        g.DrawLine(p, 2, 20, 19, 20);
                        g.DrawLine(p, 1, 4, 1, 19);
                        g.DrawLine(p, 20, 4, 20, 19);
                    }
                    bmp.SetPixel(1, 3, whiteCol);
                    bmp.SetPixel(1, 20, whiteCol);
                    bmp.SetPixel(20, 3, whiteCol);
                    bmp.SetPixel(20, 20, whiteCol);

                    using (SolidBrush cap = new SolidBrush(whiteCol))
                        g.FillRectangle(cap, 21, 8, 2, 8);
                }
            }
            return bmp;
        }

        private static Bitmap DrawTrayBitmap16(int percent, bool isCharging, bool isConnected, int style, bool isSleeping = false, Color? brandColor = null)
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (!isConnected)
                {
                    if (style == 2) // 极简纯数字: 居中双破折号
                    {
                        Color disCol = Color.FromArgb(160, 172, 192);
                        int y = 7;
                        for (int x = 4; x <= 6; x++) { bmp.SetPixel(x, y, disCol); bmp.SetPixel(x, y + 1, disCol); }
                        for (int x = 9; x <= 11; x++) { bmp.SetPixel(x, y, disCol); bmp.SetPixel(x, y + 1, disCol); }
                        return bmp;
                    }
                    else if (style == 1) // 醒目数字能量表: 严格水平垂直绝对居中
                    {
                        using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 23, 30)))
                            g.FillRectangle(bg, 0, 0, 16, 16);
                        using (Pen border = new Pen(Color.FromArgb(48, 56, 74), 1f))
                            g.DrawRectangle(border, 0, 0, 15, 15);
                        Color slateDash = Color.FromArgb(170, 180, 198);
                        int dy = 5;
                        for (int x = 4; x <= 6; x++) { bmp.SetPixel(x, dy, slateDash); bmp.SetPixel(x, dy + 1, slateDash); }
                        for (int x = 9; x <= 11; x++) { bmp.SetPixel(x, dy, slateDash); bmp.SetPixel(x, dy + 1, slateDash); }
                        using (SolidBrush barB = new SolidBrush(Color.FromArgb(40, 48, 62)))
                            g.FillRectangle(barB, 2, 12, 12, 3);
                        return bmp;
                    }
                    else // 现代胶囊电池: 四角闭合完全连贯密封，无空隙
                    {
                        Color slateBorder = Color.FromArgb(140, 150, 168);
                        Color slateDash = Color.FromArgb(170, 180, 198);
                        using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 24, 32)))
                            g.FillRectangle(bg, 1, 3, 12, 10);
                        using (Pen p = new Pen(slateBorder, 1f))
                        {
                            g.DrawLine(p, 1, 2, 12, 2);
                            g.DrawLine(p, 1, 13, 12, 13);
                            g.DrawLine(p, 0, 3, 0, 12);
                            g.DrawLine(p, 13, 3, 13, 12);
                        }
                        bmp.SetPixel(0, 2, slateBorder);
                        bmp.SetPixel(0, 13, slateBorder);
                        bmp.SetPixel(13, 2, slateBorder);
                        bmp.SetPixel(13, 13, slateBorder);
                        using (SolidBrush cap = new SolidBrush(slateBorder))
                            g.FillRectangle(cap, 14, 5, 2, 6);
                        for (int x = 4; x <= 5; x++) { bmp.SetPixel(x, 7, slateDash); bmp.SetPixel(x, 8, slateDash); }
                        for (int x = 8; x <= 9; x++) { bmp.SetPixel(x, 7, slateDash); bmp.SetPixel(x, 8, slateDash); }
                        return bmp;
                    }
                }

                Color brandAccent = brandColor.HasValue ? brandColor.Value : Color.FromArgb(0, 230, 118);
                Color accentColor = isSleeping ? Color.FromArgb(255, 183, 77) :
                                    (percent < 0 ? brandAccent :
                                    ((percent > 20 || isCharging) ? brandAccent :
                                    ((percent > 10) ? Color.FromArgb(255, 214, 0) : Color.FromArgb(255, 50, 65))));

                if (style == 2) // 极简纯数字
                {
                    DrawPureNumericTrayBitmap16(bmp, percent, isCharging, accentColor);
                }
                else if (style == 1) // 醒目数字能量表 (Centered digits)
                {
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 23, 30)))
                        g.FillRectangle(bg, 0, 0, 16, 16);
                    using (Pen border = new Pen(Color.FromArgb(48, 56, 74), 1f))
                        g.DrawRectangle(border, 0, 0, 15, 15);

                    Color digitCol = (brandAccent == Color.FromArgb(0, 229, 255)) ? brandAccent : Color.White;
                    if (isCharging)
                    {
                        bmp.SetPixel(14, 1, accentColor);
                        bmp.SetPixel(13, 2, accentColor);
                        bmp.SetPixel(14, 2, accentColor);
                        bmp.SetPixel(13, 3, accentColor);
                        DrawBolt16(bmp, 6, 4, Color.White);
                    }
                    else
                    {
                        DrawDigits16(bmp, percent, 7, 3, digitCol, false);
                    }

                    int barW = (percent < 0) ? 12 : Math.Max(2, (int)(12 * (percent / 100.0)));
                    using (SolidBrush barB = new SolidBrush(accentColor))
                        g.FillRectangle(barB, 2, 12, barW, 3);
                }
                else // 现代胶囊电池 (Default, sealed closed white corners)
                {
                    Color whiteCol = Color.White;

                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 22, 30)))
                        g.FillRectangle(bg, 1, 3, 12, 10);

                    int fillW = (percent < 0) ? 12 : Math.Max(1, (int)(12 * (percent / 100.0)));
                    using (SolidBrush fb = new SolidBrush(accentColor))
                        g.FillRectangle(fb, 1, 3, fillW, 10);

                    if (isCharging)
                        DrawBolt16(bmp, 6, 5, Color.White);
                    else
                        DrawDigits16(bmp, percent, 7, 5, Color.White, true);

                    using (Pen p = new Pen(whiteCol, 1f))
                    {
                        g.DrawLine(p, 1, 2, 12, 2);
                        g.DrawLine(p, 1, 13, 12, 13);
                        g.DrawLine(p, 0, 3, 0, 12);
                        g.DrawLine(p, 13, 3, 13, 12);
                    }
                    bmp.SetPixel(0, 2, whiteCol);
                    bmp.SetPixel(0, 13, whiteCol);
                    bmp.SetPixel(13, 2, whiteCol);
                    bmp.SetPixel(13, 13, whiteCol);

                    using (SolidBrush cap = new SolidBrush(whiteCol))
                        g.FillRectangle(cap, 14, 5, 2, 6);
                }
            }
            return bmp;
        }

        private static void DrawDigits24(Bitmap bmp, int value, int centerX, int startY, Color color, bool smartContrast = false)
        {
            if (value < 0)
            {
                int y = startY + 3;
                for (int x = centerX - 4; x <= centerX - 2; x++)
                {
                    if (x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height)
                    {
                        Color drawCol = color;
                        if (smartContrast)
                        {
                            Color bg = bmp.GetPixel(x, y);
                            int lum = (int)(bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
                            drawCol = (lum > 110) ? Color.FromArgb(10, 24, 15) : Color.White;
                        }
                        bmp.SetPixel(x, y, drawCol);
                    }
                }
                for (int x = centerX + 1; x <= centerX + 3; x++)
                {
                    if (x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height)
                    {
                        Color drawCol = color;
                        if (smartContrast)
                        {
                            Color bg = bmp.GetPixel(x, y);
                            int lum = (int)(bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
                            drawCol = (lum > 110) ? Color.FromArgb(10, 24, 15) : Color.White;
                        }
                        bmp.SetPixel(x, y, drawCol);
                    }
                }
                return;
            }

            string s = value.ToString();
            int totalW = 0;
            int[] widths = new int[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                int w = (d == 1) ? 3 : 4;
                widths[i] = w;
                totalW += w;
            }
            totalW += s.Length - 1;

            int curX = centerX - (totalW / 2);
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                int w = widths[i];
                byte[] rows = Digits4x7[d];
                for (int r = 0; r < 7; r++)
                {
                    byte row = rows[r];
                    for (int c = 0; c < w; c++)
                    {
                        int bit = (w == 3) ? (2 - c) : (3 - c);
                        if ((row & (1 << bit)) != 0)
                        {
                            int px = curX + c;
                            int py = startY + r;
                            if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                            {
                                Color drawCol = color;
                                if (smartContrast)
                                {
                                    Color bg = bmp.GetPixel(px, py);
                                    int lum = (int)(bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
                                    drawCol = (lum > 110) ? Color.FromArgb(10, 24, 15) : Color.White;
                                }
                                bmp.SetPixel(px, py, drawCol);
                            }
                        }
                    }
                }
                curX += w + 1;
            }
        }

        private static void DrawBolt24(Bitmap bmp, int cx, int cy, Color color)
        {
            Point[] pts = new Point[] {
                new Point(cx + 1, cy - 5), new Point(cx - 3, cy), new Point(cx, cy),
                new Point(cx - 2, cy + 5), new Point(cx + 3, cy - 1), new Point(cx, cy - 1)
            };
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPolygon(b, pts);
            }
        }

        private static void DrawPureNumericTrayBitmap24(Bitmap bmp, int percent, bool isCharging, Color color)
        {
            if (percent < 0)
            {
                Color dashCol = color;
                int y = 11;
                for (int x = 6; x <= 9; x++) { bmp.SetPixel(x, y, dashCol); bmp.SetPixel(x, y + 1, dashCol); }
                for (int x = 14; x <= 17; x++) { bmp.SetPixel(x, y, dashCol); bmp.SetPixel(x, y + 1, dashCol); }
                return;
            }

            int val = Math.Max(0, Math.Min(100, percent));
            int fontH = 14;
            int startY = (24 - fontH) / 2;

            string s = val.ToString();
            int totalW = 0;
            int[] widths = new int[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                int w = (d == 1) ? 5 : 7;
                if (val == 100)
                {
                    w = (i == 0) ? 3 : 6;
                }
                widths[i] = w;
                totalW += w;
            }
            totalW += (s.Length - 1) * 2;
            int curX = (24 - totalW) / 2;
            if (isCharging) curX = Math.Max(1, curX - 3);

            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                int w = widths[i];
                ushort[] srcRows = (val == 100) ? ((i == 0) ? BoldDigit1_2x10 : BoldDigit0_4x10) : BoldDigits5x10[d];
                int srcW = (val == 100) ? ((i == 0) ? 2 : 4) : ((d == 1) ? 3 : 5);

                for (int r = 0; r < fontH; r++)
                {
                    int srcR = (r * 10) / fontH;
                    ushort row = srcRows[srcR];
                    for (int c = 0; c < w; c++)
                    {
                        int srcC = (c * srcW) / w;
                        int bit = (srcW - 1) - srcC;
                        if ((row & (1 << bit)) != 0)
                        {
                            int px = curX + c;
                            int py = startY + r;
                            if (px >= 0 && px < 24 && py >= 0 && py < 24)
                                bmp.SetPixel(px, py, color);
                        }
                    }
                }
                curX += w + 2;
            }

            if (isCharging)
            {
                Color boltCol = Color.FromArgb(255, 235, 59);
                Point[] pts = new Point[] {
                    new Point(20, 1), new Point(17, 6), new Point(20, 6),
                    new Point(18, 11), new Point(23, 5), new Point(20, 5)
                };
                using (Graphics g = Graphics.FromImage(bmp))
                using (SolidBrush b = new SolidBrush(boltCol))
                {
                    g.FillPolygon(b, pts);
                }
            }
        }

        private static void DrawDigits16(Bitmap bmp, int value, int centerX, int startY, Color color, bool smartContrast = false)
        {
            if (value < 0)
            {
                int y = startY + 2;
                for (int x = centerX - 3; x <= centerX - 1; x++)
                {
                    if (x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height)
                    {
                        Color drawCol = color;
                        if (smartContrast)
                        {
                            Color bg = bmp.GetPixel(x, y);
                            int lum = (int)(bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
                            drawCol = (lum > 110) ? Color.FromArgb(10, 24, 15) : Color.White;
                        }
                        bmp.SetPixel(x, y, drawCol);
                    }
                }
                for (int x = centerX + 1; x <= centerX + 3; x++)
                {
                    if (x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height)
                    {
                        Color drawCol = color;
                        if (smartContrast)
                        {
                            Color bg = bmp.GetPixel(x, y);
                            int lum = (int)(bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
                            drawCol = (lum > 110) ? Color.FromArgb(10, 24, 15) : Color.White;
                        }
                        bmp.SetPixel(x, y, drawCol);
                    }
                }
                return;
            }

            string s = value.ToString();
            int totalW = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                int w = (d == 1 && s.Length == 3) ? 1 : (d == 1 ? 2 : 3);
                totalW += w;
                if (i < s.Length - 1) totalW += 1;
            }

            int curX = centerX - (totalW / 2);
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                byte[] rows = Digits3[d];
                int w = (d == 1 && s.Length == 3) ? 1 : (d == 1 ? 2 : 3);
                for (int r = 0; r < 5; r++)
                {
                    byte row = rows[r];
                    for (int c = 0; c < w; c++)
                    {
                        int bit = (w == 1) ? 0 : ((w == 2) ? (1 - c) : (2 - c));
                        if ((row & (1 << bit)) != 0)
                        {
                            int px = curX + c;
                            int py = startY + r;
                            if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                            {
                                Color drawCol = color;
                                if (smartContrast)
                                {
                                    Color bg = bmp.GetPixel(px, py);
                                    int lum = (int)(bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
                                    drawCol = (lum > 110) ? Color.FromArgb(10, 24, 15) : Color.White;
                                }
                                bmp.SetPixel(px, py, drawCol);
                            }
                        }
                    }
                }
                curX += w + 1;
            }
        }

        private static void DrawBolt16(Bitmap bmp, int startX, int startY, Color color)
        {
            int[,] bolt = new int[,] {
                {0, 2}, {1, 1}, {2, 0},
                {1, 2}, {2, 2}, {3, 2},
                {0, 3}, {1, 3}, {2, 3},
                {1, 4}, {2, 5}
            };
            for (int i = 0; i < bolt.GetLength(0); i++)
            {
                int px = startX + bolt[i, 0];
                int py = startY + bolt[i, 1];
                if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                    bmp.SetPixel(px, py, color);
            }
        }

        private static void DrawPureNumericTrayBitmap16(Bitmap bmp, int percent, bool isCharging, Color color)
        {
            if (percent < 0)
            {
                Color dashCol = color;
                int y = 7;
                for (int x = 4; x <= 6; x++) { bmp.SetPixel(x, y, dashCol); bmp.SetPixel(x, y + 1, dashCol); }
                for (int x = 9; x <= 11; x++) { bmp.SetPixel(x, y, dashCol); bmp.SetPixel(x, y + 1, dashCol); }
                return;
            }

            int val = Math.Max(0, Math.Min(100, percent));
            int startY = 3;

            if (val == 100)
            {
                int curX = isCharging ? 1 : 2;
                for (int r = 0; r < 10; r++)
                {
                    ushort row = BoldDigit1_2x10[r];
                    if ((row & 2) != 0) bmp.SetPixel(curX, startY + r, color);
                    if ((row & 1) != 0) bmp.SetPixel(curX + 1, startY + r, color);
                }
                curX += 3;
                for (int r = 0; r < 10; r++)
                {
                    ushort row = BoldDigit0_4x10[r];
                    for (int c = 0; c < 4; c++)
                    {
                        if ((row & (1 << (3 - c))) != 0)
                            bmp.SetPixel(curX + c, startY + r, color);
                    }
                }
                curX += 5;
                for (int r = 0; r < 10; r++)
                {
                    ushort row = BoldDigit0_4x10[r];
                    for (int c = 0; c < 4; c++)
                    {
                        if ((row & (1 << (3 - c))) != 0)
                            bmp.SetPixel(curX + c, startY + r, color);
                    }
                }
            }
            else
            {
                string s = val.ToString();
                int totalW = 0;
                int[] widths = new int[s.Length];
                for (int i = 0; i < s.Length; i++)
                {
                    int d = s[i] - '0';
                    int w = (d == 1) ? 3 : 5;
                    widths[i] = w;
                    totalW += w;
                }
                totalW += (s.Length - 1);
                int curX = (16 - totalW) / 2;
                if (isCharging) curX = Math.Max(1, curX - 2);

                for (int i = 0; i < s.Length; i++)
                {
                    int d = s[i] - '0';
                    int w = widths[i];
                    ushort[] rows = BoldDigits5x10[d];
                    for (int r = 0; r < 10; r++)
                    {
                        ushort row = rows[r];
                        for (int c = 0; c < w; c++)
                        {
                            int bit = (w == 3) ? (2 - c) : (4 - c);
                            if ((row & (1 << bit)) != 0)
                            {
                                int px = curX + c;
                                int py = startY + r;
                                if (px >= 0 && px < 16 && py >= 0 && py < 16)
                                    bmp.SetPixel(px, py, color);
                            }
                        }
                    }
                    curX += w + 1;
                }
            }

            if (isCharging)
            {
                Color boltCol = Color.FromArgb(255, 235, 59);
                bmp.SetPixel(14, 0, boltCol);
                bmp.SetPixel(13, 1, boltCol); bmp.SetPixel(14, 1, boltCol);
                bmp.SetPixel(12, 2, boltCol); bmp.SetPixel(13, 2, boltCol); bmp.SetPixel(14, 2, boltCol); bmp.SetPixel(15, 2, boltCol);
                bmp.SetPixel(13, 3, boltCol); bmp.SetPixel(14, 3, boltCol);
                bmp.SetPixel(13, 4, boltCol);
                bmp.SetPixel(12, 5, boltCol);
            }
        }

        private void OnPendingUpdateChangedHandler()
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)UpdateTrayUpdateMenuItem);
                return;
            }
            UpdateTrayUpdateMenuItem();
        }

        private void UpdateTrayUpdateMenuItem()
        {
            if (this.IsDisposed || checkUpdateMenuItem == null) return;
            var pending = UpdateManager.PendingUpdate;
            if (pending != null && !string.IsNullOrEmpty(pending.LatestVersion) && UpdateManager.IsNewerVersion(pending.LatestVersion, UpdateManager.CurrentVersion))
            {
                string cleanVer = pending.LatestVersion.Trim().TrimStart('v', 'V');
                int sp = cleanVer.IndexOf(' ');
                if (sp > 0) cleanVer = cleanVer.Substring(0, sp);
                checkUpdateMenuItem.Text = "查看新版本 (v" + cleanVer + ")";
            }
            else
            {
                checkUpdateMenuItem.Text = "检查更新";
            }
        }

        private void OnTrayUpdateMenuItemClicked()
        {
            var pending = UpdateManager.PendingUpdate;
            if (pending != null && UpdateManager.IsNewerVersion(pending.LatestVersion, UpdateManager.CurrentVersion))
            {
                // 0ms 秒开：直接从内存缓存打开更新弹窗，弹窗内 SWR 后台静默并发复检
                UpdateNoticeForm.ShowUpdateDialog(this, pending, UpdateManager.CurrentVersion);
            }
            else
            {
                TriggerManualUpdateCheck();
            }
        }

        private void ExitApp()
        {
            UpdateManager.OnPendingUpdateChanged -= OnPendingUpdateChangedHandler;
            StopDpiMonitor();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            if (osdForm != null && !osdForm.IsDisposed)
            {
                osdForm.Dispose();
            }
            Application.Exit();
        }

    }
}
