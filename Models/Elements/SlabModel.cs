namespace AutoCADToRevitApplication.Models.Elements
{
    public class SlabModel
    {
        public string LayerName { get; set; } = string.Empty;
        public List<Point2D> OuterLoop { get; set; } = new();
        public List<List<Point2D>> OpeningLoops { get; set; } = new();
        public double Thickness { get; set; }
        public double Area { get; set; }
        public Point2D CenterPoint { get; set; } = new();

        public override string ToString()
            => $"Slab {Thickness:F0}mm, area {Area:F0}, openings {OpeningLoops.Count}";
    }
}
