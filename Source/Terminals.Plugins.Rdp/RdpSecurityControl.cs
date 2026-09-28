using System;
using System.Windows.Forms;
using Terminals.Data;

namespace Terminals.Forms.EditFavorite
{
    internal partial class RdpSecurityControl : UserControl, IProtocolOptionsControl
    {
        public RdpSecurityControl()
        {
            InitializeComponent();
        }

        private void SecuritySettingsEnabledCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            this.panel2.Enabled = this.SecuritySettingsEnabledCheckbox.Checked;
        }

        private void AutoTypePassword_CheckedChanged(object sender, EventArgs e)
        {
            this.numAutoTypeDelay.Enabled = this.chkAutoTypePassword.Checked;
            this.lblAutoTypeDelay.Enabled = this.chkAutoTypePassword.Checked;
        }

        public void SaveTo(IFavorite favorite)
        {
            var rdpOptions = favorite.ProtocolProperties as RdpOptions;
            if (rdpOptions == null)
                return;

            rdpOptions.ConnectToConsole = this.chkConnectToConsole.Checked;

            rdpOptions.Security.EnableTLSAuthentication = this.EnableTLSAuthenticationCheckbox.Checked;
            rdpOptions.Security.EnableNLAAuthentication = this.EnableNLAAuthenticationCheckbox.Checked;
            rdpOptions.Security.EnableEncryption = this.EnableEncryptionCheckbox.Checked;
            rdpOptions.Security.AutoTypePassword = this.chkAutoTypePassword.Checked;
            rdpOptions.Security.AutoTypePasswordDelay = (int)this.numAutoTypeDelay.Value;

            rdpOptions.Security.Enabled = this.SecuritySettingsEnabledCheckbox.Checked;
            if (this.SecuritySettingsEnabledCheckbox.Checked)
            {
                rdpOptions.Security.WorkingFolder = this.SecurityWorkingFolderTextBox.Text;
                rdpOptions.Security.StartProgram = this.SecuriytStartProgramTextbox.Text;
                rdpOptions.FullScreen = this.SecurityStartFullScreenCheckbox.Checked;
            }
        }

        public void LoadFrom(IFavorite favorite)
        {
            var rdpOptions = favorite.ProtocolProperties as RdpOptions;
            if (rdpOptions == null)
                return;

            this.chkConnectToConsole.Checked = rdpOptions.ConnectToConsole;
            this.EnableTLSAuthenticationCheckbox.Checked = rdpOptions.Security.EnableTLSAuthentication;
            this.EnableNLAAuthenticationCheckbox.Checked = rdpOptions.Security.EnableNLAAuthentication;
            this.EnableEncryptionCheckbox.Checked = rdpOptions.Security.EnableEncryption;
            this.chkAutoTypePassword.Checked = rdpOptions.Security.AutoTypePassword;
            int delay = rdpOptions.Security.AutoTypePasswordDelay;
            this.numAutoTypeDelay.Value = Math.Max(this.numAutoTypeDelay.Minimum, Math.Min(this.numAutoTypeDelay.Maximum, delay));
            this.AutoTypePassword_CheckedChanged(this, EventArgs.Empty);
            this.SecuritySettingsEnabledCheckbox.Checked = rdpOptions.Security.Enabled;
            this.SecurityWorkingFolderTextBox.Text = rdpOptions.Security.WorkingFolder;
            this.SecuriytStartProgramTextbox.Text = rdpOptions.Security.StartProgram;
            this.SecurityStartFullScreenCheckbox.Checked = rdpOptions.FullScreen;
        }
    }
}
