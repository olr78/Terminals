using System;
using System.Drawing;
using System.Windows.Forms;

namespace Terminals.Forms
{
    /// <summary>
    /// Small window with the progress of the application start.
    /// Runs on its own UI thread, so it stays responsive while the main thread loads the data.
    /// </summary>
    internal class StartupProgressForm : Form
    {
        private readonly Label statusLabel = new Label();

        private readonly ProgressBar progressBar = new ProgressBar();

        private readonly Label percentLabel = new Label();

        internal StartupProgressForm(Image logo, string title, string status)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = true;
            this.BackColor = SystemColors.Window;
            this.Font = SystemFonts.MessageBoxFont;
            this.ClientSize = new Size(440, 118);
            this.Padding = new Padding(1);
            if (logo != null)
                this.Icon = Icon.FromHandle(new Bitmap(logo).GetHicon());

            var logoBox = new PictureBox
            {
                Image = logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 20),
                Size = new Size(32, 32)
            };

            var titleLabel = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font(this.Font.FontFamily, 12F, FontStyle.Bold),
                Location = new Point(62, 17)
            };

            this.statusLabel.Text = status;
            this.statusLabel.AutoEllipsis = true;
            this.statusLabel.ForeColor = SystemColors.GrayText;
            this.statusLabel.Location = new Point(63, 42);
            this.statusLabel.Size = new Size(360, 18);

            this.progressBar.Style = ProgressBarStyle.Continuous;
            this.progressBar.Location = new Point(20, 72);
            this.progressBar.Size = new Size(356, 18);

            this.percentLabel.TextAlign = ContentAlignment.MiddleRight;
            this.percentLabel.Location = new Point(378, 72);
            this.percentLabel.Size = new Size(42, 18);

            this.Controls.AddRange(new Control[] { logoBox, titleLabel, this.statusLabel, this.progressBar, this.percentLabel });
            this.SetProgress(status, 0);
        }

        internal void SetProgress(string status, int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            if (status != null)
                this.statusLabel.Text = status;

            this.progressBar.Value = percent;
            this.percentLabel.Text = percent + " %";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle border = this.ClientRectangle;
            border.Width--;
            border.Height--;
            e.Graphics.DrawRectangle(SystemPens.ActiveBorder, border);
        }
    }
}
