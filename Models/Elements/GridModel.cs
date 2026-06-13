namespace AutoCADToRevitApplication.Models.Elements
{
    public class GridModel
    {
        public string Name { get; set; } = string.Empty;

        public string LayerName { get; set; } = string.Empty;

        public Point2D StartPoint { get; set; } = new();

        public Point2D EndPoint { get; set; } = new();

        public bool IsVertical { get; set; }

        public double Length =>
            Math.Sqrt(Math.Pow(EndPoint.X - StartPoint.X, 2) +
                      Math.Pow(EndPoint.Y - StartPoint.Y, 2));

        public Point2D MidPoint => new()
        {
            X = (StartPoint.X + EndPoint.X) / 2,
            Y = (StartPoint.Y + EndPoint.Y) / 2
        };

        public override string ToString() =>
            $"Grid [{Name}] {(IsVertical ? "Dọc" : "Ngang")} " +
            $"({StartPoint.X:F0}, {StartPoint.Y:F0}) → ({EndPoint.X:F0}, {EndPoint.Y:F0})";
    }

    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D() { }
        public Point2D(double x, double y) { X = x; Y = y; }

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}
