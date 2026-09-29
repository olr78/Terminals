using System;
using System.Runtime.InteropServices;

namespace Terminals.Forms
{
    /// <summary>
    /// Hides the window using the desktop window manager (Windows 8 and newer), while the window is still
    /// shown and painted. Used to show the main window only after it is completely built and painted.
    /// </summary>
    internal static class WindowCloak
    {
        private const int DWMWA_CLOAK = 13;

        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_FRAME = 0x0400;

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hwnd, IntPtr updateRect, IntPtr updateRegion, uint flags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        /// <summary>
        /// Paints the window including all its child windows immediately.
        /// </summary>
        internal static void PaintNow(IntPtr handle)
        {
            RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW | RDW_FRAME);
        }

        /// <summary>
        /// Returns true, if the window cloaking was changed; false, if not supported by the system.
        /// </summary>
        internal static bool SetCloaked(IntPtr handle, bool cloaked)
        {
            try
            {
                int value = cloaked ? 1 : 0;
                return DwmSetWindowAttribute(handle, DWMWA_CLOAK, ref value, sizeof(int)) == 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}
