# AtlasToolEditor

A Windows Forms application (targeting .NET 9.0) for creating and arranging texture regions (or "atlas" regions) in a single image. The tool allows you to define rectangular regions within an image, save them to a JSON file, and later arrange those regions on a 1280×720 layout for further use in game development, UI design, or other graphics-related tasks.

## Features

- **Load an Image**: Import a single large image (e.g., spritesheet or texture atlas).
- **Define Regions**: Draw, move, and resize rectangular regions within the image. Each region includes:
  - A name
  - X, Y coordinates in the image
  - Width and height
- **Save to JSON**: Export all defined regions to a JSON file for later use or sharing.
- **Load Regions from JSON**: Reload previously defined regions onto the same or compatible image.
- **Arrange Regions**: A separate form (`ArrangementForm`) allows you to:
  - Load a full image and a JSON file defining the regions
  - Zoom and pan around a fixed 1280×720 "arrangement area"
  - Drag and place each region
  - Save the final on-screen arrangement to another JSON file with updated positions

## Requirements

- [.NET 9.0 SDK](https://dotnet.microsoft.com/) (or a later version that supports the same features)
- A compatible IDE or editor such as [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) with C# extensions.

## Getting Started

1. **Clone or Download** this repository.
2. **Open the Solution** in Visual Studio (or your preferred editor) or navigate to the project folder in a terminal.

   ```bash
   dotnet build
   ```

3. **Run the Application**:

   ```bash
   dotnet run
   ```
   You can also press **F5** (or the equivalent debug/run command) from within Visual Studio.

## Usage

1. **Load an Image**  
   - Click **Load Image**.  
   - Select a PNG, JPG, or BMP file.  

2. **Defining Regions**  
   - Left-click and drag on the image to define a new region.  
   - Enter a name for the region.  
   - Move or resize existing regions by dragging their edges or corners.  

3. **Saving/Loading Regions**  
   - Click **Save JSON** to export your defined regions to a JSON file.  
   - Click **Load JSON** to import previously saved regions.  

4. **Clearing Regions**  
   - Click **Clear** to remove all defined regions from the current session.  

5. **Zooming & Panning**  
   - Use **Zoom +** and **Zoom -** buttons to zoom in or out.  
   - Right-click and drag on the image to pan around when zoomed in.  

6. **Arranging Regions**  
   - Once you have your image and JSON file of defined regions, click **Arrange**.  
   - In the **Arrangement Form**, you'll be asked to select the JSON file of regions.
   - The tool will crop each defined region from the loaded image.  
   - Pan and zoom as needed, then drag the regions within the 1280×720 layout boundary.
   - Click **Save Arrangement** to export a new JSON describing the final on-screen positions.  

## Project Structure

- **MainForm.cs**  
  Handles the primary UI for loading images, creating/editing regions, and saving/loading JSON.

- **ArrangementForm.cs**  
  Manages the secondary UI for arranging previously defined regions within a 1280×720 area and saving those placements.

- **RegionDefinition.cs**  
  A basic class representing a region’s name, position, and size.

- **TextureItem.cs** and **TextureCanvas**  
  Classes that represent a texture item and a custom drawing canvas for the arrangement functionality.

- **Program.cs**  
  Main entry point for the Windows Forms application.

## Contributing

Contributions are welcome! If you find a bug or have a feature request, feel free to open an [issue](../../issues) or submit a pull request.

---

**Thank you for using AtlasToolEditor!** Feel free to leave suggestions or report issues so we can continue to improve the tool.
