// ============================================================================
// 模块名称: DpiManagementDialog.cs
// 所属区域: UI/Forms (业务对话框层)
// 核心职责:
//   鼠标 DPI 档位增删查改管理面板，展示当前传感器型号规格能力、各档位 DPI 列表、实时档位激活切换、输入合法性校验与硬件档位配置同步。
// 依赖关系: Controls.DpiStageRowControl, Controls.DpiActionButton, Controls.RoundedCard, Hardware.RazerDeviceHelper
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 任何 AI Agent 在修改本文件逻辑、属性或绘图逻辑时，【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法（❌ 严禁 $"..." 插值，必须使用 string.Format 或 + 拼接）。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RazerBatteryTray
{
    public class DpiManagementDialog : Form
    {
        public int[] Stages { get; private set; }
        public int ActiveStage { get; private set; }

        private string deviceName;
        private RazerDeviceHelper.RazerDpiCapability capability;
        private int minDpi;
        private int maxDpi;
        private string sensorDesc;
        private float dpiScale = 1.0f;
        private List<int> stageValues = new List<int>();
        private int currentActiveStage = 1;

        private Panel pnlStages;
        private DpiActionButton btnAddStage;
        private Label lblError;
        private DpiActionButton btnSave;
        private DpiActionButton btnCancel;
        private List<DpiStageRowControl> rowControls = new List<DpiStageRowControl>();

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ModernThemeHelper.ApplyModernWin11Theme(this.Handle);
        }

        public DpiManagementDialog(string devName, int[] initialStages, int currentStage, RazerDeviceHelper.RazerDpiCapability cap)
            : this(devName, initialStages, currentStage, cap, 0f)
        {
        }

        public DpiManagementDialog(string devName, int[] initialStages, int currentStage, RazerDeviceHelper.RazerDpiCapability cap, float scale)
        {
            this.deviceName = devName ?? "外设设备";
            this.capability = cap ?? new RazerDeviceHelper.RazerDpiCapability { MaxDpi = 35000, MinDpi = 100, Step = 1, Description = "Focus Pro 35K Gen-2 · 1-DPI 精准步进" };
            this.minDpi = (this.capability.MinDpi > 0) ? this.capability.MinDpi : 100;
            this.maxDpi = (this.capability.MaxDpi > 0) ? this.capability.MaxDpi : 35000;
            this.sensorDesc = !string.IsNullOrEmpty(this.capability.Description) ? this.capability.Description : "Focus Pro 光学传感器";
            this.currentActiveStage = currentStage > 0 ? currentStage : 1;
            this.ActiveStage = this.currentActiveStage;

            if (scale <= 0f)
            {
                try
                {
                    using (Graphics g = this.CreateGraphics())
                    {
                        scale = g.DpiX / 96.0f;
                    }
                }
                catch { scale = 1.0f; }
            }
            if (scale < 1.0f) scale = 1.0f;
            this.dpiScale = scale;

            if (initialStages != null && initialStages.Length > 0)
            {
                for (int i = 0; i < initialStages.Length && i < 5; i++)
                {
                    stageValues.Add(initialStages[i]);
                }
            }
            if (stageValues.Count == 0)
            {
                stageValues.AddRange(new int[] { 400, 800, 1600, 3200, 6400 });
            }

            InitializeUI();
        }

        private int S(int val) { return (int)Math.Round(val * dpiScale); }

        private void InitializeUI()
        {
            this.Text = "FerrisPulse · DPI 档位与板载设置";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(13, 16, 23);
            this.ForeColor = Color.White;
            try { ModernThemeHelper.ApplyModernWin11Theme(this.Handle); } catch { }

            int clientW = S(360);
            int clientH = S(446);
            this.ClientSize = new Size(clientW, clientH);

            int pad = S(16);

            // 1. Title Header
            Label lblTitle = new Label();
            lblTitle.Text = deviceName + " · DPI 档位配置";
            lblTitle.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(245, 248, 252);
            lblTitle.Location = new Point(pad, S(14));
            lblTitle.Size = new Size(clientW - pad * 2, S(22));
            lblTitle.BackColor = Color.Transparent;
            this.Controls.Add(lblTitle);

            // 2. Subtitle / Range Note
            Label lblSub = new Label();
            lblSub.Text = string.Format("支持 1~5 档 · 范围 {0} ~ {1} DPI (点击数值直接输入)", minDpi, maxDpi.ToString("N0"));
            lblSub.Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular);
            lblSub.ForeColor = Color.FromArgb(120, 135, 155);
            lblSub.Location = new Point(pad, S(38));
            lblSub.Size = new Size(clientW - pad * 2, S(16));
            lblSub.BackColor = Color.Transparent;
            this.Controls.Add(lblSub);

            // 3. Stages List Container
            pnlStages = new Panel();
            pnlStages.Location = new Point(pad, S(60));
            pnlStages.Size = new Size(clientW - pad * 2, S(240));
            pnlStages.BackColor = Color.Transparent;
            pnlStages.AutoScroll = true;
            this.Controls.Add(pnlStages);

            // 4. Add Stage Button
            btnAddStage = new DpiActionButton(dpiScale);
            btnAddStage.Location = new Point(pad, S(306));
            btnAddStage.Size = new Size(clientW - pad * 2, S(30));
            btnAddStage.CornerRadius = S(6);
            btnAddStage.Font = new Font("Microsoft YaHei UI", 8.2F, FontStyle.Regular);
            btnAddStage.Cursor = Cursors.Hand;
            btnAddStage.Click += (s, e) => AddStage();
            this.Controls.Add(btnAddStage);

            // 5. Error Label
            lblError = new Label();
            lblError.Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular);
            lblError.ForeColor = Color.FromArgb(255, 90, 95);
            lblError.Location = new Point(pad, S(340));
            lblError.Size = new Size(clientW - pad * 2, S(16));
            lblError.BackColor = Color.Transparent;
            lblError.Text = "";
            this.Controls.Add(lblError);

            // 6. Hint Note
            Label lblHint = new Label();
            lblHint.Font = new Font("Microsoft YaHei UI", 7.8F, FontStyle.Regular);
            lblHint.ForeColor = Color.FromArgb(95, 110, 130);
            lblHint.Location = new Point(pad, S(360));
            lblHint.Size = new Size(clientW - pad * 2, S(20));
            lblHint.BackColor = Color.Transparent;
            lblHint.Text = "提示: 点击「设为激活」生效当前档；保存即刻写入板载。";
            this.Controls.Add(lblHint);

            // 7. Bottom Action Buttons: tighter gap S(10), compact buttons
            int btnCancelW = S(88);
            int btnSaveW = S(148);
            int gap = S(10);
            int totalBtnW = btnCancelW + gap + btnSaveW;
            int startX = (clientW - totalBtnW) / 2;
            int btnY = S(392);

            btnCancel = new DpiActionButton(dpiScale);
            btnCancel.Text = "取消";
            btnCancel.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular);
            btnCancel.NormalColor = Color.FromArgb(24, 30, 40);
            btnCancel.HoverColor = Color.FromArgb(34, 42, 56);
            btnCancel.PressedColor = Color.FromArgb(18, 22, 30);
            btnCancel.BorderColor = Color.FromArgb(42, 52, 70);
            btnCancel.ForeColor = Color.FromArgb(200, 210, 225);
            btnCancel.CornerRadius = S(6);
            btnCancel.Location = new Point(startX, btnY);
            btnCancel.Size = new Size(btnCancelW, S(34));
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);

            btnSave = new DpiActionButton(dpiScale);
            btnSave.Text = "保存并写入板载";
            btnSave.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Bold);
            btnSave.NormalColor = Color.FromArgb(0, 200, 90);
            btnSave.HoverColor = Color.FromArgb(0, 230, 115);
            btnSave.PressedColor = Color.FromArgb(0, 160, 70);
            btnSave.BorderColor = Color.Transparent;
            btnSave.ForeColor = Color.FromArgb(10, 24, 16);
            btnSave.CornerRadius = S(6);
            btnSave.Location = new Point(startX + btnCancelW + gap, btnY);
            btnSave.Size = new Size(btnSaveW, S(34));
            btnSave.Cursor = Cursors.Hand;
            btnSave.Click += (s, e) => SaveAndClose();
            this.Controls.Add(btnSave);

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { this.DialogResult = DialogResult.Cancel; this.Close(); }
                else if (e.KeyCode == Keys.Enter) { SaveAndClose(); }
            };

            this.Shown += (s, e) =>
            {
                try { this.ActiveControl = btnSave; } catch { }
            };

            RebuildStageRows();
        }

        private void RebuildStageRows()
        {
            pnlStages.SuspendLayout();
            pnlStages.Controls.Clear();
            rowControls.Clear();

            int rowH = S(43);
            int rowGap = S(5);
            int cardW = pnlStages.ClientSize.Width - S(2);

            for (int i = 0; i < stageValues.Count; i++)
            {
                int stageNum = i + 1;
                int y = i * (rowH + rowGap);
                bool isActive = (stageNum == currentActiveStage);

                DpiStageRowControl row = new DpiStageRowControl(stageNum, stageValues[i], isActive, (stageValues.Count > 1), minDpi, maxDpi, dpiScale);
                row.Location = new Point(0, y);
                row.Size = new Size(cardW, rowH);

                int captureStageNum = stageNum;
                int captureIndex = i;

                row.OnActivateClicked += () =>
                {
                    SaveCurrentInputs();
                    currentActiveStage = captureStageNum;
                    this.ActiveStage = currentActiveStage;
                    RebuildStageRows();
                };

                row.OnDeleteClicked += () =>
                {
                    RemoveStage(captureIndex);
                };

                rowControls.Add(row);
                pnlStages.Controls.Add(row);
            }

            pnlStages.ResumeLayout();

            if (stageValues.Count < 5)
            {
                btnAddStage.Enabled = true;
                btnAddStage.Text = string.Format("+ 添加新档位  ({0}/5 档)", stageValues.Count);
                btnAddStage.NormalColor = Color.FromArgb(22, 29, 42);
                btnAddStage.HoverColor = Color.FromArgb(30, 40, 58);
                btnAddStage.PressedColor = Color.FromArgb(18, 24, 35);
                btnAddStage.BorderColor = Color.FromArgb(44, 58, 82);
                btnAddStage.ForeColor = Color.FromArgb(56, 189, 248);
            }
            else
            {
                btnAddStage.Enabled = false;
                btnAddStage.Text = "已达到板载存储上限 (5 档)";
                btnAddStage.NormalColor = Color.FromArgb(18, 22, 30);
                btnAddStage.HoverColor = Color.FromArgb(18, 22, 30);
                btnAddStage.PressedColor = Color.FromArgb(18, 22, 30);
                btnAddStage.BorderColor = Color.FromArgb(32, 40, 54);
                btnAddStage.ForeColor = Color.FromArgb(90, 105, 125);
            }
            btnAddStage.Invalidate();
        }

        private void SaveCurrentInputs()
        {
            for (int i = 0; i < rowControls.Count && i < stageValues.Count; i++)
            {
                stageValues[i] = rowControls[i].DpiValue;
            }
        }

        private void AddStage()
        {
            if (stageValues.Count >= 5) return;
            SaveCurrentInputs();
            int lastVal = stageValues.Count > 0 ? stageValues[stageValues.Count - 1] : 800;
            int nextVal = Math.Min(maxDpi, lastVal + 400);
            stageValues.Add(nextVal);
            RebuildStageRows();
        }

        private void RemoveStage(int index)
        {
            if (stageValues.Count <= 1) return;
            SaveCurrentInputs();
            if (index >= 0 && index < stageValues.Count)
            {
                int removedStageNum = index + 1;
                stageValues.RemoveAt(index);
                if (currentActiveStage == removedStageNum)
                {
                    currentActiveStage = Math.Min(currentActiveStage, stageValues.Count);
                }
                else if (currentActiveStage > removedStageNum)
                {
                    currentActiveStage--;
                }
                this.ActiveStage = currentActiveStage;
                RebuildStageRows();
            }
        }

        public void SaveAndClose()
        {
            SaveCurrentInputs();
            lblError.Text = "";

            if (stageValues.Count == 0)
            {
                lblError.Text = "至少需要保留 1 档 DPI。";
                return;
            }

            for (int i = 0; i < stageValues.Count; i++)
            {
                int v = stageValues[i];
                if (v < minDpi || v > maxDpi)
                {
                    lblError.Text = string.Format("第 {0} 档 DPI ({1}) 超出有效范围 ({2} ~ {3})",
                        i + 1, v, minDpi, maxDpi.ToString("N0"));
                    return;
                }
            }

            if (currentActiveStage < 1 || currentActiveStage > stageValues.Count)
            {
                currentActiveStage = 1;
            }

            this.ActiveStage = currentActiveStage;
            this.Stages = stageValues.ToArray();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;
            if (d <= 0) d = 1;

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}