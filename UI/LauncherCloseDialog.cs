using System;
using System.Drawing;
using System.Windows.Forms;
using LineageII.Controls;
using LineageII.Services;

namespace LineageII.UI
{
    internal enum LauncherCloseAction
    {
        Cancel,
        Hide,
        Exit
    }

    internal sealed class LauncherCloseDialog : Form
    {
        private Point dragStart;
        private bool dragging;

        public LauncherCloseAction SelectedAction { get; private set; } = LauncherCloseAction.Cancel;

        public LauncherCloseDialog()
        {
            Text = "Fechar Launcher";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 246);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9F);

            RoundedPanel shell = new RoundedPanel
            {
                Location = new Point(10, 10),
                Size = new Size(440, 226),
                BackColor = Theme.Surface,
                BorderColor = Theme.BorderHot,
                BorderThickness = 1,
                CornerRadius = 18
            };

            Label title = new Label
            {
                Text = "Fechar Launcher",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = false,
                Location = new Point(24, 22),
                Size = new Size(300, 34)
            };

            Label hint = new Label
            {
                Text = "Voce quer deixar o launcher rodando em segundo plano ou encerrar tudo agora?",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Theme.MutedText,
                AutoSize = false,
                Location = new Point(26, 62),
                Size = new Size(386, 42)
            };

            Button close = BuildIconButton("x", new Point(392, 20), Theme.SurfaceAlt);
            close.Click += (s, e) => Choose(LauncherCloseAction.Cancel);

            Button hide = BuildActionButton("Esconder", new Point(26, 124), new Size(126, 44), Theme.Orange, Theme.Text);
            hide.Click += (s, e) => Choose(LauncherCloseAction.Hide);

            Button exit = BuildActionButton("Sair", new Point(162, 124), new Size(120, 44), Theme.SurfaceAlt, Theme.Text);
            exit.FlatAppearance.BorderColor = Theme.Border;
            exit.FlatAppearance.BorderSize = 1;
            exit.Click += (s, e) => Choose(LauncherCloseAction.Exit);

            Button cancel = BuildActionButton("Cancelar", new Point(292, 124), new Size(120, 44), Theme.SurfaceDark, Theme.MutedText);
            cancel.FlatAppearance.BorderColor = Theme.Border;
            cancel.FlatAppearance.BorderSize = 1;
            cancel.Click += (s, e) => Choose(LauncherCloseAction.Cancel);

            Label footer = new Label
            {
                Text = "Esconder mantem o launcher pronto na area de notificacao.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.MutedText,
                AutoSize = false,
                Location = new Point(26, 184),
                Size = new Size(386, 20)
            };

            shell.Controls.Add(title);
            shell.Controls.Add(hint);
            shell.Controls.Add(close);
            shell.Controls.Add(hide);
            shell.Controls.Add(exit);
            shell.Controls.Add(cancel);
            shell.Controls.Add(footer);
            Controls.Add(shell);

            AcceptButton = hide;
            CancelButton = cancel;

            foreach (Control control in new Control[] { shell, title, hint })
            {
                control.MouseDown += BeginDrag;
                control.MouseMove += DragDialog;
                control.MouseUp += EndDrag;
            }
        }

        private Button BuildActionButton(string text, Point location, Size size, Color backColor, Color foreColor)
        {
            Button button = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private Button BuildIconButton(string text, Point location, Color backColor)
        {
            Button button = BuildActionButton(text, location, new Size(28, 28), backColor, Theme.Text);
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            return button;
        }

        private void Choose(LauncherCloseAction action)
        {
            SelectedAction = action;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            dragging = true;
            dragStart = e.Location;
        }

        private void DragDialog(object sender, MouseEventArgs e)
        {
            if (!dragging)
                return;

            Point screen = ((Control)sender).PointToScreen(e.Location);
            Location = new Point(screen.X - dragStart.X, screen.Y - dragStart.Y);
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            dragging = false;
        }
    }
}
