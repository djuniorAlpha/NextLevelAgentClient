
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NextLevelAgentClient.Core;
using NextLevelAgentClient.Core.Services;
using NextLevelAgentClient.Infrastructure;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace NextLevelAgentClient
{
    public partial class LockForm : Form
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly SessionManager _session;
        private readonly IComputerApiService _apiService;

        private bool _allowClose = false;
        private string _computerUuid = string.Empty;

        private WebView2? webView;
        private NotifyIcon? trayIcon;

        public LockForm()
        {
            InitializeComponent();

            _session = new SessionManager();
            _apiService = new MockComputerApiService();

            BindSessionEvents();

            ConfigureLockScreen();
            ConfigureTrayIcon();

            KeyboardHook.OnDeveloperExit += HandleDeveloperExit;
        }

        private async Task InitializeWebViewAsync()
        {
            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);

            await webView.EnsureCoreWebView2Async();

            CoreWebView2 core = webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
#if !DEBUG
            core.Settings.AreDevToolsEnabled = false;
#endif

            string webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            core.SetVirtualHostNameToFolderMapping("appassets", webRoot, CoreWebView2HostResourceAccessKind.Allow);
            core.WebMessageReceived += OnWebMessageReceived;
            core.Navigate("https://appassets/index.html");
        }

        private void PostToJs(object payload)
        {
            webView?.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
        }

        private void ShowAlert(string alertType, string title, string message)
        {
            PostToJs(new { type = "alert", alertType, title, text = message });
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string? json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            WebMessage? msg;
            try
            {
                msg = JsonSerializer.Deserialize<WebMessage>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return;
            }

            if (msg == null) return;

            switch (msg.Action)
            {
                case "buyTime": _session.ChangeState(MachineState.TimeSelection); break;
                case "login": _session.ChangeState(MachineState.Login); break;
                case "loginRequest": HandleLoginRequest(msg.Username, msg.Password); break;
                case "back": HandleBack(); break;
                case "selectTime": _session.StartPixExpectancy(msg.Minutes); break;
                case "simulatePayment": _session.ConfirmPixPayment(); break;
            }
        }

        private void HandleLoginRequest(string? username, string? password)
        {
            if (username == "admin" && password == "admin")
            {
                _session.ConfirmLogin();
            }
            else
            {
                ShowAlert("error", "Erro de Login", "Credenciais inválidas. Tente novamente.");
            }
        }

        private void HandleBack()
        {
            if (_session.CurrentState == MachineState.WaitingForPix)
                _session.CancelOperation();
            else
                _session.ChangeState(MachineState.InitialBlocked);
        }

        private async Task SynchronizeHardwareAsync()
        {
            PostToJs(new { type = "status", text = "SINCRONIZANDO COM O SERVIDOR...", color = "warning" });

            try
            {
                string macAddress = GetLocalMacAddress();
                string hostname = Dns.GetHostName();
                string ipAddress = GetLocalIpAddress();

                bool isRegistered = await _apiService.IsComputerRegisteredAsync(macAddress);

                if (!isRegistered)
                {
                    PostToJs(new { type = "status", text = "HARDWARE NÃO ENCONTRADO. REGISTRANDO...", color = "warning" });
                    _computerUuid = await _apiService.RegisterComputerAsync(macAddress, hostname, ipAddress);
                    PostToJs(new { type = "status", text = "REGISTRO CONCLUÍDO!", color = "success" });
                    await Task.Delay(1000);
                }
                else
                {
                    PostToJs(new { type = "status", text = "MÁQUINA VERIFICADA", color = "success" });
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                ShowAlert("error", "Falha de Sincronização", $"Erro crítico de rede durante a sincronização do hardware: {ex.Message}");
                PostToJs(new { type = "status", text = "MODO OFFLINE ATIVO", color = "warning" });
            }
            finally
            {
                PostToJs(new { type = "status", text = "ESTA MÁQUINA ESTÁ BLOQUEADA", color = "danger" });
            }
        }

        // Helper method to capture local network IP configuration
        private string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString(); // e.g., "192.168.1.50"
                }
            }
            return "127.0.0.1";
        }

        // Helper method to capture the active network adapter MAC Address
        private string GetLocalMacAddress()
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        return nic.GetPhysicalAddress().ToString(); // e.g., "001A2B3C4D5E"
                    }
                }
            }
            return "000000000000";
        }

        private void BindSessionEvents()
        {
            _session.OnStateChanged += HandleStateChanged;
            _session.OnPixTick += (time) => PostToJs(new { type = "pixTick", text = $"QR Code expira em: {time:mm\\:ss}" });
            _session.OnSessionTick += (time) => trayIcon?.Text = $"CyberManager - Tempo: {time:hh\\:mm\\:ss}";

            _session.OnPixExpired += () => ShowAlert("warning", "Pix Expirado", "O tempo limite para o pagamento expirou. Gerando nova sessão.");
            _session.OnSessionEnded += HandleSessionEnded;

            _session.OnSessionTick += (time) =>
            {
                if (time.TotalSeconds == 15)
                    trayIcon?.ShowBalloonTip(5000, "Atenção!", "Seu tempo está quase acabando! O PC bloqueará em 15 segundos.", ToolTipIcon.Warning);
            };
        }

        private void HandleStateChanged(MachineState state)
        {
            PostToJs(new { type = "state", state = state.ToString() });

            if (state == MachineState.ActiveSession)
            {
                this.Hide();
                trayIcon?.Visible = true;
                trayIcon?.ShowBalloonTip(3000, "Acesso Liberado!", "Aproveite sua sessão", ToolTipIcon.Info);
            }
        }

        private void HandleSessionEnded()
        {
            trayIcon?.Visible = false;
            ConfigureLockScreen();
            this.Show();

            ShowAlert("warning", "Sessão Encerrada", "Seu tempo acabou! O computador será bloqueado.");
        }

        private void ConfigureLockScreen()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1024, 768);
            this.Bounds = bounds;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(15, 15, 25);
        }

        private void ConfigureTrayIcon() => trayIcon = new NotifyIcon { Icon = SystemIcons.Information, Visible = false };

        private void HandleDeveloperExit()
        {
            _allowClose = true;
            _session.CancelOperation();
            if (trayIcon != null) trayIcon?.Visible = false;
            KeyboardHook.Stop();
            RegistryManager.UnlockManagerTask();
            this.TopMost = false;
            KeyboardHook.OnDeveloperExit -= HandleDeveloperExit;
            this.Close();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            KeyboardHook.Start();
            bool hasTaskManagerLockPrivilege = RegistryManager.LockManagerTask();

            await InitializeWebViewAsync();

            if (!hasTaskManagerLockPrivilege)
            {
                ShowAlert("error", "Erro de Privilégios", "O Agente precisa de privilégios de Administrador para bloquear o Gerenciador de Tarefas.");
            }

            await SynchronizeHardwareAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose) { e.Cancel = true; return; }
            if (trayIcon != null) trayIcon?.Visible = false;
            base.OnFormClosing(e);
        }

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x00000080; return cp; }
        }

        private sealed class WebMessage
        {
            public string Action { get; set; } = string.Empty;
            public string? Username { get; set; }
            public string? Password { get; set; }
            public int Minutes { get; set; }
        }
    }
}
