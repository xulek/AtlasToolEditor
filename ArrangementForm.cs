using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace AtlasToolEditor
{
    // Model of saved region arrangement
    public class ArrangedRegion
    {
        public string Name { get; set; }
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class ArrangementForm : Form
    {
        private Panel viewportPanel;          // View area with dimensions 1280x720
        private TextureCanvas textureCanvas;  // Canvas with zoom/pan and texture drawing support
        private Button btnSaveArrangement;
        private Button btnLoadArrangement;    // New button to load arrangement
        private Image fullImage;

        // Constructor accepting the full image from MainForm
        public ArrangementForm(Image fullImage)
        {
            this.fullImage = fullImage;
            this.Text = "Arrangement Form";
            // Form settings - larger to center the view
            this.ClientSize = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.DarkGray;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Creating viewportPanel with fixed dimensions 1280x720 - white background
            viewportPanel = new Panel();
            viewportPanel.Size = new Size(1280, 720);
            viewportPanel.Location = new Point((this.ClientSize.Width - viewportPanel.Width) / 2,
                                               (this.ClientSize.Height - viewportPanel.Height) / 2);
            viewportPanel.BackColor = Color.White;
            viewportPanel.BorderStyle = BorderStyle.None;
            this.Controls.Add(viewportPanel);

            // We create ONE instance of TextureCanvas, set Dock and enable grid
            textureCanvas = new TextureCanvas();
            textureCanvas.Dock = DockStyle.Fill;
            textureCanvas.ShowGrid = true;  // Enable grid
            viewportPanel.Controls.Add(textureCanvas);

            // Arrangement save button - placed outside viewportPanel
            btnSaveArrangement = new Button();
            btnSaveArrangement.Text = "Save Arrangement";
            btnSaveArrangement.Size = new Size(120, 30);
            btnSaveArrangement.Location = new Point(this.ClientSize.Width - btnSaveArrangement.Width - 10, 10);
            btnSaveArrangement.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveArrangement.Click += BtnSaveArrangement_Click;
            this.Controls.Add(btnSaveArrangement);

            // New button to load the layout - placed next to the save button
            btnLoadArrangement = new Button();
            btnLoadArrangement.Text = "Load Arrangement";
            btnLoadArrangement.Size = new Size(120, 30);
            btnLoadArrangement.Location = new Point(btnSaveArrangement.Left - btnLoadArrangement.Width - 10, 10);
            btnLoadArrangement.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoadArrangement.Click += BtnLoadArrangement_Click;
            this.Controls.Add(btnLoadArrangement);

            this.Load += ArrangementForm_Load;
        }

        private void ArrangementForm_Load(object sender, EventArgs e)
        {
            // The user selects a JSON file with regions (defined in MainForm)
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
                var regions = JsonSerializer.Deserialize<List<RegionDefinition>>(json);
                if (regions != null)
                {
                    foreach (var region in regions)
                    {
                        // Check if the region fits within the full image
                        Rectangle cropRect = new Rectangle(region.X, region.Y, region.Width, region.Height);
                        if (cropRect.Right > fullImage.Width || cropRect.Bottom > fullImage.Height)
                            continue;
                        // Cut out a fragment of the image
                        Bitmap croppedBitmap = new Bitmap(fullImage).Clone(cropRect, fullImage.PixelFormat);
                        // Add a new TextureItem object to the canvas - base coordinates are the original region values (0,0,1280,720)
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
                MessageBox.Show("Error loading JSON: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Saving the layout - saving world coordinates
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
                    Height = (int)item.Bounds.Height
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

        // Loading a previously saved layout
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
                    // Reset transformations to default values
                    textureCanvas.ZoomFactor = 1.0f;
                    textureCanvas.PanOffset = new PointF(0, 0);
                    // For each saved region, we update the position on the canvas (searching by name)
                    foreach (var arr in arranged)
                    {
                        var item = textureCanvas.Items.Find(x => x.Name == arr.Name);
                        if (item != null)
                        {
                            item.Bounds = new RectangleF(arr.ScreenX, arr.ScreenY, arr.Width, arr.Height);
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
    }
}