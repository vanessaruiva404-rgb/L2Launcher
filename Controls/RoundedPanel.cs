using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LineageII.Controls
{
    internal class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 18;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderThickness { get; set; } = 1;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            Resize += (_, __) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = BuildPath(ClientRectangle, CornerRadius))
            {
                Region = new Region(path);

                using (SolidBrush brush = new SolidBrush(BackColor))
                {
                    e.Graphics.FillPath(brush, path);
                }

                if (BorderThickness > 0 && BorderColor.A > 0)
                {
                    using (Pen pen = new Pen(BorderColor, BorderThickness))
                    {
                        Rectangle rect = ClientRectangle;
                        rect.Width -= 1;
                        rect.Height -= 1;
                        using (GraphicsPath borderPath = BuildPath(rect, CornerRadius))
                        {
                            e.Graphics.DrawPath(pen, borderPath);
                        }
                    }
                }
            }
        }

        private static GraphicsPath BuildPath(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();

            if (radius <= 0)
            {
                path.AddRectangle(rectangle);
                path.CloseFigure();
                return path;
            }

            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
