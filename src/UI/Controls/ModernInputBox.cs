// ============================================================================
// 模块名称: ModernInputBox.cs
// 所属区域: UI/Controls (高级功能控件层)
// 核心职责:
//   现代化深色暗黑风输入框控件，内部宿主无边框原生 TextBox 并自绘制外层圆角背景、
//   悬停微光边框与获得焦点时的常态发光绿色外圈（DpiScale 自适应）。
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
using System.Windows.Forms;

namespace RazerBatteryTray
{
    public class ModernInputBox : Control
    {
        public float DpiScale = 1.0f;
        private TextBox innerBox;
        private bool isFocused = false;
        private bool isHovered = false;

        public override string Text
        {
            get { return innerBox != null ? innerBox.Text : base.Text; }
            set
            {
                base.Text = value;
                if (innerBox != null && innerBox.Text != value) innerBox.Text = value;
            }
        }

        public ModernInputBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.FromArgb(18, 19, 23);
            ForeColor = Color.White;
            DoubleBuffered = true;

            innerBox = new TextBox();
            innerBox.BorderStyle = BorderStyle.None;
            innerBox.BackColor = Color.FromArgb(28, 32, 44);
            innerBox.ForeColor = Color.FromArgb(245, 250, 255);
            innerBox.Font = new Font("Microsoft YaHei UI", 9.5F);
            innerBox.GotFocus += (s, e) => { isFocused = true; Invalidate(); };
            innerBox.LostFocus += (s, e) => { isFocused = false; Invalidate(); };
            innerBox.MouseEnter += (s, e) => { isHovered = true; Invalidate(); };
            innerBox.MouseLeave += (s, e) => { isHovered = false; Invalidate(); };
            innerBox.TextChanged += (s, e) => { base.Text = innerBox.Text; };
            this.Controls.Add(innerBox);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (innerBox != null)
            {
                int padH = (int)(10 * DpiScale);
                int h = (int)(20 * DpiScale);
                innerBox.SetBounds(padH, (Height - h) / 2, Width - (padH * 2), h);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int r = (int)(6 * DpiScale);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg = Color.FromArgb(28, 32, 44);
            Color border = isFocused ? Color.FromArgb(0, 230, 118) : (isHovered ? Color.FromArgb(80, 110, 150) : Color.FromArgb(50, 56, 72));

            using (var path = RoundedCard.GetRoundedRectangle(rect, r))
            {
                using (var brush = new SolidBrush(bg)) g.FillPath(brush, path);
                using (var pen = new Pen(border, isFocused ? 1.5f : 1.0f)) g.DrawPath(pen, path);
            }
        }
    }
}
