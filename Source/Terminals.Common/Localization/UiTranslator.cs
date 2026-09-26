using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Terminals.Localization
{
    /// <summary>
    /// Translates texts of windows forms controls created by designer.
    /// Only the members declared as fields of forms and user controls are translated,
    /// because controls created at runtime usually present user data (favorite names, groups),
    /// which must stay untouched. Input controls (text boxes, combo boxes) are never translated.
    /// </summary>
    public static class UiTranslator
    {
        private const BindingFlags FIELDS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly object marker = new object();

        private static readonly ConditionalWeakTable<Control, object> translatedForms = new ConditionalWeakTable<Control, object>();

        private static readonly ConditionalWeakTable<Control, object> translatedContainers = new ConditionalWeakTable<Control, object>();

        private static readonly ConditionalWeakTable<Control, object> watchedContainers = new ConditionalWeakTable<Control, object>();

        public static void TranslateForm(Form form)
        {
            if (!Translator.IsActive || form == null)
                return;

            try
            {
                if (Mark(translatedForms, form))
                    form.Text = Translator.T(form.Text);

                TranslateContainerTree(form);
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to translate form " + form.GetType().FullName, exception);
            }
        }

        /// <summary>
        /// Translates the nodes created by designer. Call it only for tree views,
        /// which don't show user data and don't search the nodes by text.
        /// </summary>
        public static void TranslateNodes(TreeNodeCollection nodes, bool recursive)
        {
            if (!Translator.IsActive)
                return;

            foreach (TreeNode node in nodes)
            {
                node.Text = Translator.T(node.Text);
                node.ToolTipText = Translator.T(node.ToolTipText);
                if (recursive)
                    TranslateNodes(node.Nodes, true);
            }
        }

        private static bool Mark(ConditionalWeakTable<Control, object> table, Control control)
        {
            object existing;
            if (table.TryGetValue(control, out existing))
                return false;

            table.Add(control, marker);
            return true;
        }

        /// <summary>
        /// Translates fields of all containers in the control tree and watches for containers added later.
        /// </summary>
        private static void TranslateContainerTree(Control control)
        {
            if (IsContainerWithFields(control) && Mark(translatedContainers, control))
                TranslateFields(control);

            if (Mark(watchedContainers, control))
                control.ControlAdded += Control_ControlAdded;

            foreach (Control child in control.Controls)
                TranslateContainerTree(child);
        }

        private static bool IsContainerWithFields(Control control)
        {
            // forms and user controls created by designer declare their children as fields
            return control is ContainerControl || control.GetType().Assembly != typeof(Control).Assembly;
        }

        private static void Control_ControlAdded(object sender, ControlEventArgs e)
        {
            try
            {
                if (Translator.IsActive)
                    TranslateContainerTree(e.Control);
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to translate added control", exception);
            }
        }

        private static void TranslateFields(Control container)
        {
            var toolTips = new List<ToolTip>();
            var controls = new List<Control>();
            Type type = container.GetType();
            while (type != null && type.Assembly != typeof(Control).Assembly)
            {
                foreach (FieldInfo field in type.GetFields(FIELDS))
                {
                    if (field.IsLiteral || !IsTranslatable(field.FieldType))
                        continue;

                    object value = field.GetValue(container);
                    if (value != null)
                        TranslateMember(value, toolTips, controls);
                }

                type = type.BaseType;
            }

            foreach (ToolTip toolTip in toolTips)
                TranslateToolTips(toolTip, controls);
        }

        private static bool IsTranslatable(Type type)
        {
            return typeof(Control).IsAssignableFrom(type) || typeof(ToolStripItem).IsAssignableFrom(type) ||
                   typeof(ToolTip).IsAssignableFrom(type) || typeof(ColumnHeader).IsAssignableFrom(type) ||
                   typeof(DataGridViewColumn).IsAssignableFrom(type);
        }

        private static void TranslateMember(object value, List<ToolTip> toolTips, List<Control> controls)
        {
            var control = value as Control;
            if (control != null)
            {
                controls.Add(control);
                TranslateControl(control);
                return;
            }

            var item = value as ToolStripItem;
            if (item != null)
            {
                item.Text = Translator.T(item.Text);
                item.ToolTipText = Translator.T(item.ToolTipText);
                return;
            }

            var toolTip = value as ToolTip;
            if (toolTip != null)
            {
                toolTips.Add(toolTip);
                return;
            }

            var column = value as ColumnHeader;
            if (column != null)
            {
                column.Text = Translator.T(column.Text);
                return;
            }

            var gridColumn = value as DataGridViewColumn;
            if (gridColumn != null)
            {
                gridColumn.HeaderText = Translator.T(gridColumn.HeaderText);
                gridColumn.ToolTipText = Translator.T(gridColumn.ToolTipText);
            }
        }

        private static void TranslateControl(Control control)
        {
            if (!HasTranslatableText(control))
                return;

            var linkLabel = control as LinkLabel;
            bool wholeLink = linkLabel != null && linkLabel.LinkArea.Start == 0 && linkLabel.LinkArea.Length >= linkLabel.Text.Length;

            string text = Translator.T(control.Text);
            if (text == control.Text)
                return;

            control.Text = text;
            if (wholeLink)
                linkLabel.LinkArea = new LinkArea(0, text.Length);
        }

        private static bool HasTranslatableText(Control control)
        {
            return !(control is TextBoxBase || control is ComboBox || control is ListControl || control is UpDownBase ||
                     control is DateTimePicker || control is WebBrowserBase || control is Form || control is UserControl ||
                     control is TreeView || control is ListView || control is DataGridView || control is PictureBox ||
                     control is ProgressBar || control is TrackBar || control is ScrollBar || control is SplitContainer);
        }

        private static void TranslateToolTips(ToolTip toolTip, List<Control> controls)
        {
            foreach (Control control in controls)
            {
                string tip = toolTip.GetToolTip(control);
                if (!string.IsNullOrEmpty(tip))
                    toolTip.SetToolTip(control, Translator.T(tip));
            }
        }
    }
}
