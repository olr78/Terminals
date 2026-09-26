using System;
using System.Windows.Forms;
using Terminals.Configuration;
using Terminals.Localization;

namespace Terminals.Forms
{
    internal partial class InterfaceOptionPanel : UserControl, IOptionPanel
    {
        private readonly Settings settings = Settings.Instance;

        /// <summary>
        /// Language codes in the same order as the items of the language combo box.
        /// </summary>
        private static readonly string[] languages = new[] { Translator.AUTOMATIC, Translator.ENGLISH, Translator.UKRAINIAN };

        public InterfaceOptionPanel()
        {
            InitializeComponent();

            // language names are always shown in their own language
            this.cmbLanguage.Items.AddRange(new object[] { Translator.T("Automatic (Windows language)"), "English", "Українська" });
        }

        public void LoadSettings()
        {
            this.chkEnableGroupsMenu.Checked = settings.EnableGroupsMenu;
            this.chkMinimizeToTrayCheckbox.Checked = settings.MinimizeToTray;
            this.chkShowUserNameInTitle.Checked = settings.ShowUserNameInTitle;
            this.chkShowInformationToolTips.Checked = settings.ShowInformationToolTips;
            this.chkShowFullInfo.Checked = settings.ShowFullInformationToolTips;
            this.cmbLanguage.SelectedIndex = Math.Max(0, Array.IndexOf(languages, settings.Language ?? string.Empty));

            if (settings.Office2007BlueFeel)
                this.RenderBlueRadio.Checked = true;
            else if (settings.Office2007BlackFeel)
                this.RenderBlackRadio.Checked = true;
            else
                this.RenderNormalRadio.Checked = true;
        }

        public void SaveSettings()
        {
            settings.EnableGroupsMenu = this.chkEnableGroupsMenu.Checked;
            settings.MinimizeToTray = this.chkMinimizeToTrayCheckbox.Checked;
            settings.ShowUserNameInTitle = this.chkShowUserNameInTitle.Checked;
            settings.ShowInformationToolTips = this.chkShowInformationToolTips.Checked;
            settings.ShowFullInformationToolTips = this.chkShowFullInfo.Checked;
            settings.Language = languages[Math.Max(0, this.cmbLanguage.SelectedIndex)];

            settings.Office2007BlackFeel = false;
            settings.Office2007BlueFeel = false;

            if (this.RenderBlueRadio.Checked)
                settings.Office2007BlueFeel = true;
            else if (this.RenderBlackRadio.Checked)
                settings.Office2007BlackFeel = true;
        }

        private void chkShowInformationToolTips_CheckedChanged(object sender, EventArgs e)
        {
            this.chkShowFullInfo.Enabled = this.chkShowInformationToolTips.Checked;
            if (!this.chkShowInformationToolTips.Checked)
            {
                this.chkShowFullInfo.Checked = false;
            }
        }
    }
}
