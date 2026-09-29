using System;
using System.Drawing;

namespace Terminals.Forms.Controls
{
    /// <summary>
    /// Small note paper icon painted after the favorite name in favorites tree and search results.
    /// </summary>
    internal static class NotesIcon
    {
        /// <summary>
        /// Maximum length of the notes shown in the tool tip, the tool tip isn't scrollable.
        /// </summary>
        private const int MAX_NOTES_TOOLTIP_LENGTH = 1000;

        /// <summary>
        /// Small icon placed right after the text.
        /// </summary>
        internal static Rectangle GetBounds(Rectangle textBounds)
        {
            int size = Math.Max(Math.Min(textBounds.Height - 4, 12), 8);
            int top = textBounds.Top + (textBounds.Height - size) / 2;
            return new Rectangle(textBounds.Right + 3, top, size - 2, size);
        }

        /// <summary>
        /// Paints yellow note paper with folded corner and text lines.
        /// </summary>
        internal static void Draw(Graphics graphics, Rectangle bounds)
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

        internal static string FormatToolTip(string notes)
        {
            string text = notes.Trim();
            if (text.Length > MAX_NOTES_TOOLTIP_LENGTH)
                text = text.Substring(0, MAX_NOTES_TOOLTIP_LENGTH) + "...";

            return text;
        }
    }
}
