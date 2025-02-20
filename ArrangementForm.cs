using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace AtlasToolEditor
{
    // Model of the saved region arrangement
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
        private Panel viewportPanel;          // Viewport area 1280x720
        private TextureCanvas textureCanvas;  // Our canvas with zoom/pan and texture drawing
        private Button btnSaveArrangement;
        private Image fullImage;

        // Constructor taking the full image from MainForm
        public ArrangementForm(Image fullImage)
        {
            this.fullImage = fullImage;
            this.Text = "Arrangement Form";
            // Form is larger so the viewport is centered and margins are visible
            this.ClientSize = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.DarkGray;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Create viewportPanel with a fixed size of 1280x720 – white background
            viewportPanel = new Panel();
            viewportPanel.Size = new Size(1280, 720);
            viewportPanel.Location = new Point((this.ClientSize.Width - viewportPanel.Width) / 2,
                                               (this.ClientSize.Height - viewportPanel.Height) / 2);
            viewportPanel.BackColor = Color.White;
            // Optionally, you can set borderFixed, but the red border will be drawn by the canvas
            viewportPanel.BorderStyle = BorderStyle.None;
            this.Controls.Add(viewportPanel);

            // Create TextureCanvas that will fill the viewportPanel
            textureCanvas = new TextureCanvas();
            textureCanvas.Dock = DockStyle.Fill;
            viewportPanel.Controls.Add(textureCanvas);

            // Save arrangement button – place it outside the viewportPanel
            btnSaveArrangement = new Button();
            btnSaveArrangement.Text = "Save Arrangement";
            btnSaveArrangement.Size = new Size(120, 30);
            btnSaveArrangement.Location = new Point(this.ClientSize.Width - btnSaveArrangement.Width - 10, 10);
            btnSaveArrangement.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveArrangement.Click += BtnSaveArrangement_Click;
            this.Controls.Add(btnSaveArrangement);

            this.Load += ArrangementForm_Load;
        }

        private void ArrangementForm_Load(object sender, EventArgs e)
        {
            // User selects a JSON file with regions
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
                        // Cut out the image fragment
                        Bitmap croppedBitmap = new Bitmap(fullImage).Clone(cropRect, fullImage.PixelFormat);
                        // Add a new TextureItem object to the canvas – base coordinates are the original region values (0,0,1280,720 world)
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

        private void BtnSaveArrangement_Click(object sender, EventArgs e)
        {
            // Calculating the final positions and sizes of textures in screen coordinates
            List<ArrangedRegion> arranged = new List<ArrangedRegion>();
            foreach (var item in textureCanvas.Items)
            {
                // Positions in viewportPanel: (world X * ZoomFactor + PanOffset)
                RectangleF finalBounds = new RectangleF(
                    item.Bounds.X * textureCanvas.ZoomFactor + textureCanvas.PanOffset.X,
                    item.Bounds.Y * textureCanvas.ZoomFactor + textureCanvas.PanOffset.Y,
                    item.Bounds.Width * textureCanvas.ZoomFactor,
                    item.Bounds.Height * textureCanvas.ZoomFactor
                );
                arranged.Add(new ArrangedRegion()
                {
                    Name = item.Name,
                    ScreenX = (int)finalBounds.X,
                    ScreenY = (int)finalBounds.Y,
                    Width = (int)finalBounds.Width,
                    Height = (int)finalBounds.Height
                });
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(arranged, options);

                // Use SaveFileDialog to choose the save path
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "JSON|*.json";
                    sfd.FileName = "arranged_layout.json"; // default name, which the user can change
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

    }
}