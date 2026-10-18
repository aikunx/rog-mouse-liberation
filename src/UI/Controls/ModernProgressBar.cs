// ============================================================================
// 模块名称: ModernProgressBar.cs
// 所属区域: UI/Controls (基础原子控件层)
// 核心职责:
//   现代化高动态平滑电量进度条，支持常规电量百分比平滑填充、有线呼吸脉冲（WiredPulse）、
//   充电中渐变光波扫描流动（AnimatedFlow / ChargingFill）等多种视觉渲染特效。
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
    public class ModernProgressBar : Control
    {
        private int value = 0;
        public int Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public Color TrackColor { get; set; }
        public Color ProgressColor { get; set; }
        public Color SecondaryColor { get; set; }

        public bool IsAnimatedFlow { get; set; }
        public bool IsWiredPulse { get; set; }
        public bool IsChargingFill { get; set; }

        private float flowPhase = 0f;
        private Timer flowTimer;

        public ModernProgressBar()
        {
            TrackColor = Color.FromArgb(38, 42, 53);
            ProgressColor = Color.FromArgb(0, 230, 118);
            SecondaryColor = Color.FromArgb(255, 214, 0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            flowTimer = new Timer();
            flowTimer.Interval = 30;
            flowTimer.Tick += (s, e) =>
            {
                if (!IsAnimatedFlow)
                {
                    flowTimer.Stop();
                    return;
                }
                flowPhase += 0.08f;
                if (flowPhase > (float)(Math.PI * 200)) flowPhase = 0f;
                Invalidate();
            };
        }

        public void StartChargingFlow(Color c1, Color c2)
        {
            IsAnimatedFlow = true;
            IsWiredPulse = false;
            IsChargingFill = false;
            ProgressColor = c1;
            SecondaryColor = c2;
            if (!flowTimer.Enabled) flowTimer.Start();
            Invalidate();
        }

        public void StartWiredFlow(Color c)
        {
            IsAnimatedFlow = true;
            IsWiredPulse = true;
            IsChargingFill = false;
            ProgressColor = c;
            if (!flowTimer.Enabled) flowTimer.Start();
            Invalidate();
        }

        public void StartChargingFill(int pct, Color c1, Color c2)
        {
            this.value = Math.Max(0, Math.Min(100, pct));
            IsAnimatedFlow = true;
            IsWiredPulse = false;
            IsChargingFill = true;
            ProgressColor = c1;
            SecondaryColor = c2;
            if (!flowTimer.Enabled) flowTimer.Start();
            Invalidate();
        }

        public void StopFlow()
        {
            IsAnimatedFlow = false;
            IsWiredPulse = false;
            IsChargingFill = false;
            if (flowTimer != null) flowTimer.Stop();
            Invalidate();
        }

        public void StopFlow(int pct, Color c)
        {
            this.value = Math.Max(0, Math.Min(100, pct));
            IsAnimatedFlow = false;
            IsWiredPulse = false;
            IsChargingFill = false;
            ProgressColor = c;
            if (flowTimer != null) flowTimer.Stop();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Defense-in-depth: Offline grey color (70, 80, 100) must never render animated flow
            if (IsAnimatedFlow && ProgressColor.R == 70 && ProgressColor.G == 80 && ProgressColor.B == 100)
            {
                IsAnimatedFlow = false;
                IsWiredPulse = false;
                IsChargingFill = false;
                if (flowTimer != null) flowTimer.Stop();
            }

            Color parentBg = (Parent is RoundedCard) ? ((RoundedCard)Parent).CardColor : (Parent != null ? Parent.BackColor : Color.FromArgb(25, 27, 34));
            g.Clear(parentBg);

            int h = Height;
            int radius = h / 2;
            Rectangle rect = new Rectangle(0, 0, Width, h);

            using (GraphicsPath trackPath = RoundedCard.GetRoundedRectangle(rect, radius))
            {
                using (SolidBrush brush = new SolidBrush(TrackColor))
                {
                    g.FillPath(brush, trackPath);
                }
            }

            if (IsAnimatedFlow)
            {
                if (IsWiredPulse)
                {
                    int alpha = (int)(150 + 80 * Math.Sin(flowPhase * 1.5f));
                    if (alpha < 60) alpha = 60; if (alpha > 240) alpha = 240;
                    using (GraphicsPath fillPath = RoundedCard.GetRoundedRectangle(rect, radius))
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, ProgressColor.R, ProgressColor.G, ProgressColor.B)))
                    {
                        g.FillPath(brush, fillPath);
                    }
                }
                else if (IsChargingFill && value > 0)
                {
                    int fillWidth = (int)((Width * (value / 100.0f)));
                    if (fillWidth < radius * 2) fillWidth = radius * 2;
                    if (fillWidth > Width) fillWidth = Width;

                    Rectangle fillRect = new Rectangle(0, 0, fillWidth, h);
                    using (GraphicsPath fillPath = RoundedCard.GetRoundedRectangle(fillRect, radius))
                    {
                        using (SolidBrush brush = new SolidBrush(ProgressColor))
                        {
                            g.FillPath(brush, fillPath);
                        }

                        float cycle = (flowPhase * 35f) % (fillWidth + 100) - 50;
                        Rectangle waveRect = new Rectangle((int)cycle - 40, 0, 80, h);
                        using (LinearGradientBrush lgb = new LinearGradientBrush(new Point(waveRect.Left, 0), new Point(waveRect.Right, 0),
                            Color.FromArgb(0, SecondaryColor), Color.FromArgb(200, SecondaryColor)))
                        {
                            ColorBlend cb = new ColorBlend();
                            cb.Colors = new Color[] { Color.FromArgb(0, SecondaryColor), Color.FromArgb(220, SecondaryColor), Color.FromArgb(0, SecondaryColor) };
                            cb.Positions = new float[] { 0f, 0.5f, 1f };
                            lgb.InterpolationColors = cb;
                            g.SetClip(fillPath);
                            g.FillRectangle(lgb, waveRect);
                            g.ResetClip();
                        }
                    }
                }
                else
                {
                    using (GraphicsPath fillPath = RoundedCard.GetRoundedRectangle(rect, radius))
                    {
                        float cycle = (flowPhase * 25f) % (Width + 140) - 70;
                        Rectangle waveRect = new Rectangle((int)cycle - 70, 0, 140, h);
                        using (LinearGradientBrush lgb = new LinearGradientBrush(new Point(waveRect.Left, 0), new Point(waveRect.Right, 0),
                            Color.FromArgb(0, ProgressColor), Color.FromArgb(220, SecondaryColor)))
                        {
                            ColorBlend cb = new ColorBlend();
                            cb.Colors = new Color[] { Color.FromArgb(30, ProgressColor), Color.FromArgb(240, SecondaryColor), Color.FromArgb(30, ProgressColor) };
                            cb.Positions = new float[] { 0f, 0.5f, 1f };
                            lgb.InterpolationColors = cb;
                            g.SetClip(fillPath);
                            g.FillRectangle(lgb, waveRect);
                            g.ResetClip();
                        }
                    }
                }
            }
            else if (value > 0)
            {
                int fillWidth = (int)((Width * (value / 100.0f)));
                if (fillWidth < radius * 2) fillWidth = radius * 2;
                if (fillWidth > Width) fillWidth = Width;

                Rectangle fillRect = new Rectangle(0, 0, fillWidth, h);
                using (GraphicsPath fillPath = RoundedCard.GetRoundedRectangle(fillRect, radius))
                using (SolidBrush brush = new SolidBrush(ProgressColor))
                {
                    g.FillPath(brush, fillPath);
                }
            }
        }
    }
}
