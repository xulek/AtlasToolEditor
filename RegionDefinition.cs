namespace AtlasToolEditor
{
    // This class holds the definition of a texture region in the atlas.
    public class RegionDefinition
    {
        // Initialized to an empty string to satisfy non-nullable reference type requirements.
        public string Name { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        // Checks if the point (px, py) is inside the region.
        public bool ContainsPoint(int px, int py)
        {
            return (px >= X && px <= X + Width &&
                    py >= Y && py <= Y + Height);
        }
    }
}
