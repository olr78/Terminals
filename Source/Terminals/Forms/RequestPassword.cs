using System;
using System.Drawing;
using System.Windows.Forms;
using Terminals.Data;
using Terminals.Localization;

namespace Terminals.Security
{
    internal partial class RequestPassword : Form
    {
        private const string INVALID_PASSWORD = "Invalid Password.";

        private const string WAIT_MESSAGE = "Too many attempts. Wait {0} s.";

        private readonly Timer throttleTimer = new Timer { Interval = 250 };

        private PasswordAttemptThrottle throttle;

        private Point invalidLabelLocation;

        internal RequestPassword()
        {
            InitializeComponent();
            this.throttleTimer.Tick += this.ThrottleTimerTick;
            this.Shown += this.RequestPasswordShown;
            this.Disposed += (sender, args) => this.throttleTimer.Dispose();
        }

        private void RequestPasswordShown(object sender, EventArgs e)
        {
            if (this.throttle == null || this.throttle.RemainingDelay <= TimeSpan.Zero)
                return;

            this.invalidLabelLocation = this.label2.Location;
            this.label2.Location = new Point(this.label1.Left, this.label2.Top);
            this.SetInputEnabled(false);
            this.UpdateWaitMessage();
            this.throttleTimer.Start();
        }

        private void ThrottleTimerTick(object sender, EventArgs e)
        {
            if (this.throttle.RemainingDelay > TimeSpan.Zero)
            {
                this.UpdateWaitMessage();
                return;
            }

            this.throttleTimer.Stop();
            this.label2.Location = this.invalidLabelLocation;
            this.label2.Text = Translator.T(INVALID_PASSWORD);
            this.SetInputEnabled(true);
            this.PasswordTextBox.Focus();
        }

        private void UpdateWaitMessage()
        {
            int seconds = (int)Math.Ceiling(this.throttle.RemainingDelay.TotalSeconds);
            this.label2.Text = Translator.Format(WAIT_MESSAGE, Math.Max(seconds, 1));
            this.label2.Visible = true;
        }

        private void SetInputEnabled(bool enabled)
        {
            this.PasswordTextBox.Enabled = enabled;
            this.OkButton.Enabled = enabled;
        }

        private string Password
        {
            get
            {
                return this.PasswordTextBox.Text;
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            // Enter key reaches the accept button even if it is disabled
            if (!this.OkButton.Enabled)
                return;

            this.DialogResult = DialogResult.OK;
            this.Hide();
        }

        private void SetWrongPasswordInfo()
        {
            this.PasswordTextBox.Focus();
            this.PasswordTextBox.Text = "";
            this.label2.Visible = true;
        }

        private void CancelPasswordButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Hide();
        }

        private void PasswordTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!this.throttleTimer.Enabled)
                this.label2.Visible = false;
        }

        /// <summary>
        /// Prompts for the application master password. Repeated failed attempts are slowed down
        /// by <see cref="PasswordAttemptThrottle.MasterPassword"/>.
        /// </summary>
        internal static AuthenticationPrompt KnowsUserPassword(bool previousTrySuccess)
        {
            return KnowsUserPassword(previousTrySuccess, null, PasswordAttemptThrottle.MasterPassword);
        }

        /// <summary>
        /// Same as <see cref="KnowsUserPassword(bool)"/>, but lets the caller override the dialog
        /// title - e.g. to distinguish "confirm your master password" from "set an export password".
        /// </summary>
        internal static AuthenticationPrompt KnowsUserPassword(bool previousTrySuccess, string title)
        {
            return KnowsUserPassword(previousTrySuccess, title, null);
        }

        /// <summary>
        /// Same as <see cref="KnowsUserPassword(bool, string)"/>. If throttle is defined, the user has to wait
        /// before the password can be entered, after too many failed attempts.
        /// </summary>
        internal static AuthenticationPrompt KnowsUserPassword(bool previousTrySuccess, string title, PasswordAttemptThrottle throttle)
        {
            using (RequestPassword requestPassword = new RequestPassword())
            {
                requestPassword.throttle = throttle;
                if (!string.IsNullOrEmpty(title))
                    requestPassword.Text = title;

                if(previousTrySuccess)
                    requestPassword.SetWrongPasswordInfo();

                requestPassword.ShowDialog();
                return CreatePromptResults(requestPassword);
            }
        }

        private static AuthenticationPrompt CreatePromptResults(RequestPassword requestPassword)
        {
            bool canceled = requestPassword.DialogResult == DialogResult.Cancel;
            return new AuthenticationPrompt
                {
                    Password = requestPassword.Password,
                    Canceled = canceled
                };
        }
    }
}