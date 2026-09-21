using System;
using System.Windows.Forms;
using Terminals.Data;

namespace Terminals.Security
{
    internal partial class RequestPassword : Form
    {
        internal RequestPassword()
        {
            InitializeComponent();
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
            this.label2.Visible = false;
        }

        internal static AuthenticationPrompt KnowsUserPassword(bool previousTrySuccess)
        {
            return KnowsUserPassword(previousTrySuccess, null);
        }

        /// <summary>
        /// Same as <see cref="KnowsUserPassword(bool)"/>, but lets the caller override the dialog
        /// title - e.g. to distinguish "confirm your master password" from "set an export password".
        /// </summary>
        internal static AuthenticationPrompt KnowsUserPassword(bool previousTrySuccess, string title)
        {
            using (RequestPassword requestPassword = new RequestPassword())
            {
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