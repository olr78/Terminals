using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Terminals.Connections;
using Terminals.Data;

namespace Terminals.Forms.Controls
{
    /// <summary>
    /// Treeview in main window to present favorites organized by Tags
    /// </summary>
    internal partial class FavoritesTreeView : TreeView
    {
        private IPersistence persistence;

        private ConnectionManager connectionManager;

        internal IFavorite SelectedFavorite
        {
            get
            {
                var selectedFavoriteNode = this.SelectedNode as FavoriteTreeNode;
                if (selectedFavoriteNode != null)
                    return selectedFavoriteNode.Favorite;

                return null;
            }
        }

        /// <summary>
        /// Gets currently selected tree node in case it is a group node, otherwise null.
        /// </summary>
        internal GroupTreeNode SelectedGroupNode
        {
            get
            {
                return this.SelectedNode as GroupTreeNode;
            }
        }

        private IGroup SelectedGroup
        {
            get
            {
                if (this.SelectedGroupNode != null)
                    return this.SelectedGroupNode.Group;

                return null;
            }
        }

        /// <summary>
        /// Gets never null collection of favorites in selected group.
        /// If no group node is selected, returns empty collection.
        /// </summary>
        internal List<IFavorite> SelectedGroupFavorites
        {
            get
            {
                var groupNode = this.SelectedGroupNode;
                if (groupNode == null)
                    return new List<IFavorite>();

                return groupNode.Favorites;
            }
        }

        /// <summary>
        /// Maximum length of the notes shown in the tool tip, the tool tip isn't scrollable.
        /// </summary>
        private const int MAX_NOTES_TOOLTIP_LENGTH = 1000;

        private readonly ToolTip notesToolTip = new ToolTip();

        /// <summary>
        /// Node, for which the notes tool tip is currently shown. Null, if the tool tip isn't shown.
        /// </summary>
        private FavoriteTreeNode notesToolTipNode;

        public FavoritesTreeView()
        {
            InitializeComponent();

            this.components.Add(this.notesToolTip);
            this.DrawMode = TreeViewDrawMode.OwnerDrawText;
            this.DrawNode += this.FavoritesTreeView_DrawNode;
            this.MouseMove += this.FavoritesTreeView_MouseMove;
            this.MouseLeave += this.FavoritesTreeView_MouseLeave;
        }

        private void FavoritesTreeView_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            e.DrawDefault = true;
            var favoriteNode = e.Node as FavoriteTreeNode;
            if (favoriteNode == null || !favoriteNode.HasNotes || e.Bounds.IsEmpty)
                return;

            DrawNotesIcon(e.Graphics, GetNotesIconBounds(e.Bounds));
        }

        /// <summary>
        /// Small icon placed right after the node text.
        /// </summary>
        private static Rectangle GetNotesIconBounds(Rectangle textBounds)
        {
            int size = Math.Max(Math.Min(textBounds.Height - 4, 12), 8);
            int top = textBounds.Top + (textBounds.Height - size) / 2;
            return new Rectangle(textBounds.Right + 3, top, size - 2, size);
        }

        /// <summary>
        /// Paints yellow note paper with folded corner and text lines.
        /// </summary>
        private static void DrawNotesIcon(Graphics graphics, Rectangle bounds)
        {
            int fold = bounds.Width / 3;
            var paper = new[]
            {
                new Point(bounds.Left, bounds.Top),
                new Point(bounds.Right - fold, bounds.Top),
                new Point(bounds.Right, bounds.Top + fold),
                new Point(bounds.Right, bounds.Bottom),
                new Point(bounds.Left, bounds.Bottom)
            };

            using (var fill = new SolidBrush(Color.FromArgb(255, 232, 120)))
            using (var border = new Pen(Color.FromArgb(176, 132, 0)))
            using (var lines = new Pen(Color.FromArgb(150, 120, 60)))
            {
                graphics.FillPolygon(fill, paper);
                graphics.DrawPolygon(border, paper);
                graphics.DrawLine(border, bounds.Right - fold, bounds.Top, bounds.Right - fold, bounds.Top + fold);
                graphics.DrawLine(border, bounds.Right - fold, bounds.Top + fold, bounds.Right, bounds.Top + fold);

                for (int y = bounds.Top + fold + 2; y < bounds.Bottom - 1; y += 2)
                    graphics.DrawLine(lines, bounds.Left + 2, y, bounds.Right - 2, y);
            }
        }

        private void FavoritesTreeView_MouseMove(object sender, MouseEventArgs e)
        {
            FavoriteTreeNode node = this.FindNotesIconNodeAt(e.Location);
            if (node == this.notesToolTipNode)
                return;

            this.HideNotesToolTip();
            if (node == null)
                return;

            this.notesToolTipNode = node;
            Rectangle iconBounds = GetNotesIconBounds(node.Bounds);
            this.notesToolTip.Show(FormatNotesToolTip(node.Notes), this, iconBounds.Left, iconBounds.Bottom + 4);
        }

        private FavoriteTreeNode FindNotesIconNodeAt(Point location)
        {
            TreeViewHitTestInfo hit = this.HitTest(location);
            var node = hit.Node as FavoriteTreeNode;
            if (node == null || !node.HasNotes)
                return null;

            Rectangle iconBounds = GetNotesIconBounds(node.Bounds);
            iconBounds.Inflate(2, 2);
            return iconBounds.Contains(location) ? node : null;
        }

        private static string FormatNotesToolTip(string notes)
        {
            string text = notes.Trim();
            if (text.Length > MAX_NOTES_TOOLTIP_LENGTH)
                text = text.Substring(0, MAX_NOTES_TOOLTIP_LENGTH) + "...";

            return text;
        }

        private void FavoritesTreeView_MouseLeave(object sender, EventArgs e)
        {
            this.HideNotesToolTip();
        }

        private void HideNotesToolTip()
        {
            if (this.notesToolTipNode == null)
                return;

            this.notesToolTip.Hide(this);
            this.notesToolTipNode = null;
        }

        internal void AssignServices(IPersistence persistence, FavoriteIcons favoriteIcons, ConnectionManager connectionManager)
        {
            this.persistence = persistence;
            this.connectionManager = connectionManager;
            var iconsBuilder = new ProtocolImageListBuilder(favoriteIcons.GetProtocolIcons);
            iconsBuilder.Build(this.imageListIcons);
        }

        internal GroupTreeNode FindSelectedGroupNode()
        {
            if (this.SelectedNode == null)
                return null;

            // only leaf nodes arent group nodes
            var groupNode = this.SelectedNode as GroupTreeNode;
            if (groupNode != null)
                return groupNode;

            return this.SelectedNode.Parent as GroupTreeNode;
        }

        internal void RestoreSelectedFavorite(TreeNode groupNode, IFavorite favorite)
        {
            if (favorite == null)
                return;

            TreeNode nodeToRestore = this.FindNodeToRestore(groupNode, favorite);
            if (nodeToRestore != null)
                this.SelectedNode = nodeToRestore;
        }

        private TreeNode FindNodeToRestore(TreeNode groupNode, IFavorite favorite)
        {
            TreeNode favoriteNode = FindFavoriteNodeByName(groupNode, favorite);
            if (favoriteNode == null) // group node was removed, try find another one
                groupNode = this.FindFirstGroupNodeContainingFavorite(favorite);

            return FindFavoriteNodeByName(groupNode, favorite);
        }

        private TreeNode FindFirstGroupNodeContainingFavorite(IFavorite favorite)
        {
            foreach (TreeNode groupNode in this.Nodes)
            {
                var favoriteNode = FindFavoriteNodeByName(groupNode, favorite);
                if (favoriteNode != null)
                    return groupNode;
            }

            return null;
        }

        private static FavoriteTreeNode FindFavoriteNodeByName(TreeNode groupNode, IFavorite favorite)
        {
            if (groupNode == null)
                return null;

            List<FavoriteTreeNode> favoriteNodes = TreeListNodes.FilterFavoriteNodes(groupNode.Nodes);
            return favoriteNodes.FirstOrDefault(favoriteNode => favoriteNode.Favorite.StoreIdEquals(favorite));
        }

        private void FavsTree_DragEnter(object sender, DragEventArgs e)
        {
            TreeViewDragDrop dragDrop = this.CreateTreeViewDragDrop(e);
            e.Effect = dragDrop.Effect;
        }

        private void FavoritesTreeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            this.DoDragDrop(e.Item, TreeViewDragDrop.SUPPORTED_DROPS);
        }

        private void FavoritesTreeView_DragOver(object sender, DragEventArgs e)
        {
            // focus candidate of target node under cursor
            var targetPoint = this.PointToClient(new Point(e.X, e.Y));
            this.SelectedNode = this.GetNodeAt(targetPoint);
            // the selected node will now play the role of drop target 
            TreeViewDragDrop dragDrop = this.CreateTreeViewDragDrop(e);
            e.Effect = dragDrop.Effect;
        }

        private void FavsTree_DragDrop(object sender, DragEventArgs e)
        {
            TreeViewDragDrop dragDrop = this.CreateTreeViewDragDrop(e);
            dragDrop.Drop(this.FindForm());
        }

        private TreeViewDragDrop CreateTreeViewDragDrop(DragEventArgs e)
        {
            var keyModifiers = new KeyModifiers();
            return new TreeViewDragDrop(this.persistence, this.connectionManager, e, keyModifiers,
                this.SelectedGroup, this.SelectedFavorite);
        }
    }
}
