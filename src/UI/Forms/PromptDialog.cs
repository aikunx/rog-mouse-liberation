// ============================================================================
// 模块名称: PromptDialog.cs
// 所属区域: UI/Forms (业务对话框层)
// 核心职责:
//   现代化通用文本单行输入对话框（重命名设备、配置输入等），具备键盘 Enter/Escape 响应、自动选中输入内容与确定/取消交互。
// 依赖关系: Controls.ModernButton, Controls.ModernInputBox, Controls.RoundedCard
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 任何 AI Agent 在修改本文件逻辑、属性或绘图逻辑时，【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法（❌ 严禁 $"..." 插值，必须使用 string.Format 或 + 拼接）。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RazerBatteryTray
{
    public class PromptDialog : Form
    {
        private TextBox txtInput;
        private ModernButton btnOk;
        private ModernButton btnCancel;
        private Label lblPrompt;

        public string InputText
        {
            get { return txtInput != null ? txtInput.Text : ""; }
            set { if (txtInput != null) txtInput.Text = value; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ModernThemeHelper.ApplyModernWin11Theme(this.Handle);
        }

        public PromptDialog(string title, string prompt, string defaultValue)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(20, 24, 33);
            this.ForeColor = Color.White;
            this.Size = new Size(380, 190);

            lblPrompt = new Label();
            lblPrompt.Text = prompt;
            lblPrompt.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            lblPrompt.ForeColor = Color.FromArgb(210, 215, 225);
            lblPrompt.Location = new Point(20, 16);
            lblPrompt.Size = new Size(325, 42);
            lblPrompt.BackColor = Color.Transparent;
            this.Controls.Add(lblPrompt);

            txtInput = new TextBox();
            txtInput.Text = defaultValue ?? "";
            txtInput.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);
            txtInput.BackColor = Color.FromArgb(30, 36, 48);
            txtInput.ForeColor = Color.White;
            txtInput.BorderStyle = BorderStyle.FixedSingle;
            txtInput.Location = new Point(22, 64);
            txtInput.Size = new Size(320, 26);
            this.Controls.Add(txtInput);

            btnCancel = new ModernButton();
            btnCancel.Text = "取消";
            btnCancel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            btnCancel.NormalColor = Color.FromArgb(40, 46, 58);
            btnCancel.HoverColor = Color.FromArgb(50, 58, 72);
            btnCancel.PressedColor = Color.FromArgb(32, 38, 48);
            btnCancel.ForeColor = Color.FromArgb(200, 205, 215);
            btnCancel.CornerRadius = 6;
            btnCancel.Location = new Point(176, 106);
            btnCancel.Size = new Size(76, 30);
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);

            btnOk = new ModernButton();
            btnOk.Text = "确定";
            btnOk.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnOk.NormalColor = Color.FromArgb(0, 200, 100);
            btnOk.HoverColor = Color.FromArgb(0, 230, 118);
            btnOk.PressedColor = Color.FromArgb(0, 160, 80);
            btnOk.ForeColor = Color.FromArgb(15, 20, 25);
            btnOk.CornerRadius = 6;
            btnOk.Location = new Point(262, 106);
            btnOk.Size = new Size(80, 30);
            btnOk.Cursor = Cursors.Hand;
            btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            this.Controls.Add(btnOk);

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };
        }

        public static bool Show(IWin32Window owner, string title, string prompt, string defaultValue, out string result)
        {
            using (var dlg = new PromptDialog(title, prompt, defaultValue))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    result = dlg.InputText.Trim();
                    return true;
                }
            }
            result = null;
            return false;
        }
    }
}