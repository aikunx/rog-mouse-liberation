// ============================================================================
// 模块名称: ScanningBeamControl.cs
// 所属区域: UI/Controls (高级功能控件层)
// 核心职责:
//   现代化无线配对向导光波扫描条，60FPS 平滑循环水平横扫渐变激光光束，
//   展示正在搜索或同步外设的科技感扫描视觉。
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
    public class ScanningBeamControl : Control
    {
        private Timer animTimer;
        private float scanPos = 0f;
        public bool IsScanning = false;

        public ScanningBeamControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(25, 27, 34);
            DoubleBuffered = true;

            animTimer = new Timer();
            animTimer.Interval = 16; // 60 FPS
            animTimer.Tick += (s, e) => {
                if (IsScanning)
                {
                    scanPos += 0.03f;
                    if (scanPos > 1.0f) scanPos = 0f;
                    Invalidate();
                }
            };
        }

        public void Start()
        {
            IsScanning = true;
            Visible = true;
            animTimer.Start();
        }

        public void Stop()
        {
            IsScanning = false;
            animTimer.Stop();
            Visible = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animTimer != null)
            {
                animTimer.Stop();
                animTimer.Dispose();
                animTimer = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (!IsScanning) return;

            using (var trackBrush = new SolidBrush(Color.FromArgb(32, 36, 48)))
            {
                g.FillRectangle(trackBrush, 0, 0, Width, Height);
            }

            int beamW = (int)(Width * 0.35f);
            int beamX = (int)((Width + beamW) * scanPos) - beamW;

            Rectangle beamRect = new Rectangle(beamX, 0, beamW, Height);
            if (beamRect.Right > 0 && beamRect.Left < Width)
            {
                using (var brush = new LinearGradientBrush(new Point(beamRect.Left, 0), new Point(beamRect.Right, 0),
                    Color.FromArgb(0, 0, 230, 118), Color.FromArgb(220, 0, 230, 118)))
                {
                    ColorBlend blend = new ColorBlend(3);
                    blend.Colors = new Color[] { Color.FromArgb(0, 0, 230, 118), Color.FromArgb(240, 0, 230, 118), Color.FromArgb(0, 0, 230, 118) };
                    blend.Positions = new float[] { 0f, 0.5f, 1f };
                    brush.InterpolationColors = blend;
                    g.FillRectangle(brush, beamRect);
                }
            }
        }
    }
}
