using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Linq;

namespace AtlasToolEditor
{
    // Represents a single texture (region) for arrangement.
    public class TextureItem
    {
        public string Name { get; set; }
        public Image Image { get; set; }
        // Position and size in "base" (world) coordinates
        public RectangleF Bounds { get; set; }
        public int Z { get; set; } = 0;
    }

    // Custom canvas for drawing and interacting with textures.
    public class TextureCanvas : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<TextureItem> Items { get; private set; } = new List<TextureItem>();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float ZoomFactor { get; set; } = 1.0f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PointF PanOffset { get; set; } = new PointF(0, 0);

        // Definition of the arrangement area in world coordinates – fixed 1280x720.
        private readonly RectangleF arrangementArea = new RectangleF(0, 0, 1280, 720);

        // Single selected item (for compatibility)
        private TextureItem selectedItem = null;

        // Multi-selection fields
        private List<TextureItem> selectedItems = new List<TextureItem>();
        private bool isSelecting = false;
        private Point selectionStartPoint;
        private Rectangle selectionRect;

        private Point lastMousePos;
        private bool isDraggingItem = false;
        private bool isPanning = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowGrid { get; set; } = false;

        public TextureCanvas()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            this.MouseDown += TextureCanvas_MouseDown;
            this.MouseMove += TextureCanvas_MouseMove;
            this.MouseUp += TextureCanvas_MouseUp;
            this.MouseWheel += TextureCanvas_MouseWheel;
            this.MouseDoubleClick += TextureCanvas_MouseDoubleClick;
        }

        private void TextureCanvas_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            PointF basePt = new PointF((e.X - PanOffset.X) / ZoomFactor, (e.Y - PanOffset.Y) / ZoomFactor);
            foreach (var item in Items)
            {
                if (item.Bounds.Contains(basePt))
                {
                    string input = PromptForZValue(item.Z);
                    if (int.TryParse(input, out int newZ))
                    {
                        item.Z = newZ;
                        Invalidate();
                    }
                    break;
                }
            }
        }

        private string PromptForZValue(int currentZ)
        {
            using (var form = new Form())
            {
                form.Text = "Set Z Height";
                var label = new Label() { Text = "Enter Z height:", Left = 10, Top = 10, AutoSize = true };
                var textBox = new TextBox() { Left = 10, Top = 30, Width = 200, Text = currentZ.ToString() };
                var buttonOk = new Button() { Text = "OK", Left = 10, Width = 60, Top = 60, DialogResult = DialogResult.OK };
                form.AcceptButton = buttonOk;

                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(buttonOk);

                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(220, 100);
                return form.ShowDialog() == DialogResult.OK ? textBox.Text : currentZ.ToString();
            }
        }

        private void TextureCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            lastMousePos = e.Location;
            if (e.Button == MouseButtons.Left)
            {
                // Convert screen coordinates to world coordinates
                PointF basePt = new PointF((e.X - PanOffset.X) / ZoomFactor, (e.Y - PanOffset.Y) / ZoomFactor);
                // Check if an item was hit
                var hitItem = Items.OrderByDescending(item => item.Z)
                                   .FirstOrDefault(item => item.Bounds.Contains(basePt));
                if (hitItem != null)
                {
                    // If the hit item is already selected, keep the current selection.
                    // Otherwise, add it (with Shift) or clear and select only this item.
                    if (!selectedItems.Contains(hitItem))
                    {
                        if ((ModifierKeys & Keys.Shift) == Keys.Shift)
                        {
                            selectedItems.Add(hitItem);
                        }
                        else
                        {
                            selectedItems.Clear();
                            selectedItems.Add(hitItem);
                        }
                    }
                    // Set dragging flag for moving selected items
                    isDraggingItem = true;
                    selectedItem = hitItem; // for compatibility

                    // Force redraw to show selection immediately
                    Invalidate();
                }
                else
                {
                    // Clicked on empty space – start selection rectangle
                    isSelecting = true;
                    selectionStartPoint = e.Location;
                    selectionRect = new Rectangle(e.Location, new Size(0, 0));
                    selectedItems.Clear(); // Optionally clear previous selection
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                isPanning = true;
            }
        }

        private void TextureCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                // Update the selection rectangle in screen coordinates
                int x = Math.Min(e.X, selectionStartPoint.X);
                int y = Math.Min(e.Y, selectionStartPoint.Y);
                int width = Math.Abs(e.X - selectionStartPoint.X);
                int height = Math.Abs(e.Y - selectionStartPoint.Y);
                selectionRect = new Rectangle(x, y, width, height);
                Invalidate();
                return;
            }
            else if (isDraggingItem && selectedItems.Count > 0 && e.Button == MouseButtons.Left)
            {
                // Move all selected items
                float dx = (e.X - lastMousePos.X) / ZoomFactor;
                float dy = (e.Y - lastMousePos.Y) / ZoomFactor;
                foreach (var item in selectedItems)
                {
                    RectangleF r = item.Bounds;
                    r.X += dx;
                    r.Y += dy;
                    // Restrict movement to the arrangement area (0,0 - 1280x720)
                    if (r.X < arrangementArea.X) r.X = arrangementArea.X;
                    if (r.Y < arrangementArea.Y) r.Y = arrangementArea.Y;
                    if (r.Right > arrangementArea.Right) r.X = arrangementArea.Right - r.Width;
                    if (r.Bottom > arrangementArea.Bottom) r.Y = arrangementArea.Bottom - r.Height;
                    item.Bounds = r;
                }
                lastMousePos = e.Location;
                Invalidate();
            }
            else if (isPanning && e.Button == MouseButtons.Right)
            {
                float dx = e.X - lastMousePos.X;
                float dy = e.Y - lastMousePos.Y;
                PanOffset = new PointF(PanOffset.X + dx, PanOffset.Y + dy);
                lastMousePos = e.Location;
                Invalidate();
            }
        }

        private void TextureCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                // Convert the selection rectangle from screen to world coordinates
                RectangleF worldSelection = new RectangleF(
                    (selectionRect.X - PanOffset.X) / ZoomFactor,
                    (selectionRect.Y - PanOffset.Y) / ZoomFactor,
                    selectionRect.Width / ZoomFactor,
                    selectionRect.Height / ZoomFactor
                );
                // Add all items whose bounds intersect the selection rectangle
                foreach (var item in Items)
                {
                    if (item.Bounds.IntersectsWith(worldSelection))
                    {
                        if (!selectedItems.Contains(item))
                            selectedItems.Add(item);
                    }
                }
                isSelecting = false;
                selectionRect = Rectangle.Empty;
                Invalidate();
            }
            isDraggingItem = false;
            isPanning = false;
            selectedItem = null;
        }

        private void TextureCanvas_MouseWheel(object sender, MouseEventArgs e)
        {
            float oldZoom = ZoomFactor;
            float factor = (e.Delta > 0) ? 1.1f : 1f / 1.1f;
            ZoomFactor *= factor;
            ZoomFactor = Math.Max(0.1f, Math.Min(10f, ZoomFactor));
            // Maintain the point under the cursor in the same position
            PanOffset = new PointF(
                e.X - (e.X - PanOffset.X) * (ZoomFactor / oldZoom),
                e.Y - (e.Y - PanOffset.Y) * (ZoomFactor / oldZoom)
            );
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Set high quality rendering
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            // Apply pan and zoom transformations
            e.Graphics.TranslateTransform(PanOffset.X, PanOffset.Y);
            e.Graphics.ScaleTransform(ZoomFactor, ZoomFactor);

            // Draw grid if enabled
            if (ShowGrid)
            {
                int gridSpacing = 50;
                using (Pen gridPen = new Pen(Color.LightGray, 1 / ZoomFactor))
                {
                    for (float x = arrangementArea.X; x <= arrangementArea.Right; x += gridSpacing)
                    {
                        e.Graphics.DrawLine(gridPen, x, arrangementArea.Y, x, arrangementArea.Bottom);
                    }
                    for (float y = arrangementArea.Y; y <= arrangementArea.Bottom; y += gridSpacing)
                    {
                        e.Graphics.DrawLine(gridPen, arrangementArea.X, y, arrangementArea.Right, y);
                    }
                }
            }

            // Draw texture items in order of increasing Z
            var sortedItems = Items.OrderBy(item => item.Z).ToList();
            foreach (var item in sortedItems)
            {
                e.Graphics.DrawImage(item.Image, item.Bounds);
                using (var font = new Font("Arial", 10, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.Black))
                {
                    string text = $"{item.Name} (Z: {item.Z})";
                    SizeF textSize = e.Graphics.MeasureString(text, font);
                    PointF textPos = new PointF(
                        item.Bounds.X + (item.Bounds.Width - textSize.Width) / 2,
                        item.Bounds.Y + (item.Bounds.Height - textSize.Height) / 2
                    );
                    e.Graphics.DrawString(text, font, brush, textPos);
                }
            }

            // Highlight selected items with a blue dashed rectangle
            using (Pen selectionPen = new Pen(Color.Blue, 2 / ZoomFactor))
            {
                selectionPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                foreach (var item in selectedItems)
                {
                    e.Graphics.DrawRectangle(selectionPen, item.Bounds.X, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
                }
            }

            // Reset transformation to draw selection rectangle in screen coordinates
            e.Graphics.ResetTransform();
            // Draw the selection rectangle if currently selecting
            if (isSelecting)
            {
                using (Pen pen = new Pen(Color.Blue))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    e.Graphics.DrawRectangle(pen, selectionRect);
                }
            }

            // Draw arrangement area border
            using (Pen pen = new Pen(Color.Red, 2))
            {
                // Convert arrangementArea from world to screen coordinates
                Rectangle screenRect = new Rectangle(
                    (int)(PanOffset.X + arrangementArea.X * ZoomFactor),
                    (int)(PanOffset.Y + arrangementArea.Y * ZoomFactor),
                    (int)(arrangementArea.Width * ZoomFactor),
                    (int)(arrangementArea.Height * ZoomFactor)
                );
                e.Graphics.DrawRectangle(pen, screenRect);
            }
        }
    }
}
