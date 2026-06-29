using System.Configuration;

namespace LineageII.Services
{
    internal sealed class LauncherConfig
    {
        public string LauncherUrl { get; }
        public int DefaultWidth { get; }
        public int DefaultHeight { get; }
        public int MinimumWidth { get; }
        public int MinimumHeight { get; }

        private LauncherConfig(string launcherUrl, int defaultWidth, int defaultHeight, int minimumWidth, int minimumHeight)
        {
            LauncherUrl = launcherUrl;
            DefaultWidth = defaultWidth;
            DefaultHeight = defaultHeight;
            MinimumWidth = minimumWidth;
            MinimumHeight = minimumHeight;
        }

        public static LauncherConfig Load()
        {
            string url = ConfigurationManager.AppSettings["LauncherUrl"] ?? "https://localhost/index.php";
            int defaultWidth = ParseOrDefault("DefaultWidth", 1280);
            int defaultHeight = ParseOrDefault("DefaultHeight", 720);
            int minWidth = ParseOrDefault("MinimumWidth", 1024);
            int minHeight = ParseOrDefault("MinimumHeight", 640);

            return new LauncherConfig(url, defaultWidth, defaultHeight, minWidth, minHeight);
        }

        private static int ParseOrDefault(string key, int fallback)
        {
            string raw = ConfigurationManager.AppSettings[key];
            return int.TryParse(raw, out int value) ? value : fallback;
        }
    }
}
