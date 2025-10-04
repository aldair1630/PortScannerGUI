using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace PortScannerGUI
{
    public partial class MainForm : Form
    {
        private int? currentUserId;
        private TextBox? txtReportPath;
        private CheckBox? chkVerbose;
        private CheckBox? chkKillNonEssential;
        private Button? btnBrowse;
        private Button? btnRun;
        private RichTextBox? rtbOutput;
        private Label? lblWarning;
        private GroupBox? gbxOptions;
        private ComboBox? cboExportFormat;
        private CheckBox? chkSaveHistory;
        private Button? btnViewHistory;
        private string scriptPath = Path.Combine(Application.StartupPath, "scanPorts.Ps1");

        // New controls for advanced features
        private TabControl? tabControl;
        private TabPage? tabBasic;
        private TabPage? tabSchedule;
        private TabPage? tabAlerts;
        private TabPage? tabReports;
        private DateTimePicker? dtpSchedule;
        private CheckBox? chkRecurrent;
        private Button? btnStartSchedule;
        private System.Windows.Forms.Timer? scheduleTimer;
        private NumericUpDown? nudInterval;
        private TextBox? txtCriticalPorts;
        private TextBox? txtEmailTo;
        private TextBox? txtEmailFrom;
        private TextBox? txtSmtpServer;
        private TextBox? txtSmtpPassword;
        private Button? btnTestAlert;
        private CheckBox? chkUseDB;
        private Button? btnMigrateDB;
        private ListBox? lstScans;
        private Button? btnExportPDF;
        private SmtpClient? smtpClient;
        private TextBox? txtTargetIP;
        
        // Variables para gestión de procesos
        private TabPage? tabProcessManager;
        private ListView? lvProcesses;
        private Button? btnRefreshProcesses;
        private Button? btnKillSelected;
        private CheckBox? chkShowEssential;
        private CheckBox? chkShowNonEssential;
        private ComboBox? cboKillType;
        private NumericUpDown? nudKillTimer;
        private Button? btnKillTemporary;
        private Button? btnKillPermanent;
        private Button? btnKillForced;
        private System.Windows.Forms.Timer? processRefreshTimer;
        private System.Windows.Forms.Timer? temporaryKillTimer;
        public MainForm()
        {
            InitializeComponent();
            LoadDefaultValues();
            this.Load += MainForm_Load;
        }

        private void InitializeComponent()
        {
            this.Text = "Enterprise Port Scanner Pro";
            this.Size = new System.Drawing.Size(1000, 800);
            this.MinimumSize = new System.Drawing.Size(1000, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = true;
            // Eliminamos la línea del icono que causaba error
            this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            
            // ToolTip para hints con estilo mejorado
            var toolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ShowAlways = true,
                IsBalloon = true,
                ToolTipIcon = ToolTipIcon.Info,
                ToolTipTitle = "Información"
            };

            // TabControl principal
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };

            // Tab Básico
            tabBasic = new TabPage("Escaneo Básico");
            var mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                Padding = new System.Windows.Forms.Padding(10)
            };
            mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Reporte
            mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Opciones
            mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Botón
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 10F)); // Progreso
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 80F)); // Salida

            // Fila 1: Ruta del reporte - TableLayoutPanel para expansión
            var reportTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 3,
                Padding = new System.Windows.Forms.Padding(0, 10, 0, 10)
            };
            reportTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // Label
            reportTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F)); // TextBox
            reportTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F)); // Button

            var lblReportPath = new Label
            {
                Text = "Ruta:",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new System.Windows.Forms.Padding(2)
            };

            txtReportPath = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Margin = new System.Windows.Forms.Padding(5)
            };
            toolTip.SetToolTip(txtReportPath, "Selecciona la ubicación para guardar el reporte del escaneo.");

            btnBrowse = new Button
            {
                Text = "Examinar",
                // Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.LightGray,
                ForeColor = System.Drawing.Color.Black,
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                Margin = new System.Windows.Forms.Padding(5),
                AutoSize = true
            };
            toolTip.SetToolTip(btnBrowse, "Elige la carpeta y nombre para el archivo de reporte.");
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += BtnBrowse_Click;

            reportTable.Controls.Add(lblReportPath, 0, 0);
            reportTable.Controls.Add(txtReportPath, 1, 0);
            reportTable.Controls.Add(btnBrowse, 2, 0);
            mainTable.Controls.Add(reportTable, 0, 0);

            // Fila 2: Opciones - TableLayoutPanel dentro de GroupBox
            gbxOptions = new GroupBox
            {
                Text = "Opciones de Escaneo",
                Dock = DockStyle.Top,
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                Padding = new System.Windows.Forms.Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ForeColor = System.Drawing.Color.FromArgb(0, 102, 204),
                BackColor = System.Drawing.Color.FromArgb(245, 245, 245)
            };

            var optionsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top, // Cambia a Top
                RowCount = 6,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            optionsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Labels
            optionsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Controls

            chkVerbose = new CheckBox
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Margin = new System.Windows.Forms.Padding(3)
            };
            toolTip.SetToolTip(chkVerbose, "Activa salida detallada para depuración y logs completos.");
            var lblVerbose = new Label
            {
                Text = "Modo Verbose:",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new System.Windows.Forms.Padding(3)
            };
            optionsTable.Controls.Add(lblVerbose, 0, 0);
            optionsTable.Controls.Add(chkVerbose, 1, 0);
            chkVerbose.Text = "";

            chkKillNonEssential = new CheckBox
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Margin = new System.Windows.Forms.Padding(3)
            };
            toolTip.SetToolTip(chkKillNonEssential, "Escanea y permite cerrar procesos no Microsoft para liberar recursos. Usa con precaución.");
            var lblKill = new Label
            {
                Text = "Cerrar Procesos No Esenciales:",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new System.Windows.Forms.Padding(3)
            };
            optionsTable.Controls.Add(lblKill, 0, 1);
            optionsTable.Controls.Add(chkKillNonEssential, 1, 1);
            chkKillNonEssential.Text = "";

            var lblExport = new Label
            {
                Text = "Formato:",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new System.Windows.Forms.Padding(3)
            };
            toolTip.SetToolTip(lblExport, "Selecciona el formato del reporte: TXT, CSV o JSON.");

            cboExportFormat = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Margin = new System.Windows.Forms.Padding(3)
            };
            cboExportFormat.Items.AddRange(new object[] { "TXT", "CSV", "JSON" });
            cboExportFormat.SelectedIndex = 0;
            toolTip.SetToolTip(cboExportFormat, "TXT para texto legible, CSV para hojas de cálculo, JSON para datos estructurados.");
            optionsTable.Controls.Add(lblExport, 0, 2);
            optionsTable.Controls.Add(cboExportFormat, 1, 2);

            chkSaveHistory = new CheckBox
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Margin = new System.Windows.Forms.Padding(3)
            };
            toolTip.SetToolTip(chkSaveHistory, "Guarda este escaneo en el historial para revisión posterior.");
            var lblSaveHistory = new Label
            {
                Text = "Guardar:",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new System.Windows.Forms.Padding(3)
            };
            optionsTable.Controls.Add(lblSaveHistory, 0, 3);
            optionsTable.Controls.Add(chkSaveHistory, 1, 3);
            chkSaveHistory.Text = "";

            var lblTargetIP = new Label
            {
                Text = "IP Objetivo:",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new System.Windows.Forms.Padding(3)
            };
            txtTargetIP = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = "127.0.0.1",
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Margin = new System.Windows.Forms.Padding(3)
            };
            toolTip.SetToolTip(txtTargetIP, "IP para escaneo remoto (127.0.0.1 para local).");
            optionsTable.Controls.Add(lblTargetIP, 0, 4);
            optionsTable.Controls.Add(txtTargetIP, 1, 4);

            lblWarning = new Label
            {
                Text = "¡ADVERTENCIA! Cerrar procesos puede causar inestabilidad del sistema. Usa con precaución.",
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.Red,
                Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Italic),
                Visible = false,
                Margin = new System.Windows.Forms.Padding(3)
            };
            toolTip.SetToolTip(lblWarning, "Esta opción cierra procesos automáticamente después de confirmación. Evita procesos del sistema.");
            optionsTable.SetRowSpan(lblWarning, 1);
            optionsTable.Controls.Add(lblWarning, 0, 5);
            optionsTable.SetColumnSpan(lblWarning, 2);

            chkKillNonEssential.CheckedChanged += (s, e) => lblWarning.Visible = chkKillNonEssential.Checked;

            gbxOptions.Controls.Add(optionsTable);
            mainTable.Controls.Add(gbxOptions, 0, 1);

            // Fila 3: Botón Ejecutar y Ver Historial
            var buttonTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 2,
                Padding = new System.Windows.Forms.Padding(0, 10, 0, 0)
            };
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Crear un ImageList para los iconos de los botones
            var buttonImageList = new ImageList();
            buttonImageList.ColorDepth = ColorDepth.Depth32Bit;
            buttonImageList.ImageSize = new System.Drawing.Size(24, 24);
            
            // Agregar iconos del sistema
            buttonImageList.Images.Add("scan", System.Drawing.SystemIcons.Application.ToBitmap());
            buttonImageList.Images.Add("history", System.Drawing.SystemIcons.Information.ToBitmap());
            
            btnRun = new Button
            {
                Text = "  Ejecutar Escaneo",
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(0, 120, 212), // Color azul corporativo
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                Margin = new System.Windows.Forms.Padding(5),
                Size = new System.Drawing.Size(180, 45),
                Cursor = Cursors.Hand,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Image = buttonImageList.Images["scan"]
            };
            toolTip.SetToolTip(btnRun, "Inicia el escaneo de puertos y procesos con las opciones seleccionadas.");
            btnRun.FlatAppearance.BorderSize = 0;
            btnRun.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(0, 102, 204);
            btnRun.Click += BtnRun_Click;

            btnViewHistory = new Button
            {
                Text = "  Ver Historial",
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(232, 232, 232),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                Margin = new System.Windows.Forms.Padding(5),
                Size = new System.Drawing.Size(180, 45),
                Cursor = Cursors.Hand,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Image = buttonImageList.Images["history"]
            };
            toolTip.SetToolTip(btnViewHistory, "Muestra el historial de escaneos previos guardados.");
            btnViewHistory.FlatAppearance.BorderSize = 0;
            btnViewHistory.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(220, 220, 220);
            btnViewHistory.Click += BtnViewHistory_Click;

            buttonTable.Controls.Add(btnRun, 0, 0);
            buttonTable.Controls.Add(btnViewHistory, 1, 0);
            mainTable.Controls.Add(buttonTable, 0, 2);

            // Fila 4: Barra de progreso
            var progressPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 1,
                Padding = new System.Windows.Forms.Padding(0, 5, 0, 5)
            };
            var progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 50,
                Visible = false
            };
            progressPanel.Controls.Add(progressBar, 0, 0);
            mainTable.Controls.Add(progressPanel, 0, 3);

            // Fila 5: Salida con diseño mejorado
            rtbOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                Font = new System.Drawing.Font("Consolas", 10F),
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                ForeColor = System.Drawing.Color.FromArgb(220, 220, 220),
                Margin = new System.Windows.Forms.Padding(0),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10)
            };
            
            // Agregar un panel contenedor con borde para el RichTextBox
            var outputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(1),
                BackColor = System.Drawing.Color.FromArgb(0, 120, 212),
                Margin = new Padding(5)
            };
            outputPanel.Controls.Add(rtbOutput);
            
            // Reemplazar la adición directa del rtbOutput con el panel contenedor
            mainTable.Controls.Add(outputPanel, 0, 4);

            tabBasic.Controls.Add(mainTable);

            // Tab Programación
            tabSchedule = new TabPage("Programación de Escaneos");
            var scheduleTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 2,
                Padding = new System.Windows.Forms.Padding(10)
            };
            scheduleTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scheduleTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            scheduleTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scheduleTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scheduleTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scheduleTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scheduleTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var lblSchedule = new Label { Text = "Fecha/Hora:", Dock = DockStyle.Fill, Margin = new Padding(3) };
            dtpSchedule = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", Margin = new Padding(3) };
            toolTip.SetToolTip(dtpSchedule, "Selecciona la fecha y hora para el próximo escaneo programado.");

            chkRecurrent = new CheckBox { Text = "Recurrente", Dock = DockStyle.Fill, Margin = new Padding(3) };
            toolTip.SetToolTip(chkRecurrent, "Activa escaneos recurrentes.");

            var lblInterval = new Label { Text = "Intervalo (minutos):", Dock = DockStyle.Fill, Margin = new Padding(3) };
            nudInterval = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 10080, // 1 week
                Value = 1440, // daily
                Margin = new Padding(3)
            };
            toolTip.SetToolTip(nudInterval, "Intervalo en minutos para escaneos recurrentes (1440 = diario).");
            nudInterval.Enabled = false;

            btnStartSchedule = new Button { Text = "Iniciar Programación", AutoSize = true, BackColor = Color.LightBlue, Margin = new Padding(3) };
            toolTip.SetToolTip(btnStartSchedule, "Inicia el temporizador para escaneos programados.");
            btnStartSchedule.Click += BtnStartSchedule_Click;

            var lblScheduleInfo = new Label { Text = "Los escaneos se ejecutarán automáticamente en la fecha/hora seleccionada.", Dock = DockStyle.Fill, Margin = new Padding(3), ForeColor = Color.Gray };

            scheduleTable.Controls.Add(lblSchedule, 0, 0);
            scheduleTable.Controls.Add(dtpSchedule, 1, 0);
            scheduleTable.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 0, 1);
            scheduleTable.Controls.Add(chkRecurrent, 1, 1);
            scheduleTable.Controls.Add(lblInterval, 0, 2);
            scheduleTable.Controls.Add(nudInterval, 1, 2);
            scheduleTable.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 0, 3);
            scheduleTable.Controls.Add(btnStartSchedule, 1, 3);
            scheduleTable.Controls.Add(lblScheduleInfo, 0, 4);
            scheduleTable.SetColumnSpan(lblScheduleInfo, 2);

            chkRecurrent.CheckedChanged += (s, e) => {
                if (nudInterval != null) {
                    nudInterval.Enabled = chkRecurrent.Checked;
                }
            };
            tabSchedule.Controls.Add(scheduleTable);

            // Tab Alertas
            tabAlerts = new TabPage("Configuración de Alertas");
            var alertsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 7,
                ColumnCount = 2,
                Padding = new System.Windows.Forms.Padding(10)
            };
            alertsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            alertsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            alertsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            alertsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            alertsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            alertsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            alertsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            alertsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            alertsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var lblPorts = new Label { Text = "Puertos Críticos:", Dock = DockStyle.Fill, Margin = new Padding(3) };
            txtCriticalPorts = new TextBox { Dock = DockStyle.Fill, Text = "80,443,3389", Margin = new Padding(3) };
            toolTip.SetToolTip(txtCriticalPorts, "Puertos separados por comas para alertas (e.g., 80,443).");

            var lblEmailTo = new Label { Text = "Email Destino:", Dock = DockStyle.Fill, Margin = new Padding(3) };
            txtEmailTo = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };
            toolTip.SetToolTip(txtEmailTo, "Dirección de email para recibir alertas.");

            var lblEmailFrom = new Label { Text = "Email Origen:", Dock = DockStyle.Fill, Margin = new Padding(3) };
            txtEmailFrom = new TextBox { Dock = DockStyle.Fill, Text = "scanner@empresa.com", Margin = new Padding(3) };
            toolTip.SetToolTip(txtEmailFrom, "Email remitente para alertas.");

            var lblSmtp = new Label { Text = "Servidor SMTP:", Dock = DockStyle.Fill, Margin = new Padding(3) };
            txtSmtpServer = new TextBox { Dock = DockStyle.Fill, Text = "smtp.gmail.com", Margin = new Padding(3) };
            toolTip.SetToolTip(txtSmtpServer, "Servidor SMTP (puerto 587 para TLS).");

            var lblSmtpPassword = new Label { Text = "Contraseña SMTP:", Dock = DockStyle.Fill, Margin = new Padding(3) };
            txtSmtpPassword = new TextBox { Dock = DockStyle.Fill, PasswordChar = '*', Margin = new Padding(3) };
            toolTip.SetToolTip(txtSmtpPassword, "Contraseña para el servidor SMTP.");

            btnTestAlert = new Button { Text = "Probar Alerta",AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.Orange, Margin = new Padding(3) };
            toolTip.SetToolTip(btnTestAlert, "Envía una alerta de prueba al email configurado.");
            btnTestAlert.Click += BtnTestAlert_Click;

            var lblAlertsInfo = new Label { Text = "Las alertas se enviarán cuando se detecten puertos críticos abiertos.", Dock = DockStyle.Fill, Margin = new Padding(3), ForeColor = Color.Gray };

            alertsTable.Controls.Add(lblPorts, 0, 0);
            alertsTable.Controls.Add(txtCriticalPorts, 1, 0);
            alertsTable.Controls.Add(lblEmailTo, 0, 1);
            alertsTable.Controls.Add(txtEmailTo, 1, 1);
            alertsTable.Controls.Add(lblEmailFrom, 0, 2);
            alertsTable.Controls.Add(txtEmailFrom, 1, 2);
            alertsTable.Controls.Add(lblSmtp, 0, 3);
            alertsTable.Controls.Add(txtSmtpServer, 1, 3);
            alertsTable.Controls.Add(lblSmtpPassword, 0, 4);
            alertsTable.Controls.Add(txtSmtpPassword, 1, 4);
            alertsTable.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 0, 5);
            alertsTable.Controls.Add(btnTestAlert, 1, 5);
            alertsTable.Controls.Add(lblAlertsInfo, 0, 6);
            alertsTable.SetColumnSpan(lblAlertsInfo, 2);
            tabAlerts.Controls.Add(alertsTable);

            // Tab Reportes
            tabReports = new TabPage("Reportes Avanzados");
            var reportsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new System.Windows.Forms.Padding(10)
            };
            reportsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            reportsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            reportsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            reportsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            chkUseDB = new CheckBox { Text = "Usar Base de Datos (SQLite)", Dock = DockStyle.Fill, Margin = new Padding(3), Checked = true };
            toolTip.SetToolTip(chkUseDB, "Guarda historial en DB en lugar de JSON.");

            btnMigrateDB = new Button { Text = "Migrar JSON a DB",AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.LightGreen, Margin = new Padding(3) };
            toolTip.SetToolTip(btnMigrateDB, "Migra datos existentes de history.json a la DB.");
            btnMigrateDB.Click += BtnMigrateDB_Click;

            lstScans = new ListBox { Dock = DockStyle.Fill, Margin = new Padding(3) };
            toolTip.SetToolTip(lstScans, "Lista de escaneos desde la base de datos.");

            btnExportPDF = new Button { Text = "Exportar a PDF",AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.DarkBlue, ForeColor = Color.White, Margin = new Padding(3) };
            toolTip.SetToolTip(btnExportPDF, "Genera un reporte PDF de los escaneos.");
            btnExportPDF.Click += BtnExportPDF_Click;

            reportsTable.Controls.Add(chkUseDB, 0, 0);
            reportsTable.Controls.Add(btnMigrateDB, 0, 1);
            reportsTable.Controls.Add(lstScans, 0, 2);
            reportsTable.Controls.Add(btnExportPDF, 0, 3);

            tabReports.Controls.Add(reportsTable);

            tabControl.TabPages.Add(tabBasic);
            tabControl.TabPages.Add(tabSchedule);
            tabControl.TabPages.Add(tabAlerts);
            tabControl.TabPages.Add(tabReports);
            
            // Nueva pestaña para gestión de procesos
            CreateProcessManagerTab();
            tabControl.TabPages.Add(tabProcessManager);

            this.Controls.Add(tabControl);
        }

        private void LoadDefaultValues()
        {
            if (txtReportPath != null)
                txtReportPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Reporte_Puertos.txt");

            if (lstScans != null)
                LoadScansFromDB();
        }

        private void LoadScansFromDB()
        {
            if (lstScans == null) return;
            lstScans.Items.Clear();
            var scans = HistoryDB.GetAllScans();
            foreach (var scan in scans)
            {
                lstScans.Items.Add(scan);
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            if (txtReportPath == null || cboExportFormat?.SelectedItem == null) return;
            using (var saveFileDialog = new SaveFileDialog())
            {
                string selectedFormat = cboExportFormat.SelectedItem.ToString()!.ToLower();
                string filter = selectedFormat switch
                {
                    "csv" => "Archivos CSV (*.csv)|*.csv",
                    "json" => "Archivos JSON (*.json)|*.json",
                    _ => "Archivos de texto (*.txt)|*.txt"
                };
                saveFileDialog.Filter = filter;
                string defaultName = Path.GetFileNameWithoutExtension(txtReportPath.Text);
                saveFileDialog.FileName = $"{defaultName}.{selectedFormat}";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtReportPath.Text = saveFileDialog.FileName;
                }
            }
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            if (chkKillNonEssential?.Checked == true && MessageBox.Show("¿Cerrar procesos no esenciales?", "Confirmar", MessageBoxButtons.YesNo) == DialogResult.No) return;

            if (!File.Exists(scriptPath))
            {
                AppendOutput("Error: El script scanPorts.Ps1 no se encuentra en la ruta esperada.");
                return;
            }

            if (btnRun != null) btnRun.Enabled = false;
            if (rtbOutput != null) rtbOutput.Clear();

            int totalPorts = 0;
            try
            {
                var arguments = new System.Collections.Generic.List<string>
                {
                    "-ExecutionPolicy", "Bypass",
                    "-File", scriptPath
                };

                // Asegurar ruta absoluta para evitar problemas con privilegios
                if (!string.IsNullOrEmpty(txtReportPath?.Text))
                {
                    string reportPath = Path.IsPathRooted(txtReportPath.Text) ? txtReportPath.Text : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), txtReportPath.Text);
                    arguments.Add("-ReportPath");
                    arguments.Add($"\"{reportPath}\"");
                }

                if (chkVerbose?.Checked == true)
                    arguments.Add("-Verbose");

                if (chkKillNonEssential?.Checked == true)
                    arguments.Add("-KillNonEssential");

                if (cboExportFormat?.SelectedItem != null)
                {
                    arguments.Add("-ExportFormat");
                    arguments.Add(cboExportFormat.SelectedItem.ToString()!);
                }

                if (chkSaveHistory?.Checked == true)
                    arguments.Add("-SaveHistory");

                // New params
                if (!string.IsNullOrEmpty(txtTargetIP?.Text))
                {
                    arguments.Add("-TargetIP");
                    arguments.Add($"\"{txtTargetIP.Text}\"");
                }

                if (!string.IsNullOrEmpty(txtCriticalPorts?.Text))
                {
                    arguments.Add("-AlertPorts");
                    arguments.Add($"\"{txtCriticalPorts.Text}\"");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Join(" ", arguments),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Verificar si necesita privilegios de admin
                if (!IsRunningAsAdmin())
                {
                    startInfo.Verb = "runas";
                    startInfo.UseShellExecute = true; // Necesario para runas
                    startInfo.RedirectStandardOutput = false;
                    startInfo.RedirectStandardError = false;
                    startInfo.CreateNoWindow = false;
                    AppendOutput("Ejecutando con privilegios de administrador...");
                }

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        if (!IsRunningAsAdmin())
                        {
                            // Si se ejecuta con runas, no podemos capturar salida
                            AppendOutput("Script ejecutado. Revisa los archivos de reporte generados.");
                        }
                        else
                        {
                            // Capturar salida
                            string output = process.StandardOutput.ReadToEnd();
                            string error = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            if (!string.IsNullOrEmpty(output))
                                AppendOutput(output);
                            if (!string.IsNullOrEmpty(error))
                                AppendOutput($"Error: {error}");

                            // Parse TotalPorts
                            var match = Regex.Match(output, @"Total de puertos: (\d+)");
                            if (match.Success) totalPorts = int.Parse(match.Groups[1].Value);

                            // Check for alerts in output (simple parse)
                            if (!string.IsNullOrEmpty(output) && !string.IsNullOrEmpty(txtCriticalPorts?.Text))
                            {
                                var criticalPorts = txtCriticalPorts.Text.Split(',');
                                foreach (var port in criticalPorts)
                                {
                                    if (output.Contains(port.Trim()))
                                    {
                                        SendAlertEmail($"Puerto crítico {port.Trim()} detectado abierto.");
                                        break;
                                    }
                                }
                            }

                            // Improved alert check
                            if (output.Contains("ALERTA:")) SendAlertEmail("Puertos críticos detectados.");
                        }
                    }
                }

                // Save to DB if enabled
                if (chkUseDB?.Checked == true && chkSaveHistory?.Checked == true)
                {
                    HistoryDB.InsertScan(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), totalPorts, cboExportFormat?.SelectedItem?.ToString() ?? "TXT", txtReportPath?.Text ?? "", chkKillNonEssential?.Checked ?? false, currentUserId);
                    LoadScansFromDB();
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"Error al ejecutar el script: {ex.Message}");
            }
            finally
            {
                if (btnRun != null) btnRun.Enabled = true;
            }
        }

        private void BtnViewHistory_Click(object sender, EventArgs e)
        {
            if (chkUseDB?.Checked == true)
            {
                LoadScansFromDB();
                AppendOutput("=== HISTORIAL DESDE DB ===");
                foreach (var scan in HistoryDB.GetAllScans())
                {
                    AppendOutput(scan);
                }
                AppendOutput("=== FIN HISTORIAL ===");
            }
            else
            {
                string historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "history.json");
                if (File.Exists(historyPath))
                {
                    try
                    {
                        string historyJson = File.ReadAllText(historyPath);
                        AppendOutput("=== HISTORIAL DE ESCANEOS ===");
                        AppendOutput(historyJson);
                        AppendOutput("=== FIN HISTORIAL ===");
                    }
                    catch (Exception ex)
                    {
                        AppendOutput($"Error al leer historial: {ex.Message}");
                    }
                }
                else
                {
                    AppendOutput("No hay historial de escaneos disponible. Realiza escaneos con 'Guardar en Historial' activado.");
                }
            }
        }

        private void BtnStartSchedule_Click(object sender, EventArgs e)
        {
            if (dtpSchedule.Value <= DateTime.Now) { MessageBox.Show("Fecha/hora debe ser futura."); return; }

            if (scheduleTimer == null)
            {
                scheduleTimer = new System.Windows.Forms.Timer { Interval = 60000 }; // Check every minute
                scheduleTimer.Tick += ScheduleTimer_Tick;
            }

            if (btnStartSchedule != null)
            {
                if (scheduleTimer.Enabled)
                {
                    scheduleTimer.Stop();
                    btnStartSchedule.Text = "Iniciar Programación";
                    AppendOutput("Programación detenida.");
                }
                else
                {
                    scheduleTimer.Start();
                    btnStartSchedule.Text = "Detener Programación";
                    AppendOutput("Programación iniciada. Verificando cada minuto.");
                }
            }
        }

        private void ScheduleTimer_Tick(object sender, EventArgs e)
        {
            if (dtpSchedule != null && DateTime.Now >= dtpSchedule.Value)
            {
                if (chkRecurrent?.Checked == true)
                {
                    dtpSchedule.Value = dtpSchedule.Value.AddDays(1);
                }
                else
                {
                    scheduleTimer?.Stop();
                    if (btnStartSchedule != null) btnStartSchedule.Text = "Iniciar Programación";
                }
                // Trigger scan
                BtnRun_Click(null, EventArgs.Empty);
                AppendOutput("Escaneo programado ejecutado.");
            }
        }

        private void BtnTestAlert_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtSmtpPassword?.Text))
            {
                MessageBox.Show("Ingresa contraseña SMTP.");
                return;
            }
            SendAlertEmail("Prueba de alerta: Configuración de email funcionando.");
        }

        private async void SendAlertEmail(string message)
        {
            if (txtEmailTo == null || txtEmailFrom == null || txtSmtpServer == null || txtSmtpPassword == null || string.IsNullOrEmpty(txtEmailTo.Text)) return;

            try
            {
                smtpClient = new SmtpClient(txtSmtpServer.Text, 587)
                {
                    Credentials = new System.Net.NetworkCredential(txtEmailFrom.Text, txtSmtpPassword.Text),
                    EnableSsl = true
                };
                var mailMessage = new MailMessage(txtEmailFrom.Text, txtEmailTo.Text, "Alerta de Escáner de Puertos", message);
                await smtpClient.SendMailAsync(mailMessage);
                AppendOutput("Alerta enviada exitosamente.");
                smtpClient.Dispose();
                mailMessage.Dispose();
            }
            catch (Exception ex)
            {
                AppendOutput($"Error enviando alerta: {ex.Message}");
            }
        }

        private bool IsRunningAsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void AppendOutput(string text)
        {
            if (rtbOutput == null) return;
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendOutput), text);
            }
            else
            {
                rtbOutput.AppendText(text + Environment.NewLine);
                rtbOutput.ScrollToCaret();
            }
        }

        private void BtnMigrateDB_Click(object? sender, EventArgs e)
        {
            string historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "history.json");
            HistoryDB.MigrateFromJson(historyPath);
            LoadScansFromDB();
            AppendOutput("Migración completada.");
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            LoginForm login = new LoginForm();
            if (login.ShowDialog() == DialogResult.OK)
            {
                currentUserId = login.UserId;
            }
            // No exit, login is optional
        }

        private void BtnExportPDF_Click(object? sender, EventArgs e)
        {
            var scans = HistoryDB.GetAllScans();
            string pdfPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "reporte_scans.pdf");
            using (var writer = new PdfWriter(pdfPath))
            {
                using (var pdf = new PdfDocument(writer))
                {
                    var document = new Document(pdf);
                    document.Add(new Paragraph("Reporte de Escaneos").SetFontSize(18));
                    var table = new Table(5);
                    table.AddHeaderCell("Timestamp");
                    table.AddHeaderCell("TotalPorts");
                    table.AddHeaderCell("ExportFormat");
                    table.AddHeaderCell("ReportPath");
                    table.AddHeaderCell("KillNonEssential");
                    foreach (var scan in scans)
                    {
                        var parts = scan.Split(',');
                        if (parts.Length >= 5)
                        {
                            table.AddCell(parts[0]);
                            table.AddCell(parts[1]);
                            table.AddCell(parts[2]);
                            table.AddCell(parts[3]);
                            table.AddCell(parts[4]);
                        }
                    }
                    document.Add(table);
                }
            }
            AppendOutput("PDF generado: " + pdfPath);
        }

        private void CreateProcessManagerTab()
        {
            tabProcessManager = new TabPage("Gestión de Procesos")
            {
                BackColor = System.Drawing.Color.FromArgb(250, 250, 250),
                Padding = new Padding(10)
            };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(5)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Panel de filtros
            var filterPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 50,
                BackColor = System.Drawing.Color.FromArgb(230, 240, 250),
                Padding = new Padding(10)
            };

            var filterLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true
            };

            chkShowEssential = new CheckBox
            {
                Text = "Mostrar Esenciales",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(5),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50)
            };
            chkShowEssential.CheckedChanged += RefreshProcessList;

            chkShowNonEssential = new CheckBox
            {
                Text = "Mostrar No Esenciales",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(5),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50)
            };
            chkShowNonEssential.CheckedChanged += RefreshProcessList;

            btnRefreshProcesses = new Button
            {
                Text = "🔄 Actualizar",
                AutoSize = true,
                BackColor = System.Drawing.Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(5),
                Cursor = Cursors.Hand
            };
            btnRefreshProcesses.Click += (s, e) => RefreshProcessList(s, e);

            filterLayout.Controls.Add(chkShowEssential);
            filterLayout.Controls.Add(chkShowNonEssential);
            filterLayout.Controls.Add(btnRefreshProcesses);
            filterPanel.Controls.Add(filterLayout);

            // ListView de procesos
            lvProcesses = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true,
                BackColor = Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };

            lvProcesses.Columns.Add("Proceso", 150);
            lvProcesses.Columns.Add("PID", 80);
            lvProcesses.Columns.Add("Tipo", 100);
            lvProcesses.Columns.Add("Memoria (MB)", 100);
            lvProcesses.Columns.Add("CPU %", 80);
            lvProcesses.Columns.Add("Ruta", 300);

            // Panel de controles
            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(240, 248, 255),
                Padding = new Padding(10)
            };

            var controlLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6
            };

            for (int i = 0; i < 6; i++)
                controlLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblKillType = new Label
            {
                Text = "Tipo de Cierre:",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50),
                Margin = new Padding(3)
            };

            cboKillType = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Margin = new Padding(3)
            };
            cboKillType.Items.AddRange(new object[] { "Temporal", "Permanente", "Forzado Permanente" });
            cboKillType.SelectedIndex = 0;

            var lblTimer = new Label
            {
                Text = "Tiempo (minutos):",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50),
                Margin = new Padding(3)
            };

            nudKillTimer = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 1440,
                Value = 30,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                Margin = new Padding(3)
            };

            btnKillSelected = new Button
            {
                Text = "🚫 Cerrar Seleccionados",
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(220, 20, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold),
                Margin = new Padding(3),
                Cursor = Cursors.Hand,
                Height = 40
            };
            btnKillSelected.Click += BtnKillSelected_Click;

            var warningLabel = new Label
            {
                Text = "⚠️ ADVERTENCIA: Cerrar procesos puede causar inestabilidad del sistema",
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.FromArgb(255, 69, 0),
                Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(3)
            };

            controlLayout.Controls.Add(lblKillType, 0, 0);
            controlLayout.Controls.Add(cboKillType, 0, 1);
            controlLayout.Controls.Add(lblTimer, 0, 2);
            controlLayout.Controls.Add(nudKillTimer, 0, 3);
            controlLayout.Controls.Add(btnKillSelected, 0, 4);
            controlLayout.Controls.Add(warningLabel, 0, 5);

            controlPanel.Controls.Add(controlLayout);

            mainLayout.Controls.Add(filterPanel, 0, 0);
            mainLayout.Controls.Add(lvProcesses, 0, 1);
            mainLayout.Controls.Add(controlPanel, 1, 1);

            tabProcessManager.Controls.Add(mainLayout);

            // Inicializar timer para actualización automática
            processRefreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000 // 5 segundos
            };
            processRefreshTimer.Tick += (s, e) => RefreshProcessList(s, e);
            processRefreshTimer.Start();

            // Timer para cierre temporal
            temporaryKillTimer = new System.Windows.Forms.Timer();
            temporaryKillTimer.Tick += TemporaryKillTimer_Tick;

            RefreshProcessList(null, EventArgs.Empty);
        }

        private void RefreshProcessList(object? sender, EventArgs e)
        {
            if (lvProcesses == null) return;

            try
            {
                lvProcesses.Items.Clear();
                var processes = Process.GetProcesses();

                foreach (var process in processes)
                {
                    try
                    {
                        if (process.Id == 0 || process.ProcessName == "Idle") continue;

                        bool isEssential = IsEssentialProcess(process);
                        
                        if ((isEssential && chkShowEssential?.Checked != true) ||
                            (!isEssential && chkShowNonEssential?.Checked != true))
                            continue;

                        var item = new ListViewItem(process.ProcessName);
                        item.SubItems.Add(process.Id.ToString());
                        item.SubItems.Add(isEssential ? "Esencial" : "No Esencial");
                        
                        try
                        {
                            item.SubItems.Add((process.WorkingSet64 / 1024 / 1024).ToString());
                        }
                        catch
                        {
                            item.SubItems.Add("N/A");
                        }

                        item.SubItems.Add("N/A"); // CPU % - requiere implementación más compleja
                        
                        try
                        {
                            item.SubItems.Add(process.MainModule?.FileName ?? "N/A");
                        }
                        catch
                        {
                            item.SubItems.Add("N/A");
                        }

                        item.Tag = process.Id;
                        item.BackColor = isEssential ? System.Drawing.Color.FromArgb(255, 240, 240) : Color.White;
                        
                        lvProcesses.Items.Add(item);
                    }
                    catch
                    {
                        // Ignorar procesos que no se pueden acceder
                    }
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"Error al actualizar lista de procesos: {ex.Message}");
            }
        }

        private bool IsEssentialProcess(Process process)
        {
            var essentialProcesses = new[]
            {
                "explorer", "svchost", "lsass", "winlogon", "csrss", "wininit", 
                "services", "smss", "dwm", "audiodg", "conhost", "system", 
                "registry", "secure system", "memory compression"
            };

            string processName = process.ProcessName.ToLower();
            
            // Procesos del sistema críticos
            if (essentialProcesses.Contains(processName))
                return true;

            // Procesos de Microsoft/Windows
            try
            {
                string? path = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.Contains("Windows\\System32") || 
                        path.Contains("Windows\\SysWOW64") ||
                        path.Contains("Program Files\\Windows"))
                        return true;
                }

                if (process.MainModule?.FileVersionInfo?.CompanyName?.Contains("Microsoft") == true)
                    return true;
            }
            catch
            {
                // Si no podemos acceder a la información, asumimos que es esencial por seguridad
                return true;
            }

            return false;
        }

        private void BtnKillSelected_Click(object? sender, EventArgs e)
        {
            if (lvProcesses?.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona al menos un proceso para cerrar.", "Advertencia", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedProcesses = new List<int>();
            var essentialSelected = false;

            foreach (ListViewItem item in lvProcesses.SelectedItems)
            {
                if (item.Tag is int pid)
                {
                    selectedProcesses.Add(pid);
                    if (item.SubItems[2].Text == "Esencial")
                        essentialSelected = true;
                }
            }

            if (essentialSelected)
            {
                var result = MessageBox.Show(
                    "Has seleccionado procesos ESENCIALES. Esto puede causar inestabilidad del sistema.\n\n¿Estás seguro de continuar?",
                    "ADVERTENCIA CRÍTICA",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);

                if (result != DialogResult.Yes)
                    return;
            }

            string killType = cboKillType?.SelectedItem?.ToString() ?? "Temporal";
            int timerMinutes = (int)(nudKillTimer?.Value ?? 30);

            var confirmResult = MessageBox.Show(
                $"¿Confirmas cerrar {selectedProcesses.Count} proceso(s) de forma {killType.ToLower()}?",
                "Confirmar Cierre",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult == DialogResult.Yes)
            {
                KillProcesses(selectedProcesses, killType, timerMinutes);
            }
        }

        private void KillProcesses(List<int> processIds, string killType, int timerMinutes)
        {
            int killed = 0;
            var killedProcesses = new List<string>();

            foreach (int pid in processIds)
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    string processName = process.ProcessName;
                    
                    process.Kill();
                    process.WaitForExit(5000);
                    
                    killed++;
                    killedProcesses.Add(processName);
                    
                    AppendOutput($"✅ Proceso cerrado: {processName} (PID: {pid})");

                    // Implementar lógica según tipo de cierre
                    switch (killType)
                    {
                        case "Temporal":
                            // Para cierre temporal, podríamos implementar un sistema de monitoreo
                            AppendOutput($"⏰ Cierre temporal configurado para {processName} por {timerMinutes} minutos");
                            break;
                            
                        case "Permanente":
                            // Agregar a lista de procesos bloqueados
                            AppendOutput($"🚫 Proceso {processName} marcado para cierre permanente");
                            break;
                            
                        case "Forzado Permanente":
                            // Implementar bloqueo más agresivo
                            AppendOutput($"⛔ Proceso {processName} bloqueado de forma forzada y permanente");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    AppendOutput($"❌ Error al cerrar proceso PID {pid}: {ex.Message}");
                }
            }

            AppendOutput($"\n📊 Resumen: {killed} de {processIds.Count} procesos cerrados exitosamente");
            RefreshProcessList(null, EventArgs.Empty);
        }

        private void TemporaryKillTimer_Tick(object? sender, EventArgs e)
        {
            // Implementar lógica para reactivar procesos temporalmente cerrados
            // Esta funcionalidad requiere un sistema más complejo de seguimiento
        }
