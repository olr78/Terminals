using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Terminals.Plugins.Rdp.Properties;
using Terminals.TerminalServices;
using Terminals.Localization;

namespace Terminals.Connections
{
    internal class RdpMenuVisitor : IToolbarExtender
    {
        internal const string TERMINAL_SERVER_MENU_BUTTON_NAME = "TerminalServerMenuButton";

        internal const string TYPE_PASSWORD_BUTTON_NAME = "RdpTypePasswordButton";

        private readonly ICurrenctConnectionProvider connectionProvider;

        private ToolStripDropDownButton TerminalServerMenuButton;

        private ToolStripButton typePasswordButton;

        /// <summary>
        /// Connection, for which the terminal server info is just loaded on background.
        /// </summary>
        private volatile RDPConnection loadingConnection;

        public RdpMenuVisitor(ICurrenctConnectionProvider connectionProvider)
        {
            this.connectionProvider = connectionProvider;
        }

        public void Visit(ToolStrip standardToolbar)
        {
            this.EnusereMenuCreated(standardToolbar);

            bool commandsAvailable = this.connectionProvider.CurrentConnection is RDPConnection;
            this.TerminalServerMenuButton.Visible = commandsAvailable;
            this.typePasswordButton.Visible = commandsAvailable;
        }

        private void EnusereMenuCreated(ToolStrip standardToolbar)
        {
            if (standardToolbar.Items[TERMINAL_SERVER_MENU_BUTTON_NAME] == null)
                this.CreateAdminSwitchButton(standardToolbar);

            var existing = standardToolbar.Items[TYPE_PASSWORD_BUTTON_NAME] as ToolStripButton;
            if (existing == null)
                this.CreateTypePasswordButton(standardToolbar);
            else
                this.typePasswordButton = existing;
        }

        private void CreateTypePasswordButton(ToolStrip standardToolbar)
        {
            this.typePasswordButton = new ToolStripButton();
            this.typePasswordButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.typePasswordButton.Image = CreateKeyImage();
            this.typePasswordButton.Name = TYPE_PASSWORD_BUTTON_NAME;
            this.typePasswordButton.Size = new Size(23, 22);
            this.typePasswordButton.Text = Translator.T("Type saved password");
            this.typePasswordButton.ToolTipText = Translator.T("Type the saved password into the remote session (logon or lock screen)");
            this.typePasswordButton.Click += this.TypePasswordButton_Click;
            standardToolbar.Items.Add(this.typePasswordButton);
        }

        private void TypePasswordButton_Click(object sender, EventArgs e)
        {
            var currentConnection = this.connectionProvider.CurrentConnection as RDPConnection;
            if (currentConnection != null)
                currentConnection.TypeSavedPassword();
        }

        /// <summary>
        /// Simple key icon, the plugin resources don't contain one.
        /// </summary>
        private static Image CreateKeyImage()
        {
            var image = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(image))
            using (var fill = new SolidBrush(Color.FromArgb(230, 180, 40)))
            using (var border = new Pen(Color.FromArgb(140, 100, 0)))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // ring
                graphics.FillEllipse(fill, 1, 4, 8, 8);
                graphics.DrawEllipse(border, 1, 4, 8, 8);
                graphics.FillEllipse(Brushes.White, 3.5f, 6.5f, 3, 3);
                // shaft and teeth
                graphics.FillRectangle(fill, 8, 7, 7, 2);
                graphics.DrawRectangle(border, 8, 7, 7, 2);
                graphics.FillRectangle(fill, 12, 9, 2, 3);
                graphics.DrawRectangle(border, 12, 9, 2, 3);
            }

            return image;
        }

        private void CreateAdminSwitchButton(ToolStrip standardToolbar)
        {
            this.TerminalServerMenuButton = new ToolStripDropDownButton();
            this.TerminalServerMenuButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.TerminalServerMenuButton.Image = Resources.server_network;
            this.TerminalServerMenuButton.ImageTransparentColor = Color.Magenta;
            this.TerminalServerMenuButton.Name = TERMINAL_SERVER_MENU_BUTTON_NAME;
            this.TerminalServerMenuButton.Size = new Size(29, 22);
            this.TerminalServerMenuButton.Text = Translator.T("Terminal Server");
            this.TerminalServerMenuButton.DropDownOpening += new EventHandler(this.TerminalServerMenuButton_DropDownOpening);
            standardToolbar.Items.Add(this.TerminalServerMenuButton);
        }

        private void TerminalServerMenuButton_DropDownOpening(object sender, EventArgs e)
        {
            TerminalServerMenuButton.DropDownItems.Clear();
            var currentConnection = this.connectionProvider.CurrentConnection as RDPConnection;
            if (currentConnection == null)
                return;

            this.AddDisabledItem("Loading...");
            if (this.loadingConnection == currentConnection)
                return;

            this.loadingConnection = currentConnection;
            ThreadPool.QueueUserWorkItem(this.LoadTerminalServer, currentConnection);
        }

        private void LoadTerminalServer(object state)
        {
            var connection = (RDPConnection)state;
            try
            {
                connection.LoadTerminalServer();
                Control owner = this.TerminalServerMenuButton.Owner;
                if (owner != null && !owner.IsDisposed)
                    owner.BeginInvoke(new Action<RDPConnection>(this.OnTerminalServerLoaded), connection);
            }
            catch (Exception exception)
            {
                // any exception on the thread pool thread would terminate the application
                Logging.Error("Unable to load terminal server menu", exception);
            }
            finally
            {
                this.loadingConnection = null;
            }
        }

        private void OnTerminalServerLoaded(RDPConnection connection)
        {
            try
            {
                // user may already switch to another tab or close the menu
                if (this.connectionProvider.CurrentConnection != connection || !this.TerminalServerMenuButton.DropDown.Visible)
                    return;

                this.TerminalServerMenuButton.DropDownItems.Clear();
                this.FillTerminalServerMenu(connection);
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to fill terminal server menu", exception);
            }
        }

        private void AddDisabledItem(string text)
        {
            var item = new ToolStripMenuItem(text);
            item.Enabled = false;
            this.TerminalServerMenuButton.DropDownItems.Add(item);
        }

        private void FillTerminalServerMenu(RDPConnection currentConnection)
        {
            if (currentConnection.IsTerminalServer)
            {
                var sessions = new ToolStripMenuItem(Resources.Sessions);
                sessions.Tag = currentConnection.Server;
                TerminalServerMenuButton.DropDownItems.Add(sessions);
                var svr = new ToolStripMenuItem(Resources.Server);
                svr.Tag = currentConnection.Server;
                TerminalServerMenuButton.DropDownItems.Add(svr);
                var sd = new ToolStripMenuItem(Resources.Shutdown);
                sd.Click += new EventHandler(sd_Click);
                sd.Tag = currentConnection.Server;
                svr.DropDownItems.Add(sd);
                var rb = new ToolStripMenuItem(Resources.Reboot);
                rb.Click += new EventHandler(sd_Click);
                rb.Tag = currentConnection.Server;
                svr.DropDownItems.Add(rb);

                if (currentConnection.Server.Sessions != null)
                {
                    foreach (TerminalServices.Session session in currentConnection.Server.Sessions)
                    {
                        if (session.Client.ClientName != "")
                        {
                            var sess = new ToolStripMenuItem(String.Format("{1} - {2} ({0})", session.State.ToString().Replace("WTS", ""), session.Client.ClientName, session.Client.UserName));
                            sess.Tag = session;
                            sessions.DropDownItems.Add(sess);
                            var msg = new ToolStripMenuItem(Resources.SendMessage);
                            msg.Click += new EventHandler(sd_Click);
                            msg.Tag = session;
                            sess.DropDownItems.Add(msg);

                            var lo = new ToolStripMenuItem(Resources.Logoff);
                            lo.Click += new EventHandler(sd_Click);
                            lo.Tag = session;
                            sess.DropDownItems.Add(lo);

                            if (session.IsTheActiveSession)
                            {
                                var lo1 = new ToolStripMenuItem(Resources.Logoff);
                                lo1.Click += new EventHandler(sd_Click);
                                lo1.Tag = session;
                                svr.DropDownItems.Add(lo1);
                            }
                        }
                    }
                }
            }
            else
            {
                this.AddDisabledItem("Terminal server info is not available (SMB/RPC not reachable)");
            }
        }

        private void sd_Click(object sender, EventArgs e)
        {
            var menu = sender as ToolStripMenuItem;
            if (menu != null)
            {
                if (menu.Text == Resources.Shutdown)
                {
                    var server = menu.Tag as TerminalServer;
                    if (server != null && MessageBox.Show(Resources.Areyousureyouwanttoshutthismachineoff, Resources.Confirmation, MessageBoxButtons.OKCancel) == DialogResult.OK)
                        TerminalServicesAPI.ShutdownSystem(server, false);
                }
                else if (menu.Text == Resources.Reboot)
                {
                    var server = menu.Tag as TerminalServer;
                    if (server != null && MessageBox.Show(Resources.Areyousureyouwanttorebootthismachine, Resources.Confirmation, MessageBoxButtons.OKCancel) == DialogResult.OK)
                        TerminalServicesAPI.ShutdownSystem(server, true);
                }
                else if (menu.Text == Resources.Logoff)
                {
                    var session = menu.Tag as Session;
                    if (session != null && MessageBox.Show(Resources.Areyousureyouwanttologthissessionoff, Resources.Confirmation, MessageBoxButtons.OKCancel) == DialogResult.OK)
                        TerminalServicesAPI.LogOffSession(session, false);
                }
                else if (menu.Text == Resources.SendMessage)
                {
                    var session = menu.Tag as Session;
                    TerminalServer.SendMessageToSession(session);
                }
            }
        }
    }
}