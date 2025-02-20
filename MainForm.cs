using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace AtlasToolEditor
{
    public partial class MainForm : Form
    {
        private Bitmap _loadedImage;                  // Loaded image
        private List<RegionDefinition> _regions;      // List of all regions
        private RegionDefinition _selectedRegion;     // Currently selected region (for editing)

        // Helper variable for drawing a new region
        private bool _isDrawingNew;
        private Point _drawStartPoint;                // Starting point for drawing a new region
        private Rectangle _drawCurrentRect;           // Current rectangle being drawn

        // Helper variable for moving/resizing an existing region
        private bool _isMovingOrResizing;
        private Point _moveResizeStartMouse;          // Mouse position when starting move/resize
        private Rectangle _originalSelectedBounds;    // Original bounds of the region when starting
        private ResizeMode _resizeMode;               // Determines if and where we are grabbing for resize

        // Margin (in pixels) to "grab" an edge for resize
        private const int EdgeGrabSize = 8;
        private enum ResizeMode
        {
            None,
            Move,
            LeftEdge,
            RightEdge,
            TopEdge,
            BottomEdge,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        // Variables for zoom and panning
        private float _zoomFactor = 1.0f;
        private float _panX = 0f;
        private float _panY = 0f;
        private const float ZoomStep = 1.1f;

        // Variables for panning (moving the image) using RMB
        private bool _isPanning;
        private Point _lastPanPoint;

        public MainForm()
        {
            InitializeComponent();

            _regions = new List<RegionDefinition>();

            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.Image = null; // We draw manually

            // Mouse events
            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;
            pictureBox1.Paint += PictureBox1_Paint;

            // Buttons
            buttonLoad.Click += ButtonLoad_Click;
            buttonSave.Click += ButtonSave_Click;
            buttonLoadJson.Click += ButtonLoadJson_Click;
            buttonClear.Click += ButtonClear_Click;
            buttonZoomIn.Click += (s, e) => ZoomAt(ZoomStep, pictureBox1.PointToClient(Cursor.Position));
            buttonZoomOut.Click += (s, e) => ZoomAt(1f / ZoomStep, pictureBox1.PointToClient(Cursor.Position));
            buttonArrange.Click += new System.EventHandler(this.buttonArrange_Click);

            // Mouse wheel handling (zoom) – in Form
            this.MouseWheel += MainForm_MouseWheel;
        }

        #region Basic functions

        private void ButtonLoad_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _loadedImage = new Bitmap(ofd.FileName);

                    // Reset regions and parameters
                    _regions.Clear();
                    _selectedRegion = null;
                    _zoomFactor = 1.0f;
                    _panX = 0f;
                    _panY = 0f;

                    pictureBox1.Invalidate();
                }
            }
        }

        private void ButtonSave_Click(object sender, EventArgs e)
        {
            if (_regions.Count == 0)
            {
                MessageBox.Show("No regions selected to save.");
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON|*.json";
                sfd.FileName = "atlas.json";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(_regions, options);
                    File.WriteAllText(sfd.FileName, json);
                    MessageBox.Show("Saved to JSON file.");
                }
            }
        }

        private void ButtonLoadJson_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(ofd.FileName);
                        var loaded = JsonSerializer.Deserialize<List<RegionDefinition>>(json);

                        if (loaded != null)
                        {
                            _regions = loaded;
                            _selectedRegion = null;
                            pictureBox1.Invalidate();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("JSON read error: " + ex.Message);
                    }
                }
            }
        }

        private void ButtonClear_Click(object sender, EventArgs e)
        {
            _regions.Clear();
            _selectedRegion = null;
            pictureBox1.Invalidate();
        }

        #endregion

        private void buttonArrange_Click(object? sender, EventArgs e)
        {
            if (_loadedImage == null)
            {
                MessageBox.Show("Load an image first.", "No image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // We open arrangement window – all regions will be loaded from a JSON file
            ArrangementForm form = new ArrangementForm(_loadedImage);
            form.ShowDialog();
        }

        #region Mouse handling - creating and editing regions + panning

        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (_loadedImage == null) return;

            if (e.Button == MouseButtons.Left)
            {
                Point imagePoint = ScreenToImage(e.Location);

                // Check if an existing region was clicked
                RegionDefinition clickedRegion = null;
                for (int i = _regions.Count - 1; i >= 0; i--)
                {
                    var reg = _regions[i];
                    if (reg.ContainsPoint(imagePoint.X, imagePoint.Y))
                    {
                        clickedRegion = reg;
                        break;
                    }
                }

                if (clickedRegion != null)
                {
                    _selectedRegion = clickedRegion;
                    pictureBox1.Invalidate();

                    _resizeMode = GetResizeMode(_selectedRegion, imagePoint);
                    if (_resizeMode != ResizeMode.None)
                    {
                        _isMovingOrResizing = true;
                        _moveResizeStartMouse = imagePoint;
                        _originalSelectedBounds = new Rectangle(_selectedRegion.X, _selectedRegion.Y,
                                                                _selectedRegion.Width, _selectedRegion.Height);
                    }
                    else
                    {
                        _resizeMode = ResizeMode.Move;
                        _isMovingOrResizing = true;
                        _moveResizeStartMouse = imagePoint;
                        _originalSelectedBounds = new Rectangle(_selectedRegion.X, _selectedRegion.Y,
                                                                _selectedRegion.Width, _selectedRegion.Height);
                    }
                }
                else
                {
                    // New region
                    _isDrawingNew = true;
                    _drawStartPoint = imagePoint;
                    _drawCurrentRect = new Rectangle(imagePoint.X, imagePoint.Y, 0, 0);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Panning the image instead of a context menu
                _isPanning = true;
                _lastPanPoint = e.Location; // in PictureBox coordinates
            }
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (_loadedImage == null) return;

            if (_isPanning)
            {
                // Move the image (pan)
                float dx = e.Location.X - _lastPanPoint.X;
                float dy = e.Location.Y - _lastPanPoint.Y;
                _panX += dx;
                _panY += dy;
                _lastPanPoint = e.Location;
                pictureBox1.Invalidate();
                return;
            }

            Point imagePoint = ScreenToImage(e.Location);

            if (_isDrawingNew)
            {
                // Calculate candidate rectangle
                int x = Math.Min(_drawStartPoint.X, imagePoint.X);
                int y = Math.Min(_drawStartPoint.Y, imagePoint.Y);
                int w = Math.Abs(_drawStartPoint.X - imagePoint.X);
                int h = Math.Abs(_drawStartPoint.Y - imagePoint.Y);

                // Clamp to image
                x = Math.Max(0, x);
                y = Math.Max(0, y);
                w = Math.Min(w, _loadedImage.Width - x);
                h = Math.Min(h, _loadedImage.Height - y);

                Rectangle candidateRect = new Rectangle(x, y, w, h);

                // Check for collisions with existing regions
                bool overlaps = false;
                foreach (var region in _regions)
                {
                    Rectangle existing = new Rectangle(region.X, region.Y, region.Width, region.Height);
                    if (candidateRect.IntersectsWith(existing))
                    {
                        overlaps = true;
                        break;
                    }
                }

                // If it collides, don't update _drawCurrentRect
                if (!overlaps)
                {
                    _drawCurrentRect = candidateRect;
                    pictureBox1.Invalidate();
                }
            }
            else if (_isMovingOrResizing && _selectedRegion != null)
            {
                // Moving / resizing
                int dx = imagePoint.X - _moveResizeStartMouse.X;
                int dy = imagePoint.Y - _moveResizeStartMouse.Y;

                // Start from the original rectangle
                Rectangle candidate = _originalSelectedBounds;

                // Modify edges depending on the mode
                // (without flipping – if we reach the edge, we stop)
                int left = candidate.Left;
                int right = candidate.Right;
                int top = candidate.Top;
                int bottom = candidate.Bottom;

                switch (_resizeMode)
                {
                    case ResizeMode.Move:
                        left += dx; right += dx;
                        top += dy; bottom += dy;
                        break;

                    case ResizeMode.LeftEdge:
                        left += dx;
                        break;
                    case ResizeMode.RightEdge:
                        right += dx;
                        break;
                    case ResizeMode.TopEdge:
                        top += dy;
                        break;
                    case ResizeMode.BottomEdge:
                        bottom += dy;
                        break;

                    case ResizeMode.TopLeft:
                        left += dx;
                        top += dy;
                        break;
                    case ResizeMode.TopRight:
                        right += dx;
                        top += dy;
                        break;
                    case ResizeMode.BottomLeft:
                        left += dx;
                        bottom += dy;
                        break;
                    case ResizeMode.BottomRight:
                        right += dx;
                        bottom += dy;
                        break;
                }

                // Stop at the edges of the image
                if (left < 0) left = 0;
                if (top < 0) top = 0;
                if (right > _loadedImage.Width) right = _loadedImage.Width;
                if (bottom > _loadedImage.Height) bottom = _loadedImage.Height;

                // Make sure left <= right and top <= bottom
                if (left > right) left = right;
                if (top > bottom) top = bottom;

                Rectangle newRect = new Rectangle(left, top, right - left, bottom - top);

                // Check for collisions with other regions
                bool overlaps = false;
                for (int i = 0; i < _regions.Count; i++)
                {
                    var reg = _regions[i];
                    if (reg == _selectedRegion) continue; // skip itself
                    Rectangle existing = new Rectangle(reg.X, reg.Y, reg.Width, reg.Height);
                    if (newRect.IntersectsWith(existing))
                    {
                        overlaps = true;
                        break;
                    }
                }

                // If no collision, update the region
                if (!overlaps)
                {
                    _selectedRegion.X = newRect.X;
                    _selectedRegion.Y = newRect.Y;
                    _selectedRegion.Width = newRect.Width;
                    _selectedRegion.Height = newRect.Height;
                }

                pictureBox1.Invalidate();
            }
            else
            {
                // Set the cursor (hover)
                RegionDefinition hoveredRegion = null;
                for (int i = _regions.Count - 1; i >= 0; i--)
                {
                    if (_regions[i].ContainsPoint(imagePoint.X, imagePoint.Y))
                    {
                        hoveredRegion = _regions[i];
                        break;
                    }
                }

                if (hoveredRegion != null)
                {
                    var mode = GetResizeMode(hoveredRegion, imagePoint);
                    pictureBox1.Cursor = GetCursorForResizeMode(mode);
                }
                else
                {
                    pictureBox1.Cursor = Cursors.Default;
                }
            }
        }

        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (_loadedImage == null) return;

            // Finish panning
            if (_isPanning && e.Button == MouseButtons.Right)
            {
                _isPanning = false;
                return;
            }

            if (_isDrawingNew)
            {
                _isDrawingNew = false;
                if (_drawCurrentRect.Width > 5 && _drawCurrentRect.Height > 5)
                {
                    // Add a new region
                    string name = PromptForName();
                    if (!string.IsNullOrEmpty(name))
                    {
                        var newRegion = new RegionDefinition
                        {
                            Name = name,
                            X = _drawCurrentRect.X,
                            Y = _drawCurrentRect.Y,
                            Width = _drawCurrentRect.Width,
                            Height = _drawCurrentRect.Height
                        };
                        _regions.Add(newRegion);
                        _selectedRegion = newRegion;
                    }
                }
                _drawCurrentRect = Rectangle.Empty;
                pictureBox1.Invalidate();
            }
            else if (_isMovingOrResizing)
            {
                _isMovingOrResizing = false;
            }
        }

        #endregion

        #region Drawing (Paint)

        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (_loadedImage == null) return;

            // Draw the image, taking into account panning and zoom
            int drawW = (int)(_loadedImage.Width * _zoomFactor);
            int drawH = (int)(_loadedImage.Height * _zoomFactor);
            var destRect = new Rectangle((int)_panX, (int)_panY, drawW, drawH);

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_loadedImage, destRect);

            using (var penSelected = new Pen(Color.Lime, 2))
            using (var penNormal = new Pen(Color.Red, 1))
            using (var brushName = new SolidBrush(Color.Blue))
            using (var font = new Font("Arial", 10, FontStyle.Bold))
            {
                // Draw existing regions
                foreach (var region in _regions)
                {
                    Rectangle screenRect = ImageToScreen(region.X, region.Y, region.Width, region.Height);
                    var pen = (region == _selectedRegion) ? penSelected : penNormal;
                    e.Graphics.DrawRectangle(pen, screenRect);
                    string text = $"{region.Name} ({region.Width}x{region.Height})";
                    e.Graphics.DrawString(text, font, brushName, screenRect.Left + 2, screenRect.Top + 2);
                }
            }

            // Draw the currently created region
            if (_isDrawingNew && _drawCurrentRect.Width > 0 && _drawCurrentRect.Height > 0)
            {
                Rectangle screenRect = ImageToScreen(_drawCurrentRect.X, _drawCurrentRect.Y, _drawCurrentRect.Width, _drawCurrentRect.Height);
                using (var penDash = new Pen(Color.Green, 2))
                {
                    penDash.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    e.Graphics.DrawRectangle(penDash, screenRect);
                    string sizeInfo = $"{_drawCurrentRect.Width} x {_drawCurrentRect.Height}";
                    using (Font font = new Font("Arial", 10, FontStyle.Bold))
                    using (SolidBrush brushName = new SolidBrush(Color.Blue))
                    {
                        e.Graphics.DrawString(sizeInfo, font, brushName, screenRect.Left + 2, screenRect.Top + 2);
                    }
                }
            }
        }

        #endregion

        #region Helper methods (zoom, transformations, menu, etc.)

        /// <summary>
        /// Zoom using the mouse wheel (Form.MouseWheel)
        /// </summary>
        private void MainForm_MouseWheel(object sender, MouseEventArgs e)
        {
            var picPos = pictureBox1.PointToClient(Cursor.Position);
            if (!pictureBox1.ClientRectangle.Contains(picPos))
                return;

            float factor = (e.Delta > 0) ? ZoomStep : (1f / ZoomStep);
            ZoomAt(factor, picPos);
        }

        /// <summary>
        /// ZoomAt – zooms in/out the image at the cursor position (screenMousePos in pictureBox1 coordinates).
        /// </summary>
        private void ZoomAt(float factor, Point screenMousePos)
        {
            if (_loadedImage == null) return;

            float oldCoordX = (screenMousePos.X - _panX) / _zoomFactor;
            float oldCoordY = (screenMousePos.Y - _panY) / _zoomFactor;

            _zoomFactor *= factor;
            if (_zoomFactor < 0.1f) _zoomFactor = 0.1f;
            if (_zoomFactor > 10f) _zoomFactor = 10f;

            _panX = screenMousePos.X - oldCoordX * _zoomFactor;
            _panY = screenMousePos.Y - oldCoordY * _zoomFactor;

            pictureBox1.Invalidate();
        }

        /// <summary>
        /// Converts a screen point (PictureBox) to image coordinates (0..width, 0..height).
        /// </summary>
        private Point ScreenToImage(Point screenPt)
        {
            if (_loadedImage == null) return Point.Empty;

            float ix = (screenPt.X - _panX) / _zoomFactor;
            float iy = (screenPt.Y - _panY) / _zoomFactor;
            return new Point((int)ix, (int)iy);
        }

        /// <summary>
        /// Converts a rectangle from image coords to screen coords (PictureBox).
        /// </summary>
        private Rectangle ImageToScreen(int x, int y, int w, int h)
        {
            float sx = _panX + x * _zoomFactor;
            float sy = _panY + y * _zoomFactor;
            float sw = w * _zoomFactor;
            float sh = h * _zoomFactor;
            return new Rectangle((int)sx, (int)sy, (int)sw, (int)sh);
        }

        private string PromptForName()
        {
            using (var form = new Form())
            {
                form.Text = "Region name";
                var label = new Label() { Text = "Name:", Left = 10, Top = 10, AutoSize = true };
                var textBox = new TextBox() { Left = 10, Top = 30, Width = 200 };
                var buttonOk = new Button() { Text = "OK", Left = 10, Width = 60, Top = 60, DialogResult = DialogResult.OK };
                form.AcceptButton = buttonOk;

                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(buttonOk);

                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(220, 100);
                return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
            }
        }

        private void ShowContextMenu(Point screenPoint)
        {
            var contextMenu = new ContextMenuStrip();
            var itemRename = new ToolStripMenuItem("Rename");
            var itemDelete = new ToolStripMenuItem("Delete region");

            itemRename.Click += (s, e) =>
            {
                if (_selectedRegion != null)
                {
                    string newName = PromptForName();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        _selectedRegion.Name = newName;
                        pictureBox1.Invalidate();
                    }
                }
            };

            itemDelete.Click += (s, e) =>
            {
                if (_selectedRegion != null)
                {
                    _regions.Remove(_selectedRegion);
                    _selectedRegion = null;
                    pictureBox1.Invalidate();
                }
            };

            contextMenu.Items.Add(itemRename);
            contextMenu.Items.Add(itemDelete);
            // Show the menu in pictureBox coordinates
            contextMenu.Show(pictureBox1, screenPoint);
        }

        private ResizeMode GetResizeMode(RegionDefinition region, Point mouseInImage)
        {
            bool left = Math.Abs(mouseInImage.X - region.X) <= EdgeGrabSize;
            bool right = Math.Abs(mouseInImage.X - (region.X + region.Width)) <= EdgeGrabSize;
            bool top = Math.Abs(mouseInImage.Y - region.Y) <= EdgeGrabSize;
            bool bottom = Math.Abs(mouseInImage.Y - (region.Y + region.Height)) <= EdgeGrabSize;

            if (top && left) return ResizeMode.TopLeft;
            if (top && right) return ResizeMode.TopRight;
            if (bottom && left) return ResizeMode.BottomLeft;
            if (bottom && right) return ResizeMode.BottomRight;
            if (top) return ResizeMode.TopEdge;
            if (bottom) return ResizeMode.BottomEdge;
            if (left) return ResizeMode.LeftEdge;
            if (right) return ResizeMode.RightEdge;

            if (region.ContainsPoint(mouseInImage.X, mouseInImage.Y))
            {
                return ResizeMode.Move;
            }
            return ResizeMode.None;
        }

        private Cursor GetCursorForResizeMode(ResizeMode mode)
        {
            switch (mode)
            {
                case ResizeMode.LeftEdge:
                case ResizeMode.RightEdge:
                    return Cursors.SizeWE;
                case ResizeMode.TopEdge:
                case ResizeMode.BottomEdge:
                    return Cursors.SizeNS;
                case ResizeMode.TopLeft:
                case ResizeMode.BottomRight:
                    return Cursors.SizeNWSE;
                case ResizeMode.TopRight:
                case ResizeMode.BottomLeft:
                    return Cursors.SizeNESW;
                case ResizeMode.Move:
                    return Cursors.SizeAll;
                default:
                    return Cursors.Default;
            }
        }

        #endregion
    }
}