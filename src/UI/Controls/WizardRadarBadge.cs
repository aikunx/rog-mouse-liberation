// ============================================================================
// 模块名称: WizardRadarBadge.cs
// 所属区域: UI/Controls (高级功能控件层)
// 核心职责:
//   现代化设备配对向导雷达扫描徽章，具备 20FPS 动态径向波纹脉冲动画与高亮绿色核心灯。
// 依赖关系: 无
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
    public class WizardRadarBadge : Control
    {
        public float DpiScale = 1.0f;
        private Timer pulseTimer;
        private float pulseStep = 0f;

        public WizardRadarBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            DoubleBuffered = true;
            pulseTimer = new Timer();
            pulseTimer.Interval = 50;
            pulseTimer.Tick += (s, e) => {
                pulseStep += 0.08f;
                if (pulseStep > 1.0f) pulseStep = 0f;
                Invalidate();
            };
            pulseTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && pulseTimer != null)
            {
                pulseTimer.Stop();
                pulseTimer.Dispose();
                pulseTimer = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = Width / 2f;
            float cy = Height / 2f;
            float maxR = Math.Min(Width, Height) / 2f - (2f * DpiScale);

            // Dark background circle
            using (var bgBrush = new SolidBrush(Color.FromArgb(28, 34, 48)))
            {
                g.FillEllipse(bgBrush, cx - maxR, cy - maxR, maxR * 2, maxR * 2);
            }

            // Expanding pulse wave
            float curR = maxR * pulseStep;
            int alpha = (int)(180 * (1f - pulseStep));
            if (alpha > 0 && curR > 2f)
            {
                using (var pulsePen = new Pen(Color.FromArgb(alpha, 0, 230, 118), 1.5f * DpiScale))
                {
                    g.DrawEllipse(pulsePen, cx - curR, cy - curR, curR * 2, curR * 2);
                }
            }

            // Outer boundary ring
            using (var ringPen = new Pen(Color.FromArgb(60, 0, 230, 118), 1f * DpiScale))
            {
                g.DrawEllipse(ringPen, cx - maxR, cy - maxR, maxR * 2, maxR * 2);
            }

            // Center glowing beacon
            using (var centerBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
            {
                float cr = 3.5f * DpiScale;
                g.FillEllipse(centerBrush, cx - cr, cy - cr, cr * 2, cr * 2);
            }
        }
    }
}
