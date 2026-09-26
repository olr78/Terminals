using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Terminals.Localization
{
    /// <summary>
    /// Connects the translator to the application: translates each form when it is activated for the first time,
    /// translates the message boxes and the string resources of Terminals assemblies.
    /// Has to be installed on the user interface thread before the first window is shown.
    /// </summary>
    public static class LocalizationHooks
    {
        private const int WH_CBT = 5;
        private const int HCBT_ACTIVATE = 5;
        private const int HCBT_DESTROYWND = 4;
        private const int WM_GETFONT = 0x0031;
        private const int MESSAGE_TEXT_ID = 0xFFFF;
        private const uint DT_CALCRECT = 0x400;
        private const uint DT_WORDBREAK = 0x10;
        private const uint DT_EXPANDTABS = 0x40;
        private const uint DT_NOPREFIX = 0x800;
        private const uint SWP_NOMOVE = 0x2;
        private const uint SWP_NOZORDER = 0x4;
        private const uint SWP_NOACTIVATE = 0x10;
        private const uint SWP_NOSIZE = 0x1;

        private static readonly Dictionary<int, string> messageBoxButtons = new Dictionary<int, string>
        {
            { 1, "OK" }, { 2, "\u0421\u043a\u0430\u0441\u0443\u0432\u0430\u0442\u0438" }, { 3, "&\u041f\u0435\u0440\u0435\u0440\u0432\u0430\u0442\u0438" }, { 4, "&\u041f\u043e\u0432\u0442\u043e\u0440\u0438\u0442\u0438" }, { 5, "\u041f\u0440&\u043e\u043f\u0443\u0441\u0442\u0438\u0442\u0438" },
            { 6, "&\u0422\u0430\u043a" }, { 7, "&\u041d\u0456" }, { 9, "\u0414\u043e\u0432\u0456\u0434\u043a\u0430" }, { 10, "&\u041f\u043e\u0432\u0442\u043e\u0440\u0438\u0442\u0438" }, { 11, "&\u041f\u0440\u043e\u0434\u043e\u0432\u0436\u0438\u0442\u0438" }
        };

        private static readonly HashSet<IntPtr> translatedDialogs = new HashSet<IntPtr>();

        private static HookProc hookProc;

        private static IntPtr hook = IntPtr.Zero;

        /// <summary>
        /// Loads the translation and installs the hooks for current thread.
        /// </summary>
        public static void Install(string configuredLanguage)
        {
            Translator.Initialize(configuredLanguage);
            if (!Translator.IsActive)
                return;

            try
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("uk-UA");
            }
            catch (CultureNotFoundException)
            {
                // not important, only the thread default
            }

            PatchResourceManagers();
            InstallWindowsHook();
        }

        private static void InstallWindowsHook()
        {
            hookProc = HookCallback;
            hook = SetWindowsHookEx(WH_CBT, hookProc, IntPtr.Zero, GetCurrentThreadId());
            if (hook == IntPtr.Zero)
                Logging.Error("Unable to install localization hook, error " + Marshal.GetLastWin32Error());
        }

        private static IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code == HCBT_ACTIVATE)
                    OnActivate(wParam);
                else if (code == HCBT_DESTROYWND)
                    translatedDialogs.Remove(wParam);
            }
            catch (Exception exception)
            {
                // never let the exception escape into native code
                Logging.Error("Localization hook failed", exception);
            }

            return CallNextHookEx(hook, code, wParam, lParam);
        }

        private static void OnActivate(IntPtr window)
        {
            var form = Control.FromHandle(window) as Form;
            if (form != null)
            {
                UiTranslator.TranslateForm(form);
                return;
            }

            if (!translatedDialogs.Contains(window) && IsMessageBox(window))
            {
                translatedDialogs.Add(window);
                TranslateMessageBox(window);
            }
        }

        private static bool IsMessageBox(IntPtr window)
        {
            if (GetClassName(window) != "#32770")
                return false;

            bool onlyButtonsAndStatics = true;
            EnumChildWindows(window, (child, param) =>
            {
                string className = GetClassName(child);
                if (className != "Button" && className != "Static")
                {
                    onlyButtonsAndStatics = false;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return onlyButtonsAndStatics && GetDlgItem(window, MESSAGE_TEXT_ID) != IntPtr.Zero;
        }

        private static void TranslateMessageBox(IntPtr dialog)
        {
            string caption = GetText(dialog);
            string translatedCaption = Translator.T(caption);
            if (translatedCaption != caption)
                SetWindowText(dialog, translatedCaption);

            var buttons = new List<IntPtr>();
            EnumChildWindows(dialog, (child, param) =>
            {
                if (GetClassName(child) == "Button")
                {
                    buttons.Add(child);
                    string label;
                    if (messageBoxButtons.TryGetValue(GetDlgCtrlID(child), out label))
                        SetWindowText(child, label);
                }

                return true;
            }, IntPtr.Zero);

            IntPtr textControl = GetDlgItem(dialog, MESSAGE_TEXT_ID);
            string message = GetText(textControl);
            string translatedMessage = Translator.T(message);
            if (translatedMessage == message)
                return;

            SetWindowText(textControl, translatedMessage);
            FitMessageText(dialog, textControl, translatedMessage, buttons);
        }

        /// <summary>
        /// The message box is sized for the original text, grow it, if the translation needs more lines.
        /// </summary>
        private static void FitMessageText(IntPtr dialog, IntPtr textControl, string text, List<IntPtr> buttons)
        {
            RECT textBounds = GetClientBounds(dialog, textControl);
            int width = textBounds.Right - textBounds.Left;
            int height = textBounds.Bottom - textBounds.Top;

            var required = new RECT { Left = 0, Top = 0, Right = width, Bottom = 0 };
            IntPtr dc = GetDC(textControl);
            try
            {
                IntPtr font = SendMessage(textControl, WM_GETFONT, IntPtr.Zero, IntPtr.Zero);
                IntPtr oldFont = SelectObject(dc, font);
                DrawText(dc, text, text.Length, ref required, DT_CALCRECT | DT_WORDBREAK | DT_EXPANDTABS | DT_NOPREFIX);
                SelectObject(dc, oldFont);
            }
            finally
            {
                ReleaseDC(textControl, dc);
            }

            int delta = required.Bottom - height;
            if (delta <= 0)
                return;

            SetWindowPos(textControl, IntPtr.Zero, 0, 0, width, height + delta, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
            foreach (IntPtr button in buttons)
            {
                RECT buttonBounds = GetClientBounds(dialog, button);
                SetWindowPos(button, IntPtr.Zero, buttonBounds.Left, buttonBounds.Top + delta, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }

            RECT dialogBounds;
            GetWindowRect(dialog, out dialogBounds);
            SetWindowPos(dialog, IntPtr.Zero, dialogBounds.Left, dialogBounds.Top - delta / 2,
                dialogBounds.Right - dialogBounds.Left, dialogBounds.Bottom - dialogBounds.Top + delta, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private static RECT GetClientBounds(IntPtr parent, IntPtr child)
        {
            RECT bounds;
            GetWindowRect(child, out bounds);
            var topLeft = new POINT { X = bounds.Left, Y = bounds.Top };
            var bottomRight = new POINT { X = bounds.Right, Y = bounds.Bottom };
            ScreenToClient(parent, ref topLeft);
            ScreenToClient(parent, ref bottomRight);
            return new RECT { Left = topLeft.X, Top = topLeft.Y, Right = bottomRight.X, Bottom = bottomRight.Y };
        }

        private static string GetClassName(IntPtr window)
        {
            var name = new StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            return name.ToString();
        }

        private static string GetText(IntPtr window)
        {
            int length = GetWindowTextLength(window);
            var text = new StringBuilder(length + 1);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        /// <summary>
        /// Replaces the resource managers of generated resource classes in Terminals assemblies,
        /// so all string resources are translated. Covers also plugins loaded later.
        /// </summary>
        private static void PatchResourceManagers()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => PatchAssembly(args.LoadedAssembly);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                PatchAssembly(assembly);
        }

        private static void PatchAssembly(Assembly assembly)
        {
            try
            {
                if (assembly.IsDynamic || !assembly.GetName().Name.StartsWith("Terminals", StringComparison.OrdinalIgnoreCase))
                    return;

                foreach (Type type in GetLoadableTypes(assembly))
                    PatchResourceClass(type);
            }
            catch (Exception exception)
            {
                Logging.Info("Unable to translate resources of " + assembly.FullName, exception);
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                // plugin dependencies (e.g. ActiveX interop) may be missing, the resource classes are still usable
                return exception.Types.Where(type => type != null);
            }
        }

        private static void PatchResourceClass(Type type)
        {
            const BindingFlags STATIC = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo field = type.GetField("resourceMan", STATIC);
            PropertyInfo property = type.GetProperty("ResourceManager", STATIC);
            if (field == null || property == null || field.FieldType != typeof(ResourceManager))
                return;

            var current = property.GetValue(null, null) as ResourceManager;
            if (current == null || current is TranslatingResourceManager)
                return;

            field.SetValue(null, new TranslatingResourceManager(current.BaseName, type.Assembly));
        }

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);

        [DllImport("user32.dll")]
        private static extern int GetDlgCtrlID(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int DrawText(IntPtr hdc, string lpchText, int cchText, ref RECT lprc, uint dwDTFormat);
    }
}
