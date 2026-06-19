using System;
using System.Windows.Forms;
using Terminals.Services;

namespace Terminals
{
    internal partial class AboutForm : Form
    {
        private readonly string persistenceName;

        public AboutForm(string persistenceName)
        {
            InitializeComponent();

            this.persistenceName = persistenceName;
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void LblTerminals_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ExternalLinks.ShowReleasePage();
        }

        private void LinkLabel4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ExternalLinks.OpenAuthorPage();
        }

        private void AboutForm_Load(object sender, EventArgs e)
        {
            this.titleLabel.Text += string.Format("({0})", Program.Info.Description);
            this.lblVersion.Text = Program.Info.GetAboutText(persistenceName);
            this.textBox1.Text = this.FormatDetails();
        }

        private string FormatDetails()
        {
            DateTime dt = Program.Info.BuildDate;
            const string DETAILS = "{0}\r\n" +
                                   "This version of terminals was build for you on {1} at {2}\r\n\r\n" +
                                   "Version: {3}\r\n" +
                                   "OSVersion: {4}\r\n" +
                                   "ProcessorCount: {5}\r\n" +
                                   "Framework Version: {6}\r\n" +
                                   "WorkingSet: {7}\r\n" +
                                   "Is 64bit OS: {8}\r\n" +
                                   "Is 64bit Proces: {9}\r\n\r\n";

            return string.Format(DETAILS,
                                    this.textBox1.Text,
                                    dt.ToLongDateString(),
                                    dt.ToLongTimeString(),
                                    Program.Info.DLLVersion,
                                    Environment.OSVersion,
                                    Environment.ProcessorCount,
                                    Environment.Version,
                                    Environment.WorkingSet,
                                    Native.Wow.Is64BitOperatingSystem,
                                    Native.Wow.Is64BitProcess);
        }
    }
}