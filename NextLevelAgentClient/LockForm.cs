
namespace NextLevelAgentClient
{
    public partial class LockForm : Form
    {
        private bool _allowClosed = false;
        private bool _machineUnlocked = false;

        private Panel centralPanel;
        private Label lblTitle;
        private Label lblStatus;
        private Button btnBuyTimer;
        private Label lblInstruction;

        private Label lblTimerSubtitle;
        private FlowLayoutPanel containerTimerOptions;
        private Button btnBack;

        private Label lblPixSubtitle;
        private Panel panelQRCode;
        private TextBox txtPixCopiaCola;
        private Button btnCopyPix;
        private Label lblCounterPix;
        private System.Windows.Forms.Timer timerPix;
        private int timeRemainPix = 300;

        private Button btnSimulatePayment;
        private Label lblSessionTime;
        private System.Windows.Forms.Timer SessiontTimer;
        private int SessionTimeRemain = 0;

        private NotifyIcon trayIcon;

        public LockForm()
        {
            InitializeComponent();
            ConfigureLockScreen();
            CreateVisualComponents();
            ConfigureTrayIcon();

            KeyboardHook.OnDeveloperExit += ClosedByDeveloper;
        }

        private void ClosedByDeveloper()
        {
            _allowClosed = true;

            if (timerPix != null) timerPix.Stop();
            if (SessiontTimer != null) SessiontTimer.Stop();

            if (trayIcon != null) trayIcon.Visible = false;

            KeyboardHook.Stop();
            RegistryManager.UnlockManagerTask();

            KeyboardHook.OnDeveloperExit -= ClosedByDeveloper;

            this.Close();
        }

        private void ConfigureTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                // Usa o ícone padrão do sistema (informação) para testes. 
                // Em produção, substitua por: new Icon("caminho/seu_icone.ico")
                Icon = SystemIcons.Information,
                Visible = false
            };

            trayIcon.DoubleClick += (s, e) => {
                if (_machineUnlocked)
                {
                    TimeSpan t = TimeSpan.FromSeconds(SessionTimeRemain);
                    trayIcon.ShowBalloonTip(3000, "CyberManager", $"Você ainda tem {t.ToString(@"hh\:mm\:ss")} de uso.", ToolTipIcon.Info);
                }
            };
        }

        private void ConfigureLockScreen()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;

            this.Bounds = Screen.PrimaryScreen.Bounds;

            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(15, 15, 25);
        }

        private void CreateVisualComponents()
        {
            centralPanel = new Panel
            {
                Size = new Size(500, 600),
                BackColor = Color.FromArgb(24, 24, 38),
                BorderStyle = BorderStyle.None,
            };

            centralPanel.Location = new Point(
                (this.Width - centralPanel.Width) / 2,
                (this.Height - centralPanel.Height) / 2
            );

            this.Controls.Add(centralPanel);

            ApplyRegionRounded(centralPanel, 20);

            lblTitle = new Label
            {
                Text = "💻 CyberManager",
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(centralPanel.Width, 60),
                Location = new Point(0, 50),
                TextAlign = ContentAlignment.MiddleCenter
            };
            centralPanel.Controls.Add(lblTitle);

            lblStatus = new Label
            {
                Text = "ESTA MÁQUINA ESTÁ BLOQUEADA",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 75, 75),
                AutoSize = false,
                Size = new Size(centralPanel.Width, 30),
                Location = new Point(0, 130),
                TextAlign = ContentAlignment.MiddleCenter
            };
            centralPanel.Controls.Add(lblStatus);

            lblInstruction = new Label
            {
                Text = "Para liberar o acesso e começar a navegar, clique no botão abaixo para escolher seu tempo e realizar o pagamento via Pix.",
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 200),
                AutoSize = false,
                Size = new Size(centralPanel.Width - 80, 80),
                Location = new Point(40, 200),
                TextAlign = ContentAlignment.MiddleCenter
            };
            centralPanel.Controls.Add(lblInstruction);

            btnBuyTimer = new Button
            {
                Text = "🛒 COMPRAR TEMPO",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 180, 110), // Verde Pix profissional
                ForeColor = Color.White,
                Size = new Size(300, 60),
                Location = new Point((centralPanel.Width - 300) / 2, 340),
                FlatStyle = FlatStyle.Flat
            };
            btnBuyTimer.FlatAppearance.BorderSize = 0;
            btnBuyTimer.Cursor = Cursors.Hand;
            btnBuyTimer.Click += BtnBuyTime_Click;
            centralPanel.Controls.Add(btnBuyTimer);

            lblTimerSubtitle = new Label
            {
                Text = "Selecione o tempo de uso desejado:",
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                ForeColor = Color.White,
                Size = new Size(centralPanel.Width, 40),
                Location = new Point(0, 130),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            centralPanel.Controls.Add(lblTimerSubtitle);

            containerTimerOptions = new FlowLayoutPanel
            {
                Size = new Size(400, 280),
                Location = new Point(50, 180),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Visible = false
            };
            centralPanel.Controls.Add(containerTimerOptions);

            CreateTimerCard("1 Hora", "R$ 5,00", 1);
            CreateTimerCard("2 Horas", "R$ 9,00", 120);
            CreateTimerCard("3 Horas (Desconto)", "R$ 12,00", 180);

            btnBack = new Button
            {
                Text = "← Voltar",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(150, 150, 170),
                Size = new Size(100, 40),
                Location = new Point(20, 530),
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Cursor = Cursors.Hand;
            btnBack.Click += BtnBack_Click;
            centralPanel.Controls.Add(btnBack);

            lblPixSubtitle = new Label
            {
                Text = "Escaneie o QR Code para pagar",
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                ForeColor = Color.White,
                Size = new Size(centralPanel.Width, 30),
                Location = new Point(0, 110),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            centralPanel.Controls.Add(lblPixSubtitle);

            panelQRCode = new Panel
            {
                Size = new Size(200, 200),
                Location = new Point((centralPanel.Width - 200) / 2, 160),
                BackColor = Color.White, 
                Visible = false
            };
            centralPanel.Controls.Add(panelQRCode);

            Label lblPlaceholderQR = new Label
            {
                Text = "[ QR CODE PIX ]",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelQRCode.Controls.Add(lblPlaceholderQR);

            txtPixCopiaCola = new TextBox
            {
                Text = "00020101021226870014br.gov.bcb.pix2565api.mercadopago.com...",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(270, 35),
                Location = new Point(60, 390),
                ReadOnly = true,
                Visible = false
            };
            centralPanel.Controls.Add(txtPixCopiaCola);

            btnCopyPix = new Button
            {
                Text = "Copiar",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 180, 110),
                ForeColor = Color.White,
                Size = new Size(100, 25),
                Location = new Point(340, 390),
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            btnCopyPix.FlatAppearance.BorderSize = 0;
            btnCopyPix.Cursor = Cursors.Hand;
            btnCopyPix.Click += BtnCopyPix_Click;
            centralPanel.Controls.Add(btnCopyPix);

            lblCounterPix = new Label
            {
                Text = "O QR Code expira em: 05:00",
                Font = new Font("Segoe UI", 11, FontStyle.Italic),
                ForeColor = Color.FromArgb(255, 180, 0),
                Size = new Size(centralPanel.Width, 30),
                Location = new Point(0, 440),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            centralPanel.Controls.Add(lblCounterPix);

            timerPix = new System.Windows.Forms.Timer { Interval = 1000 };
            timerPix.Tick += TimerPix_Tick;

            btnSimulatePayment = new Button
            {
                Text = "🟢 SIMULAR: PIX PAGO",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.DodgerBlue,
                ForeColor = Color.White,
                Size = new Size(300, 45),
                Location = new Point((centralPanel.Width - 300) / 2, 460),
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            btnSimulatePayment.FlatAppearance.BorderSize = 0;
            btnSimulatePayment.Cursor = Cursors.Hand;
            btnSimulatePayment.Click += BtnSimulatePayment_Click;
            centralPanel.Controls.Add(btnSimulatePayment);

            lblSessionTime = new Label
            {
                Text = "Tempo Restante: 00:00:00",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(20, 20, 30),
                ForeColor = Color.SpringGreen,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            this.Controls.Add(lblSessionTime);

            SessiontTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            SessiontTimer.Tick += SessionTimer_Tick;


        }

        private void CreateTimerCard(string labelTimer, string textValue, int minutes)
        {
            Button btnOption = new Button
            {
                Text = $"{labelTimer} — {textValue}",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.White,
                Size = new Size(385, 65),
                Margin = new Padding(0, 0, 0, 15),
                FlatStyle = FlatStyle.Flat,
                Tag = minutes
            };
            btnOption.FlatAppearance.BorderSize = 1;
            btnOption.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 90);
            btnOption.Cursor = Cursors.Hand;
            btnOption.Click += OptionTimer_Click;

            containerTimerOptions.Controls.Add(btnOption);
        }

        private void BtnSimulatePayment_Click(object sender, EventArgs e)
        {
            timerPix.Stop();
            _machineUnlocked = true;

            KeyboardHook.Stop();
            RegistryManager.UnlockManagerTask();

            this.Hide();

            trayIcon.Visible = true;
            TimeSpan t = TimeSpan.FromSeconds(SessionTimeRemain);
            trayIcon.Text = $"CyberManager - Tempo: {t.ToString(@"hh\:mm\:ss")}";

            trayIcon.ShowBalloonTip(3000, "Acesso Liberado!", $"Bom uso! Você tem {t.ToString(@"hh\:mm\:ss")} restantes.", ToolTipIcon.Info);

            SessiontTimer.Start();
        }

        private void SessionTimer_Tick(object sender, EventArgs e)
        {
            if (SessionTimeRemain <= 0)
            {
                SessiontTimer.Stop();
                _machineUnlocked = false;

                if (trayIcon != null)
                    trayIcon.Visible = false;


                ConfigureLockScreen();
                this.Show();
                centralPanel.Visible = true;
                ToogleScreens(showInicial: true, showTime: false, showPix: false);

                KeyboardHook.Start();
                RegistryManager.LockManagerTask();

                MessageBox.Show("Seu tempo acabou! O computador será bloqueado.", "Sessão Encerrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;
            }

            
            SessionTimeRemain--;
            TimeSpan tempo = TimeSpan.FromSeconds(SessionTimeRemain);

            if (trayIcon != null && trayIcon.Visible)
            {
                trayIcon.Text = $"CyberManager - Tempo: {tempo.ToString(@"hh\:mm\:ss")}";
            }

            if (SessionTimeRemain == 15 && trayIcon != null)
            {
                trayIcon.ShowBalloonTip(5000, "Atenção!", "Seu tempo está quase acabando! O PC bloqueará em 15 segundos.", ToolTipIcon.Warning);
            }
        }

        private void OptionTimer_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;

            SessionTimeRemain = (int)btn.Tag * 60;

            ToogleScreens(showInicial: false, showTime: false, showPix: true);

            timeRemainPix = 300;
            lblCounterPix.Text = "O QR Code expira em: 05:00";
            timerPix.Start();
        }

        private void BtnBack_Click(object sender, EventArgs e)
        {
            
            if (panelQRCode.Visible)
            {
                timerPix.Stop();
                ToogleScreens(showInicial: false, showTime: true, showPix: false);
            }
            else if (containerTimerOptions.Visible)
            {
                ToogleScreens(showInicial: true, showTime: false, showPix: false);
            }
        }

        private void BtnBuyTime_Click(object sender, EventArgs e)
        {
            ToogleScreens(showInicial: false, showTime: true, showPix: false);
        }

        private void BtnCopyPix_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(txtPixCopiaCola.Text);
            MessageBox.Show("Código Pix Copia e Cola copiado para a área de transferência!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TimerPix_Tick(object sender, EventArgs e)
        {
            if (timeRemainPix > 0)
            {
                timeRemainPix--;
                TimeSpan time = TimeSpan.FromSeconds(timeRemainPix);
                lblCounterPix.Text = $"O QR Code expira em: {time.ToString(@"mm\:ss")}";
            }
            else
            {
                timerPix.Stop();
                MessageBox.Show("O tempo limite para o pagamento expirou. Gerando nova sessão.", "Pix Expirado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ToogleScreens(showInicial: true, showTime: false, showPix: false);
            }
        }

        private void ToogleScreens(bool showInicial, bool showTime, bool showPix)
        {
            lblStatus.Visible = showInicial;
            lblInstruction.Visible = showInicial;
            btnBuyTimer.Visible = showInicial;

            lblTimerSubtitle.Visible = showTime;
            containerTimerOptions.Visible = showTime;

            lblPixSubtitle.Visible = showPix;
            panelQRCode.Visible = showPix;
            txtPixCopiaCola.Visible = showPix;
            btnCopyPix.Visible = showPix;
            lblCounterPix.Visible = showPix;
            btnSimulatePayment.Visible = showPix;

            btnBack.Visible = !showInicial;
        }

        private void ApplyRegionRounded(Control control, int radius)
        {
            System.Drawing.Drawing2D.GraphicsPath gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddArc(0, 0, radius, radius, 180, 90);
            gp.AddArc(control.Width - radius, 0, radius, radius, 270, 90);
            gp.AddArc(control.Width - radius, control.Height - radius, radius, radius, 0, 90);
            gp.AddArc(0, control.Height - radius, radius, radius, 90, 90);
            control.Region = new Region(gp);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if(!_machineUnlocked)
            {
                KeyboardHook.Start();
                RegistryManager.LockManagerTask();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            if (!_allowClosed)
            {
                e.Cancel = true;
                return;
            }

            timerPix.Stop();
            SessiontTimer.Stop();
            KeyboardHook.Stop();
            RegistryManager.UnlockManagerTask();
            KeyboardHook.OnDeveloperExit -= ClosedByDeveloper;

            base.OnFormClosing(e);
        }

        protected override CreateParams CreateParams { 
            get { 
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080;
                return cp;
            }
        }
    }
}
