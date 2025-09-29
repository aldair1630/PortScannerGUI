using System;
using System.Windows.Forms;

namespace PortScannerGUI
{
    public partial class LoginForm : Form
    {
        public int? UserId { get; private set; }

        private TextBox? txtUser;
        private TextBox? txtPass;

        public LoginForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Acceso - PortScannerGUI";
            this.Size = new System.Drawing.Size(350, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10)
            };

            Label lblUser = new Label { Text = "Usuario:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            txtUser = new TextBox { Width = 200 };
            Label lblPass = new Label { Text = "Contraseña:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            txtPass = new TextBox { Width = 200, PasswordChar = '*' };

            Button btnLogin = new Button { Text = "Iniciar Sesión", AutoSize = true };
            Button btnRegister = new Button { Text = "Registrarse", AutoSize = true };
            Button btnSkip = new Button { Text = "Usar sin Iniciar Sesión", AutoSize = true };

            btnLogin.Click += BtnLogin_Click;
            btnRegister.Click += BtnRegister_Click;
            btnSkip.Click += (s, e) =>
            {
                UserId = null;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            mainTable.Controls.Add(lblUser, 0, 0);
            mainTable.Controls.Add(txtUser, 1, 0);
            mainTable.Controls.Add(lblPass, 0, 1);
            mainTable.Controls.Add(txtPass, 1, 1);
            mainTable.Controls.Add(btnLogin, 0, 2);
            mainTable.Controls.Add(btnRegister, 1, 2);
            mainTable.Controls.Add(btnSkip, 0, 3);
            mainTable.SetColumnSpan(btnSkip, 2);

            this.Controls.Add(mainTable);
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtUser.Text) || string.IsNullOrEmpty(txtPass.Text))
            {
                MessageBox.Show("Ingrese usuario y contraseña.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var userId = HistoryDB.LoginUser(txtUser.Text, txtPass.Text);
            if (userId.HasValue)
            {
                UserId = userId.Value;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Credenciales incorrectas.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRegister_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtUser.Text) || string.IsNullOrEmpty(txtPass.Text))
            {
                MessageBox.Show("Ingrese usuario y contraseña para registrarse.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (HistoryDB.RegisterUser(txtUser.Text, txtPass.Text))
            {
                MessageBox.Show("Usuario registrado exitosamente. Ahora puede iniciar sesión.", "Registro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Error al registrar usuario. El usuario ya existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
