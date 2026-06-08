namespace AutoCADToRevitApplication.Models.Elements
{
    /// <summary>
    /// Đại diện cho một đường lưới trục được đọc từ file DWG.
    /// </summary>
    public class GridModel
    {
        /// <summary>Tên trục (A, B, C... hoặc 1, 2, 3...)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Tên layer gốc trong file DWG</summary>
        public string LayerName { get; set; } = string.Empty;

        /// <summary>Điểm đầu của đường trục (đơn vị mm)</summary>
        public Point2D StartPoint { get; set; } = new();

        /// <summary>Điểm cuối của đường trục (đơn vị mm)</summary>
        public Point2D EndPoint { get; set; } = new();

        /// <summary>True = trục dọc (vertical), False = trục ngang (horizontal)</summary>
        public bool IsVertical { get; set; }

        /// <summary>Độ dài đường trục (mm)</summary>
        public double Length =>
            Math.Sqrt(Math.Pow(EndPoint.X - StartPoint.X, 2) +
                      Math.Pow(EndPoint.Y - StartPoint.Y, 2));

        /// <summary>Tọa độ trung điểm — dùng để đặt Grid trong Revit</summary>
        public Point2D MidPoint => new()
        {
            X = (StartPoint.X + EndPoint.X) / 2,
            Y = (StartPoint.Y + EndPoint.Y) / 2
        };

        public override string ToString() =>
            $"Grid [{Name}] {(IsVertical ? "Dọc" : "Ngang")} " +
            $"({StartPoint.X:F0}, {StartPoint.Y:F0}) → ({EndPoint.X:F0}, {EndPoint.Y:F0})";
    }

    /// <summary>Tọa độ 2D đơn giản (không phụ thuộc thư viện ngoài)</summary>
    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D() { }
        public Point2D(double x, double y) { X = x; Y = y; }

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}
