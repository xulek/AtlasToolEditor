using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace AtlasToolEditor
{
    // Represents a saved arrangement region.
    public class ArrangedRegion
    {
        public string Name { get; set; }
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Z { get; set; }
    }

    public class ArrangementForm : Form
    {
        private Panel viewportPanel;
        private TextureCanvas textureCanvas;
        private Button btnSaveArrangement;
        private Button btnLoadArrangement;
        private Button btnUndo; // New undo button
        private Image fullImage;

        private Stack<List<UndoItem>> _undoStack = new Stack<List<UndoItem>>();
        private bool _undoStateRecorded = false;

        // Helper class to store undo state
        private class UndoItem
        {
            public string Name { get; set; }
            public RectangleF Bounds { get; set; }
            public int Z { get; set; }
        }

        public ArrangementForm(Image fullImage)
        {
            this.fullImage = fullImage;
            this.Text = "Arrangement Form";
            this.ClientSize = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.DarkGray;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            viewportPanel = new Panel();
            viewportPanel.Size = new Size(1280, 720);
            viewportPanel.Location = new Point((this.ClientSize.Width - viewportPanel.Width) / 2,
                                               (this.ClientSize.Height - viewportPanel.Height) / 2);
            viewportPanel.BackColor = Color.White;
            viewportPanel.BorderStyle = BorderStyle.None;
            this.Controls.Add(viewportPanel);

            textureCanvas = new TextureCanvas();
            textureCanvas.Dock = DockStyle.Fill;
            textureCanvas.ShowGrid = true;
            viewportPanel.Controls.Add(textureCanvas);

            btnSaveArrangement = new Button();
            btnSaveArrangement.Text = "Save Arrangement";
            btnSaveArrangement.Size = new Size(120, 30);
            btnSaveArrangement.Location = new Point(this.ClientSize.Width - btnSaveArrangement.Width - 10, 10);
            btnSaveArrangement.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveArrangement.Click += BtnSaveArrangement_Click;
            this.Controls.Add(btnSaveArrangement);

            btnLoadArrangement = new Button();
            btnLoadArrangement.Text = "Load Arrangement";
            btnLoadArrangement.Size = new Size(120, 30);
            btnLoadArrangement.Location = new Point(btnSaveArrangement.Left - btnLoadArrangement.Width - 10, 10);
            btnLoadArrangement.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoadArrangement.Click += BtnLoadArrangement_Click;
            this.Controls.Add(btnLoadArrangement);

            btnUndo = new Button();
            btnUndo.Text = "Undo";
            btnUndo.Size = new Size(120, 30);
            btnUndo.Location = new Point(btnLoadArrangement.Left - btnUndo.Width - 10, 10);
            btnUndo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUndo.Click += BtnUndo_Click;
            this.Controls.Add(btnUndo);

            this.Load += ArrangementForm_Load;

            // Subscribe to canvas mouse events for undo tracking
            textureCanvas.MouseDown += TextureCanvas_MouseDownForUndo;
            textureCanvas.MouseUp += TextureCanvas_MouseUpForUndo;
        }

        private void ArrangementForm_Load(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "JSON|*.json";
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                this.Close();
                return;
            }
            try
            {
                string json = File.ReadAllText(ofd.FileName);
                // Deserialize textures saved from MainForm
                var regions = JsonSerializer.Deserialize<List<RegionDefinition>>(json);
                if (regions != null)
                {
                    foreach (var region in regions)
                    {
                        Rectangle cropRect = new Rectangle(region.X, region.Y, region.Width, region.Height);
                        if (cropRect.Right > fullImage.Width || cropRect.Bottom > fullImage.Height)
                            continue;
                        Bitmap croppedBitmap = new Bitmap(fullImage).Clone(cropRect, fullImage.PixelFormat);
                        textureCanvas.Items.Add(new TextureItem()
                        {
                            Name = region.Name,
                            Image = croppedBitmap,
                            Bounds = new RectangleF(region.X, region.Y, region.Width, region.Height)
                        });
                    }
                    textureCanvas.Invalidate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading textures JSON: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Save arrangement positions to a JSON file.
        private void BtnSaveArrangement_Click(object sender, EventArgs e)
        {
            List<ArrangedRegion> arranged = new List<ArrangedRegion>();
            foreach (var item in textureCanvas.Items)
            {
                arranged.Add(new ArrangedRegion()
                {
                    Name = item.Name,
                    ScreenX = (int)item.Bounds.X,
                    ScreenY = (int)item.Bounds.Y,
                    Width = (int)item.Bounds.Width,
                    Height = (int)item.Bounds.Height,
                    Z = item.Z
                });
            }
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(arranged, options);
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "JSON|*.json";
                    sfd.FileName = "arranged_layout.json";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(sfd.FileName, json);
                        MessageBox.Show("Arrangement saved to: " + sfd.FileName, "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving arrangement: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Load arranged positions from a JSON file.
        private void BtnLoadArrangement_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "JSON|*.json";
            if (ofd.ShowDialog() != DialogResult.OK) return;
            try
            {
                string json = File.ReadAllText(ofd.FileName);
                var arranged = JsonSerializer.Deserialize<List<ArrangedRegion>>(json);
                if (arranged != null)
                {
                    textureCanvas.ZoomFactor = 1.0f;
                    textureCanvas.PanOffset = new PointF(0, 0);
                    foreach (var arr in arranged)
                    {
                        var item = textureCanvas.Items.Find(x => x.Name == arr.Name);
                        if (item != null)
                        {
                            item.Bounds = new RectangleF(arr.ScreenX, arr.ScreenY, arr.Width, arr.Height);
                            item.Z = arr.Z;
                        }
                    }
                    textureCanvas.Invalidate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading arrangement: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Undo button click handler.
        private void BtnUndo_Click(object sender, EventArgs e)
        {
            Undo();
        }

        // Save current state of items.
        private void SaveState()
        {
            var snapshot = new List<UndoItem>();
            foreach (var item in textureCanvas.Items)
            {
                snapshot.Add(new UndoItem() { Name = item.Name, Bounds = item.Bounds, Z = item.Z });
            }
            _undoStack.Push(snapshot);
        }

        // Restore last saved state.
        private void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var snapshot = _undoStack.Pop();
                foreach (var snap in snapshot)
                {
                    var item = textureCanvas.Items.Find(x => x.Name == snap.Name);
                    if (item != null)
                    {
                        item.Bounds = snap.Bounds;
                        item.Z = snap.Z;
                    }
                }
                textureCanvas.Invalidate();
            }
            else
            {
                MessageBox.Show("Nothing to undo.", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Track mouse down on canvas to record state before a drag operation.
        private void TextureCanvas_MouseDownForUndo(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointF worldPt = new PointF((e.X - textureCanvas.PanOffset.X) / textureCanvas.ZoomFactor,
                                            (e.Y - textureCanvas.PanOffset.Y) / textureCanvas.ZoomFactor);
                foreach (var item in textureCanvas.Items)
                {
                    if (item.Bounds.Contains(worldPt))
                    {
                        if (!_undoStateRecorded)
                        {
                            SaveState();
                            _undoStateRecorded = true;
                        }
                        break;
                    }
                }
            }
        }

        // Reset undo state flag on mouse up.
        private void TextureCanvas_MouseUpForUndo(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _undoStateRecorded = false;
            }
        }
    }
}
