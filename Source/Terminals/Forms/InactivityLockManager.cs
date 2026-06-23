using System;
using System.Windows.Forms;
using Terminals.Configuration;
using Terminals.Data;
using Terminals.Security;

namespace Terminals.Forms
{
    internal sealed class InactivityLockManager : IMessageFilter, IDisposable
    {
        private const int TIMEOUT_MS     = 30 * 60 * 1000; // 30 minutes
        private const int CHECK_MS       = 30 * 1000;       // poll every 30 s

        private const int WM_MOUSEMOVE   = 0x0200;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        private readonly Timer checkTimer;
        private DateTime lastActivity;
        private bool isLocked;

        internal InactivityLockManager()
        {
            this.lastActivity = DateTime.UtcNow;
            this.checkTimer = new Timer { Interval = CHECK_MS };
            this.checkTimer.Tick += this.OnCheckTick;
        }

        internal void Start()
        {
            Application.AddMessageFilter(this);
            this.lastActivity = DateTime.UtcNow;
            this.checkTimer.Start();
        }

        internal void Stop()
        {
            this.checkTimer.Stop();
            Application.RemoveMessageFilter(this);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (!this.isLocked)
            {
                switch (m.Msg)
                {
                    case WM_MOUSEMOVE:
                    case WM_KEYDOWN:
                    case WM_SYSKEYDOWN:
                    case WM_LBUTTONDOWN:
                    case WM_RBUTTONDOWN:
                    case WM_MBUTTONDOWN:
                        this.lastActivity = DateTime.UtcNow;
                        break;
                }
            }
            return false;
        }

        private void OnCheckTick(object sender, EventArgs e)
        {
            if (!this.isLocked &&
                AuthenticationSequence.IsMasterPasswordDefined() &&
                (DateTime.UtcNow - this.lastActivity).TotalMilliseconds >= TIMEOUT_MS)
            {
                this.LockSession();
            }
        }

        private void LockSession()
        {
            this.isLocked = true;
            this.checkTimer.Stop();

            bool wrongPassword = false;
            while (true)
            {
                AuthenticationPrompt prompt = RequestPassword.KnowsUserPassword(wrongPassword);
                if (!prompt.Canceled &&
                    PasswordFunctions2.MasterPasswordIsValid(prompt.Password, Settings.Instance.MasterPasswordHash))
                    break;

                // show "wrong password" hint only after a failed attempt (not after cancel)
                wrongPassword = !prompt.Canceled;
            }

            this.lastActivity = DateTime.UtcNow;
            this.isLocked = false;
            this.checkTimer.Start();
        }

        public void Dispose()
        {
            this.Stop();
            this.checkTimer.Dispose();
        }
    }
}
