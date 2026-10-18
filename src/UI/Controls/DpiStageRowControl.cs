// ============================================================================
// 模块名称: DpiStageRowControl.cs
// 所属区域: UI/Controls (高级功能控件层)
// 核心职责:
//   DPI 档位管理列表中的单档数据行控件，包含档位胶囊徽标、原生无边框高亮数值输入框、
//   DPI 对齐单位标签、设为激活/已生效状态按钮以及矢量正方形删除按钮。
// 依赖关系: RoundedCard
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 任何 AI Agent 在修改本文件逻辑、属性或绘图逻辑时，【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法（❌ 严禁 $"..." 插值，必须使用 string.Format 或 + 拼接）。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Windows.Forms;

namespace RazerBatteryTray
{
    public class DpiStageRowControl : Control
    {
        public int StageNumber { get; private set; }
        public bool IsActive { get; private set; }
        public bool CanDelete { get; private set; }
        public float DpiScale { get; private set; }

        public int DpiValue
        {
            get
            {
                int v;
                if (int.TryParse(txtDpi.Text.Trim(), out v)) return v;
                return 800;
            }
        }

        public event Action OnActivateClicked;
        public event Action OnDeleteClicked;

        private TextBox txtDpi;
        private Rectangle rectBadge;
        private Rectangle rectInputBox;
        private Rectangle rectUnit;
        private Rectangle rectActiveBtn;
        private Rectangle rectDeleteBtn;

        private bool isHoverActiveBtn = false;
        private bool isHoverDeleteBtn = false;
        private bool isHoverInputBox = false;
        private bool isHoverRow = false;
        private bool isInputFocused = false;
        private int minDpi;
        private int maxDpi;

        private int S(int val) { return (int)Math.Round(val * DpiScale); }

        public DpiStageRowControl(int stageNum, int dpiVal, bool active, bool canDel, int min, int max, float scale)
        {
            this.StageNumber = stageNum;
            this.IsActive = active;
            this.CanDelete = canDel;
            this.minDpi = min;
            this.maxDpi = max;
            this.DpiScale = scale > 0 ? scale : 1.0f;

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.BackColor = Color.FromArgb(14, 17, 24);

            txtDpi = new TextBox();
            txtDpi.Text = dpiVal.ToString();
            txtDpi.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            txtDpi.BackColor = Color.FromArgb(10, 13, 19);
            txtDpi.ForeColor = Color.FromArgb(245, 248, 255);
            txtDpi.BorderStyle = BorderStyle.None;
            txtDpi.TextAlign = HorizontalAlignment.Center;

            txtDpi.Click += (s, e) => txtDpi.SelectAll();
            txtDpi.GotFocus += (s, e) => { isInputFocused = true; txtDpi.SelectAll(); Invalidate(); };
            txtDpi.LostFocus += (s, e) => { isInputFocused = false; Invalidate(); };

            txtDpi.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            txtDpi.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    Form dlg = this.FindForm();
                    if (dlg != null)
                    {
                        MethodInfo mi = dlg.GetType().GetMethod("SaveAndClose");
                        if (mi != null) mi.Invoke(dlg, null);
                        else { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                    }
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    Form dlg = this.FindForm();
                    if (dlg != null) { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); }
                }
            };
            this.Controls.Add(txtDpi);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutSubElements();
        }

        private void LayoutSubElements()
        {
            int h = Height;
            int elemH = S(28);
            int elemY = (h - elemH) / 2;

            // 1. Stage Badge: compact "第 1 档" -> S(56)
            int badgeW = S(56);
            int x = S(11);
            rectBadge = new Rectangle(x, elemY, badgeW, elemH);
            x = rectBadge.Right + S(8);

            // 2. Input Box: Dedicated number input box -> S(74)
            int inputW = S(74);
            rectInputBox = new Rectangle(x, elemY, inputW, elemH);
            x = rectInputBox.Right + S(8);

            // Position TextBox inside input box (centered horizontally & vertically)
            int tbH = txtDpi.PreferredHeight;
            int tbY = rectInputBox.Y + (elemH - tbH) / 2;
            txtDpi.SetBounds(rectInputBox.X + S(2), tbY, rectInputBox.Width - S(4), tbH);

            // 3. Unit Label: "DPI" in fixed, aligned column across all rows -> S(28)
            int unitW = S(28);
            rectUnit = new Rectangle(x, elemY, unitW, elemH);
            x = rectUnit.Right + S(11);

            // 4. Active Button: S(82) x S(26)
            int activeW = S(82);
            int activeH = S(26);
            int activeY = (h - activeH) / 2;
            rectActiveBtn = new Rectangle(x, activeY, activeW, activeH);
            x = rectActiveBtn.Right + S(11);

            // 5. Delete Button: perfectly square vector cross S(24) x S(24)
            int delW = S(24);
            int delH = S(24);
            int delY = (h - delH) / 2;
            rectDeleteBtn = new Rectangle(x, delY, delW, delH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool oldHoverActive = isHoverActiveBtn;
            bool oldHoverDel = isHoverDeleteBtn;
            bool oldHoverBox = isHoverInputBox;

            isHoverActiveBtn = rectActiveBtn.Contains(e.Location) && !IsActive;
            isHoverDeleteBtn = CanDelete && rectDeleteBtn.Contains(e.Location);
            isHoverInputBox = rectInputBox.Contains(e.Location) || rectUnit.Contains(e.Location);

            if (isHoverActiveBtn || isHoverDeleteBtn) Cursor = Cursors.Hand;
            else if (isHoverInputBox) Cursor = Cursors.IBeam;
            else Cursor = Cursors.Default;

            if (oldHoverActive != isHoverActiveBtn || oldHoverDel != isHoverDeleteBtn || oldHoverBox != isHoverInputBox)
            {
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHoverRow = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHoverRow = false;
            isHoverActiveBtn = false;
            isHoverDeleteBtn = false;
            isHoverInputBox = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (CanDelete && rectDeleteBtn.Contains(e.Location))
                {
                    if (OnDeleteClicked != null) OnDeleteClicked();
                    return;
                }

                if (rectInputBox.Contains(e.Location) || rectUnit.Contains(e.Location))
                {
                    txtDpi.Focus();
                    txtDpi.SelectAll();
                    return;
                }

                if (!IsActive && rectActiveBtn.Contains(e.Location))
                {
                    if (OnActivateClicked != null) OnActivateClicked();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 1. Draw Row Card Background
            Color cardBg = IsActive ? Color.FromArgb(16, 36, 26) : (isHoverRow ? Color.FromArgb(22, 28, 38) : Color.FromArgb(17, 21, 30));
            Color cardBorder = IsActive ? Color.FromArgb(0, 230, 118) : (isHoverRow ? Color.FromArgb(44, 55, 76) : Color.FromArgb(28, 35, 50));
            float borderW = IsActive ? 1.5f : 1f;

            Rectangle rectCard = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rectCard, S(6)))
            {
                using (SolidBrush b = new SolidBrush(cardBg))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(cardBorder, borderW))
                {
                    g.DrawPath(p, path);
                }
            }

            // 2. Draw Stage Badge (Pill)
            Color badgeBg = IsActive ? Color.FromArgb(0, 200, 95) : Color.FromArgb(26, 33, 45);
            Color badgeFg = IsActive ? Color.FromArgb(10, 24, 16) : Color.FromArgb(195, 208, 225);
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rectBadge, S(5)))
            {
                using (SolidBrush b = new SolidBrush(badgeBg))
                {
                    g.FillPath(b, path);
                }
                if (!IsActive)
                {
                    using (Pen p = new Pen(Color.FromArgb(40, 50, 70), 1f))
                    {
                        g.DrawPath(p, path);
                    }
                }
            }
            using (Font f = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, string.Format("第 {0} 档", StageNumber), f, rectBadge, badgeFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // 3. Draw DPI Input Box Container
            Color inputBorder = isInputFocused ? Color.FromArgb(0, 230, 118) : (isHoverInputBox ? Color.FromArgb(60, 78, 106) : Color.FromArgb(36, 46, 64));
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rectInputBox, S(5)))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 13, 19)))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(inputBorder, isInputFocused ? 1.5f : 1f))
                {
                    g.DrawPath(p, path);
                }
            }

            // DPI Unit label
            Color unitColor = IsActive ? Color.FromArgb(140, 180, 160) : Color.FromArgb(120, 136, 160);
            using (Font f = new Font("Segoe UI", 8F, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "DPI", f, rectUnit, unitColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // 4. Draw Active Button / Status Badge
            if (IsActive)
            {
                using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rectActiveBtn, S(4)))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(0, 200, 95)))
                    {
                        g.FillPath(b, path);
                    }
                }
                using (Font f = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold))
                {
                    TextRenderer.DrawText(g, "✓ 生效中", f, rectActiveBtn, Color.FromArgb(10, 24, 16),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
            else
            {
                Color btnBg = isHoverActiveBtn ? Color.FromArgb(32, 42, 58) : Color.FromArgb(22, 28, 40);
                Color btnBorder = isHoverActiveBtn ? Color.FromArgb(58, 76, 106) : Color.FromArgb(36, 46, 64);
                Color btnFg = isHoverActiveBtn ? Color.FromArgb(240, 246, 255) : Color.FromArgb(140, 155, 175);

                using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rectActiveBtn, S(4)))
                {
                    using (SolidBrush b = new SolidBrush(btnBg))
                    {
                        g.FillPath(b, path);
                    }
                    using (Pen p = new Pen(btnBorder, 1f))
                    {
                        g.DrawPath(p, path);
                    }
                }
                using (Font f = new Font("Microsoft YaHei UI", 7.8F, FontStyle.Regular))
                {
                    TextRenderer.DrawText(g, "设为激活", f, rectActiveBtn, btnFg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }

            // 5. Draw Perfectly Square Vector Cross Delete Button
            if (CanDelete)
            {
                if (isHoverDeleteBtn)
                {
                    using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rectDeleteBtn, S(4)))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(45, 255, 82, 82)))
                        {
                            g.FillPath(b, path);
                        }
                    }
                }

                Color xCol = isHoverDeleteBtn ? Color.FromArgb(255, 100, 100) : Color.FromArgb(100, 115, 135);
                float penW = Math.Max(2.0f, 2.0f * DpiScale);
                using (Pen xPen = new Pen(xCol, penW))
                {
                    xPen.StartCap = LineCap.Round;
                    xPen.EndCap = LineCap.Round;
                    int cPad = (int)Math.Round(6.5f * DpiScale);
                    g.DrawLine(xPen, rectDeleteBtn.X + cPad, rectDeleteBtn.Y + cPad, rectDeleteBtn.Right - cPad, rectDeleteBtn.Bottom - cPad);
                    g.DrawLine(xPen, rectDeleteBtn.Right - cPad, rectDeleteBtn.Y + cPad, rectDeleteBtn.X + cPad, rectDeleteBtn.Bottom - cPad);
                }
            }
        }
    }
}
