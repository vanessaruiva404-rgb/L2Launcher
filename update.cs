using System.Net;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using LineageII.Services;
using LineageII.UI;

namespace LineageII
{
    public partial class update : Form
    {
        private readonly LauncherConfig _config;
        private static readonly HttpClient client = new HttpClient();

        private NotifyIcon notifyIcon;
        private ContextMenuStrip notifyMenu;
        private bool forceExit = false;
        private bool maintenanceRunning = false;

        private readonly SemaphoreSlim maintenanceLock = new SemaphoreSlim(1, 1);
        private bool launcherBusy = false;
        private bool clientVerified = false;
        private string launcherStateStatus = "Inicializando...";
        private string launcherStateDownload = "Aguardando ação...";

        private DateTime lastWebStateAt = DateTime.MinValue;

        private const int DownloadBufferSize = 1024 * 1024;
        private const int MaxDownloadAttempts = 6;

        private Point dragStart;
        private bool dragging;

        public static string defaultUrl = "http://localhost";


        private static string NormalizeBaseUrl(string launcherUrl)
        {
            if (string.IsNullOrWhiteSpace(launcherUrl))
                return "http://localhost";

            string url = launcherUrl.Trim();

            if (url.EndsWith("/index.php", StringComparison.OrdinalIgnoreCase))
                url = url.Substring(0, url.Length - "/index.php".Length);

            return url.TrimEnd('/');
        }

        static update()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            client.Timeout = TimeSpan.FromHours(6);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LineageII-Launcher/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");

        }

        public update()
        {
            _config = LauncherConfig.Load();
            defaultUrl = NormalizeBaseUrl(_config.LauncherUrl);

            InitializeComponent();

            ApplyTheme();
            ConfigureWindow();
            ConfigureNotifyIcon();
            WireEvents();

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await ConfigureBrowser();

                lblStatus.Text = "Verificando cliente...";
                lblDownloadStatus.Text = "Lendo versão local.";

                await RefreshLauncherVerification(false);

                if (clientVerified)
                {
                    lblStatus.Text = "Cliente pronto ✔";
                    lblDownloadStatus.Text = "Cliente já instalado e versão compatível.";
                }
                else
                {
                    lblStatus.Text = "Cliente precisa verificar";
                    lblDownloadStatus.Text = "Clique em Reparar para validar ou instalar.";
                }

                await SendLauncherStateToWeb();
            }
            catch (Exception ex)
            {
                clientVerified = false;
                launcherBusy = false;
                lblStatus.Text = "Erro crítico";
                lblDownloadStatus.Text = ex.Message;
                await SendLauncherStateToWeb();
            }
        }


        private void ConfigureWindow()
        {
            Text = "Lineage II Launcher";
            MinimumSize = new Size(_config.MinimumWidth, _config.MinimumHeight);
            Size = new Size(_config.DefaultWidth, _config.DefaultHeight);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
        }

        private void ConfigureNotifyIcon()
        {
            notifyMenu = new ContextMenuStrip();

            notifyMenu.Items.Add("Abrir Launcher", null, (s, e) => ShowLauncher());
            notifyMenu.Items.Add("Iniciar jogo", null, async (s, e) => await StartGameSmart());
            notifyMenu.Items.Add("Reparar", null, async (s, e) => await RunFullCheck());
            notifyMenu.Items.Add(new ToolStripSeparator());
            notifyMenu.Items.Add("Sair", null, (s, e) => ExitLauncher());

            notifyIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Text = "Lineage II Launcher",
                ContextMenuStrip = notifyMenu,
                Visible = false
            };

            notifyIcon.DoubleClick += (s, e) => ShowLauncher();
        }

        private void WireEvents()
        {
            FormClosing += Update_FormClosing;

            titleBarPanel.MouseDown += BeginDrag;
            titleBarPanel.MouseMove += DragWindow;
            titleBarPanel.MouseUp += EndDrag;

            lblTitle.MouseDown += BeginDrag;
            lblTitle.MouseMove += DragWindow;
            lblTitle.MouseUp += EndDrag;



            SizeChanged += (s, e) =>
            {
                NotifyBrowserOfResize();
                AutoZoom();
            };

            btnClose.Click += (s, e) => Close();
            btnMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;

            btnSettings.Click += BtnSettings_Click;
        }

        private void Update_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (forceExit)
                return;

            e.Cancel = true;

            if (ShowLauncherCloseDialog())
                return;

            DialogResult result = MessageBox.Show(
                "Deseja esconder o launcher em segundo plano?\n\nSim = Esconder\nNão = Sair\nCancelar = Voltar",
                "Lineage II Launcher",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
                HideLauncher();

            else if (result == DialogResult.No)
                ExitLauncher();
        }

        private bool ShowLauncherCloseDialog()
        {
            using (LauncherCloseDialog dialog = new LauncherCloseDialog())
            {
                dialog.ShowDialog(this);

                if (dialog.SelectedAction == LauncherCloseAction.Hide)
                    HideLauncher();
                else if (dialog.SelectedAction == LauncherCloseAction.Exit)
                    ExitLauncher();
            }

            return true;
        }

        private void HideLauncher()
        {
            Hide();
            ShowInTaskbar = false;

            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(
                2500,
                "Lineage II Launcher",
                "O launcher continua aberto.",
                ToolTipIcon.Info
            );
        }

        private void ShowLauncher()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();

            notifyIcon.Visible = false;
        }

        private void ExitLauncher()
        {
            forceExit = true;

            if (notifyIcon != null)
                notifyIcon.Visible = false;

            Application.Exit();
        }

        private async Task ConfigureBrowser()
        {
            await EnsureWebView2Ready();

            CoreWebView2Settings settings = browser.CoreWebView2.Settings;

            settings.AreDefaultContextMenusEnabled = true;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.IsZoomControlEnabled = false;

            await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetLauncherBridgeScript());

            browser.Source = new Uri(BuildResponsiveUrl(browser.Width, browser.Height));

            browser.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    dynamic data = JsonConvert.DeserializeObject(e.WebMessageAsJson);
                    string action = data?.action;

                    if (!string.IsNullOrEmpty(action))
                        HandleWebMessage(action);
                }
                catch
                {
                    lblStatus.Text = "Erro JSON";
                }
            };

            browser.CoreWebView2.NavigationCompleted += async (s, e) =>
            {
                await browser.CoreWebView2.ExecuteScriptAsync(@"
                    (function() {
                        const checkbox = document.querySelector('input[name=""agree""]');
                        const button = document.getElementById('btnRegister');

                        if (checkbox && button) {
                            button.disabled = !checkbox.checked;
                            button.style.opacity = checkbox.checked ? '1' : '0.5';

                            checkbox.addEventListener('change', () => {
                                button.disabled = !checkbox.checked;
                                button.style.opacity = checkbox.checked ? '1' : '0.5';
                            });
                        }
                    })();
                ");

                await browser.CoreWebView2.ExecuteScriptAsync(@"
(function() {
    const KEY = 'elysian_music_muted';
    const audio = document.getElementById('bgMusic');
    const btn = document.getElementById('musicToggle');

    if (!audio) return;

    let muted = localStorage.getItem(KEY);
    if (muted === null) {
        muted = '0';
        localStorage.setItem(KEY, muted);
    }

    function applyMusicState() {
        const isMuted = localStorage.getItem(KEY) === '1';

        audio.muted = isMuted;
        audio.volume = isMuted ? 0 : 0.45;

        if (btn) {
            btn.textContent = isMuted ? '🔇' : '🔊';
            btn.title = isMuted ? 'Ativar música' : 'Silenciar música';
            btn.setAttribute('aria-label', btn.title);
        }

        if (!isMuted) {
            audio.play().catch(function(){});
        } else {
            audio.pause();
        }
    }

    if (btn && !btn.dataset.launcherMusicReady) {
        btn.dataset.launcherMusicReady = '1';

        btn.addEventListener('click', function(e) {
            e.preventDefault();

            const nextMuted = !(localStorage.getItem(KEY) === '1');
            localStorage.setItem(KEY, nextMuted ? '1' : '0');

            applyMusicState();
        });
    }

    document.addEventListener('click', function unlockOnce() {
        if (localStorage.getItem(KEY) !== '1') {
            audio.play().catch(function(){});
        }
        document.removeEventListener('click', unlockOnce);
    });

    applyMusicState();
})();
");

                await InjectLauncherBridgeAsync();

                AutoZoom();
                NotifyBrowserOfResize();

                await SendLauncherStateToWeb();
            };
        }

        private async Task EnsureWebView2Ready()
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LineageII", "WebView2");

            try
            {
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await browser.EnsureCoreWebView2Async(env);
                return;
            }
            catch
            {
            }

            string installer = await DownloadWebView2();
            await InstallWebView2WithUserConsent(installer);

            CoreWebView2Environment newEnv = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await browser.EnsureCoreWebView2Async(newEnv);
        }

        private async Task<string> DownloadWebView2()
        {
            string url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
            string path = Path.Combine(Application.UserAppDataPath, "WebView2Setup.exe");

            if (File.Exists(path))
                return path;

            lblStatus.Text = "Baixando WebView2...";

            using (HttpClient http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(2);
                byte[] data = await http.GetByteArrayAsync(url);
                File.WriteAllBytes(path, data);
            }

            return path;
        }

        private async Task InstallWebView2WithUserConsent(string installerPath)
        {
            DialogResult result = MessageBox.Show(
                "Este launcher precisa do Microsoft WebView2 para abrir a interface web.\n\nDeseja instalar agora?",
                "WebView2 necessário",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information
            );

            if (result != DialogResult.Yes)
                throw new Exception("Instalação do WebView2 recusada.");

            lblStatus.Text = "Instalando WebView2...";

            Process process = new Process();
            process.StartInfo.FileName = installerPath;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = "runas";

            process.Start();

            await Task.Run(() => process.WaitForExit());
        }

        private async void HandleWebMessage(string msg)
        {
            switch (msg)
            {
                case "play":
                    if (launcherBusy)
                    {
                        lblStatus.Text = "Aguarde o processo terminar.";
                        lblDownloadStatus.Text = "O launcher ainda está trabalhando.";
                        await SendLauncherStateToWeb();
                        return;
                    }

                    if (!clientVerified)
                    {
                        lblStatus.Text = "Cliente ainda não verificado.";
                        lblDownloadStatus.Text = "Clique em Reparar antes de jogar.";
                        await SendLauncherStateToWeb();
                        return;
                    }

                    await StartGameSmart();
                    break;

                case "repair":
                    await RunFullCheck();
                    break;

                case "minimize":
                    WindowState = FormWindowState.Minimized;
                    break;

                case "close":
                    Close();
                    break;

                default:
                    lblStatus.Text = $"Comando desconhecido: {msg}";
                    await SendLauncherStateToWeb();
                    break;
            }
        }


        private async Task SendToWeb(string json)
        {
            try
            {
                if (browser.CoreWebView2 == null)
                    return;

                await browser.CoreWebView2.ExecuteScriptAsync(
                    $"window.onLauncherMessage && window.onLauncherMessage({json});"
                );
            }
            catch
            {
            }
        }

        private async Task SendLauncherStateToWeb()
        {
            try
            {
                launcherStateStatus = lblStatus.Text;
                launcherStateDownload = lblDownloadStatus.Text;

                var state = new
                {
                    launcher = true,
                    busy = launcherBusy,
                    verified = clientVerified,
                    status = launcherStateStatus,
                    download = launcherStateDownload,
                    localVersion = GetLocalVersion()
                };

                string json = JsonConvert.SerializeObject(state);

                if (browser.CoreWebView2 != null)
                {
                    await browser.CoreWebView2.ExecuteScriptAsync(
                        $"window.onLauncherState && window.onLauncherState({json});"
                    );
                }
            }
            catch
            {
            }
        }

        private async Task InjectLauncherBridgeAsync()
        {
            try
            {
                if (browser.CoreWebView2 == null)
                    return;

                await browser.CoreWebView2.ExecuteScriptAsync(GetLauncherBridgeScript());
            }
            catch
            {
            }
        }

        private string GetLauncherBridgeScript()
        {
            return @"
(function() {
    const PANEL_ID = 'elysianLauncherPanel';
    const STYLE_ID = 'elysianLauncherPanelStyle';
    const POSITION_KEY = 'elysian_launcher_panel_position';

    function sendLauncher(action) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage({ action: action });
        }
    }

    function ensureStyle() {
        if (!document.head || document.getElementById(STYLE_ID)) return;

        const style = document.createElement('style');
        style.id = STYLE_ID;
        style.textContent = `
            .elysian-launcher-panel {
                position: fixed;
                left: 22px;
                bottom: 22px;
                z-index: 999999;
                width: 316px;
                padding: 15px;
                border-radius: 14px;
                background: rgba(8, 6, 8, .96);
                border: 1px solid rgba(255, 91, 24, .50);
                box-shadow: 0 18px 45px rgba(0,0,0,.65), 0 0 30px rgba(255,91,24,.16);
                color: #f6f0ec;
                font-family: Montserrat, Arial, sans-serif;
                backdrop-filter: blur(10px);
            }
            .elysian-launcher-head {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 10px;
                margin-bottom: 7px;
                cursor: move;
                user-select: none;
            }
            .elysian-launcher-title {
                margin: 0;
                font-size: 14px;
                font-weight: 900;
                color: #e7af5e;
                letter-spacing: .06em;
                text-transform: uppercase;
            }
            .elysian-launcher-badge {
                border-radius: 999px;
                padding: 4px 8px;
                background: rgba(87, 224, 128, .12);
                border: 1px solid rgba(87, 224, 128, .26);
                color: #57e080;
                font-size: 10px;
                font-weight: 800;
                white-space: nowrap;
                text-transform: uppercase;
            }
            .elysian-launcher-panel p {
                margin: 0 0 12px;
                font-size: 12px;
                line-height: 1.35;
                color: #b0a69e;
            }
            .elysian-launcher-actions {
                display: flex;
                gap: 8px;
            }
            .elysian-launcher-actions button {
                flex: 1;
                border: 0;
                border-radius: 12px;
                padding: 11px 10px;
                font-weight: 800;
                cursor: pointer;
                color: #fff;
                background: linear-gradient(135deg, #ff5b18, #c93608);
                user-select: none;
            }
            .elysian-launcher-actions button.secondary {
                background: rgba(255,255,255,.08);
                border: 1px solid rgba(255,255,255,.15);
            }
            .elysian-launcher-actions button:disabled {
                opacity: .45;
                cursor: not-allowed;
                filter: grayscale(1);
            }
        `;
        document.head.appendChild(style);
    }

    function ensurePanel() {
        ensureStyle();
        if (!document.body) return;
        const existingPanel = document.getElementById(PANEL_ID);
        if (existingPanel) {
            makePanelDraggable(existingPanel);
            applyLauncherState(window.__elysianLauncherLastState);
            return;
        }

        const panel = document.createElement('div');
        panel.id = PANEL_ID;
        panel.className = 'elysian-launcher-panel';
        panel.innerHTML = `
            <div class='elysian-launcher-head'>
                <h4 class='elysian-launcher-title'>Launcher L2 RP</h4>
                <span class='elysian-launcher-badge'>Oficial</span>
            </div>
            <p id='elysianLauncherStatus'>Carregando status...</p>
            <div class='elysian-launcher-actions'>
                <button id='elysianLauncherPlay' disabled>Jogar</button>
                <button id='elysianLauncherRepair' class='secondary'>Reparar</button>
            </div>
        `;
        document.body.appendChild(panel);
        makePanelDraggable(panel);
        restorePanelPosition(panel);
        applyLauncherState(window.__elysianLauncherLastState);
    }

    function clamp(value, min, max) {
        return Math.max(min, Math.min(value, max));
    }

    function restorePanelPosition(panel) {
        try {
            const raw = localStorage.getItem(POSITION_KEY);
            if (!raw) return;

            const pos = JSON.parse(raw);
            if (!pos || typeof pos.left !== 'number' || typeof pos.top !== 'number') return;

            const margin = 12;
            const maxLeft = Math.max(margin, window.innerWidth - panel.offsetWidth - margin);
            const maxTop = Math.max(margin, window.innerHeight - panel.offsetHeight - margin);

            panel.style.left = clamp(pos.left, margin, maxLeft) + 'px';
            panel.style.top = clamp(pos.top, margin, maxTop) + 'px';
            panel.style.right = 'auto';
            panel.style.bottom = 'auto';
        } catch (e) {}
    }

    function savePanelPosition(panel) {
        try {
            localStorage.setItem(POSITION_KEY, JSON.stringify({
                left: panel.offsetLeft,
                top: panel.offsetTop
            }));
        } catch (e) {}
    }

    function makePanelDraggable(panel) {
        if (!panel || panel.dataset.dragReady === '1') return;
        panel.dataset.dragReady = '1';

        const handle = panel.querySelector('.elysian-launcher-head') || panel;
        let dragging = false;
        let startX = 0;
        let startY = 0;
        let startLeft = 0;
        let startTop = 0;

        function beginDrag(clientX, clientY) {
            const rect = panel.getBoundingClientRect();
            dragging = true;
            startX = clientX;
            startY = clientY;
            startLeft = rect.left;
            startTop = rect.top;
            panel.style.left = rect.left + 'px';
            panel.style.top = rect.top + 'px';
            panel.style.right = 'auto';
            panel.style.bottom = 'auto';
        }

        function moveDrag(clientX, clientY) {
            if (!dragging) return;

            const margin = 12;
            const maxLeft = Math.max(margin, window.innerWidth - panel.offsetWidth - margin);
            const maxTop = Math.max(margin, window.innerHeight - panel.offsetHeight - margin);
            const nextLeft = clamp(startLeft + clientX - startX, margin, maxLeft);
            const nextTop = clamp(startTop + clientY - startY, margin, maxTop);

            panel.style.left = nextLeft + 'px';
            panel.style.top = nextTop + 'px';
        }

        function endDrag() {
            if (!dragging) return;
            dragging = false;
            savePanelPosition(panel);
        }

        handle.addEventListener('mousedown', function(e) {
            if (e.button !== 0) return;
            e.preventDefault();
            beginDrag(e.clientX, e.clientY);
        });

        document.addEventListener('mousemove', function(e) {
            moveDrag(e.clientX, e.clientY);
        });

        document.addEventListener('mouseup', endDrag);

        handle.addEventListener('touchstart', function(e) {
            if (!e.touches || !e.touches.length) return;
            const touch = e.touches[0];
            beginDrag(touch.clientX, touch.clientY);
        }, { passive: true });

        document.addEventListener('touchmove', function(e) {
            if (!dragging || !e.touches || !e.touches.length) return;
            const touch = e.touches[0];
            moveDrag(touch.clientX, touch.clientY);
        }, { passive: true });

        document.addEventListener('touchend', endDrag);

        window.addEventListener('resize', function() {
            restorePanelPosition(panel);
            savePanelPosition(panel);
        });
    }

    function applyLauncherState(state) {
        const play = document.getElementById('elysianLauncherPlay');
        const repair = document.getElementById('elysianLauncherRepair');
        const status = document.getElementById('elysianLauncherStatus');

        if (!play || !repair || !status || !state) return;

        const busy = !!state.busy;
        const verified = !!state.verified;

        play.disabled = busy || !verified;
        repair.disabled = busy;

        if (busy) {
            status.textContent = state.download || state.status || 'Processando...';
        } else if (verified) {
            status.textContent = 'Cliente verificado. Pronto para jogar.';
        } else {
            status.textContent = state.status || 'Cliente precisa ser verificado.';
        }
    }

    if (!window.__elysianLauncherClickReady) {
        window.__elysianLauncherClickReady = true;

        document.addEventListener('click', function(e) {
            const el = e.target.closest('[data-launcher-action], #elysianLauncherPlay, #elysianLauncherRepair');
            if (!el) return;

            let action = el.getAttribute('data-launcher-action');
            if (!action && el.id === 'elysianLauncherPlay') action = 'play';
            if (!action && el.id === 'elysianLauncherRepair') action = 'repair';
            if (!action) return;

            e.preventDefault();
            e.stopPropagation();
            sendLauncher(action);
        }, true);
    }

    window.onLauncherState = function(state) {
        window.__elysianLauncherLastState = state;
        ensurePanel();
        applyLauncherState(state);
    };

    if (!window.__elysianLauncherObserverReady) {
        window.__elysianLauncherObserverReady = true;

        const boot = function() {
            ensurePanel();

            if (!window.__elysianLauncherInterval) {
                window.__elysianLauncherInterval = setInterval(ensurePanel, 1200);
            }

            try {
                const root = document.documentElement || document.body;
                if (root && window.MutationObserver && !window.__elysianLauncherObserver) {
                    window.__elysianLauncherObserver = new MutationObserver(function() {
                        if (!document.getElementById(PANEL_ID)) ensurePanel();
                    });
                    window.__elysianLauncherObserver.observe(root, { childList: true, subtree: true });
                }
            } catch (e) {}
        };

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', boot, { once: true });
        } else {
            boot();
        }

        window.addEventListener('pageshow', ensurePanel);
        window.addEventListener('hashchange', ensurePanel);
        window.addEventListener('popstate', ensurePanel);
    } else {
        ensurePanel();
    }
})();
";
        }

        private async Task RefreshLauncherVerification(bool deepCheck)
        {
            try
            {
                Manifest manifest = await FetchManifest();
                string localVersion = GetLocalVersion();

                bool basicOk =
                    HasPlayableClient() &&
                    !string.IsNullOrWhiteSpace(localVersion) &&
                    !string.Equals(localVersion, "0", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(localVersion, manifest.Latest_version, StringComparison.OrdinalIgnoreCase);

                if (!basicOk)
                {
                    clientVerified = false;
                    return;
                }

                // Verifica rapidamente se todos os arquivos do manifesto existem fisicamente e possuem o tamanho correto
                if (manifest.Files != null)
                {
                    foreach (FileEntry file in manifest.Files)
                    {
                        if (string.IsNullOrWhiteSpace(file.Path))
                            continue;

                        try
                        {
                            string localPath = GetLocalGamePath(file.Path);
                            if (!File.Exists(localPath))
                            {
                                clientVerified = false;
                                return;
                            }

                            if (file.Size > 0)
                            {
                                FileInfo info = new FileInfo(localPath);
                                if (info.Length != file.Size)
                                {
                                    clientVerified = false;
                                    return;
                                }
                            }
                        }
                        catch
                        {
                            clientVerified = false;
                            return;
                        }
                    }
                }

                if (!deepCheck)
                {
                    clientVerified = true;
                    return;
                }

                List<FileEntry> broken = await Task.Run(() => GetFilesNeedingRepair(manifest.Files));
                clientVerified = broken.Count == 0;
            }
            catch
            {
                clientVerified = false;
            }
        }

        private void SetLauncherBusy(bool busy, string status, string download)
        {
            launcherBusy = busy;
            maintenanceRunning = busy;

            if (!string.IsNullOrWhiteSpace(status))
                lblStatus.Text = status;

            if (!string.IsNullOrWhiteSpace(download))
                lblDownloadStatus.Text = download;
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed || action == null)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch
                {
                }

                return;
            }

            action();
        }

        private void QueueLauncherStatePush(bool force = false)
        {
            DateTime now = DateTime.UtcNow;

            if (!force && (now - lastWebStateAt).TotalMilliseconds < 450)
                return;

            lastWebStateAt = now;

            RunOnUi(() => _ = SendLauncherStateToWeb());
        }

        private void SetActivityProgress(string caption, int value, string detail = null)
        {
            RunOnUi(() =>
            {
                patchProgress.Caption = string.IsNullOrWhiteSpace(caption) ? "Atividade" : caption;
                patchProgress.Value = Math.Max(0, Math.Min(value, 100));

                if (!string.IsNullOrWhiteSpace(detail))
                    lblDownloadStatus.Text = detail;

                QueueLauncherStatePush();
            });
        }

        private void SetCheckProgress(int value, string status = null, string detail = null)
        {
            RunOnUi(() =>
            {
                downloadProgress.Caption = "Progresso";
                downloadProgress.Value = Math.Max(0, Math.Min(value, 100));

                if (!string.IsNullOrWhiteSpace(status))
                    lblStatus.Text = status;

                if (!string.IsNullOrWhiteSpace(detail))
                    lblDownloadStatus.Text = detail;

                QueueLauncherStatePush();
            });
        }


        private string BuildResponsiveUrl(int width, int height)
        {
            string separator = _config.LauncherUrl.Contains("?") ? "&" : "?";

            return $"{_config.LauncherUrl}{separator}" +
                   $"launcherWidth={width}" +
                   $"&launcherHeight={height}" +
                   $"&theme=dark-purple" +
                   $"&version={GetLocalVersion()}" +
                   $"&platform=windows" +
                   $"&launcher=LineageII";
        }

        private async Task<Manifest> FetchManifest()
        {
            string manifestUrl = "http://157.254.248.55:50100/L2UpdaterWeb/api/manifest.php?t=" + DateTime.UtcNow.Ticks;
            string json = await client.GetStringAsync(manifestUrl);
            Manifest manifest = JsonConvert.DeserializeObject<Manifest>(json);

            if (manifest == null)
                throw new Exception("Manifest invalido.");

            NormalizeManifest(manifest);
            return manifest;
        }

        private void NormalizeManifest(Manifest manifest)
        {
            if (manifest.Files == null)
                manifest.Files = new List<FileEntry>();

            if (manifest.Patches == null)
                manifest.Patches = new List<Patch>();

            if (string.IsNullOrWhiteSpace(manifest.Latest_version))
                manifest.Latest_version = manifest.Full_package?.Version ?? "0";

            manifest.Latest_version = manifest.Latest_version.Trim();

            if (manifest.Full_package != null)
            {
                manifest.Full_package.File = NormalizeRelativePath(manifest.Full_package.File);
                manifest.Full_package.Hash = NormalizeHash(manifest.Full_package.Hash);
                manifest.Full_package.Version = string.IsNullOrWhiteSpace(manifest.Full_package.Version)
                    ? manifest.Latest_version
                    : manifest.Full_package.Version.Trim();
            }

            foreach (Patch patch in manifest.Patches)
            {
                patch.File = NormalizeRelativePath(patch.File);
                patch.Hash = NormalizeHash(patch.Hash);
            }

            foreach (FileEntry file in manifest.Files)
            {
                file.Path = NormalizeRelativePath(file.Path);
                file.Package = NormalizeRelativePath(file.Package);
                file.Hash = NormalizeHash(file.Hash);
                file.Package_hash = NormalizeHash(file.Package_hash);
            }
        }

        private async void NotifyBrowserOfResize()
        {
            try
            {
                if (browser.CoreWebView2 == null)
                    return;

                await browser.CoreWebView2.ExecuteScriptAsync(
                    $"window.onLauncherResize && window.onLauncherResize({browser.Width}, {browser.Height});"
                );
            }
            catch
            {
            }
        }

        private void AutoZoom()
        {
            if (browser.Width <= 0 || browser.Height <= 0)
                return;

            float baseW = 1232f;
            float baseH = 670f;

            float scaleX = browser.Width / baseW;
            float scaleY = browser.Height / baseH;
            float scale = (scaleX + scaleY) / 2f;

            scale = Math.Max(0.75f, Math.Min(scale, 1.25f));

            browser.ZoomFactor = scale;
        }

        private async Task StartGameSmart()
        {
            if (launcherBusy)
            {
                lblStatus.Text = "Aguarde o processo terminar.";
                lblDownloadStatus.Text = "Não é possível iniciar enquanto verifica/baixa.";
                await SendLauncherStateToWeb();
                return;
            }

            try
            {
                await RefreshLauncherVerification(false);

                if (!clientVerified)
                {
                    lblStatus.Text = "Cliente não está pronto.";
                    lblDownloadStatus.Text = "Use Reparar para validar ou instalar o cliente.";
                    await SendLauncherStateToWeb();
                    return;
                }

                StartL2();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Erro ao iniciar";
                lblDownloadStatus.Text = ex.Message;
                await SendLauncherStateToWeb();
            }
        }


        private void StartL2()
        {
            string exe = Path.Combine(Application.StartupPath, "system", "l2.exe");

            if (!File.Exists(exe))
            {
                MessageBox.Show("Cliente não encontrado.", "Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe),
                Arguments = "-from-launcher",
                UseShellExecute = true
            });

            SimulateAction("Iniciando jogo", 100, 100);

            HideLauncher();
        }

        private bool HasPlayableClient()
        {
            return File.Exists(Path.Combine(Application.StartupPath, "system", "l2.exe"));
        }

        private bool ShouldDownloadFullClient(Manifest manifest, string currentVersion)
        {
            if (manifest?.Full_package == null)
                return false;

            if (!HasPlayableClient())
                return true;

            int total = manifest.Files?.Count ?? 0;

            if (total == 0)
                return string.Equals(currentVersion, "0", StringComparison.OrdinalIgnoreCase);

            int existing = CountExistingManifestFiles(manifest.Files);

            if (existing == 0)
                return true;

            return total >= 20 && existing <= Math.Max(3, total / 10);
        }

        private int CountExistingManifestFiles(List<FileEntry> files)
        {
            if (files == null || files.Count == 0)
                return 0;

            int count = 0;

            foreach (FileEntry file in files)
            {
                if (string.IsNullOrWhiteSpace(file.Path))
                    continue;

                try
                {
                    if (File.Exists(GetLocalGamePath(file.Path)))
                        count++;
                }
                catch
                {
                }
            }

            return count;
        }

        private string GetLocalGamePath(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);

            if (string.IsNullOrWhiteSpace(normalized))
                throw new Exception("Caminho de arquivo vazio no manifest.");

            string root = Path.GetFullPath(Application.StartupPath + Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(Application.StartupPath, normalized));

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Manifest contem caminho fora da pasta do launcher: " + relativePath);

            return fullPath;
        }

        private string NormalizeRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().Replace('\\', '/').TrimStart('/');
        }

        private string NormalizeHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().Replace("-", string.Empty).ToUpperInvariant();
        }

        private async Task RunFullCheck()
        {
            if (!await maintenanceLock.WaitAsync(0))
            {
                lblStatus.Text = "Update já está em andamento.";
                lblDownloadStatus.Text = "Aguarde terminar.";
                await SendLauncherStateToWeb();
                return;
            }

            SetLauncherBusy(true, "Buscando atualização...", "Preparando verificação.");
            clientVerified = false;
            SetActivityProgress("Atividade", 0, "Preparando verificacao.");
            SetCheckProgress(0, "Buscando atualizacao...");
            await SendLauncherStateToWeb();

            try
            {
                Manifest manifest = await FetchManifest();

                if (manifest == null)
                    throw new Exception("Manifest inválido.");

                if (manifest.Files == null)
                    manifest.Files = new List<FileEntry>();

                if (manifest.Patches == null)
                    manifest.Patches = new List<Patch>();

                string currentVersion = GetLocalVersion();
                bool shouldDownloadFull = ShouldDownloadFullClient(manifest, currentVersion);

                if (shouldDownloadFull)
                {
                    if (manifest.Full_package == null)
                        throw new Exception("Pacote full não encontrado no manifest.");

                    SetCheckProgress(15, "Instalacao nova detectada. Baixando cliente full...");
                    await SendLauncherStateToWeb();

                    await DownloadAndExtractFullPackage(manifest);

                    SaveLocalVersion(manifest.Full_package.Version ?? manifest.Latest_version);
                    currentVersion = GetLocalVersion();

                    SetCheckProgress(55, "Cliente full instalado. Preparando verificacao.");
                    await SendLauncherStateToWeb();
                }
                else
                {
                    List<Patch> chain = string.Equals(currentVersion, "0", StringComparison.OrdinalIgnoreCase)
                        ? new List<Patch>()
                        : BuildPatchChain(manifest, currentVersion);

                    if (chain.Count > 0)
                    {
                        int total = chain.Count;
                        int index = 0;

                        foreach (Patch patch in chain)
                        {
                            index++;

                            int checkStart = 15 + (int)(((index - 1) * 45.0) / total);
                            SetCheckProgress(checkStart, $"Aplicando patch {patch.To_version}...");
                            await SendLauncherStateToWeb();

                            await DownloadAndExtractPatch(manifest, patch);
                            SaveLocalVersion(patch.To_version);

                            currentVersion = patch.To_version;
                            SetCheckProgress(15 + (int)((index * 45.0) / total), $"Patch {patch.To_version} aplicado.");
                        }
                    }
                    else if (!string.Equals(currentVersion, manifest.Latest_version, StringComparison.OrdinalIgnoreCase) && manifest.Files.Count == 0)
                    {
                        if (manifest.Full_package == null)
                            throw new Exception("Sem cadeia de patch e sem pacote full.");

                        SetCheckProgress(15, "Cadeia de patch nao encontrada. Baixando full...");
                        await SendLauncherStateToWeb();

                        await DownloadAndExtractFullPackage(manifest);

                        SaveLocalVersion(manifest.Full_package.Version ?? manifest.Latest_version);
                        currentVersion = GetLocalVersion();

                        SetCheckProgress(60, "Cliente full instalado. Preparando verificacao.");
                        await SendLauncherStateToWeb();
                    }
                }

                SetCheckProgress(65, "Verificando arquivos locais...", "Calculando integridade do cliente.");
                await SendLauncherStateToWeb();

                List<FileEntry> missingOrBrokenFiles =
                    await Task.Run(() => GetFilesNeedingRepair(manifest.Files, 65, 80));

                if (missingOrBrokenFiles.Count > 0)
                {
                    lblStatus.Text = $"Repair inteligente: {missingOrBrokenFiles.Count} arquivo(s)...";
                    await SendLauncherStateToWeb();

                    await RepairFilesIndividually(manifest, missingOrBrokenFiles);
                }

                SetCheckProgress(95, "Conferindo resultado final...");
                await SendLauncherStateToWeb();

                List<FileEntry> finalRepair =
                    await Task.Run(() => GetFilesNeedingRepair(manifest.Files, 95, 99));

                if (finalRepair.Count > 0)
                    throw new Exception($"{finalRepair.Count} arquivo(s) ainda com problema.");

                SaveLocalVersion(manifest.Latest_version);
                clientVerified = true;

                SetActivityProgress("Pronto", 100, "Cliente validado com sucesso.");
                SetCheckProgress(100, "Atualizacao concluida.");

                lblStatus.Text = "Atualização concluída ✔";
                lblDownloadStatus.Text = "Cliente validado com sucesso.";

                CreateDesktopShortcut();
            }
            catch (Exception ex)
            {
                clientVerified = false;
                SetActivityProgress("Atividade", 0, ex.Message);
                SetCheckProgress(0, "Erro no update");

                lblStatus.Text = "Erro no update";
                lblDownloadStatus.Text = ex.Message;
            }
            finally
            {
                SetLauncherBusy(false, null, null);
                maintenanceLock.Release();
                await SendLauncherStateToWeb();
            }
        }


        private List<FileEntry> GetFilesNeedingRepair(List<FileEntry> files, int progressStart = -1, int progressEnd = -1)
        {
            List<FileEntry> result = new List<FileEntry>();

            if (files == null || files.Count == 0)
                return result;

            int total = files.Count;
            int index = 0;

            foreach (FileEntry file in files)
            {
                index++;

                if (progressStart >= 0 && progressEnd >= progressStart &&
                    (index == 1 || index == total || index % 10 == 0))
                {
                    int progress = progressStart + (int)(((progressEnd - progressStart) * index) / Math.Max(1.0, total));
                    SetCheckProgress(progress, "Verificando arquivos locais...", $"Processo: {index}/{total} - {file.Path}");
                }

                if (string.IsNullOrWhiteSpace(file.Path))
                    continue;

                string localPath = GetLocalGamePath(file.Path);

                if (!File.Exists(localPath))
                {
                    result.Add(file);
                    continue;
                }

                FileInfo info = new FileInfo(localPath);

                if (file.Size > 0 && info.Length != file.Size)
                {
                    result.Add(file);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(file.Hash) &&
                    !ComputeFileSha256(localPath).Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                    result.Add(file);
            }

            return result;
        }

        private async Task RepairFilesIndividually(Manifest manifest, List<FileEntry> filesToRepair)
        {
            if (filesToRepair == null || filesToRepair.Count == 0)
                return;

            // 1. Arquivos avulsos (sem pacote ZIP associado)
            List<FileEntry> rawFiles = filesToRepair
                .Where(file => string.IsNullOrWhiteSpace(file.Package))
                .ToList();

            if (rawFiles.Count > 0)
            {
                int rawTotal = rawFiles.Count;
                int rawIndex = 0;
                foreach (FileEntry file in rawFiles)
                {
                    rawIndex++;
                    SetCheckProgress(
                        Math.Min(80, (int)((rawIndex * 80.0) / rawTotal)),
                        $"Baixando: {file.Path}"
                    );

                    await DownloadRawFile(manifest, file);
                }
            }

            // 2. Arquivos empacotados em ZIP
            List<FileEntry> packagesToRepair = filesToRepair
                .Where(file => !string.IsNullOrWhiteSpace(file.Package))
                .GroupBy(file => file.Package, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (packagesToRepair.Count > 0)
            {
                int total = packagesToRepair.Count;
                int index = 0;

                foreach (FileEntry file in packagesToRepair)
                {
                    index++;

                    SetCheckProgress(
                        Math.Min(94, 80 + (int)(((index - 1) * 14.0) / total)),
                        $"Repair: {file.Package}"
                    );

                    await DownloadAndExtractSingleFilePackage(manifest, file);

                    SetCheckProgress(
                        Math.Min(94, 80 + (int)((index * 14.0) / total)),
                        $"Repair aplicado: {file.Package}"
                    );
                }
            }
        }

        private async Task DownloadRawFile(Manifest manifest, FileEntry file)
        {
            string baseUrl = GetBuildBaseUrl(manifest);
            string url = BuildPackageUrl(baseUrl, file.Path);
            string targetPath = Path.GetFullPath(Path.Combine(Application.StartupPath, NormalizeRelativePath(file.Path)));

            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await DownloadFile(url, targetPath, file.Size);
        }

        private string GetBuildBaseUrl(Manifest manifest)
        {
            string baseUrl = manifest?.Base_url;

            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = defaultUrl + "/L2UpdaterWeb/api/build";

            baseUrl = baseUrl.Trim().TrimEnd('/');

            // Segurança contra manifest antigo com caminho errado (/UpdaterWeb sem o L2).
            baseUrl = baseUrl.Replace("http://localhost/UpdaterWeb", defaultUrl);
            baseUrl = baseUrl.Replace("http://l2rp.com/UpdaterWeb", defaultUrl);

            return baseUrl;
        }

        private string BuildPackageUrl(string baseUrl, string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);

            if (string.IsNullOrWhiteSpace(normalized))
                throw new Exception("Caminho do pacote vazio no manifest.");

            string[] segments = normalized
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();

            if (segments.Any(segment => segment == ".."))
                throw new Exception("Manifest contem pacote com caminho invalido: " + relativePath);

            segments = segments.Select(Uri.EscapeDataString).ToArray();

            return baseUrl.TrimEnd('/') + "/" + string.Join("/", segments);
        }

        private string GetPackageFolder(string packagePath)
        {
            string normalized = NormalizeRelativePath(packagePath);

            if (normalized.StartsWith("files/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("files/".Length);

            int lastSlash = normalized.LastIndexOf('/');

            if (lastSlash <= 0)
                return string.Empty;

            return normalized.Substring(0, lastSlash);
        }

        private string GetPackageCachePath(string packagePath)
        {
            string cacheDir = Path.Combine(Application.StartupPath, ".launcher-cache");
            Directory.CreateDirectory(cacheDir);

            string normalized = NormalizeRelativePath(packagePath);
            string fileName = string.IsNullOrWhiteSpace(normalized)
                ? Guid.NewGuid().ToString("N") + ".zip"
                : normalized.Replace('/', '_').Replace('\\', '_');

            foreach (char invalid in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalid, '_');

            if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                fileName += ".zip";

            return Path.Combine(cacheDir, fileName);
        }

        private void VerifyDownloadedHash(string filePath, string expectedHash, string label)
        {
            expectedHash = NormalizeHash(expectedHash);

            if (string.IsNullOrWhiteSpace(expectedHash))
                return;

            string actualHash = ComputeFileSha256(filePath);

            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"{label} corrompido. Hash esperado {expectedHash}, recebido {actualHash}.");
        }

        private async Task DownloadAndExtractSingleFilePackage(Manifest manifest, FileEntry file)
        {
            string baseUrl = GetBuildBaseUrl(manifest);
            string url = BuildPackageUrl(baseUrl, file.Package);
            string tempZip = GetPackageCachePath(file.Package);

            try
            {
                await DownloadFile(url, tempZip, file.Package_size);
                SetActivityProgress("Hash", 100, $"Validando pacote {Path.GetFileName(file.Package)}.");
                await Task.Run(() => VerifyDownloadedHash(tempZip, file.Package_hash, Path.GetFileName(file.Package)));

                string folder = GetPackageFolder(file.Package);
                await Task.Run(() => ExtractZip(tempZip, folder, "Extracao"));
            }
            finally
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
        }

        private async Task DownloadAndExtractFullPackage(Manifest manifest)
        {
            if (manifest.Full_package == null)
                throw new Exception("Pacote full não informado.");

            string baseUrl = GetBuildBaseUrl(manifest);
            string url = BuildPackageUrl(baseUrl, manifest.Full_package.File);
            string zipPath = Path.Combine(Application.StartupPath, Path.GetFileName(manifest.Full_package.File));

            lblStatus.Text = $"Baixando FULL {manifest.Full_package.Version}...";

            try
            {
                await DownloadFile(url, zipPath, manifest.Full_package.Size_bytes);
                SetActivityProgress("Hash", 100, "Validando pacote full.");
                await Task.Run(() => VerifyDownloadedHash(zipPath, manifest.Full_package.Hash, "Pacote full"));

                lblStatus.Text = "Extraindo FULL...";
                await SendLauncherStateToWeb();

                await Task.Run(() => ExtractZip(zipPath, "", "Extracao FULL"));
            }
            finally
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
        }

        private async Task DownloadAndExtractPatch(Manifest manifest, Patch patch)
        {
            string baseUrl = GetBuildBaseUrl(manifest);
            string url = BuildPackageUrl(baseUrl, patch.File);
            string zipPath = Path.Combine(Application.StartupPath, Path.GetFileName(patch.File));

            lblStatus.Text = $"Baixando patch {patch.To_version}...";

            try
            {
                await DownloadFile(url, zipPath, patch.Size_bytes);
                SetActivityProgress("Hash", 100, $"Validando patch {patch.To_version}.");
                await Task.Run(() => VerifyDownloadedHash(zipPath, patch.Hash, "Patch " + patch.To_version));

                lblStatus.Text = $"Extraindo patch {patch.To_version}...";
                await SendLauncherStateToWeb();

                await Task.Run(() => ExtractZip(zipPath, "", "Extracao patch"));
            }
            finally
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
        }

        private void ExtractZip(string zipPath, string fallbackFolder = "", string activityName = "Extracao")
        {
            string root = Path.GetFullPath(Application.StartupPath + Path.DirectorySeparatorChar);

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                long totalBytes = archive.Entries
                    .Where(entry => !string.IsNullOrEmpty(entry.Name))
                    .Sum(entry => Math.Max(0L, entry.Length));

                int totalEntries = Math.Max(1, archive.Entries.Count);
                int entryIndex = 0;
                long extractedBytes = 0;
                DateTime lastUi = DateTime.MinValue;
                Stopwatch stopwatch = Stopwatch.StartNew();
                byte[] buffer = new byte[DownloadBufferSize];

                SetActivityProgress(activityName, 0, $"{activityName}: preparando arquivos.");

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    entryIndex++;

                    string targetName = GetZipEntryTargetName(entry, fallbackFolder);
                    string filePath = Path.GetFullPath(Path.Combine(Application.StartupPath, targetName));

                    if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("ZIP invalido: caminho fora da pasta do launcher.");

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(filePath);
                        continue;
                    }

                    string dir = Path.GetDirectoryName(filePath);

                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    using (Stream source = entry.Open())
                    using (FileStream target = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize))
                    {
                        int read;

                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            target.Write(buffer, 0, read);
                            extractedBytes += read;

                            int progress = totalBytes > 0
                                ? (int)((extractedBytes * 100L) / totalBytes)
                                : (int)((entryIndex * 100.0) / totalEntries);

                            progress = Math.Max(0, Math.Min(progress, 100));

                            DateTime now = DateTime.UtcNow;
                            if ((now - lastUi).TotalMilliseconds >= 180 || progress >= 100)
                            {
                                lastUi = now;
                                double speed = stopwatch.Elapsed.TotalSeconds > 0
                                    ? extractedBytes / stopwatch.Elapsed.TotalSeconds
                                    : 0;

                                string detail = totalBytes > 0
                                    ? $"{activityName}: {progress}% - {FormatSize(extractedBytes)} / {FormatSize(totalBytes)}"
                                    : $"{activityName}: {entryIndex}/{totalEntries} arquivos";

                                SetActivityProgress($"{activityName} {FormatSpeed(speed)}", progress, detail);
                            }
                        }
                    }
                }
            }

            SetActivityProgress(activityName, 100, $"{activityName}: concluida.");
        }

        private string GetZipEntryTargetName(ZipArchiveEntry entry, string fallbackFolder)
        {
            string entryName = NormalizeRelativePath(entry.FullName);
            fallbackFolder = NormalizeRelativePath(fallbackFolder);

            if (!string.IsNullOrWhiteSpace(fallbackFolder) && !entryName.Contains("/"))
                return fallbackFolder + "/" + entryName;

            return entryName;
        }

        private async Task DownloadFile(string url, string destination, long totalSize)
        {
            string dir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string tempDestination = destination + ".download";
            string fileName = Path.GetFileName(destination);

            if (File.Exists(destination))
            {
                FileInfo existing = new FileInfo(destination);

                if (totalSize <= 0 || existing.Length == totalSize)
                {
                    if (File.Exists(tempDestination))
                        File.Delete(tempDestination);

                    SetActivityProgress("Download", 100, $"{fileName}: pacote ja baixado.");
                    return;
                }

                File.Delete(destination);
            }

            Exception lastError = null;

            for (int attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
            {
                try
                {
                    bool completed = await DownloadFileAttempt(url, destination, tempDestination, totalSize, attempt);

                    if (!completed)
                        continue;

                    if (File.Exists(destination))
                        File.Delete(destination);

                    File.Move(tempDestination, destination);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt >= MaxDownloadAttempts || !IsTransientDownloadError(ex))
                        throw;

                    lastError = ex;
                    SetActivityProgress(
                        "Reconectando",
                        totalSize > 0 && File.Exists(tempDestination)
                            ? (int)Math.Min(100, (new FileInfo(tempDestination).Length * 100L) / totalSize)
                            : 0,
                        $"Conexao caiu. Retomando {fileName} ({attempt + 1}/{MaxDownloadAttempts})..."
                    );

                    await Task.Delay(GetDownloadRetryDelay(attempt));
                }
            }

            throw new Exception($"Download interrompido: {fileName}. {lastError?.Message}");
        }

        private async Task<bool> DownloadFileAttempt(string url, string destination, string tempDestination, long totalSize, int attempt)
        {
            long resumeFrom = File.Exists(tempDestination) ? new FileInfo(tempDestination).Length : 0;

            if (totalSize > 0 && resumeFrom > totalSize)
            {
                File.Delete(tempDestination);
                resumeFrom = 0;
            }

            string fileName = Path.GetFileName(destination);
            int startProgress = totalSize > 0 ? (int)Math.Min(100, (resumeFrom * 100L) / totalSize) : 0;

            SetActivityProgress(
                resumeFrom > 0 ? "Retomando" : "Download",
                startProgress,
                resumeFrom > 0
                    ? $"Retomando {fileName} de {FormatSize(resumeFrom)}."
                    : $"Conectando: {url}"
            );

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (resumeFrom > 0)
                    request.Headers.Range = new RangeHeaderValue(resumeFrom, null);

                using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && resumeFrom > 0)
                    {
                        if (totalSize > 0 && resumeFrom == totalSize)
                            return true;

                        File.Delete(tempDestination);
                        return false;
                    }

                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"Falha ao baixar ({(int)response.StatusCode} {response.ReasonPhrase}): {url}");

                    bool appending = resumeFrom > 0 && response.StatusCode == HttpStatusCode.PartialContent;

                    if (resumeFrom > 0 && !appending)
                    {
                        File.Delete(tempDestination);
                        resumeFrom = 0;
                    }

                    long contentLength = response.Content.Headers.ContentLength ?? 0;
                    long expectedSize = totalSize;

                    if (expectedSize <= 0 && response.Content.Headers.ContentRange != null &&
                        response.Content.Headers.ContentRange.Length.HasValue)
                        expectedSize = response.Content.Headers.ContentRange.Length.Value;

                    if (expectedSize <= 0 && contentLength > 0)
                        expectedSize = resumeFrom + contentLength;

                    using (Stream stream = await response.Content.ReadAsStreamAsync())
                    using (FileStream fs = new FileStream(
                        tempDestination,
                        appending ? FileMode.Append : FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        DownloadBufferSize))
                    {
                        byte[] buffer = new byte[DownloadBufferSize];
                        long totalRead = resumeFrom;
                        long sessionStart = resumeFrom;
                        DateTime lastUi = DateTime.MinValue;
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        int read;

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            int progress = expectedSize > 0
                                ? (int)((totalRead * 100L) / expectedSize)
                                : 0;

                            progress = Math.Max(0, Math.Min(progress, 100));

                            DateTime now = DateTime.UtcNow;
                            if ((now - lastUi).TotalMilliseconds >= 180 || totalRead == resumeFrom + read || progress >= 100)
                            {
                                lastUi = now;
                                double speed = stopwatch.Elapsed.TotalSeconds > 0
                                    ? (totalRead - sessionStart) / stopwatch.Elapsed.TotalSeconds
                                    : 0;

                                string detail = expectedSize > 0
                                    ? $"{progress}% - {FormatSize(totalRead)} / {FormatSize(expectedSize)}"
                                    : $"{FormatSize(totalRead)} baixados";

                                SetActivityProgress($"Download {FormatSpeed(speed)}", progress, detail);
                            }
                        }

                        await fs.FlushAsync();
                    }

                    FileInfo downloaded = new FileInfo(tempDestination);

                    if (expectedSize > 0 && downloaded.Length != expectedSize)
                        throw new IOException($"Download incompleto: {fileName} ({FormatSize(downloaded.Length)} de {FormatSize(expectedSize)}).");

                    SetActivityProgress("Download", 100, $"{fileName}: download completo ({FormatSize(downloaded.Length)}).");
                    return true;
                }
            }
        }

        private bool IsTransientDownloadError(Exception ex)
        {
            return ex is IOException ||
                   ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   ex is OperationCanceledException;
        }

        private int GetDownloadRetryDelay(int attempt)
        {
            return Math.Min(6000, 1000 + (attempt * 900));
        }

        private string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / 1024f / 1024f:0.00} MB";

            if (bytes >= 1024)
                return $"{bytes / 1024f:0.00} KB";

            return $"{bytes} B";
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
                return $"{bytesPerSecond / 1024d / 1024d:0.00} MB/s";

            if (bytesPerSecond >= 1024)
                return $"{bytesPerSecond / 1024d:0.00} KB/s";

            return $"{bytesPerSecond:0} B/s";
        }

        private string GetLocalVersion()
        {
            string path = Path.Combine(Application.StartupPath, "version.dat");

            if (!File.Exists(path))
                return "0";

            return File.ReadAllText(path).Trim();
        }

        private void SaveLocalVersion(string version)
        {
            string path = Path.Combine(Application.StartupPath, "version.dat");
            File.WriteAllText(path, version);
        }

        private string ComputeFileSha256(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
        }

        private List<Patch> BuildPatchChain(Manifest manifest, string currentVersion)
        {
            List<Patch> result = new List<Patch>();

            if (manifest == null || manifest.Patches == null || manifest.Patches.Count == 0)
                return result;

            string cursor = currentVersion;

            List<Patch> ordered = manifest.Patches
                .Where(x => string.Equals(x.Patch_type, "incremental", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.From_version)
                .ThenBy(x => x.To_version)
                .ToList();

            while (!string.Equals(cursor, manifest.Latest_version, StringComparison.OrdinalIgnoreCase))
            {
                Patch next = ordered.FirstOrDefault(x =>
                    string.Equals(x.From_version, cursor, StringComparison.OrdinalIgnoreCase));

                if (next == null)
                    return new List<Patch>();

                result.Add(next);
                cursor = next.To_version;
            }

            return result;
        }

        private void SimulateAction(string status, int patchTarget, int downloadTarget)
        {
            lblStatus.Text = status;
            lblDownloadStatus.Text = $"Atualizando interface em {DateTime.Now:HH:mm:ss}";
            SetActivityProgress(status, patchTarget);
            SetCheckProgress(downloadTarget, status);
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using (SettingsDialog dialog = new SettingsDialog(Width, Height))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    Size = dialog.SelectedSize;
                    NotifyBrowserOfResize();
                    AutoZoom();
                }
            }
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            dragging = true;
            dragStart = e.Location;
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (!dragging)
                return;

            Point currentScreenPos = PointToScreen(e.Location);
            Location = new Point(currentScreenPos.X - dragStart.X, currentScreenPos.Y - dragStart.Y);
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        private void CreateDesktopShortcut()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktop, "LineageII.lnk");

                if (File.Exists(shortcutPath))
                    return;

                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);

                shortcut.TargetPath = Application.ExecutablePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                shortcut.IconLocation = Application.ExecutablePath;

                shortcut.Save();
            }
            catch
            {
            }
        }

        private void ApplyTheme()
        {
            BackColor = Theme.Background;

            titleBarPanel.BackColor = Theme.Surface;
            footerPanel.BackColor = Theme.Surface;
            browserHostPanel.BackColor = Theme.Background;
            browserHostPanel.BorderColor = Theme.BorderHot;
            browserHostPanel.BorderThickness = 1;
            browserHostPanel.CornerRadius = 18;

            lblTitle.ForeColor = Theme.Text;

            lblStatus.ForeColor = Theme.Success;
            lblDownloadStatus.ForeColor = Theme.MutedText;

            btnSettings.BackColor = Theme.SurfaceAlt;
            btnSettings.ForeColor = Theme.Text;
            btnSettings.FlatAppearance.BorderColor = Theme.Border;
            btnSettings.FlatAppearance.BorderSize = 1;

            btnMinimize.BackColor = Theme.SurfaceAlt;
            btnMinimize.ForeColor = Theme.Text;
            btnMinimize.FlatAppearance.BorderColor = Theme.Border;
            btnMinimize.FlatAppearance.BorderSize = 1;

            btnClose.BackColor = Theme.SurfaceAlt;
            btnClose.ForeColor = Theme.Text;
            btnClose.FlatAppearance.BorderColor = Theme.Border;
            btnClose.FlatAppearance.BorderSize = 1;

            patchProgress.FillColor = Theme.Orange;
            patchProgress.TrackColor = Color.FromArgb(34, 30, 34);
            patchProgress.BorderColor = Theme.BorderHot;
            patchProgress.TextColor = Theme.Text;
            patchProgress.Caption = "Atividade";

            downloadProgress.FillColor = Theme.Gold;
            downloadProgress.TrackColor = Color.FromArgb(34, 30, 34);
            downloadProgress.BorderColor = Theme.Border;
            downloadProgress.TextColor = Theme.Text;
            downloadProgress.Caption = "Progress";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            notifyIcon?.Dispose();
            notifyMenu?.Dispose();

            base.OnFormClosed(e);
        }

        public class Manifest
        {
            public string Product { get; set; }
            public string Latest_version { get; set; }
            public string Generated_at { get; set; }
            public string Base_url { get; set; }
            public FullPackage Full_package { get; set; }
            public List<Patch> Patches { get; set; }
            public List<FileEntry> Files { get; set; }
        }

        public class FullPackage
        {
            public string Version { get; set; }
            public string File { get; set; }
            public long Size_bytes { get; set; }
            public string Hash { get; set; }
            public int File_count { get; set; }
        }

        public class Patch
        {
            public string Patch_type { get; set; }
            public string From_version { get; set; }
            public string To_version { get; set; }
            public string File { get; set; }
            public long Size_bytes { get; set; }
            public string Hash { get; set; }
            public int File_count { get; set; }
            public string Created_at { get; set; }
        }

        public class FileEntry
        {
            public string Path { get; set; }
            public string Hash { get; set; }
            public long Size { get; set; }
            public string Package { get; set; }
            public long Package_size { get; set; }
            public string Package_hash { get; set; }
            public string Last_version { get; set; }
        }
    }
}
