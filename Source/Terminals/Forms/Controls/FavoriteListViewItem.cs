using System.Windows.Forms;
using Terminals.Data;

namespace Terminals.Forms.Controls
{
    internal class FavoriteListViewItem : ListViewItem
    {
        internal FavoriteListViewItem(IFavorite favorite)
        {
            this.Tag = favorite;
            this.Text = favorite.Name;
            this.Notes = favorite.Notes;
        }

        /// <summary>
        /// Gets decrypted favorite notes cached for painting, to prevent decryption on each paint.
        /// </summary>
        internal string Notes { get; private set; }

        internal bool HasNotes
        {
            get { return !string.IsNullOrWhiteSpace(this.Notes); }
        }
    }
}
