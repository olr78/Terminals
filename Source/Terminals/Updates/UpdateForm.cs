using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Terminals.Services;

namespace Terminals.Updates
{
    /// <summary>
    /// Shows available release and downloads the package, if the user confirms the update.
    /// DialogResult.OK means the package was downloaded, verified and is ready in PackagePath.
    /// DialogResult.Ignore means the user wants to skip this version.
    /// </summary>
    internal class UpdateForm : Form
    {
        private readonly Release release;
        private readonly UpdateInstaller installer;
        private readonly ReleaseAsset asset;

        private Label headerLabel;
        private TextBox notesTextBox;
        private Label statusLabel;
        private ProgressBar progressBar;
        private Button updateButton;
        private Button releasePageButton;
        private Button skipButton;
        private Button laterButton;

        private WebClient client;

        /// <summary>
        /// Gets full path to the downloaded and verified release package.
        /// </summary>
        internal string PackagePath { get; private set; }

        private bool Downloading
        {
            get { return this.client != null; }
        }

        internal UpdateForm(Release release, UpdateInstaller installer)
        {
            this.release = release;
            this.installer = installer;
            this.asset = installer.SelectAsset(release);
            this.InitializeComponent();
            this.FillReleaseInfo();
        }

        private void InitializeComponent()
        {
            this.headerLabel = new Label();
            this.notesTextBox = new TextBox();
            this.statusLabel = new Label();
            this.progressBar = new ProgressBar();
            this.updateButton = new Button();
            this.releasePageButton = new Button();
            this.skipButton = new Button();
            this.laterButton = new Button();
            var buttonsPanel = new FlowLayoutPanel();

            this.headerLabel.Dock = DockStyle.Top;
            this.headerLabel.Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold);
            this.headerLabel.Height = 48;
            this.headerLabel.Padding = new Padding(0, 4, 0, 4);

            this.notesTextBox.Dock = DockStyle.Fill;
            this.notesTextBox.Multiline = true;
            this.notesTextBox.ReadOnly = true;
            this.notesTextBox.ScrollBars = ScrollBars.Vertical;
            this.notesTextBox.BackColor = SystemColors.Window;

            this.statusLabel.Dock = DockStyle.Bottom;
            this.statusLabel.Height = 36;
            this.statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            this.progressBar.Dock = DockStyle.Bottom;
            this.progressBar.Height = 18;
            this.progressBar.Visible = false;

            this.updateButton.Text = "&Update now";
            this.updateButton.AutoSize = true;
            this.updateButton.Click += this.UpdateButton_Click;

            this.releasePageButton.Text = "&Release page";
            this.releasePageButton.AutoSize = true;
            this.releasePageButton.Click += this.ReleasePageButton_Click;

            this.skipButton.Text = "&Skip this version";
            this.skipButton.AutoSize = true;
            this.skipButton.Click += this.SkipButton_Click;

            this.laterButton.Text = "&Later";
            this.laterButton.AutoSize = true;
            this.laterButton.Click += this.LaterButton_Click;

            buttonsPanel.Dock = DockStyle.Bottom;
            buttonsPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonsPanel.Height = 38;
            buttonsPanel.Padding = new Padding(0, 6, 0, 0);
            buttonsPanel.Controls.Add(this.laterButton);
            buttonsPanel.Controls.Add(this.skipButton);
            buttonsPanel.Controls.Add(this.releasePageButton);
            buttonsPanel.Controls.Add(this.updateButton);

            this.Controls.Add(this.notesTextBox);
            this.Controls.Add(this.headerLabel);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(buttonsPanel);

            this.Text = "Terminals Update";
            this.ClientSize = new Size(560, 400);
            this.MinimumSize = new Size(420, 300);
            this.Padding = new Padding(10);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.ShowIcon = false;
            this.AcceptButton = this.updateButton;
            this.CancelButton = this.laterButton;
            this.FormClosing += this.UpdateForm_FormClosing;
        }

        private void FillReleaseInfo()
        {
            this.headerLabel.Text = string.Format("Terminals {0} is available (you have {1}).\r\nPublished {2:d}",
                this.release.Version, Release.Normalize(Program.Info.Version), this.release.Published.ToLocalTime());

            string notes = string.IsNullOrEmpty(this.release.Notes) ? "No release notes." : this.release.Notes;
            this.notesTextBox.Text = notes.Replace("\r\n", "\n").Replace("\n", "\r\n");

            if (this.asset != null)
            {
                string type = this.installer.InstallationType == InstallationType.Msi ? "installer" : "portable package";
                this.statusLabel.Text = string.Format("Update downloads {0} ({1}), Terminals will be restarted.", type, this.asset.Name);
                return;
            }

            this.updateButton.Enabled = false;
            this.AcceptButton = this.releasePageButton;
            this.statusLabel.Text = this.installer.InstallationType == InstallationType.NotSupported
                ? "Automatic update isn't possible, application directory is read only. Download the release manually."
                : "The release doesn't contain package for this installation type. Download the release manually.";
        }

        private void UpdateButton_Click(object sender, EventArgs e)
        {
            try
            {
                this.StartDownload();
            }
            catch (Exception exception)
            {
                this.ShowFailure("Unable to start the download", exception);
            }
        }

        private void StartDownload()
        {
            string directory = this.installer.PrepareDownloadDirectory(this.release.Version);
            string packagePath = Path.Combine(directory, Path.GetFileName(this.asset.Name));

            this.SetDownloadingState(true);
            this.statusLabel.Text = string.Format("Downloading {0}...", this.asset.Name);
            this.client = UpdateManager.CreateWebClient();
            this.client.DownloadProgressChanged += this.Client_DownloadProgressChanged;
            this.client.DownloadFileCompleted += (s, args) => this.Client_DownloadFileCompleted(args, packagePath);
            this.client.DownloadFileAsync(new Uri(this.asset.DownloadUrl), packagePath);
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            this.progressBar.Value = e.ProgressPercentage;
        }

        private void Client_DownloadFileCompleted(AsyncCompletedEventArgs args, string packagePath)
        {
            this.client.Dispose();
            this.client = null;

            if (this.IsDisposed)
                return;

            if (args.Cancelled)
            {
                this.SetDownloadingState(false);
                this.statusLabel.Text = "Download was cancelled.";
                return;
            }

            if (args.Error != null)
            {
                this.ShowFailure("Download failed", args.Error);
                return;
            }

            this.statusLabel.Text = "Verifying the downloaded package...";
            Task.Factory.StartNew(() => UpdateInstaller.VerifyChecksum(packagePath, this.asset))
                .ContinueWith(task => this.OnPackageVerified(task, packagePath), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnPackageVerified(Task verification, string packagePath)
        {
            if (this.IsDisposed)
                return;

            if (verification.Exception != null)
            {
                this.ShowFailure("Downloaded package is not valid", verification.Exception.GetBaseException());
                return;
            }

            this.PackagePath = packagePath;
            this.DialogResult = DialogResult.OK;
        }

        private void ShowFailure(string message, Exception exception)
        {
            Logging.Error(message, exception);
            this.SetDownloadingState(false);
            this.statusLabel.Text = string.Format("{0}: {1}", message, exception.Message);
        }

        private void SetDownloadingState(bool downloading)
        {
            this.progressBar.Value = 0;
            this.progressBar.Visible = downloading;
            this.updateButton.Enabled = !downloading;
            this.skipButton.Enabled = !downloading;
            this.laterButton.Text = downloading ? "&Cancel" : "&Later";
        }

        private void ReleasePageButton_Click(object sender, EventArgs e)
        {
            string url = string.IsNullOrEmpty(this.release.HtmlUrl) ? ExternalLinks.TerminalsReleasesUrl : this.release.HtmlUrl;
            ExternalLinks.OpenPath(url);
        }

        private void SkipButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Ignore;
        }

        private void LaterButton_Click(object sender, EventArgs e)
        {
            if (this.Downloading)
                this.client.CancelAsync();
            else
                this.DialogResult = DialogResult.Cancel;
        }

        private void UpdateForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.Downloading)
                this.client.CancelAsync();
        }
    }
}
