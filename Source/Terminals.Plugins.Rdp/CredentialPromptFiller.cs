using System;
using System.Collections.Generic;
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
    /// the "Windows Security" credential dialog and ignores the password configured by Terminals.
    /// This watcher finds the dialog, fills the saved password and confirms it.
    /// On Windows 10 the dialog is hosted in this process, on Windows 11 it is shown
    /// by the CredentialUIBroker process. The dialog of other process is filled only,
    /// if it mentions the server of this connection.
    /// </summary>
    internal sealed class CredentialPromptFiller : IDisposable
    {
        private const string CREDENTIAL_DIALOG_CLASS = "Credential Dialog Xaml Host";

        private const string CREDENTIAL_BROKER_PROCESS = "CredentialUIBroker";

        /// <summary>
        /// Stop watching, if the connection doesn't show the dialog in this time.
        /// </summary>
        private const int WATCH_DURATION = 60000;

        private const int CHECK_INTERVAL = 300;

        private readonly Timer timer = new Timer();

        private readonly Func<string> passwordProvider;

        private readonly string connectionName;

        private readonly string serverName;

        private readonly string userName;

        /// <summary>
        /// Dialog windows already checked, prevents repeated filling of the same dialog.
        /// </summary>
        private readonly HashSet<IntPtr> examined = new HashSet<IntPtr>();

        /// <summary>
        /// Process names by id, the windows are enumerated several times per second.
        /// </summary>
        private readonly Dictionary<int, string> processNames = new Dictionary<int, string>();

        private DateTime watchStarted;

        private volatile bool filled;

        internal CredentialPromptFiller(string connectionName, string serverName, string userName, Func<string> passwordProvider)
        {
            this.connectionName = connectionName;
            this.serverName = serverName;
            this.userName = userName;
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
            this.examined.Clear();
            this.processNames.Clear();
            this.timer.Start();
        }

        internal void Stop()
        {
            this.timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (this.filled)
            {
                this.Stop();
                return;
            }

            if ((DateTime.Now - this.watchStarted).TotalMilliseconds > WATCH_DURATION)
            {
                this.Stop();
                Logging.Info("Password auto typing: credential dialog not found for " + this.connectionName + ". " + this.DescribeCandidates());
                return;
            }

            foreach (DialogCandidate candidate in this.FindCredentialDialogs())
            {
                if (!this.examined.Add(candidate.Window))
                    continue;

                Logging.Info(string.Format("Password auto typing: credential dialog found for {0} in process {1}",
                    this.connectionName, candidate.ProcessName));
                string password = this.passwordProvider();
                // UI Automation must not be called from the thread, which owns the dialog
                ThreadPool.QueueUserWorkItem(state => this.Fill(candidate, password));
            }
        }

        private void Fill(DialogCandidate candidate, string password)
        {
            try
            {
                AutomationElement root = AutomationElement.FromHandle(candidate.Window);
                if (!candidate.OwnProcess && !IsOwnedByThisProcess(candidate.Window) && !this.MentionsConnection(root))
                {
                    Logging.Info("Password auto typing: credential dialog isn't owned by Terminals and doesn't mention server or user of " +
                        this.connectionName + ", skipped. Dialog texts: " + DescribeTexts(root));
                    return;
                }

                if (TryFill(root, password))
                {
                    this.filled = true;
                    Logging.Info("Password auto typing: credential dialog filled for " + this.connectionName);
                }
                else
                {
                    Logging.Info("Password auto typing: password field or OK button not found in credential dialog");
                }
            }
            catch (Exception exception)
            {
                // any exception on the thread pool thread would terminate the application
                Logging.Error("Password auto typing: unable to fill the credential dialog", exception);
            }
        }

        /// <summary>
        /// The credential broker shows the dialog for the window, which requested it,
        /// the owner window belongs to this process.
        /// </summary>
        private static bool IsOwnedByThisProcess(IntPtr dialog)
        {
            const uint GW_OWNER = 4;
            int currentProcess = Process.GetCurrentProcess().Id;
            IntPtr owner = GetWindow(dialog, GW_OWNER);
            while (owner != IntPtr.Zero)
            {
                int ownerProcess;
                GetWindowThreadProcessId(owner, out ownerProcess);
                if (ownerProcess == currentProcess)
                    return true;

                owner = GetWindow(owner, GW_OWNER);
            }

            return false;
        }

        /// <summary>
        /// The dialog texts contain the target server, e.g. "These credentials will be used to connect to server",
        /// or at least the user of the connection (Windows 11 shows only the user tile).
        /// The server may be shown without the domain part or with port, the user with or without domain.
        /// </summary>
        private bool MentionsConnection(AutomationElement root)
        {
            var expected = new List<string>();
            if (!string.IsNullOrEmpty(this.serverName))
            {
                string server = this.serverName.Trim();
                int dot = server.IndexOf('.');
                bool isAddress = server.Length > 0 && char.IsDigit(server[0]);
                expected.Add(server);
                if (!isAddress && dot > 0)
                    expected.Add(server.Substring(0, dot));
            }

            string user = GetUserWithoutDomain(this.userName);
            if (!string.IsNullOrEmpty(user))
                expected.Add(user);

            if (expected.Count == 0)
                return false;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, Condition.TrueCondition))
                {
                    string text = GetText(element);
                    if (string.IsNullOrEmpty(text))
                        continue;

                    foreach (string value in expected)
                    {
                        if (text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }

                // the dialog content is created asynchronously
                Thread.Sleep(150);
            }

            return false;
        }

        /// <summary>
        /// "DOMAIN\user" or "user@domain" gives "user".
        /// </summary>
        internal static string GetUserWithoutDomain(string user)
        {
            if (string.IsNullOrEmpty(user))
                return null;

            string result = user.Trim();
            int backslash = result.LastIndexOf('\\');
            if (backslash >= 0)
                result = result.Substring(backslash + 1);

            int at = result.IndexOf('@');
            if (at > 0)
                result = result.Substring(0, at);

            // too short names would match unrelated texts
            return result.Length >= 3 ? result : null;
        }

        private static string GetText(AutomationElement element)
        {
            string name = element.Current.Name;
            object pattern;
            if (element.Current.IsPassword || !element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
                return name;

            return name + " " + ((ValuePattern)pattern).Current.Value;
        }

        /// <summary>
        /// Diagnostics, why the dialog wasn't recognized. The password field is never included.
        /// </summary>
        private static string DescribeTexts(AutomationElement root)
        {
            var texts = new List<string>();
            foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, Condition.TrueCondition))
            {
                string text = GetText(element);
                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text.Trim()))
                    continue;

                texts.Add("'" + text.Trim() + "'");
                if (texts.Count == 10)
                    break;
            }

            return string.Join(", ", texts.ToArray());
        }

        private static bool TryFill(AutomationElement root, string password)
        {
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

            AutomationElement okButton = WaitFor(root, new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton")) ??
                                         FindButton(root, "OK");
            if (okButton == null || !okButton.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                return false;

            ((InvokePattern)pattern).Invoke();
            return true;
        }

        private static AutomationElement FindButton(AutomationElement root, string name)
        {
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty, name));
            return root.FindFirst(TreeScope.Descendants, condition);
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

        private List<DialogCandidate> FindCredentialDialogs()
        {
            int currentProcess = Process.GetCurrentProcess().Id;
            var found = new List<DialogCandidate>();
            EnumWindows((window, param) =>
            {
                if (!IsWindowVisible(window))
                    return true;

                int windowProcess;
                GetWindowThreadProcessId(window, out windowProcess);
                string className = GetClassName(window);
                bool ownProcess = windowProcess == currentProcess;
                if (ownProcess && className == CREDENTIAL_DIALOG_CLASS)
                {
                    found.Add(new DialogCandidate(window, true, "Terminals"));
                    return true;
                }

                if (!ownProcess && (className == CREDENTIAL_DIALOG_CLASS || this.IsCredentialBroker(windowProcess)))
                    found.Add(new DialogCandidate(window, false, this.GetProcessName(windowProcess)));

                return true;
            }, IntPtr.Zero);
            return found;
        }

        private bool IsCredentialBroker(int processId)
        {
            return string.Equals(this.GetProcessName(processId), CREDENTIAL_BROKER_PROCESS, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Diagnostics for the log: class and process of windows, which could be the credential dialog.
        /// Window titles aren't logged, they may contain user data.
        /// </summary>
        private string DescribeCandidates()
        {
            var described = new List<string>();
            EnumWindows((window, param) =>
            {
                if (!IsWindowVisible(window))
                    return true;

                string className = GetClassName(window);
                int processId;
                GetWindowThreadProcessId(window, out processId);
                string processName = this.GetProcessName(processId);
                if (className.IndexOf("Credential", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    processName.IndexOf("Credential", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    processName.Equals("consent", StringComparison.OrdinalIgnoreCase) ||
                    processId == Process.GetCurrentProcess().Id)
                {
                    described.Add(processName + "/" + className);
                }

                return true;
            }, IntPtr.Zero);
            return "Visible windows: " + string.Join(", ", described.ToArray());
        }

        private static string GetClassName(IntPtr window)
        {
            var className = new StringBuilder(128);
            GetClassName(window, className, className.Capacity);
            return className.ToString();
        }

        private string GetProcessName(int processId)
        {
            string name;
            if (this.processNames.TryGetValue(processId, out name))
                return name;

            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    name = process.ProcessName;
                }
            }
            catch (Exception)
            {
                name = string.Empty;
            }

            this.processNames[processId] = name;
            return name;
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

        private sealed class DialogCandidate
        {
            internal IntPtr Window { get; private set; }

            internal bool OwnProcess { get; private set; }

            internal string ProcessName { get; private set; }

            internal DialogCandidate(IntPtr window, bool ownProcess, string processName)
            {
                this.Window = window;
                this.OwnProcess = ownProcess;
                this.ProcessName = processName;
            }
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
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

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
