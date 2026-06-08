namespace AutoCADToRevitApplication.Models.Elements
{
    /// <summary>
    /// Represents one column footprint read from CAD geometry.
    /// All dimensions and coordinates are in millimeters.
    /// </summary>
    public class ColumnModel
    {
        public string LayerName { get; set; } = string.Empty;
        public Point2D CenterPoint { get; set; } = new();
        public double Width { get; set; }
        public double Height { get; set; }
        public double RotationDegrees { get; set; }
        public string PrimaryAxis => Height >= Width ? "H" : "B";

        public override string ToString()
            => $"Column {Width:F0}x{Height:F0} at {CenterPoint}, angle {RotationDegrees:F1}";
    }
}
