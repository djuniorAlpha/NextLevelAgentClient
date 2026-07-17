
using NextLevelAgentClient.Core;
using NextLevelAgentClient.Core.Modal;
using NextLevelAgentClient.Core.Services;
using NextLevelAgentClient.Infrastructure;
using System.Net;
using System.Net.NetworkInformation;

namespace NextLevelAgentClient
{
    public partial class LockForm : Form
    {
        private readonly SessionManager _session;
        private readonly IComputerApiService _apiService;

        private bool _allowClose = false;
        private string _computerUuid = string.Empty;

        // UI Components
        private Panel? mainPanel;
        private Panel? pnlBlocked, pnlTimeSelection, pnlPix, pnlLogin;
        private Label? lblTitle, lblStatus, lblInstructions, lblTimeSubtitle, lblPixSubtitle, lblPixCounter, lblLogin;
        private FlowLayoutPanel? timeOptionsContainer;
        private Button? btnBuyTime, btnBack, btnSimulatePayment, btnLogin, btnLoginRequest;
        private TextBox? txtPixCode, txtUsername, txtPassword;
        private NotifyIcon? trayIcon;

        public LockForm()
        {
            InitializeComponent();

            _session = new SessionManager();
            _apiService = new MockComputerApiService();


            BindSessionEvents();

            ConfigureLockScreen();
            CreateVisualComponents();
            ConfigureTrayIcon();

            KeyboardHook.OnDeveloperExit += HandleDeveloperExit;
        }

        private async Task SynchronizeHardwareAsync()
        {
            lblStatus?.Text = "SYNCHRONIZING WITH SERVER...";
            lblStatus?.ForeColor = Color.Orange;
            btnBuyTime?.Enabled = false;

            try
            {
                string macAddress = GetLocalMacAddress();
                string hostname = Dns.GetHostName();
                string ipAddress = GetLocalIpAddress();

                bool isRegistered = await _apiService.IsComputerRegisteredAsync(macAddress);

                if (!isRegistered)
                {
                    lblStatus?.Text = "HARDWARE NOT FOUND. REGISTERING...";
                    _computerUuid = await _apiService.RegisterComputerAsync(macAddress, hostname, ipAddress);
                    lblStatus?.Text = "REGISTRATION SUCCESSFUL!";
                    lblStatus?.ForeColor = Color.Green;
                    await Task.Delay(1000);
                }
                else
                {
                    lblStatus?.Text = "MACHINE RECORD VERIFIED";
                    lblStatus?.ForeColor = Color.Green;
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Critical Network Error during hardware synchronization: {ex.Message}",
                                "Sync Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus?.Text = "OFFLINE MODE ACTIVE";
                lblStatus?.ForeColor = Color.DarkOrange;
            }
            finally
            {
                // Restore UI state to standard blocked screen layout
                lblStatus?.Text = "THIS MACHINE IS LOCKED";
                lblStatus?.ForeColor = Color.Red;
                btnBuyTime?.Enabled = true;
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
            _session.OnStateChanged += RenderState;
            _session.OnPixTick += (time) => lblPixCounter?.Text = $"QR Code expira em: {time:mm\\:ss}";
            _session.OnSessionTick += (time) => trayIcon?.Text = $"CyberManager - Tempo: {time:hh\\:mm\\:ss}";

            _session.OnPixExpired += () => MessageBox.Show("O tempo limite para o pagamento expirou. Gerando nova sessão.", "Pix Expirado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _session.OnSessionEnded += HandleSessionEnded;

            _session.OnSessionTick += (time) =>
            {
                if (time.TotalSeconds == 15)
                    trayIcon?.ShowBalloonTip(5000, "Atenção!", "Seu tempo está quase acabando! O PC bloqueará em 15 segundos.", ToolTipIcon.Warning);
            };
        }

        private void RenderState(MachineState state)
        {
            pnlBlocked?.Visible = (state == MachineState.InitialBlocked);
            pnlTimeSelection?.Visible = (state == MachineState.TimeSelection);
            pnlPix?.Visible = (state == MachineState.WaitingForPix);
            pnlLogin?.Visible = (state == MachineState.Login);

            btnBack?.Visible = (state != MachineState.ActiveSession && state != MachineState.InitialBlocked);

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

            MessageBox.Show("Seu tempo acabou! O computador será bloqueado.", "Sessão Encerrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void BtnBuyTime_Click(object? sender, EventArgs e) => _session.ChangeState(MachineState.TimeSelection);

        private void BtnLogin_Click(object? sender, EventArgs e) => _session.ChangeState(MachineState.Login);

        logout
        private async void BtnLoginRequest_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername?.Text) || string.IsNullOrWhiteSpace(txtPassword?.Text))
            {
                MessageBox.Show("Por favor, preencha ambos os campos de usuário e senha.", "Campos Vazios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LoginResponse loginResponse = await _apiService.SendLoginRequestAsync(GetLocalMacAddress(), txtUsername?.Text, txtPassword?.Text);
            if (loginResponse._isValid)
            {
                _session.ConfirmLogin(loginResponse._sessionTime);
                txtUsername.Clear();
                txtPassword.Clear();
                return;
            }

            MessageBox.Show("Credenciais inválidas. Tente novamente.", "Erro de Login", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            if (_session.CurrentState == MachineState.WaitingForPix)
                _session.CancelOperation();
            else if (_session.CurrentState == MachineState.TimeSelection)
                _session.ChangeState(MachineState.InitialBlocked);
            else
                _session.ChangeState(MachineState.InitialBlocked);

        }

        private void TimeOption_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is int minutes)
            {
                _session.StartPixExpectancy(minutes);
            }
        }

        private void BtnSimulatePayment_Click(object? sender, EventArgs e) => _session.ConfirmPixPayment();

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

        private static Image? LoadLogoImage()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Images", "Logo.png");
                return File.Exists(path) ? Image.FromFile(path) : null;
            }
            catch
            {
                return null;
            }
        }

        private void CreateVisualComponents()
        {
            this.SuspendLayout();

            mainPanel = new Panel { Size = new Size(500, 600), BackColor = Color.FromArgb(24, 24, 38) };
            mainPanel.Location = new Point((this.Width - mainPanel.Width) / 2, (this.Height - mainPanel.Height) / 2);
            mainPanel.SuspendLayout();
            this.Controls.Add(mainPanel);

            var pictureBox = new PictureBox { Name = "pictureBoxLogo", Size = new Size(70, 80), Margin = new Padding(0, 0, 0, 0),  SizeMode = PictureBoxSizeMode.Zoom, Image = LoadLogoImage() };
            lblTitle = new Label { Text = "CyberManager", Font = new Font("Segoe UI", 28, FontStyle.Bold), ForeColor = Color.White, Size = new Size(300, 60), Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft};

            int headerWidth = pictureBox.Width + pictureBox.Margin.Horizontal + lblTitle.Width + lblTitle.Margin.Horizontal;
            var headerContainer = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Size = new Size(headerWidth, 80),
                Location = new Point((mainPanel.Width - headerWidth) / 2, 40),
                BackColor = Color.Transparent
            };
            headerContainer.Controls.Add(pictureBox);
            headerContainer.Controls.Add(lblTitle);
            mainPanel.Controls.Add(headerContainer);

            var size = new Size(mainPanel.Width, 500);
            var panelBounds = new Rectangle(Point.Empty, size);

            // Estado: InitialBlocked
            pnlBlocked = new Panel { Bounds = panelBounds };
            lblStatus = new Label { Text = "ESTA MÁQUINA ESTÁ BLOQUEADA", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.Red, Size = new Size(mainPanel.Width, 30), Location = new Point(0, 130), TextAlign = ContentAlignment.MiddleCenter }; pnlBlocked.Controls.Add(lblStatus);
            lblInstructions = new Label { Text = "Para liberar o acesso e começar a navegar, clique no botão abaixo para escolher seu tempo e realizar o pagamento via Pix.", Font = new Font("Segoe UI", 11), ForeColor = Color.LightGray, Size = new Size(mainPanel.Width - 80, 60), Location = new Point(40, 200), TextAlign = ContentAlignment.MiddleCenter };
            pnlBlocked.Controls.Add(lblInstructions);

            btnLogin = new Button { Text = "🔑 LOGIN", Font = new Font("Segoe UI", 14, FontStyle.Bold), BackColor = Color.DarkBlue, ForeColor = Color.White, Size = new Size(300, 60), Location = new Point(100, 400), FlatStyle = FlatStyle.Flat };
            btnLogin.Click += BtnLogin_Click;
            pnlBlocked.Controls.Add(btnLogin);

            btnBuyTime = new Button { Text = "🛒 COMPRAR TEMPO", Font = new Font("Segoe UI", 14, FontStyle.Bold), BackColor = Color.Green, ForeColor = Color.White, Size = new Size(300, 60), Location = new Point(100, 300), FlatStyle = FlatStyle.Flat };
            btnBuyTime.Click += BtnBuyTime_Click;
            pnlBlocked.Controls.Add(btnBuyTime);
            mainPanel.Controls.Add(pnlBlocked);

            // Estado: TimeSelection
            pnlTimeSelection = new Panel { Bounds = panelBounds, Visible = false };
            lblTimeSubtitle = new Label { Text = "Selecione o tempo de uso desejado:", Font = new Font("Segoe UI", 14), ForeColor = Color.White, Size = new Size(mainPanel.Width, 40), Location = new Point(0, 120), TextAlign = ContentAlignment.MiddleCenter }; pnlTimeSelection.Controls.Add(lblTimeSubtitle);
            timeOptionsContainer = new FlowLayoutPanel { Size = new Size(400, 200), Location = new Point(50, 180) }; pnlTimeSelection.Controls.Add(timeOptionsContainer);

            Button btn1Min = new Button { Text = "1 Minute (Test) - R$ 0,10", Size = new Size(380, 50), BackColor = Color.DimGray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Tag = 1 };
            btn1Min.Click += TimeOption_Click;
            timeOptionsContainer.Controls.Add(btn1Min);
            mainPanel.Controls.Add(pnlTimeSelection);

            // Estado: WaitingForPix
            pnlPix = new Panel { Bounds = panelBounds, Visible = false };
            lblPixSubtitle = new Label { Text = "Escaneie o QR Code para pagar", Font = new Font("Segoe UI", 14), ForeColor = Color.White, Size = new Size(mainPanel.Width, 30), Location = new Point(0, 110), TextAlign = ContentAlignment.MiddleCenter }; pnlPix.Controls.Add(lblPixSubtitle);
            txtPixCode = new TextBox { Text = "[ QR CODE PIX ]", Location = new Point(50, 320), Size = new Size(300, 25) }; pnlPix.Controls.Add(txtPixCode);
            lblPixCounter = new Label { Text = "O QR Code expira em: 05:00", ForeColor = Color.Orange, Location = new Point(0, 360), Size = new Size(mainPanel.Width, 25), TextAlign = ContentAlignment.MiddleCenter }; pnlPix.Controls.Add(lblPixCounter);

            btnSimulatePayment = new Button { Text = "🟢 SIMULAR: PIX PAGO", BackColor = Color.Blue, ForeColor = Color.White, Location = new Point(100, 400), Size = new Size(300, 45), FlatStyle = FlatStyle.Flat };
            btnSimulatePayment.Click += BtnSimulatePayment_Click;
            pnlPix.Controls.Add(btnSimulatePayment);
            mainPanel.Controls.Add(pnlPix);

            // Estado: Login
            pnlLogin = new Panel { Bounds = panelBounds, Visible = false };
            lblLogin = new Label { Text = "Faça login", Font = new Font("Segoe UI", 10), ForeColor = Color.LightGray, Size = new Size(mainPanel.Width, 30), Location = new Point(0, 500), TextAlign = ContentAlignment.MiddleCenter };
            txtUsername = new TextBox { PlaceholderText = "Usuário", Location = new Point(100, 320), Size = new Size(300, 25) };
            txtPassword = new TextBox { PlaceholderText = "Senha", Location = new Point(100, 360), Size = new Size(300, 25), PasswordChar = '*' };
            btnLoginRequest = new Button { Text = "Login", BackColor = Color.DarkGreen, ForeColor = Color.White, Location = new Point(100, 390), Size = new Size(300, 30), FlatStyle = FlatStyle.Flat };
            btnLoginRequest.Click += BtnLoginRequest_Click;

            pnlLogin.Controls.Add(lblLogin);
            pnlLogin.Controls.Add(txtUsername);
            pnlLogin.Controls.Add(txtPassword);
            pnlLogin.Controls.Add(btnLoginRequest);
            mainPanel.Controls.Add(pnlLogin);

            // Compartilhado entre estados (não pertence a um único painel)
            btnBack = new Button { Text = "← Voltar", ForeColor = Color.Gray, Location = new Point(20, 540), FlatStyle = FlatStyle.Flat, Visible = false };
            btnBack.Click += BtnBack_Click;
            mainPanel.Controls.Add(btnBack);

            mainPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

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
            RegistryManager.LockManagerTask();

            // Fire and forget hardware sync without freezing the UI layout thread
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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                if (_session.CurrentState == MachineState.Login)
                {
                    btnLoginRequest?.PerformClick();
                }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
