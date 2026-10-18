// ============================================================================
// 模块名称: ModernBadge.cs
// 所属区域: UI/Controls (基础原子控件层)
// 核心职责:
//   胶囊型发光徽章状态指示器，支持自定义外边框发光色、状态指示圆点颜色与抗锯齿截断文本。
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
using System.Windows.Forms;

namespace RazerBatteryTray
{
    public class ModernBadge : Control
    {
        public Color BadgeColor = Color.FromArgb(0, 229, 255);
        public Color DotColor = Color.FromArgb(0, 229, 255);

        public ModernBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.FromArgb(235, 240, 250);
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color parentBg = (Parent is RoundedCard) ? ((RoundedCard)Parent).CardColor : (Parent != null ? Parent.BackColor : Color.FromArgb(18, 19, 23));
            g.Clear(parentBg);

            int radius = Math.Max(2, Height / 2);
            using (var path = RoundedCard.GetRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), radius))
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(28, 15, 25, 38)))
                {
                    g.FillPath(bgBrush, path);
                }
                using (var borderPen = new Pen(Color.FromArgb(70, BadgeColor), 1f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            int dotSize = 7;
            int dotY = (Height - dotSize) / 2;
            int dotX = 10;
            using (var dotBrush = new SolidBrush(DotColor))
            {
                g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
            }

            Rectangle textRect = new Rectangle(dotX + dotSize + 6, 0, Width - (dotX + dotSize + 12), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
