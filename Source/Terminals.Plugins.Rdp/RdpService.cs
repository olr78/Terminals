using System;
using Terminals.TerminalServices;

namespace Terminals.Connections
{
    /// <summary>
    /// Loads terminal server sessions info on demand only.
    /// WTS API talks to the server over SMB/RPC (TCP 445/139), so it must not be called
    /// automatically after each RDP connect - the server is often reachable only on the RDP port.
    /// </summary>
    internal class RdpService
    {
        /// <summary>
        /// How long the last result is reused, before the server is queried again.
        /// </summary>
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromSeconds(60);

        private readonly object loadLock = new object();

        private DateTime lastLoaded = DateTime.MinValue;

        public TerminalServer Server { get; private set; }

        public bool IsTerminalServer { get; private set; }

        /// <summary>
        /// Blocking call, may take tens of seconds, if the server doesn't respond on SMB ports.
        /// Call it only from background thread.
        /// </summary>
        public void LoadServer(string serverName)
        {
            lock (this.loadLock)
            {
                if (DateTime.Now - this.lastLoaded < CACHE_DURATION)
                    return;

                try
                {
                    this.Server = TerminalServer.LoadServer(serverName);
                    this.IsTerminalServer = this.Server.IsATerminalServer && this.Server.Sessions != null;
                }
                catch (Exception exception)
                {
                    const string MESSAGE = "Checked to see if {0} is a terminal server. {0} is not a terminal server";
                    string message = String.Format(MESSAGE, serverName);
                    Logging.Error(message, exception);
                    this.Server = null;
                    this.IsTerminalServer = false;
                }

                this.lastLoaded = DateTime.Now;
            }
        }
    }
}
