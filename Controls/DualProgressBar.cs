using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LineageII.Controls
{
    internal class DualProgressBar : Control
    {
        private int _value;

        [DefaultValue(0)]
        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        [DefaultValue(typeof(Color), "Orange")]
        public Color FillColor { get; set; } = Color.Orange;

        [DefaultValue(typeof(Color), "DimGray")]
        public Color TrackColor { get; set; } = Color.DimGray;

        [DefaultValue(typeof(Color), "White")]
        public Color TextColor { get; set; } = Color.White;

        [DefaultValue(typeof(Color), "Black")]
        public Color BorderColor { get; set; } = Color.Black;

        public string Caption { get; set; } = string.Empty;

        public DualProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Height = 22;
            Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle trackRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath trackPath = BuildRoundedRectangle(trackRect, 10))
            using (SolidBrush trackBrush = new SolidBrush(TrackColor))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            if (Value > 0)
            {
                int fillWidth = Math.Max(6, (int)Math.Round((Width - 1) * (Value / 100.0)));
                fillWidth = Math.Min(fillWidth, Width - 1);
                Rectangle fillRect = new Rectangle(0, 0, fillWidth, Height - 1);
                using (GraphicsPath fillPath = BuildRoundedRectangle(fillRect, 10))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(fillRect, Lighten(FillColor, 0.28f), FillColor, 90f))
                {
                    e.Graphics.FillPath(fillBrush, fillPath);
                }
            }

            using (GraphicsPath borderPath = BuildRoundedRectangle(trackRect, 10))
            using (Pen borderPen = new Pen(BorderColor))
            {
                e.Graphics.DrawPath(borderPen, borderPath);
            }

            string caption = string.IsNullOrWhiteSpace(Caption) ? "Progresso" : Caption;
            string percent = $"{Value}%";
            Size percentSize = TextRenderer.MeasureText(e.Graphics, percent, Font);
            Rectangle percentRect = new Rectangle(
                Math.Max(8, Width - percentSize.Width - 12),
                0,
                Math.Min(Width - 8, percentSize.Width + 4),
                Height);
            Rectangle captionRect = new Rectangle(10, 0, Math.Max(0, percentRect.Left - 16), Height);

            TextRenderer.DrawText(e.Graphics, caption, Font, captionRect, TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, percent, Font, percentRect, TextColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private static GraphicsPath BuildRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            if (rect.Width <= diameter || rect.Height <= diameter)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Lighten(Color color, float amount)
        {
            int r = color.R + (int)((255 - color.R) * amount);
            int g = color.G + (int)((255 - color.G) * amount);
            int b = color.B + (int)((255 - color.B) * amount);
            return Color.FromArgb(color.A, Math.Min(255, r), Math.Min(255, g), Math.Min(255, b));
        }
    }
}
