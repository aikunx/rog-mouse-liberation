// ============================================================================
// 模块名称: AsusMouseForm.cs
// 所属区域: UI (界面渲染与用户交互层)
// 核心职责:
//   ROG / ASUS 鼠标设置面板，视觉风格与 FerrisPulse 主界面一致
//   （深色底 13,16,23 / ModernButton / RoundedCard / 微软雅黑 UI）。
//   三个页签：设备状态、按键绑定、宏。
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 修改逻辑时【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RazerBatteryTray
{
    public class AsusMouseForm : Form
    {
        private static readonly Color BgColor = Color.FromArgb(13, 16, 23);
        private static readonly Color CardColor = Color.FromArgb(20, 25, 35);
        private static readonly Color TextColor = Color.FromArgb(245, 248, 252);
        private static readonly Color SubColor = Color.FromArgb(120, 135, 155);
        private static readonly Color HintColor = Color.FromArgb(95, 110, 130);
        private static readonly Color AccentColor = Color.FromArgb(56, 189, 248);
        private static readonly Color GreenColor = Color.FromArgb(0, 200, 90);
        private static readonly Color ErrorColor = Color.FromArgb(255, 90, 95);

        private Label lblTitle;
        private Label lblSub;
        private Label lblHintBottom;
        private ModernButton tabDevice;
        private ModernButton tabButtons;
        private ModernButton tabMacros;
        private Panel pageDevice;
        private Panel pageButtons;
        private Panel pageMacros;
        private Panel pageAdvanced;
        private ModernButton tabAdvanced;
        private TextBox[] txtDpiStage = new TextBox[4];
        private Label lblDpiCurrent;
        private CheckBox chkAngleSnap;
        private NumericUpDown numAngleTuning;
        private ComboBox cbDebounce;
        private ComboBox cbLiftOff;
        private ComboBox cbLightDir;
        private ComboBox cbLightSpeed;
        private CheckBox chkLightRandom;
        private NumericUpDown numRapidL;
        private NumericUpDown numRapidR;
        private NumericUpDown numRapidS;
        private ComboBox cbTrigger;
        private ModernButton btnListener;

        private Label lblStatus;
        private Label lblProfile;
        private Label lblDpi;
        private Label lblPower;
        private ComboBox cbProfile;
        private ComboBox cbPolling;
        private ComboBox cbLightMode;
        private ComboBox cbPowerOff;
        private NumericUpDown numBright;
        private TextBox txtRgb;

        private ListBox lstButtons;
        private ComboBox cbAction;

        private ListBox lstMacros;
        private TextBox txtMacroName;
        private ModernButton btnRecord;

        private MouseBatteryInfo device;
        private ushort[] bindings = new ushort[8];
        private int activePage = 0;

        private static readonly string[] SlotNames = { "左键", "右键", "滚轮键", "侧后退", "侧前进", "DPI 键", "滚上", "滚下" };

        private static readonly Dictionary<string, ushort> ActionMap = new Dictionary<string, ushort>
        {
            { "左键", 0x01F0 }, { "右键", 0x01F1 }, { "中键", 0x01F2 },
            { "后退", 0x01E4 }, { "前进", 0x01E5 }, { "DPI", 0x01E6 },
            { "滚上", 0x01E8 }, { "滚下", 0x01E9 },
            { "音量+", 0x00F6 }, { "音量-", 0x00F5 }, { "静音", 0x00F4 },
            { "播放/暂停", 0x00F0 }, { "下一曲", 0x00F2 }, { "上一曲", 0x00F3 },
            { "禁用", 0x0000 },
            { "A", 0x0004 }, { "B", 0x0005 }, { "C", 0x0006 }, { "D", 0x0007 },
            { "E", 0x0008 }, { "F", 0x0009 }, { "G", 0x000A }, { "H", 0x000B },
            { "Enter", 0x0028 }, { "Esc", 0x0029 }, { "Space", 0x002C }, { "Tab", 0x002B },
            { "F1", 0x003A }, { "F2", 0x003B }, { "F3", 0x003C }, { "F4", 0x003D },
            { "Left Ctrl", 0x00E0 }, { "Left Shift", 0x00E1 }, { "Left Alt", 0x00E2 }
        };

        public AsusMouseForm()
        {
            Text = "FerrisPulse · ROG 鼠标";
            ClientSize = new Size(620, 590);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = BgColor;
            ForeColor = TextColor;
            Font = new Font("Microsoft YaHei UI", 9F);

            BuildShell();
            BuildDevicePage();
            BuildButtonPage();
            BuildMacroPage();
            BuildAdvancedPage();
            ShowPage(0);

            MacroEngine.Load();
            RefreshMacroList();
            RefreshDevice();

            try { ModernThemeHelper.ApplyModernWin11Theme(this.Handle); } catch { }
        }

        // ---------------- shell ----------------
        private void BuildShell()
        {
            lblTitle = new Label();
            lblTitle.Text = "ROG 鼠标控制";
            lblTitle.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            lblTitle.ForeColor = TextColor;
            lblTitle.BackColor = Color.Transparent;
            lblTitle.Location = new Point(24, 18);
            lblTitle.AutoSize = true;
            Controls.Add(lblTitle);

            lblSub = new Label();
            lblSub.Text = "ASUS HID 直连 · 无需 Armoury Crate";
            lblSub.Font = new Font("Microsoft YaHei UI", 8F);
            lblSub.ForeColor = SubColor;
            lblSub.BackColor = Color.Transparent;
            lblSub.Location = new Point(24, 46);
            lblSub.AutoSize = true;
            Controls.Add(lblSub);

            tabDevice = MakeTab("设备", 24, 76);
            tabButtons = MakeTab("按键", 84, 76);
            tabMacros = MakeTab("宏", 144, 76);
            tabAdvanced = MakeTab("高级", 204, 76);
            tabDevice.Click += delegate { ShowPage(0); };
            tabButtons.Click += delegate { ShowPage(1); };
            tabMacros.Click += delegate { ShowPage(2); };
            tabAdvanced.Click += delegate { ShowPage(3); };
            Controls.Add(tabDevice);
            Controls.Add(tabButtons);
            Controls.Add(tabMacros);
            Controls.Add(tabAdvanced);

            lblHintBottom = new Label();
            lblHintBottom.ForeColor = HintColor;
            lblHintBottom.BackColor = Color.Transparent;
            lblHintBottom.Font = new Font("Microsoft YaHei UI", 7.8F);
            lblHintBottom.Location = new Point(24, 556);
            lblHintBottom.Size = new Size(570, 20);
            lblHintBottom.Text = "协议由 FerrisPulse 独立实现；宏在主机侧执行，设备不存储宏。";
            Controls.Add(lblHintBottom);
        }

        private ModernButton MakeTab(string text, int x, int y)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(54, 30);
            b.Font = new Font("Microsoft YaHei UI", 8.8F);
            b.CornerRadius = 8;
            b.ForeColor = SubColor;
            b.NormalColor = CardColor;
            b.HoverColor = Color.FromArgb(30, 38, 52);
            b.PressedColor = Color.FromArgb(24, 30, 42);
            b.BorderColor = Color.FromArgb(42, 52, 70);
            return b;
        }

        private RoundedCard MakeCard(int x, int y, int w, int h)
        {
            RoundedCard c = new RoundedCard();
            c.Location = new Point(x, y);
            c.Size = new Size(w, h);
            c.BackColor = CardColor;
            c.CornerRadius = 12;
            c.BorderColor = Color.FromArgb(38, 46, 62);
            return c;
        }

        private Label MakeLabel(Control parent, string text, int x, int y, Color color, float size, bool bold)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.AutoSize = true;
            l.BackColor = Color.Transparent;
            l.ForeColor = color;
            l.Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            parent.Controls.Add(l);
            return l;
        }

        private ComboBox MakeCombo(Control parent, int x, int y, int w)
        {
            ComboBox c = new ComboBox();
            c.Location = new Point(x, y);
            c.Width = w;
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Color.FromArgb(28, 35, 48);
            c.ForeColor = TextColor;
            parent.Controls.Add(c);
            return c;
        }

        private ModernButton MakeActionButton(Control parent, string text, int x, int y, int w, Color normal, Color hover, Color border, Color fg)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 30);
            b.Font = new Font("Microsoft YaHei UI", 8.6F);
            b.CornerRadius = 8;
            b.NormalColor = normal;
            b.HoverColor = hover;
            b.PressedColor = normal;
            b.BorderColor = border;
            b.ForeColor = fg;
            parent.Controls.Add(b);
            return b;
        }

        private void ShowPage(int index)
        {
            activePage = index;
            pageDevice.Visible = index == 0;
            pageButtons.Visible = index == 1;
            pageMacros.Visible = index == 2;
            pageAdvanced.Visible = index == 3;
            StyleTab(tabDevice, index == 0);
            StyleTab(tabButtons, index == 1);
            StyleTab(tabMacros, index == 2);
            StyleTab(tabAdvanced, index == 3);
        }

        private void StyleTab(ModernButton b, bool active)
        {
            if (active)
            {
                b.ForeColor = Color.FromArgb(10, 24, 16);
                b.NormalColor = GreenColor;
                b.HoverColor = Color.FromArgb(0, 220, 105);
                b.BorderColor = GreenColor;
            }
            else
            {
                b.ForeColor = SubColor;
                b.NormalColor = CardColor;
                b.HoverColor = Color.FromArgb(30, 38, 52);
                b.BorderColor = Color.FromArgb(42, 52, 70);
            }
        }

        // ---------------- device page ----------------
        private void BuildDevicePage()
        {
            pageDevice = new Panel();
            pageDevice.Location = new Point(24, 118);
            pageDevice.Size = new Size(572, 430);
            pageDevice.BackColor = Color.Transparent;
            Controls.Add(pageDevice);

            RoundedCard cardStatus = MakeCard(0, 0, 572, 118);
            pageDevice.Controls.Add(cardStatus);

            MakeLabel(cardStatus, "设备状态", 16, 12, SubColor, 8F, false);
            lblStatus = MakeLabel(cardStatus, "检测中…", 16, 34, TextColor, 10F, true);
            lblProfile = MakeLabel(cardStatus, "", 16, 62, SubColor, 8.2F, false);
            lblDpi = MakeLabel(cardStatus, "", 16, 84, SubColor, 8.2F, false);
            lblPower = MakeLabel(cardStatus, "", 300, 62, SubColor, 8.2F, false);

            RoundedCard cardPerf = MakeCard(0, 130, 572, 130);
            pageDevice.Controls.Add(cardPerf);
            MakeLabel(cardPerf, "性能", 16, 12, SubColor, 8F, false);
            MakeLabel(cardPerf, "Profile", 16, 40, TextColor, 8.4F, false);
            cbProfile = MakeCombo(cardPerf, 90, 37, 90);
            for (int i = 1; i <= 5; i++) cbProfile.Items.Add(i.ToString());
            cbProfile.SelectedIndex = 0;
            cbProfile.SelectedIndexChanged += delegate { ApplyProfile(); };

            MakeLabel(cardPerf, "轮询率", 210, 40, TextColor, 8.4F, false);
            cbPolling = MakeCombo(cardPerf, 280, 37, 110);
            cbPolling.Items.AddRange(new object[] { "125", "250", "500", "1000", "2000", "4000", "8000" });
            cbPolling.SelectedIndex = 3;
            cbPolling.SelectedIndexChanged += delegate { ApplyPolling(); };

            MakeLabel(cardPerf, "自动省电", 16, 82, TextColor, 8.4F, false);
            cbPowerOff = MakeCombo(cardPerf, 90, 79, 110);
            cbPowerOff.Items.AddRange(new object[] { "1 分钟", "2 分钟", "3 分钟", "5 分钟", "10 分钟", "永不" });
            cbPowerOff.SelectedIndex = 0;
            cbPowerOff.SelectedIndexChanged += delegate { ApplyPower(); };

            ModernButton btnProfiles = MakeActionButton(cardPerf, "查看板载", 380, 78, 84,
                Color.FromArgb(22, 29, 42), Color.FromArgb(30, 40, 58), Color.FromArgb(44, 58, 82), AccentColor);
            btnProfiles.Click += delegate { ShowProfiles(); };

            ModernButton btnRefresh = MakeActionButton(cardPerf, "刷新", 470, 78, 80,
                Color.FromArgb(22, 29, 42), Color.FromArgb(30, 40, 58), Color.FromArgb(44, 58, 82), AccentColor);
            btnRefresh.Click += delegate { RefreshDevice(); };

            RoundedCard cardLight = MakeCard(0, 272, 572, 150);
            pageDevice.Controls.Add(cardLight);
            MakeLabel(cardLight, "灯效", 16, 12, SubColor, 8F, false);

            MakeLabel(cardLight, "模式", 16, 42, TextColor, 8.4F, false);
            cbLightMode = MakeCombo(cardLight, 90, 39, 150);
            cbLightMode.Items.AddRange(new object[] { "静态", "呼吸", "循环", "反应", "电量", "关闭" });
            cbLightMode.SelectedIndex = 0;

            MakeLabel(cardLight, "亮度", 260, 42, TextColor, 8.4F, false);
            numBright = new NumericUpDown();
            numBright.Location = new Point(310, 39);
            numBright.Width = 70;
            numBright.Minimum = 0;
            numBright.Maximum = 100;
            numBright.Value = 25;
            numBright.BackColor = Color.FromArgb(28, 35, 48);
            numBright.ForeColor = TextColor;
            numBright.BorderStyle = BorderStyle.FixedSingle;
            cardLight.Controls.Add(numBright);

            MakeLabel(cardLight, "颜色", 16, 82, TextColor, 8.4F, false);
            txtRgb = new TextBox();
            txtRgb.Location = new Point(90, 79);
            txtRgb.Width = 120;
            txtRgb.Text = "255,0,0";
            txtRgb.BackColor = Color.FromArgb(28, 35, 48);
            txtRgb.ForeColor = TextColor;
            txtRgb.BorderStyle = BorderStyle.FixedSingle;
            cardLight.Controls.Add(txtRgb);

            MakeLabel(cardLight, "方向", 16, 118, TextColor, 8.4F, false);
            cbLightDir = MakeCombo(cardLight, 90, 115, 90);
            cbLightDir.Items.AddRange(new object[] { "顺时针", "逆时针" });
            cbLightDir.SelectedIndex = 0;

            MakeLabel(cardLight, "速度", 200, 118, TextColor, 8.4F, false);
            cbLightSpeed = MakeCombo(cardLight, 250, 115, 90);
            cbLightSpeed.Items.AddRange(new object[] { "慢", "中", "快" });
            cbLightSpeed.SelectedIndex = 1;

            chkLightRandom = new CheckBox();
            chkLightRandom.Text = "随机色";
            chkLightRandom.Location = new Point(360, 116);
            chkLightRandom.AutoSize = true;
            chkLightRandom.ForeColor = TextColor;
            chkLightRandom.BackColor = Color.Transparent;
            chkLightRandom.FlatStyle = FlatStyle.Flat;
            cardLight.Controls.Add(chkLightRandom);

            ModernButton btnLight = MakeActionButton(cardLight, "应用灯效", 260, 76, 110,
                Color.FromArgb(22, 29, 42), Color.FromArgb(30, 40, 58), Color.FromArgb(44, 58, 82), AccentColor);
            btnLight.Click += delegate { ApplyLighting(); };
        }

        // ---------------- buttons page ----------------
        private void BuildButtonPage()
        {
            pageButtons = new Panel();
            pageButtons.Location = new Point(24, 118);
            pageButtons.Size = new Size(572, 430);
            pageButtons.BackColor = Color.Transparent;
            pageButtons.Visible = false;
            Controls.Add(pageButtons);

            RoundedCard card = MakeCard(0, 0, 572, 430);
            pageButtons.Controls.Add(card);
            MakeLabel(card, "按键重映射", 16, 12, SubColor, 8F, false);

            lstButtons = new ListBox();
            lstButtons.Location = new Point(16, 40);
            lstButtons.Size = new Size(540, 240);
            lstButtons.BackColor = Color.FromArgb(28, 35, 48);
            lstButtons.ForeColor = TextColor;
            lstButtons.BorderStyle = BorderStyle.FixedSingle;
            lstButtons.Font = new Font("Microsoft YaHei UI", 9F);
            lstButtons.SelectedIndexChanged += delegate { SyncSelectedButton(); };
            card.Controls.Add(lstButtons);

            MakeLabel(card, "改为", 16, 296, TextColor, 8.6F, false);
            cbAction = MakeCombo(card, 70, 293, 200);
            foreach (string k in ActionMap.Keys) cbAction.Items.Add(k);
            cbAction.SelectedIndex = 0;

            ModernButton btnApply = MakeActionButton(card, "应用到选中", 290, 290, 120,
                Color.FromArgb(0, 170, 80), Color.FromArgb(0, 210, 100), Color.FromArgb(0, 200, 90), Color.FromArgb(10, 24, 16));
            btnApply.Click += delegate { ApplyButton(); };

            MakeLabel(card, "RapidFire 间隔 (ms)", 16, 330, SubColor, 8F, false);
            MakeLabel(card, "左", 16, 358, TextColor, 8.4F, false);
            numRapidL = MakeSpin(card, 44, 355, 60, 0, 1000, 50);
            MakeLabel(card, "右", 118, 358, TextColor, 8.4F, false);
            numRapidR = MakeSpin(card, 146, 355, 60, 0, 1000, 0);
            MakeLabel(card, "滚轮", 220, 358, TextColor, 8.4F, false);
            numRapidS = MakeSpin(card, 268, 355, 60, 0, 1000, 0);

            ModernButton btnRapid = MakeActionButton(card, "应用 RapidFire", 350, 353, 130,
                Color.FromArgb(22, 29, 42), Color.FromArgb(30, 40, 58), Color.FromArgb(44, 58, 82), AccentColor);
            btnRapid.Click += delegate { ApplyRapidFire(); };

            MakeLabel(card, "设备只存动作码；宏与手势属主机侧功能。", 16, 392, HintColor, 7.8F, false);
        }

        // ---------------- macro page ----------------
        private void BuildMacroPage()
        {
            pageMacros = new Panel();
            pageMacros.Location = new Point(24, 118);
            pageMacros.Size = new Size(572, 430);
            pageMacros.BackColor = Color.Transparent;
            pageMacros.Visible = false;
            Controls.Add(pageMacros);

            RoundedCard card = MakeCard(0, 0, 572, 430);
            pageMacros.Controls.Add(card);
            MakeLabel(card, "宏（主机侧录制与回放）", 16, 12, SubColor, 8F, false);

            lstMacros = new ListBox();
            lstMacros.Location = new Point(16, 40);
            lstMacros.Size = new Size(540, 250);
            lstMacros.BackColor = Color.FromArgb(28, 35, 48);
            lstMacros.ForeColor = TextColor;
            lstMacros.BorderStyle = BorderStyle.FixedSingle;
            lstMacros.Font = new Font("Microsoft YaHei UI", 9F);
            card.Controls.Add(lstMacros);

            MakeLabel(card, "名称", 16, 306, TextColor, 8.6F, false);
            txtMacroName = new TextBox();
            txtMacroName.Location = new Point(70, 303);
            txtMacroName.Width = 170;
            txtMacroName.Text = "macro1";
            txtMacroName.BackColor = Color.FromArgb(28, 35, 48);
            txtMacroName.ForeColor = TextColor;
            txtMacroName.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(txtMacroName);

            btnRecord = MakeActionButton(card, "录制", 254, 300, 88,
                Color.FromArgb(22, 29, 42), Color.FromArgb(30, 40, 58), Color.FromArgb(44, 58, 82), AccentColor);
            btnRecord.Click += delegate { ToggleRecord(); };

            ModernButton btnPlay = MakeActionButton(card, "播放", 350, 300, 88,
                Color.FromArgb(0, 170, 80), Color.FromArgb(0, 210, 100), Color.FromArgb(0, 200, 90), Color.FromArgb(10, 24, 16));
            btnPlay.Click += delegate { PlayMacro(); };

            ModernButton btnDel = MakeActionButton(card, "删除", 446, 300, 88,
                Color.FromArgb(60, 24, 28), Color.FromArgb(80, 30, 36), Color.FromArgb(120, 50, 58), ErrorColor);
            btnDel.Click += delegate { DeleteMacro(); };

            MakeLabel(card, "触发键", 16, 352, TextColor, 8.6F, false);
            cbTrigger = MakeCombo(card, 76, 349, 120);
            for (int k = 13; k <= 24; k++) cbTrigger.Items.Add("F" + k);
            cbTrigger.SelectedIndex = 11; // F24
            btnListener = MakeActionButton(card, "启用触发", 210, 346, 110,
                Color.FromArgb(22, 29, 42), Color.FromArgb(30, 40, 58), Color.FromArgb(44, 58, 82), AccentColor);
            btnListener.Click += delegate { ToggleListener(); };

            MakeLabel(card, "把鼠标按键绑到 F13–F24 后即可触发宏（设备端只支持单键，主机端负责执行）。",
                16, 388, HintColor, 7.8F, false);
        }


        // ---------------- advanced page ----------------
        private void BuildAdvancedPage()
        {
            pageAdvanced = new Panel();
            pageAdvanced.Location = new Point(24, 118);
            pageAdvanced.Size = new Size(572, 430);
            pageAdvanced.BackColor = Color.Transparent;
            pageAdvanced.Visible = false;
            Controls.Add(pageAdvanced);

            RoundedCard cardDpi = MakeCard(0, 0, 572, 120);
            pageAdvanced.Controls.Add(cardDpi);
            MakeLabel(cardDpi, "DPI 档位（50 为步进，100–36000）", 16, 12, SubColor, 8F, false);
            for (int i = 0; i < 4; i++)
            {
                MakeLabel(cardDpi, "档 " + (i + 1), 16 + i * 128, 40, TextColor, 8.4F, false);
                TextBox tb = new TextBox();
                tb.Location = new Point(52 + i * 128, 37);
                tb.Width = 72;
                tb.BackColor = Color.FromArgb(28, 35, 48);
                tb.ForeColor = TextColor;
                tb.BorderStyle = BorderStyle.FixedSingle;
                tb.Text = "800";
                cardDpi.Controls.Add(tb);
                txtDpiStage[i] = tb;
            }
            MakeLabel(cardDpi, "当前使用", 280, 76, TextColor, 8.4F, false);
            lblDpiCurrent = MakeLabel(cardDpi, "第 1 档", 344, 76, SubColor, 8.4F, false);
            MakeLabel(cardDpi, "（切换档位请按鼠标上的 DPI 键）", 280, 96, HintColor, 7.5F, false);

            ModernButton btnDpi = MakeActionButton(cardDpi, "应用全部 DPI 档", 16, 76, 150,
                Color.FromArgb(0, 170, 80), Color.FromArgb(0, 210, 100), Color.FromArgb(0, 200, 90), Color.FromArgb(10, 24, 16));
            btnDpi.Click += delegate { ApplyAllDpi(); };

            RoundedCard cardAdv = MakeCard(0, 132, 572, 150);
            pageAdvanced.Controls.Add(cardAdv);
            MakeLabel(cardAdv, "传感器与按键", 16, 12, SubColor, 8F, false);

            chkAngleSnap = new CheckBox();
            chkAngleSnap.Text = "角度吸附";
            chkAngleSnap.Location = new Point(16, 40);
            chkAngleSnap.AutoSize = true;
            chkAngleSnap.ForeColor = TextColor;
            chkAngleSnap.BackColor = Color.Transparent;
            chkAngleSnap.FlatStyle = FlatStyle.Flat;
            chkAngleSnap.CheckedChanged += delegate { AsusDeviceHelper.SetAngleSnapping(chkAngleSnap.Checked); };
            cardAdv.Controls.Add(chkAngleSnap);

            MakeLabel(cardAdv, "角度微调", 160, 42, TextColor, 8.4F, false);
            numAngleTuning = new NumericUpDown();
            numAngleTuning.Location = new Point(230, 39);
            numAngleTuning.Width = 70;
            numAngleTuning.Minimum = 0;
            numAngleTuning.Maximum = 1000;
            numAngleTuning.Value = 0;
            numAngleTuning.BackColor = Color.FromArgb(28, 35, 48);
            numAngleTuning.ForeColor = TextColor;
            numAngleTuning.BorderStyle = BorderStyle.FixedSingle;
            numAngleTuning.ValueChanged += delegate { AsusDeviceHelper.SetAngleTuning((int)numAngleTuning.Value); };
            cardAdv.Controls.Add(numAngleTuning);

            MakeLabel(cardAdv, "抬起距离", 330, 42, TextColor, 8.4F, false);
            cbLiftOff = MakeCombo(cardAdv, 400, 39, 100);
            cbLiftOff.Items.AddRange(new object[] { "低", "高" });
            cbLiftOff.SelectedIndex = 0;
            cbLiftOff.SelectedIndexChanged += delegate { AsusDeviceHelper.SetLiftOffDistance((byte)cbLiftOff.SelectedIndex); };

            MakeLabel(cardAdv, "按键去抖", 16, 86, TextColor, 8.4F, false);
            cbDebounce = MakeCombo(cardAdv, 90, 83, 120);
            cbDebounce.Items.AddRange(new object[] { "关闭", "8 ms", "12 ms", "16 ms", "20 ms", "24 ms", "28 ms", "32 ms" });
            cbDebounce.SelectedIndex = 0;
            cbDebounce.SelectedIndexChanged += delegate { AsusDeviceHelper.SetDebounce((byte)cbDebounce.SelectedIndex); };

            RoundedCard cardNote = MakeCard(0, 294, 572, 130);
            pageAdvanced.Controls.Add(cardNote);
            MakeLabel(cardNote, "说明", 16, 12, SubColor, 8F, false);
            MakeLabel(cardNote, "以上参数直接写入设备固件，即时生效且断电保留。", 16, 40, HintColor, 8F, false);
            MakeLabel(cardNote, "DPI 档位需为 50 的整数倍；角度微调步进 5。", 16, 64, HintColor, 8F, false);
            MakeLabel(cardNote, "修改后建议在「设备」页点刷新确认状态。", 16, 88, HintColor, 8F, false);
        }

        private void ApplyAllDpi()
        {
            int[] vals = new int[4];
            for (int i = 0; i < 4; i++)
            {
                int v;
                if (!int.TryParse(txtDpiStage[i].Text.Trim(), out v)) v = 800;
                vals[i] = v;
            }
            AsusDeviceHelper.SetAllDpiStages(vals, 255, 0, 0);
            RefreshDevice();
            for (int i = 0; i < 4; i++)
            {
                if (device != null)
                {
                    int[] st = AsusDeviceHelper.GetDpiStages();
                    if (st != null) txtDpiStage[i].Text = st[i].ToString();
                }
            }
        }

        private NumericUpDown MakeSpin(Control parent, int x, int y, int w, int min, int max, int val)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(x, y);
            n.Width = w;
            n.Minimum = min;
            n.Maximum = max;
            n.Value = val;
            n.BackColor = Color.FromArgb(28, 35, 48);
            n.ForeColor = TextColor;
            n.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(n);
            return n;
        }

        private static readonly ushort[] FKeyScanCodes = new ushort[]
        { 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F }; // F13..F24

        private void ToggleListener()
        {
            if (MacroEngine.ListenerActive)
            {
                MacroEngine.StopListener();
                btnListener.Text = "启用触发";
                btnListener.ForeColor = AccentColor;
                return;
            }
            int idx = lstMacros.SelectedIndex;
            if (idx < 0 || idx >= MacroEngine.Macros.Count) return;
            ushort scan = FKeyScanCodes[cbTrigger.SelectedIndex];
            if (MacroEngine.StartListener(scan, MacroEngine.Macros[idx].Name))
            {
                btnListener.Text = "停用触发";
                btnListener.ForeColor = ErrorColor;
            }
        }

        private void ApplyRapidFire()
        {
            AsusDeviceHelper.SetRapidFire((ushort)numRapidL.Value, (ushort)numRapidR.Value, (ushort)numRapidS.Value);
        }

        // ---------------- actions ----------------
        private void ShowProfiles()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 1; i <= 5; i++)
            {
                AsusDeviceHelper.ProfileSnapshot snap = AsusDeviceHelper.ReadProfile(i);
                if (snap == null) { sb.AppendLine("Profile " + i + "：读取失败"); continue; }
                string dpi = snap.Dpi == null ? "-" : string.Join("/", Array.ConvertAll(snap.Dpi, x => x.ToString()));
                sb.AppendLine("Profile " + i
                    + "  DPI " + dpi
                    + "  灯 " + snap.LightMode + "/" + snap.LightBrightness
                    + "  RGB " + snap.R + "," + snap.G + "," + snap.B
                    + "  回报 " + snap.PollingRate + "Hz");
            }
            MessageBox.Show(sb.ToString(), "板载配置（设备内 5 组）", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RefreshDevice()
        {
            device = AsusDeviceHelper.QueryAsusDeviceInfo();
            if (device == null)
            {
                lblStatus.Text = "未检测到 ROG 接收器";
                lblStatus.ForeColor = ErrorColor;
                lblProfile.Text = "";
                lblDpi.Text = "";
                lblPower.Text = "";
                return;
            }
            lblStatus.ForeColor = TextColor;
            lblStatus.Text = device.DeviceName + "   " + device.BatteryPercent + "%" +
                             (device.IsCharging ? "  ⚡充电中" : "") + "   " + device.PollingRate + " Hz";

            int profile = AsusDeviceHelper.GetCurrentProfile();
            lblProfile.Text = "当前 Profile: " + (profile < 0 ? "?" : profile.ToString());
            if (profile >= 1 && profile <= 5) cbProfile.SelectedIndex = profile - 1;

            int curStage = AsusDeviceHelper.GetCurrentDpiStage();
            if (curStage >= 1 && curStage <= 4 && lblDpiCurrent != null)
            {
                lblDpiCurrent.Text = "第 " + curStage + " 档";
            }
            int[] stages = AsusDeviceHelper.GetDpiStages();
            lblDpi.Text = "DPI 档: " + (stages == null ? "不可用" : string.Join(" / ", Array.ConvertAll(stages, x => x.ToString())));

            int[] power = AsusDeviceHelper.GetPowerSettings();
            if (power != null)
            {
                lblPower.Text = "省电档 " + power[0] + " · 低电 " + power[1] + "%";
                if (power[0] >= 0 && power[0] <= 4) cbPowerOff.SelectedIndex = power[0];
                else if (power[0] == 0xFF) cbPowerOff.SelectedIndex = 5;
            }

            bindings = AsusDeviceHelper.GetButtonBindings();
            RefreshButtonList();
        }

        private void RefreshButtonList()
        {
            lstButtons.Items.Clear();
            if (bindings == null) return;
            for (int i = 0; i < bindings.Length && i < SlotNames.Length; i++)
            {
                lstButtons.Items.Add(i + "  " + SlotNames[i].PadRight(6) + "  →  " + AsusDeviceHelper.ButtonActionName(bindings[i]));
            }
        }

        private void SyncSelectedButton()
        {
            int idx = lstButtons.SelectedIndex;
            if (idx < 0 || bindings == null || idx >= bindings.Length) return;
            string name = AsusDeviceHelper.ButtonActionName(bindings[idx]);
            foreach (string key in ActionMap.Keys)
            {
                if (ActionMap[key] == bindings[idx]) { cbAction.SelectedItem = key; break; }
            }
        }

        private void ApplyProfile()
        {
            if (cbProfile.SelectedIndex < 0) return;
            AsusDeviceHelper.SwitchProfile(cbProfile.SelectedIndex + 1);
            RefreshDevice();
        }

        private void ApplyPolling()
        {
            if (cbPolling.SelectedItem == null) return;
            int hz = int.Parse(cbPolling.SelectedItem.ToString());
            AsusDeviceHelper.SetPollingRate(hz);
            RefreshDevice();
        }

        private void ApplyLighting()
        {
            byte modeCode;
            switch (cbLightMode.SelectedIndex)
            {
                case 1: modeCode = 1; break;
                case 2: modeCode = 2; break;
                case 3: modeCode = 4; break;
                case 4: modeCode = 6; break;
                case 5: modeCode = 0xF0; break;
                default: modeCode = 0; break;
            }
            byte r = 255, g = 0, b = 0;
            string[] parts = txtRgb.Text.Split(',');
            if (parts.Length == 3)
            {
                byte.TryParse(parts[0].Trim(), out r);
                byte.TryParse(parts[1].Trim(), out g);
                byte.TryParse(parts[2].Trim(), out b);
            }
            byte dir = (byte)cbLightDir.SelectedIndex;
            byte speed = (byte)(cbLightSpeed.SelectedIndex == 0 ? 0x9 : (cbLightSpeed.SelectedIndex == 1 ? 0x7 : 0x5));
            bool random = chkLightRandom.Checked;
            AsusDeviceHelper.SetLightingFull(modeCode, (byte)numBright.Value, r, g, b, dir, random, speed, 0);
        }

        private void ApplyPower()
        {
            int idx = cbPowerOff.SelectedIndex;
            byte setting = idx == 5 ? (byte)0xFF : (byte)idx;
            AsusDeviceHelper.SetPowerSettings(setting, 10);
            RefreshDevice();
        }

        private void ApplyButton()
        {
            int slot = lstButtons.SelectedIndex;
            if (slot < 0) return;
            string name = cbAction.SelectedItem as string;
            if (name == null || !ActionMap.ContainsKey(name)) return;
            AsusDeviceHelper.SetButtonBinding(slot, ActionMap[name]);
            RefreshDevice();
        }

        private void ToggleRecord()
        {
            if (!MacroEngine.IsRecording)
            {
                string name = string.IsNullOrEmpty(txtMacroName.Text) ? "macro" + (MacroEngine.Macros.Count + 1) : txtMacroName.Text;
                if (MacroEngine.StartRecording(name))
                {
                    btnRecord.Text = "停止";
                    btnRecord.ForeColor = ErrorColor;
                }
            }
            else
            {
                MacroEngine.StopRecording();
                btnRecord.Text = "录制";
                btnRecord.ForeColor = AccentColor;
                RefreshMacroList();
            }
        }

        private void RefreshMacroList()
        {
            lstMacros.Items.Clear();
            foreach (Macro m in MacroEngine.Macros)
            {
                lstMacros.Items.Add(m.Name + "   (" + m.Steps.Count + " 步)");
            }
        }

        private void PlayMacro()
        {
            int idx = lstMacros.SelectedIndex;
            if (idx < 0 || idx >= MacroEngine.Macros.Count) return;
            MacroEngine.Play(MacroEngine.Macros[idx]);
        }

        private void DeleteMacro()
        {
            int idx = lstMacros.SelectedIndex;
            if (idx < 0 || idx >= MacroEngine.Macros.Count) return;
            MacroEngine.Delete(MacroEngine.Macros[idx].Name);
            RefreshMacroList();
        }
    }
}
