using System.Drawing;

namespace LineageII.Services
{
    internal static class Theme
    {
        public static Color Background => Color.FromArgb(5, 4, 5);
        public static Color Surface => Color.FromArgb(16, 15, 20);
        public static Color SurfaceAlt => Color.FromArgb(25, 23, 30);
        public static Color SurfaceDark => Color.FromArgb(10, 9, 13);

        public static Color Border => Color.FromArgb(54, 39, 43);
        public static Color BorderHot => Color.FromArgb(111, 53, 22);

        public static Color Orange => Color.FromArgb(255, 91, 24);
        public static Color OrangeHover => Color.FromArgb(255, 116, 45);
        public static Color Gold => Color.FromArgb(231, 175, 94);

        public static Color Text => Color.FromArgb(246, 240, 236);
        public static Color MutedText => Color.FromArgb(176, 166, 158);

        public static Color Success => Color.FromArgb(87, 224, 128);
        public static Color Blue => Color.FromArgb(76, 132, 255);

        public static Color Danger => Color.FromArgb(255, 80, 80);
        public static Color Transparent => Color.Transparent;
    }
}