
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NextLevelAgentClient.Core;
using NextLevelAgentClient.Core.Services;
using NextLevelAgentClient.Infrastructure;
using System.Diagnostics;
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
        private string? _apiKey;
        private int? _machineNumber;
        private string? _currentPaymentId;
        private string? _pendingCustomerToken;
        private string? _activeSessionId;
        private int _activeSessionAllocatedSeconds;

        private WebView2? webView;
        private NotifyIcon? trayIcon;
        private System.Windows.Forms.Timer? _heartbeatTimer;
        private RealtimeClient? _realtimeClient;

        public LockForm()
        {
            InitializeComponent();

            _session = new SessionManager();
            _apiService = new ComputerApiService(AppConfig.Current.BackendBaseUrl);

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

            // Navigate() não espera a página carregar: sem isso, mensagens postadas logo em seguida
            // (ex.: machineNumber) podem chegar antes do app.js registrar o listener e se perder.
            var navigationCompleted = new TaskCompletionSource();
            void OnNavigationCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => navigationCompleted.TrySetResult();
            core.NavigationCompleted += OnNavigationCompleted;

            core.Navigate("https://appassets/index.html");
            await navigationCompleted.Task;
            core.NavigationCompleted -= OnNavigationCompleted;
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
                case "changePasswordRequest": HandleChangePasswordRequest(msg.NewPassword, msg.ConfirmPassword); break;
                case "back": HandleBack(); break;
                case "selectTime": HandleSelectTime(msg); break;
                case "redeemToken": _session.ChangeState(MachineState.RedeemToken); break;
                case "redeemTokenRequest": HandleRedeemTokenRequest(msg.Code); break;
            }
        }

        private async void HandleSelectTime(WebMessage msg)
        {
            _session.StartPixExpectancy(msg.Minutes);

            if (string.IsNullOrEmpty(_computerUuid) || string.IsNullOrEmpty(_apiKey))
            {
                ShowAlert("error", "Sem conexão", "Não foi possível gerar a cobrança: máquina não está sincronizada com o servidor.");
                _session.CancelOperation();
                return;
            }

            try
            {
                string? timePackageId = msg.Kind == "package" ? msg.OptionId : null;
                string? hourlyRateId = msg.Kind == "hourly" ? msg.OptionId : null;

                PixPaymentResult result = await _apiService.CreatePixPaymentAsync(_computerUuid, _apiKey, timePackageId, hourlyRateId, msg.Minutes);
                _currentPaymentId = result.PaymentId;

                PostToJs(new { type = "pixData", qrCodeBase64 = result.QrCodeBase64, qrCodeText = result.QrCodeText, amountCents = result.AmountCents });
            }
            catch (Exception ex)
            {
                ShowAlert("error", "Falha ao gerar cobrança", $"Não foi possível gerar a cobrança Pix: {ex.Message}");
                _session.CancelOperation();
            }
        }

        private async void HandleLoginRequest(string? username, string? password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowAlert("error", "Erro de Login", "Informe usuário e senha.");
                return;
            }

            try
            {
                CustomerLoginResult login = await _apiService.LoginCustomerAsync(username, password);

                if (login.Customer.MustChangePassword)
                {
                    _pendingCustomerToken = login.AccessToken;
                    _session.ChangeState(MachineState.ChangePassword);
                    return;
                }

                await StartCustomerSessionAsync(login.AccessToken);
            }
            catch (Exception ex)
            {
                ShowAlert("error", "Erro de Login", ex.Message);
            }
        }

        private async void HandleChangePasswordRequest(string? newPassword, string? confirmPassword)
        {
            if (string.IsNullOrEmpty(_pendingCustomerToken))
            {
                ShowAlert("error", "Sessão expirada", "Faça login novamente.");
                _session.ChangeState(MachineState.Login);
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                ShowAlert("error", "Senha inválida", "A nova senha precisa ter pelo menos 6 caracteres.");
                return;
            }

            if (newPassword != confirmPassword)
            {
                ShowAlert("error", "Senhas não conferem", "A confirmação precisa ser igual à nova senha.");
                return;
            }

            try
            {
                ChangePasswordResult result = await _apiService.ChangeCustomerPasswordAsync(_pendingCustomerToken, newPassword);
                string token = result.AccessToken ?? _pendingCustomerToken;
                _pendingCustomerToken = null;

                await StartCustomerSessionAsync(token);
            }
            catch (Exception ex)
            {
                ShowAlert("error", "Erro ao trocar senha", ex.Message);
            }
        }

        private async Task StartCustomerSessionAsync(string customerAccessToken)
        {
            if (string.IsNullOrEmpty(_computerUuid) || string.IsNullOrEmpty(_apiKey))
            {
                ShowAlert("error", "Sem conexão", "Não foi possível iniciar a sessão: máquina não está sincronizada com o servidor.");
                return;
            }

            try
            {
                StartSessionResult session = await _apiService.StartSessionForCustomerAsync(_computerUuid, _apiKey, customerAccessToken);
                _activeSessionId = session.SessionId;
                _activeSessionAllocatedSeconds = session.AllocatedSeconds;
                _session.ConfirmLogin(session.AllocatedSeconds);
            }
            catch (Exception ex)
            {
                ShowAlert("error", "Erro ao iniciar sessão", ex.Message);
            }
        }

        private async void HandleRedeemTokenRequest(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                ShowAlert("error", "Código inválido", "Informe o código do seu Pix.");
                return;
            }

            if (string.IsNullOrEmpty(_computerUuid) || string.IsNullOrEmpty(_apiKey))
            {
                ShowAlert("error", "Sem conexão", "Não foi possível validar o código: máquina não está sincronizada com o servidor.");
                return;
            }

            try
            {
                RedeemTokenResult result = await _apiService.RedeemPixTokenAsync(_computerUuid, _apiKey, code);
                _activeSessionId = result.SessionId;
                _activeSessionAllocatedSeconds = result.AllocatedSeconds;
                _session.ConfirmLogin(result.AllocatedSeconds);
            }
            catch (Exception ex)
            {
                ShowAlert("error", "Código inválido", ex.Message);
            }
        }

        // Reporta ao backend quanto tempo foi de fato consumido numa sessão de cliente logado,
        // pra descontar do saldo. Best-effort: não interrompe o fluxo se falhar.
        private async Task ReportSessionEndIfNeededAsync()
        {
            if (string.IsNullOrEmpty(_activeSessionId) || string.IsNullOrEmpty(_computerUuid) || string.IsNullOrEmpty(_apiKey))
                return;

            string sessionId = _activeSessionId;
            int consumedSeconds = Math.Max(0, _activeSessionAllocatedSeconds - _session.RemainingSessionTime);
            _activeSessionId = null;

            bool ok = await _apiService.EndSessionAsync(_computerUuid, _apiKey, sessionId, consumedSeconds);
            if (!ok) Debug.WriteLine("Falha ao reportar fim de sessão.");
        }

        private void HandleBack()
        {
            if (_session.CurrentState == MachineState.WaitingForPix)
            {
                _session.CancelOperation();
                _currentPaymentId = null;
            }
            else
            {
                _pendingCustomerToken = null;
                _session.ChangeState(MachineState.InitialBlocked);
            }
        }

        private async Task SynchronizeHardwareAsync()
        {
            PostToJs(new { type = "status", text = "SINCRONIZANDO COM O SERVIDOR...", color = "warning" });

            string macAddress = GetLocalMacAddress();

            try
            {
                MachineCredentials? stored = MachineCredentialsStore.Load();

                if (stored != null && stored.MacAddress == macAddress)
                {
                    _computerUuid = stored.ComputerUuid;
                    _apiKey = stored.ApiKey;
                    _machineNumber = stored.MachineNumber;

                    PostToJs(new { type = "status", text = "MÁQUINA VERIFICADA", color = "success" });
                    await Task.Delay(1000);
                }
                else
                {
                    string hostname = Dns.GetHostName();
                    string ipAddress = GetLocalIpAddress();

                    MachineRegistration? registration = await _apiService.GetRegistrationAsync(macAddress);

                    if (registration == null)
                    {
                        PostToJs(new { type = "status", text = "HARDWARE NÃO ENCONTRADO. REGISTRANDO...", color = "warning" });
                        registration = await _apiService.RegisterComputerAsync(macAddress, hostname, ipAddress);

                        if (string.IsNullOrEmpty(registration.ApiKey))
                            throw new InvalidOperationException("Backend não retornou a chave de API no registro.");

                        _apiKey = registration.ApiKey;
                        MachineCredentialsStore.Save(new MachineCredentials(registration.ComputerUuid, registration.ApiKey, registration.MachineNumber, macAddress));

                        PostToJs(new { type = "status", text = "REGISTRO CONCLUÍDO!", color = "success" });
                        await Task.Delay(1000);
                    }
                    else
                    {
                        // Já existe na base do backend, mas sem chave local (ex.: reinstalação do Windows).
                        // Sem endpoint pra reemitir a chave, a máquina fica identificada mas sem heartbeat autenticado.
                        _computerUuid = registration.ComputerUuid;
                        _machineNumber = registration.MachineNumber;
                        PostToJs(new { type = "machineNumber", number = _machineNumber });
                        ShowAlert("error", "Credenciais ausentes", "Esta máquina já está registrada no servidor, mas a chave de acesso local não foi encontrada. Contate o administrador para reemitir a chave.");
                        return;
                    }

                    _computerUuid = registration.ComputerUuid;
                    _machineNumber = registration.MachineNumber;
                }

                PostToJs(new { type = "machineNumber", number = _machineNumber });
                StartHeartbeat();
                await LoadPricingOptionsAsync();
                await ConnectRealtimeAsync();
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

        private async Task LoadPricingOptionsAsync()
        {
            try
            {
                IReadOnlyList<TimePackageDto> packages = await _apiService.GetTimePackagesAsync();
                IReadOnlyList<HourlyRateDto> hourlyRates = await _apiService.GetHourlyRatesAsync();

                PostToJs(new
                {
                    type = "pricingOptions",
                    packages = packages.Select(p => new { id = p.Id, label = p.Label, minutes = p.Minutes, priceCents = p.PriceCents }),
                    hourlyRates = hourlyRates.Select(r => new { id = r.Id, label = r.Label, ratePerHourCents = r.RatePerHourCents }),
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao carregar pacotes/tarifas: {ex.Message}");
                PostToJs(new { type = "pricingOptions", packages = Array.Empty<object>(), hourlyRates = Array.Empty<object>() });
            }
        }

        private void StartHeartbeat()
        {
            if (string.IsNullOrEmpty(_computerUuid) || string.IsNullOrEmpty(_apiKey)) return;

            _heartbeatTimer ??= new System.Windows.Forms.Timer { Interval = 10_000 };
            _heartbeatTimer.Tick -= OnHeartbeatTick;
            _heartbeatTimer.Tick += OnHeartbeatTick;
            _heartbeatTimer.Start();
        }

        private async void OnHeartbeatTick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_computerUuid) || string.IsNullOrEmpty(_apiKey)) return;

            string status = MapStateToBackendStatus(_session.CurrentState);
            bool ok = await _apiService.SendHeartbeatAsync(_computerUuid, _apiKey, status);
            if (!ok) Debug.WriteLine("Heartbeat falhou.");
        }

        private static string MapStateToBackendStatus(MachineState state) => state switch
        {
            MachineState.TimeSelection => "time_selection",
            MachineState.WaitingForPix => "waiting_pix",
            MachineState.ActiveSession => "active",
            _ => "locked",
        };

        private async Task ConnectRealtimeAsync()
        {
            if (string.IsNullOrEmpty(_apiKey)) return;

            try
            {
                _realtimeClient = new RealtimeClient(AppConfig.Current.BackendBaseUrl, _apiKey);
                _realtimeClient.OnPaymentConfirmed += HandlePaymentConfirmedFromSocket;
                _realtimeClient.OnForceAction += HandleForceActionFromSocket;
                await _realtimeClient.ConnectAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao conectar ao WebSocket de tempo real: {ex.Message}");
            }
        }

        // Chamado pela biblioteca Socket.IO numa thread própria - precisa voltar pra thread da UI.
        private void HandlePaymentConfirmedFromSocket(string paymentId, string? tokenCode, string? sessionId)
        {
            if (paymentId != _currentPaymentId) return;
            this.BeginInvoke(new MethodInvoker(() => CompletePixPayment(tokenCode, sessionId)));
        }

        private void CompletePixPayment(string? tokenCode, string? sessionId)
        {
            if (_session.CurrentState != MachineState.WaitingForPix) return;
            _currentPaymentId = null;

            // O backend já abre a sessão nesta máquina vinculada ao token assim que aprova o Pix.
            // Guardamos o id pra poder reportar quanto foi consumido lá na frente (ReportSessionEndIfNeededAsync),
            // o que faz o backend descontar do saldo do token - sem isso, o saldo nunca decrementaria
            // para quem paga e usa o tempo na própria máquina (só funcionaria pro resgate em outra máquina).
            if (!string.IsNullOrEmpty(sessionId))
            {
                _activeSessionId = sessionId;
                _activeSessionAllocatedSeconds = _session.RemainingSessionTime;
            }

            // Todo pagamento Pix aprovado gera um código de saldo restante - se o cliente não usar
            // todo o tempo, esse código pode ser resgatado depois em qualquer máquina via
            // "Tenho um código" (start-with-token).
            if (!string.IsNullOrEmpty(tokenCode))
                ShowPixTokenCode(tokenCode);

            _session.ConfirmPixPayment();
        }

        private void ShowPixTokenCode(string tokenCode)
        {
            try { Clipboard.SetText(tokenCode); } catch { /* melhor esforço: área de transferência pode estar indisponível */ }

            MessageBox.Show(
                this,
                $"Guarde este código: {tokenCode}\n\nSe sobrar tempo não utilizado ao final da sua sessão, use-o em qualquer computador desta lan house para continuar de onde parou.\n\n(Já copiado para a área de transferência.)",
                "Seu código Pix",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // Chamado pela biblioteca Socket.IO numa thread própria - precisa voltar pra thread da UI.
        private void HandleForceActionFromSocket(string action)
        {
            this.BeginInvoke(new MethodInvoker(() => HandleForceAction(action)));
        }

        private async void HandleForceAction(string action)
        {
            switch (action)
            {
                case "lock":
                    _currentPaymentId = null;
                    await ReportSessionEndIfNeededAsync();
                    _session.CancelOperation();
                    trayIcon?.Visible = false;
                    ConfigureLockScreen();
                    this.Show();
                    ShowAlert("warning", "Bloqueado", "Esta máquina foi bloqueada pelo atendente.");
                    break;
                case "unlock":
                    _currentPaymentId = null;
                    await ReportSessionEndIfNeededAsync();
                    _session.ForceUnlock();
                    break;
                case "shutdown":
                    // Precisa terminar de reportar ANTES de desligar: "shutdown /t 0" derruba o processo
                    // imediatamente, então um fire-and-forget aqui nunca chegaria a sair da máquina.
                    await ReportSessionEndIfNeededAsync();
                    try
                    {
                        Process.Start("shutdown", "/s /t 0 /f");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Falha ao desligar a máquina: {ex.Message}");
                    }
                    break;
            }
        }

        // Fallback caso o WebSocket falhe: consulta o status a cada 5s enquanto aguarda o Pix.
        private async void HandlePixTickPoll(TimeSpan remaining)
        {
            if (string.IsNullOrEmpty(_currentPaymentId) || string.IsNullOrEmpty(_apiKey)) return;
            if ((int)remaining.TotalSeconds % 5 != 0) return;

            try
            {
                PixPaymentStatus status = await _apiService.GetPaymentStatusAsync(_apiKey, _currentPaymentId);
                if (status.Status == "approved")
                    CompletePixPayment(status.PixToken?.Code, status.Session?.Id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao consultar status do pagamento: {ex.Message}");
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
            _session.OnPixTick += (time) => PostToJs(new { type = "pixTick", text = $"{time:mm\\:ss}" });
            _session.OnPixTick += HandlePixTickPoll;
            _session.OnSessionTick += (time) => trayIcon?.Text = $"Next Level Gaming House - Tempo: {time:hh\\:mm\\:ss}";

            _session.OnPixExpired += () =>
            {
                _currentPaymentId = null;
                ShowAlert("warning", "Pix Expirado", "O tempo limite para o pagamento expirou. Gerando nova sessão.");
            };
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

        private async void HandleSessionEnded()
        {
            await ReportSessionEndIfNeededAsync();

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

        private void ConfigureTrayIcon()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Encerrar sessão", null, HandleEndSessionRequestedByUser);

            trayIcon = new NotifyIcon { Icon = LoadTrayIcon(), Visible = false, ContextMenuStrip = menu };
        }

        // Item do menu do tray icon: permite ao próprio cliente encerrar a sessão ativa e bloquear
        // a máquina, sem depender de uma ação remota do atendente.
        private async void HandleEndSessionRequestedByUser(object? sender, EventArgs e)
        {
            if (_session.CurrentState != MachineState.ActiveSession) return;

            DialogResult confirm = MessageBox.Show(
                "Deseja realmente encerrar sua sessão agora? A máquina será bloqueada.",
                "Encerrar sessão",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            await ReportSessionEndIfNeededAsync();

            _currentPaymentId = null;
            _session.CancelOperation();
            trayIcon!.Visible = false;
            ConfigureLockScreen();
            this.Show();
            ShowAlert("info", "Sessão encerrada", "Sua sessão foi encerrada. Obrigado por jogar com a gente!");
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "assets", "tray-icon.ico");
                return File.Exists(path) ? new Icon(path) : SystemIcons.Information;
            }
            catch
            {
                return SystemIcons.Information;
            }
        }

        private async void HandleDeveloperExit()
        {
            _allowClose = true;
            // Precisa esperar terminar antes de fechar: com fire-and-forget aqui, o this.Close() logo
            // abaixo podia derrubar o processo antes do HTTP de fim de sessão sair, perdendo o report.
            await ReportSessionEndIfNeededAsync();
            _session.CancelOperation();
            _heartbeatTimer?.Stop();
            if (_realtimeClient != null) _ = _realtimeClient.DisposeAsync();
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
            PostToJs(new { type = "environment", value = AppConfig.Current.Environment.ToString().ToUpperInvariant() });

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
            public string? Kind { get; set; }
            public string? OptionId { get; set; }
            public string? NewPassword { get; set; }
            public string? ConfirmPassword { get; set; }
            public string? Code { get; set; }
        }
    }
}
