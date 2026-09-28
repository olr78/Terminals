using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using Timer = System.Windows.Forms.Timer;

namespace Terminals.Connections
{
    /// <summary>
    /// Some servers require the client to ask for the password on every connection. The RDP client then shows
    /// the "Windows Security" credential dialog in this process and ignores the password configured by Terminals.
    /// This watcher finds the dialog, fills the saved password and confirms it.
    /// </summary>
    internal sealed class CredentialPromptFiller : IDisposable
    {
        private const string CREDENTIAL_DIALOG_CLASS = "Credential Dialog Xaml Host";

        /// <summary>
        /// Stop watching, if the connection doesn't show the dialog in this time.
        /// </summary>
        private const int WATCH_DURATION = 60000;

        private const int CHECK_INTERVAL = 300;

        private readonly Timer timer = new Timer();

        private readonly Func<string> passwordProvider;

        private readonly string connectionName;

        private DateTime watchStarted;

        private bool filled;

        internal CredentialPromptFiller(string connectionName, Func<string> passwordProvider)
        {
            this.connectionName = connectionName;
            this.passwordProvider = passwordProvider;
            this.timer.Interval = CHECK_INTERVAL;
            this.timer.Tick += this.Timer_Tick;
        }

        /// <summary>
        /// Starts to watch for the credential dialog. The dialog is filled only once,
        /// if the password is wrong, the user has to type it.
        /// </summary>
        internal void Start()
        {
            this.watchStarted = DateTime.Now;
            this.filled = false;
            this.timer.Start();
        }

        internal void Stop()
        {
            this.timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if ((DateTime.Now - this.watchStarted).TotalMilliseconds > WATCH_DURATION)
            {
                this.Stop();
                return;
            }

            IntPtr dialog = FindCredentialDialog();
            if (dialog == IntPtr.Zero || this.filled)
                return;

            this.filled = true;
            this.Stop();
            string password = this.passwordProvider();
            Logging.Info("Password auto typing: credential dialog found for " + this.connectionName);
            // UI Automation must not be called from the thread, which owns the dialog
            ThreadPool.QueueUserWorkItem(state => this.Fill(dialog, password));
        }

        private void Fill(IntPtr dialog, string password)
        {
            try
            {
                if (TryFill(dialog, password))
                    Logging.Info("Password auto typing: credential dialog filled for " + this.connectionName);
                else
                    Logging.Info("Password auto typing: password field or OK button not found in credential dialog");
            }
            catch (Exception exception)
            {
                // any exception on the thread pool thread would terminate the application
                Logging.Error("Password auto typing: unable to fill the credential dialog", exception);
            }
        }

        private static bool TryFill(IntPtr dialog, string password)
        {
            AutomationElement root = AutomationElement.FromHandle(dialog);
            // the dialog content is created asynchronously
            AutomationElement passwordBox = WaitFor(root, new PropertyCondition(AutomationElement.IsPasswordProperty, true));
            if (passwordBox == null)
                return false;

            object pattern;
            if (passwordBox.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
            {
                ((ValuePattern)pattern).SetValue(password);
            }
            else
            {
                passwordBox.SetFocus();
                TypeUnicode(password);
            }

            AutomationElement okButton = WaitFor(root, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton"));
            if (okButton == null || !okButton.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                return false;

            ((InvokePattern)pattern).Invoke();
            return true;
        }

        private static AutomationElement WaitFor(AutomationElement root, Condition condition)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                AutomationElement found = root.FindFirst(TreeScope.Descendants, condition);
                if (found != null)
                    return found;

                Thread.Sleep(150);
            }

            return null;
        }

        private static IntPtr FindCredentialDialog()
        {
            int processId = Process.GetCurrentProcess().Id;
            IntPtr found = IntPtr.Zero;
            EnumWindows((window, param) =>
            {
                int windowProcess;
                GetWindowThreadProcessId(window, out windowProcess);
                if (windowProcess != processId || !IsWindowVisible(window))
                    return true;

                var className = new StringBuilder(64);
                GetClassName(window, className, className.Capacity);
                if (className.ToString() != CREDENTIAL_DIALOG_CLASS)
                    return true;

                found = window;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>
        /// Fallback, if the password box doesn't support value pattern.
        /// Unicode input doesn't depend on the keyboard layout.
        /// </summary>
        private static void TypeUnicode(string text)
        {
            var inputs = new INPUT[text.Length * 2];
            for (int index = 0; index < text.Length; index++)
            {
                inputs[index * 2] = CreateUnicodeInput(text[index], false);
                inputs[index * 2 + 1] = CreateUnicodeInput(text[index], true);
            }

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static INPUT CreateUnicodeInput(char character, bool keyUp)
        {
            const uint KEYEVENTF_UNICODE = 0x0004;
            const uint KEYEVENTF_KEYUP = 0x0002;
            var input = new INPUT { type = 1 };
            input.ki.wScan = character;
            input.ki.dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0);
            return input;
        }

        public void Dispose()
        {
            this.timer.Dispose();
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public KEYBDINPUT ki;
            // padding to the size of the largest union member (MOUSEINPUT) on both 32 and 64 bit
            private readonly int padding1;
            private readonly int padding2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    }
}
