using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Terminals.Forms.Controls
{
    /// <summary>
    /// List view of favorites, which paints the notes icon after the name of favorites with notes,
    /// the same way as the favorites tree does.
    /// </summary>
    internal class FavoritesListView : ListView
    {
        private const int WM_PAINT = 0x000F;

        private const int WM_REFLECT_NOTIFY = 0x2000 + 0x004E;

        private const int LVN_GETINFOTIPA = -157;

        private const int LVN_GETINFOTIPW = -158;

        [StructLayout(LayoutKind.Sequential)]
        private struct NMHDR
        {
            public IntPtr hwndFrom;
            public IntPtr idFrom;
            public int code;
        }

        /// <summary>
        /// The header has to be nested structure, because on 64 bit it is padded to the pointer size.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NMLVGETINFOTIP
        {
            public NMHDR hdr;
            public int dwFlags;
            public IntPtr pszText;
            public int cchTextMax;
            public int iItem;
            public int iSubItem;
            public IntPtr lParam;
        }

        private readonly ToolTip notesToolTip = new ToolTip();

        /// <summary>
        /// Item, for which the notes tool tip is currently shown. Null, if the tool tip isn't shown.
        /// </summary>
        private FavoriteListViewItem notesToolTipItem;

        /// <summary>
        /// Gets the width needed to fit the notes icon after the item text.
        /// </summary>
        internal static int NotesIconWidth
        {
            get { return 16; }
        }

        internal bool HasItemsWithNotes
        {
            get { return this.Items.OfType<FavoriteListViewItem>().Any(item => item.HasNotes); }
        }

        /// <summary>
        /// Gets or sets the builder of the item tool tip. The tool tip is built only when the list asks for it,
        /// because building it for SQL persistence loads the favorite details from database for each item.
        /// </summary>
        internal Func<FavoriteListViewItem, string> ItemToolTipBuilder { get; set; }

        /// <summary>
        /// The icon is painted over the natively painted items, to keep the system look of the items.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_REFLECT_NOTIFY)
                this.EnsureItemToolTip(m.LParam);

            base.WndProc(ref m);

            if (m.Msg == WM_PAINT)
                this.PaintNotesIcons();
        }

        /// <summary>
        /// The base list view copies the item ToolTipText, when the native control asks for the item info tip.
        /// </summary>
        private void EnsureItemToolTip(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero || this.ItemToolTipBuilder == null)
                return;

            var info = (NMLVGETINFOTIP)Marshal.PtrToStructure(lParam, typeof(NMLVGETINFOTIP));
            if ((info.hdr.code != LVN_GETINFOTIPW && info.hdr.code != LVN_GETINFOTIPA) || info.iItem < 0 || info.iItem >= this.Items.Count)
                return;

            var item = this.Items[info.iItem] as FavoriteListViewItem;
            if (item != null && string.IsNullOrEmpty(item.ToolTipText))
                item.ToolTipText = this.ItemToolTipBuilder(item);
        }

        private void PaintNotesIcons()
        {
            if (!this.IsHandleCreated || this.View != View.Details)
                return;

            using (Graphics graphics = this.CreateGraphics())
            {
                foreach (FavoriteListViewItem item in this.Items.OfType<FavoriteListViewItem>())
                {
                    if (!item.HasNotes)
                        continue;

                    Rectangle iconBounds = this.GetNotesIconBounds(item);
                    if (this.ClientRectangle.IntersectsWith(iconBounds))
                        NotesIcon.Draw(graphics, iconBounds);
                }
            }
        }

        private Rectangle GetNotesIconBounds(ListViewItem item)
        {
            Rectangle label = item.GetBounds(ItemBoundsPortion.Label);
            int textWidth = TextRenderer.MeasureText(item.Text, item.Font).Width;
            var textBounds = new Rectangle(label.Left, label.Top, Math.Min(textWidth, label.Width), label.Height);
            return NotesIcon.GetBounds(textBounds);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            FavoriteListViewItem item = this.FindNotesIconItemAt(e.Location);
            if (item == this.notesToolTipItem)
                return;

            this.HideNotesToolTip();
            if (item == null)
                return;

            this.notesToolTipItem = item;
            Rectangle iconBounds = this.GetNotesIconBounds(item);
            this.notesToolTip.Show(NotesIcon.FormatToolTip(item.Notes), this, iconBounds.Left, iconBounds.Bottom + 4);
        }

        private FavoriteListViewItem FindNotesIconItemAt(Point location)
        {
            if (this.View != View.Details)
                return null;

            foreach (FavoriteListViewItem item in this.Items.OfType<FavoriteListViewItem>())
            {
                if (!item.HasNotes)
                    continue;

                Rectangle iconBounds = this.GetNotesIconBounds(item);
                iconBounds.Inflate(2, 2);
                if (iconBounds.Contains(location))
                    return item;
            }

            return null;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            this.HideNotesToolTip();
        }

        private void HideNotesToolTip()
        {
            if (this.notesToolTipItem == null)
                return;

            this.notesToolTip.Hide(this);
            this.notesToolTipItem = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this.notesToolTip.Dispose();

            base.Dispose(disposing);
        }
    }
}
