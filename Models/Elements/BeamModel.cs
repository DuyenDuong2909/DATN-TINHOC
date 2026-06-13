namespace AutoCADToRevitApplication.Models.Elements
{
    public class BeamModel
    {
        public string LayerName { get; set; } = string.Empty;
        public Point2D StartPoint { get; set; } = new();
        public Point2D EndPoint { get; set; } = new();
        public Point2D CenterPoint { get; set; } = new();
        public double Width { get; set; }
        public double Height { get; set; }
        public double RotationDegrees { get; set; }
        public string DimensionText { get; set; } = string.Empty;
        public string SourceType { get; set; } = "AxisLine";

        public double Length =>
            Math.Sqrt(Math.Pow(EndPoint.X - StartPoint.X, 2) +
                      Math.Pow(EndPoint.Y - StartPoint.Y, 2));

        public override string ToString()
            => $"Beam {Width:F0}x{Height:F0} {StartPoint} -> {EndPoint}, angle {RotationDegrees:F1}";
    }
}
