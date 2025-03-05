using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

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

        private TextureItem selectedItem = null;
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
                PointF basePt = new PointF((e.X - PanOffset.X) / ZoomFactor, (e.Y - PanOffset.Y) / ZoomFactor);

                selectedItem = Items.OrderByDescending(item => item.Z)
                                    .FirstOrDefault(item => item.Bounds.Contains(basePt));

                if (selectedItem != null)
                {
                    isDraggingItem = true;
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                isPanning = true;
            }
        }

        private void TextureCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingItem && selectedItem != null && e.Button == MouseButtons.Left)
            {
                // Movement in world coordinates
                float dx = (e.X - lastMousePos.X) / ZoomFactor;
                float dy = (e.Y - lastMousePos.Y) / ZoomFactor;
                RectangleF r = selectedItem.Bounds;
                r.X += dx;
                r.Y += dy;
                // Restriction to the arrangement area (world coordinates: 0,0,1280,720)
                if (r.X < arrangementArea.X) r.X = arrangementArea.X;
                if (r.Y < arrangementArea.Y) r.Y = arrangementArea.Y;
                if (r.Right > arrangementArea.Right) r.X = arrangementArea.Right - r.Width;
                if (r.Bottom > arrangementArea.Bottom) r.Y = arrangementArea.Bottom - r.Height;
                selectedItem.Bounds = r;
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
            // Maintaining the point under the cursor in the same position
            PanOffset = new PointF(
                e.X - (e.X - PanOffset.X) * (ZoomFactor / oldZoom),
                e.Y - (e.Y - PanOffset.Y) * (ZoomFactor / oldZoom)
            );
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            // Setting the transformation: first the offset, then the scaling
            e.Graphics.TranslateTransform(PanOffset.X, PanOffset.Y);
            e.Graphics.ScaleTransform(ZoomFactor, ZoomFactor);

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

            using (Pen pen = new Pen(Color.Red, 2 / ZoomFactor))
            {
                e.Graphics.DrawRectangle(pen, arrangementArea.X, arrangementArea.Y, arrangementArea.Width, arrangementArea.Height);
            }
        }
    }
}