// ============================================================================
// 模块名称: TactileSmoothSliderControl.cs
// 所属区域: UI/Controls (基础原子控件层)
// 核心职责:
//   现代化无级跟手滑块控件（用于亮度、音量或灵敏度调节），具备触觉视觉微光晕、
//   DPI 缩放自适应、无级拖拽、滚轮步进调节以及刻度点视觉参考。
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
    public class TactileSmoothSliderControl : Control
    {
        public float DpiScale = 1.0f;
        private int _value = 100;
        private bool _isDragging = false;
        private bool _isHovered = false;
        private readonly int[] ticks = new int[] { 0, 25, 50, 75, 100 };

        public event EventHandler ValueChanged;
        public event EventHandler ValueCommitted;

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        public void SetValueWithoutEvents(int val)
        {
            int clamped = Math.Max(0, Math.Min(100, val));
            if (_value != clamped)
            {
                _value = clamped;
                Invalidate();
            }
        }

        public TactileSmoothSliderControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                Capture = true;
                UpdateValueFromMouse(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                UpdateValueFromMouse(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                UpdateValueFromMouse(e.X);
                if (ValueCommitted != null) ValueCommitted(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int step = e.Delta > 0 ? 5 : -5;
            Value = Math.Max(0, Math.Min(100, _value + step));
            if (ValueCommitted != null) ValueCommitted(this, EventArgs.Empty);
        }

        private void UpdateValueFromMouse(int mouseX)
        {
            int pad = (int)(10 * DpiScale);
            int trackW = Width - pad * 2;
            if (trackW <= 0) return;

            float ratio = (float)(mouseX - pad) / trackW;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            int targetVal = (int)Math.Round(ratio * 100);
            Value = targetVal;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color parentBg = (Parent is RoundedCard) ? ((RoundedCard)Parent).CardColor : (Parent != null ? Parent.BackColor : Color.FromArgb(25, 27, 34));
            g.Clear(parentBg);

            int pad = (int)(10 * DpiScale);
            int trackH = (int)(6 * DpiScale);
            int trackY = (Height - trackH) / 2;
            int trackW = Width - pad * 2;

            // 1. 轨道底槽 (暗调圆角)
            Rectangle trackRect = new Rectangle(pad, trackY, trackW, trackH);
            using (var trackPath = RoundedCard.GetRoundedRectangle(trackRect, trackH / 2))
            using (var trackBrush = new SolidBrush(Color.FromArgb(36, 42, 54)))
            {
                g.FillPath(trackBrush, trackPath);
            }

            // 2. 激活进度高光渐变
            int fillW = (int)(trackW * (_value / 100.0f));
            if (fillW > 0)
            {
                Rectangle fillRect = new Rectangle(pad, trackY, fillW, trackH);
                using (var fillPath = RoundedCard.GetRoundedRectangle(fillRect, trackH / 2))
                using (var grad = new LinearGradientBrush(new Point(pad, 0), new Point(pad + trackW, 0),
                    Color.FromArgb(0, 180, 255), Color.FromArgb(0, 229, 255)))
                {
                    g.FillPath(grad, fillPath);
                }
            }

            // 3. 刻度参考点 (0, 25, 50, 75, 100)
            int dotD = Math.Max(2, (int)(3 * DpiScale));
            for (int i = 0; i < ticks.Length; i++)
            {
                int tx = pad + (int)(trackW * (ticks[i] / 100.0f));
                int ty = trackY + trackH / 2;
                bool isPast = _value >= ticks[i];
                Color dotColor = isPast ? Color.FromArgb(200, 255, 255, 255) : Color.FromArgb(90, 80, 95, 120);
                using (var db = new SolidBrush(dotColor))
                {
                    g.FillEllipse(db, tx - dotD / 2, ty - dotD / 2, dotD, dotD);
                }
            }

            // 4. 拇指旋钮 (Thumb Knob)
            int thumbX = pad + fillW;
            int thumbY = Height / 2;
            int thumbR = (_isDragging || _isHovered) ? (int)(8 * DpiScale) : (int)(7 * DpiScale);

            // 旋钮外晕光
            int glowR = thumbR + (int)(4 * DpiScale);
            using (var glowBrush = new SolidBrush(Color.FromArgb(_isDragging ? 70 : (_isHovered ? 45 : 25), 0, 229, 255)))
            {
                g.FillEllipse(glowBrush, thumbX - glowR, thumbY - glowR, glowR * 2, glowR * 2);
            }

            // 旋钮主体
            using (var thumbBrush = new SolidBrush(Color.FromArgb(245, 250, 255)))
            {
                g.FillEllipse(thumbBrush, thumbX - thumbR, thumbY - thumbR, thumbR * 2, thumbR * 2);
            }

            // 旋钮中心青色核心点
            int coreR = Math.Max(2, (int)(3 * DpiScale));
            using (var coreBrush = new SolidBrush(Color.FromArgb(0, 190, 240)))
            {
                g.FillEllipse(coreBrush, thumbX - coreR, thumbY - coreR, coreR * 2, coreR * 2);
            }
        }
    }
}
