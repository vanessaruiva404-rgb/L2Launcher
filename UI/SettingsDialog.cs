using System;
using System.Drawing;
using System.Windows.Forms;
using LineageII.Services;

namespace LineageII.UI
{
    internal sealed partial class SettingsDialog : Form
    {
        private readonly ComboBox comboSize;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public Size SelectedSize { get; private set; }

        public SettingsDialog(int currentWidth, int currentHeight)
        {
            Text = "Configurações do Launcher";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 170);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9F);

            Label title = new Label
            {
                Text = "Tamanho da janela",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(22, 20),
                ForeColor = Theme.Text
            };

            Label hint = new Label
            {
                Text = "Escolha uma resolução para o launcher.",
                AutoSize = true,
                Location = new Point(24, 50),
                ForeColor = Theme.MutedText
            };

            comboSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(26, 78),
                Size = new Size(368, 28)
            };

            comboSize.Items.Add("1024 x 640");
            comboSize.Items.Add("1280 x 720");
            comboSize.Items.Add("1366 x 768");
            comboSize.Items.Add("1440 x 810");
            comboSize.Items.Add("1600 x 900");
            comboSize.SelectedItem = $"{currentWidth} x {currentHeight}";
            if (comboSize.SelectedIndex < 0)
                comboSize.SelectedIndex = 1;

            btnOk = new Button
            {
                Text = "Aplicar",
                DialogResult = DialogResult.OK,
                Location = new Point(214, 122),
                Size = new Size(86, 32),
                BackColor = Theme.Orange,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

            btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(308, 122),
                Size = new Size(86, 32),
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = Theme.Border;

            btnOk.Click += (s, e) =>
            {
                SelectedSize = ParseSelectedSize(comboSize.SelectedItem?.ToString(), currentWidth, currentHeight);
            };

            Controls.Add(title);
            Controls.Add(hint);
            Controls.Add(comboSize);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            SelectedSize = new Size(currentWidth, currentHeight);
        }

        private static Size ParseSelectedSize(string value, int fallbackWidth, int fallbackHeight)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new Size(fallbackWidth, fallbackHeight);

            string[] parts = value.ToLowerInvariant().Split('x');
            if (parts.Length != 2)
                return new Size(fallbackWidth, fallbackHeight);

            if (int.TryParse(parts[0].Trim(), out int w) && int.TryParse(parts[1].Trim(), out int h))
                return new Size(w, h);

            return new Size(fallbackWidth, fallbackHeight);
        }
    }
}
