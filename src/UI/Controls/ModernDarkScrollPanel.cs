// ============================================================================
// 模块名称: ModernDarkScrollPanel.cs
// 所属区域: UI/Controls (基础原子控件层)
// 核心职责:
//   现代化暗黑微晶滚动容器，彻底废除 Win32 原生粗糙白色横向/竖向滚动条，
//   提供纯 GDI+ 矢量纤细圆角悬浮滚动条、平滑鼠标滚轮交互与自适应换行。
// 依赖关系: ModernThemeHelper
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
    public class ModernDarkScrollPanel : Panel
    {
        private Panel contentPanel;
        private int scrollOffset = 0;
        private bool isDraggingThumb = false;
        private int dragStartY = 0;
        private int dragStartOffset = 0;
        private bool isThumbHovered = false;

        private int scrollBarWidth = 6;
        private int scrollBarMargin = 3;

        private Color thumbColorNormal = Color.FromArgb(60, 68, 86);
        private Color thumbColorHover = Color.FromArgb(90, 102, 130);
        private Color thumbColorDrag = Color.FromArgb(0, 200, 83);
        private Color trackColor = Color.FromArgb(18, 20, 26);

        public Panel Content
        {
            get { return contentPanel; }
        }

        public int ScrollOffset
        {
            get { return scrollOffset; }
            set
            {
                int max = GetMaxScroll();
                int newVal = Math.Max(0, Math.Min(max, value));
                if (scrollOffset != newVal)
                {
                    scrollOffset = newVal;
                    contentPanel.Top = -scrollOffset;
                    Invalidate();
                }
            }
        }

        public ModernDarkScrollPanel()
        {
            this.AutoScroll = false;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            contentPanel = new Panel();
            contentPanel.Location = new Point(0, 0);
            contentPanel.BackColor = Color.Transparent;
            contentPanel.AutoSize = false;
            this.Controls.Add(contentPanel);

            this.MouseWheel += OnPanelMouseWheel;
            contentPanel.MouseWheel += OnPanelMouseWheel;
            contentPanel.ControlAdded += OnChildAdded;
        }

        private void OnChildAdded(object sender, ControlEventArgs e)
        {
            if (e.Control != null)
            {
                HookChildMouseWheel(e.Control);
            }
        }

        public void HookChildMouseWheel(Control ctrl)
        {
            if (ctrl == null) return;
            ctrl.MouseWheel -= OnPanelMouseWheel;
            ctrl.MouseWheel += OnPanelMouseWheel;
            foreach (Control sub in ctrl.Controls)
            {
                HookChildMouseWheel(sub);
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateContentLayout();
        }

        public void UpdateContentLayout()
        {
            if (contentPanel == null) return;

            int availW = this.ClientSize.Width - scrollBarWidth - (scrollBarMargin * 2);
            if (availW < 10) availW = 10;
            contentPanel.Width = availW;

            int totalH = 0;
            foreach (Control c in contentPanel.Controls)
            {
                if (c.Visible)
                {
                    int bottom = c.Top + c.Height;
                    if (bottom > totalH) totalH = bottom;
                }
            }
            contentPanel.Height = totalH;

            int max = GetMaxScroll();
            if (scrollOffset > max)
            {
                ScrollOffset = max;
            }
            else
            {
                contentPanel.Top = -scrollOffset;
            }
            Invalidate();
        }

        private int GetMaxScroll()
        {
            if (contentPanel == null) return 0;
            int max = contentPanel.Height - this.ClientSize.Height;
            return max > 0 ? max : 0;
        }

        private void OnPanelMouseWheel(object sender, MouseEventArgs e)
        {
            int max = GetMaxScroll();
            if (max <= 0) return;

            int delta = e.Delta > 0 ? -36 : 36;
            ScrollOffset += delta;
        }

        private Rectangle GetThumbRectangle()
        {
            int max = GetMaxScroll();
            if (max <= 0 || this.ClientSize.Height <= 0) return Rectangle.Empty;

            int clientH = this.ClientSize.Height;
            int contentH = contentPanel.Height;
            if (contentH <= clientH) return Rectangle.Empty;

            float ratio = (float)clientH / (float)contentH;
            int thumbH = (int)(clientH * ratio);
            if (thumbH < 24) thumbH = 24;
            if (thumbH > clientH - 8) thumbH = clientH - 8;

            int trackH = clientH - thumbH;
            int thumbY = (int)((float)scrollOffset / (float)max * trackH);
            int thumbX = this.ClientSize.Width - scrollBarWidth - scrollBarMargin;

            return new Rectangle(thumbX, thumbY, scrollBarWidth, thumbH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int max = GetMaxScroll();
            if (max <= 0) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int clientH = this.ClientSize.Height;
            int trackX = this.ClientSize.Width - scrollBarWidth - scrollBarMargin;

            using (SolidBrush trackBrush = new SolidBrush(trackColor))
            {
                using (GraphicsPath trackPath = ModernThemeHelper.GetRoundedRectangle(new Rectangle(trackX, 2, scrollBarWidth, clientH - 4), scrollBarWidth / 2))
                {
                    g.FillPath(trackBrush, trackPath);
                }
            }

            Rectangle thumbRect = GetThumbRectangle();
            if (thumbRect.IsEmpty) return;

            Color thumbColor = thumbColorNormal;
            if (isDraggingThumb) thumbColor = thumbColorDrag;
            else if (isThumbHovered) thumbColor = thumbColorHover;

            using (SolidBrush thumbBrush = new SolidBrush(thumbColor))
            {
                using (GraphicsPath thumbPath = ModernThemeHelper.GetRoundedRectangle(thumbRect, scrollBarWidth / 2))
                {
                    g.FillPath(thumbBrush, thumbPath);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle thumbRect = GetThumbRectangle();
            bool hovered = (!thumbRect.IsEmpty && thumbRect.Contains(e.Location));
            if (isThumbHovered != hovered)
            {
                isThumbHovered = hovered;
                Invalidate();
            }

            if (isDraggingThumb)
            {
                int max = GetMaxScroll();
                int clientH = this.ClientSize.Height;
                int thumbH = thumbRect.Height;
                int trackH = clientH - thumbH;
                if (trackH > 0 && max > 0)
                {
                    int deltaY = e.Y - dragStartY;
                    float deltaRatio = (float)deltaY / (float)trackH;
                    int newOffset = dragStartOffset + (int)(deltaRatio * max);
                    ScrollOffset = newOffset;
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Rectangle thumbRect = GetThumbRectangle();
                if (!thumbRect.IsEmpty && thumbRect.Contains(e.Location))
                {
                    isDraggingThumb = true;
                    dragStartY = e.Y;
                    dragStartOffset = scrollOffset;
                    Invalidate();
                }
                else
                {
                    int trackX = this.ClientSize.Width - scrollBarWidth - (scrollBarMargin * 2);
                    if (e.X >= trackX)
                    {
                        int max = GetMaxScroll();
                        int clientH = this.ClientSize.Height;
                        if (clientH > 0)
                        {
                            float clickRatio = (float)e.Y / (float)clientH;
                            ScrollOffset = (int)(clickRatio * max);
                        }
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (isDraggingThumb)
            {
                isDraggingThumb = false;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (isThumbHovered)
            {
                isThumbHovered = false;
                Invalidate();
            }
        }
    }
}
