using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Terminals.Configuration;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Forms.Controls;
using Terminals.Integration;
using Terminals.Integration.Export;
using Terminals.Security;

namespace Terminals.Forms
{
    internal partial class ExportForm : Form
    {
        private readonly IPersistence persistence;
        private readonly FavoriteTreeListLoader treeLoader;
        private readonly Exporters exporters;

        private readonly ConnectionManager connectionManager;

        private readonly FavoriteIcons favoriteIcons;

        public ExportForm(IPersistence persistence, ConnectionManager connectionManager, FavoriteIcons favoriteIcons)
        {
            this.persistence = persistence;
            this.InitializeComponent();

            this.favoriteIcons = favoriteIcons;
            this.treeLoader = new FavoriteTreeListLoader(this.favsTree, this.persistence, this.favoriteIcons);
            this.treeLoader.LoadRootNodes();
            this.connectionManager = connectionManager;
            this.exporters = new Exporters(this.persistence, this.connectionManager);
            this.saveFileDialog.Filter = this.exporters.GetProvidersDialogFilter();
        }

        private void ExportForm_Load(object sender, EventArgs e)
        {
            this.favsTree.AssignServices(this.persistence, this.favoriteIcons, this.connectionManager);
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        
        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (this.checkBox1.Checked && !this.encryptFileCheckBox.Checked)
            {
                MessageBox.Show("Passwords can only be exported into an encrypted file. Enable \"Encrypt exported file with a password\" first.",
                    "Terminals export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string encryptionPassword = null;
            if (this.encryptFileCheckBox.Checked && !this.TryGetNewExportPassword(out encryptionPassword))
                return;

            if (this.saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (this.checkBox1.Checked && !this.ConfirmMasterPassword())
                    return;

                if (this.favsTree.SelectedNode != null)
                    this.RunExport(encryptionPassword);

                string message = "Done exporting, you can find your exported file at " + this.saveFileDialog.FileName;
                MessageBox.Show(message, "Terminals export");
                this.Close();
            }
        }

        /// <summary>
        /// Asks for a new password to protect the exported file, entered twice to confirm.
        /// This is independent of the persistence master password, so the export stays
        /// portable to another machine/install.
        /// </summary>
        private bool TryGetNewExportPassword(out string password)
        {
            password = null;
            MessageBox.Show("Enter a password to protect the exported file. You will need to enter it again when importing.",
                "Terminals export", MessageBoxButtons.OK, MessageBoxIcon.Information);

            AuthenticationPrompt first = RequestPassword.KnowsUserPassword(false, "Terminals Export - Set Password");
            if (first.Canceled)
                return false;

            if (string.IsNullOrEmpty(first.Password))
            {
                MessageBox.Show("Password can't be empty.", "Terminals export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            AuthenticationPrompt second = RequestPassword.KnowsUserPassword(false, "Terminals Export - Confirm Password");
            if (second.Canceled)
                return false;

            if (first.Password != second.Password)
            {
                MessageBox.Show("Passwords didn't match.", "Terminals export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            password = first.Password;
            return true;
        }

        private bool ConfirmMasterPassword()
        {
            if (!AuthenticationSequence.IsMasterPasswordDefined())
                return true;

            bool firstTry = true;
            while (true)
            {
                AuthenticationPrompt prompt = RequestPassword.KnowsUserPassword(!firstTry);
                if (prompt.Canceled)
                    return false;

                if (PasswordFunctions2.MasterPasswordIsValid(prompt.Password, Settings.Instance.MasterPasswordHash))
                    return true;

                firstTry = false;
            }
        }

        private void RunExport(string encryptionPassword)
        {
            List<FavoriteConfigurationElement> favorites = this.GetFavoritesToExport();
            // filter index is 1 based
            int filterSplitIndex = (this.saveFileDialog.FilterIndex - 1) * 2;
            string providerFilter = this.saveFileDialog.Filter.Split('|')[filterSplitIndex];
            var options = new ExportOptions
                {
                    ProviderFilter = providerFilter,
                    Favorites = favorites,
                    FileName = this.saveFileDialog.FileName,
                    IncludePasswords = this.checkBox1.Checked,
                    EncryptFile = this.encryptFileCheckBox.Checked,
                    EncryptionPassword = encryptionPassword
                };
            this.exporters.Export(options);
        }

        private List<FavoriteConfigurationElement> GetFavoritesToExport()
        {
            List<IFavorite> favorites = TreeListNodes.FindAllCheckedFavorites(this.favsTree.Nodes);
            return this.ConvertFavoritesToExport(favorites);
        }

        private List<FavoriteConfigurationElement> ConvertFavoritesToExport(List<IFavorite> favorites)
        {
            return favorites.Distinct()
                .Select(favorite => ModelConverterV2ToV1.ConvertToFavorite(favorite, this.persistence, this.connectionManager))
                .ToList();
        }

        private void FavsTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            var groupNode = e.Node as GroupTreeNode;
            if (groupNode != null)
                groupNode.CheckChildsByParent();
        }

        private void BtnSelect_Click(object sender, EventArgs e)
        {
            // dont expand only load complete subtree
            this.treeLoader.LoadGroupNodesRecursive();
            TreeListNodes.CheckChildNodesRecursive(this.favsTree.Nodes, true);
        }

        private void ExportForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.treeLoader.UnregisterEvents();
        }
    }
}
