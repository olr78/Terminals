using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using MSTSCLib;

namespace Terminals.Connections
{
    /// <summary>
    /// Types the password into the remote session as keyboard scan codes.
    /// Used for servers, which always ask for the password on their logon screen
    /// and ignore the credentials sent by the client.
    /// The scan codes depend on the keyboard layout used by the remote session,
    /// which is the layout announced by the client when connecting.
    /// </summary>
    internal static class PasswordTyper
    {
        internal const string US_LAYOUT = "00000409";

        private const int SCAN_LEFT_SHIFT = 0x2A;
        private const int SCAN_ENTER = 0x1C;
        private const uint MAPVK_VK_TO_VSC = 0;
        private const uint KLF_NOTELLSHELL = 0x80;

        /// <summary>
        /// One key press or release.
        /// </summary>
        internal struct KeyEvent
        {
            internal int ScanCode;
            internal bool KeyUp;

            internal KeyEvent(int scanCode, bool keyUp)
            {
                this.ScanCode = scanCode;
                this.KeyUp = keyUp;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:X2}{1}", this.ScanCode, this.KeyUp ? "u" : "d");
            }
        }

        /// <summary>
        /// Resolves keyboard layout, which is able to type all characters of the password.
        /// Prefers the current input layout, which the client announces to the server by default.
        /// Returns US layout name in layoutToAnnounce, if the client has to announce it explicitly,
        /// or null, if the current layout is used. Returns false, if no supported layout can type the password.
        /// </summary>
        internal static bool TryResolveLayout(string password, out IntPtr layout, out string layoutToAnnounce)
        {
            layoutToAnnounce = null;
            layout = GetKeyboardLayout(0);
            List<KeyEvent> events;
            if (TryCreateKeys(password, layout, false, out events))
                return true;

            layout = LoadUsLayout();
            layoutToAnnounce = US_LAYOUT;
            return layout != IntPtr.Zero && TryCreateKeys(password, layout, false, out events);
        }

        internal static IntPtr LoadUsLayout()
        {
            return LoadKeyboardLayout(US_LAYOUT, KLF_NOTELLSHELL);
        }

        /// <summary>
        /// Creates key presses to type the text followed by Enter.
        /// Returns false, if some character can't be typed using the layout by Shift only.
        /// </summary>
        internal static bool TryCreateKeys(string text, IntPtr layout, bool capsLock, out List<KeyEvent> events)
        {
            events = new List<KeyEvent>();
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char character in text)
            {
                short keyScan = VkKeyScanEx(character, layout);
                if (keyScan == -1)
                    return false;

                int virtualKey = keyScan & 0xFF;
                int modifiers = (keyScan >> 8) & 0xFF;
                // Ctrl or Alt (AltGr) combinations aren't supported, the logon screen may interpret them
                if ((modifiers & ~1) != 0)
                    return false;

                int scanCode = (int)MapVirtualKeyEx((uint)virtualKey, MAPVK_VK_TO_VSC, layout);
                if (scanCode == 0)
                    return false;

                bool shift = (modifiers & 1) != 0;
                // Caps Lock state is synchronized to the remote session and inverts the letters
                if (capsLock && char.IsLetter(character))
                    shift = !shift;

                if (shift)
                    events.Add(new KeyEvent(SCAN_LEFT_SHIFT, false));

                events.Add(new KeyEvent(scanCode, false));
                events.Add(new KeyEvent(scanCode, true));

                if (shift)
                    events.Add(new KeyEvent(SCAN_LEFT_SHIFT, true));
            }

            events.Add(new KeyEvent(SCAN_ENTER, false));
            events.Add(new KeyEvent(SCAN_ENTER, true));
            return true;
        }

        /// <summary>
        /// Sends the keys one by one, the interop marshals only the first item of the arrays.
        /// </summary>
        internal static void Send(IMsRdpClientNonScriptable4 client, IEnumerable<KeyEvent> events)
        {
            foreach (KeyEvent keyEvent in events)
            {
                bool keyUp = keyEvent.KeyUp;
                int scanCode = keyEvent.ScanCode;
                client.SendKeys(1, ref keyUp, ref scanCode);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern short VkKeyScanEx(char ch, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);
    }
}
