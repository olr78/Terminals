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

            NotesIcon.Draw(e.Graphics, NotesIcon.GetBounds(e.Bounds));
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
            Rectangle iconBounds = NotesIcon.GetBounds(node.Bounds);
            this.notesToolTip.Show(NotesIcon.FormatToolTip(node.Notes), this, iconBounds.Left, iconBounds.Bottom + 4);
        }

        private FavoriteTreeNode FindNotesIconNodeAt(Point location)
        {
            TreeViewHitTestInfo hit = this.HitTest(location);
            var node = hit.Node as FavoriteTreeNode;
            if (node == null || !node.HasNotes)
                return null;

            Rectangle iconBounds = NotesIcon.GetBounds(node.Bounds);
            iconBounds.Inflate(2, 2);
            return iconBounds.Contains(location) ? node : null;
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
