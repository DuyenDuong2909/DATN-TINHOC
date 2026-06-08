using Autodesk.Revit.DB;
using AutoCADToRevitApplication.Models.Elements;

namespace AutoCADToRevitApplication.Services.Parsing
{
    public class ColumnReaderService
    {
        private const double PointToleranceMm = 20.0;
        private const double OrthogonalToleranceDegrees = 10.0;
        private const double MinColumnSideMm = 100.0;
        private const double MaxColumnSideMm = 3000.0;

        public List<ColumnModel> ReadColumns(
            Dictionary<string, List<GeometryObject>> geometryByLayer,
            IEnumerable<string> columnLayerNames)
        {
            var layerSet = new HashSet<string>(columnLayerNames, StringComparer.OrdinalIgnoreCase);
            if (layerSet.Count == 0) return new List<ColumnModel>();

            var columns = new List<ColumnModel>();

            foreach (var (layerName, geometryObjects) in geometryByLayer)
            {
                if (!layerSet.Contains(layerName)) continue;

                foreach (var geometryObject in geometryObjects)
                {
                    var column = ReadColumnFromGeometry(geometryObject, layerName);
                    if (column != null)
                        columns.Add(column);
                }
            }

            return Deduplicate(columns);
        }

        private static ColumnModel? ReadColumnFromGeometry(GeometryObject geometryObject, string layerName)
        {
            if (geometryObject is not PolyLine polyLine)
                return null;

            var points = ToClosedPointList(polyLine);
            if (points.Count != 4)
                return null;

            if (!TryReadRectangle(points, out var width, out var height, out var center, out var rotationDegrees))
                return null;

            return new ColumnModel
            {
                LayerName = layerName,
                Width = width,
                Height = height,
                CenterPoint = center,
                RotationDegrees = rotationDegrees
            };
        }

        private static List<Point2D> ToClosedPointList(PolyLine polyLine)
        {
            var points = polyLine.GetCoordinates()
                .Select(ToPoint2D)
                .ToList();

            if (points.Count > 1 && AreSamePoint(points[0], points[^1]))
                points.RemoveAt(points.Count - 1);

            return RemoveConsecutiveDuplicates(points);
        }

        private static bool TryReadRectangle(
            IReadOnlyList<Point2D> points,
            out double width,
            out double height,
            out Point2D center,
            out double rotationDegrees)
        {
            width = height = rotationDegrees = 0;
            center = new Point2D();

            var edges = new List<(Point2D Start, Point2D End, double Length, double Angle)>();
            for (int i = 0; i < points.Count; i++)
            {
                var start = points[i];
                var end = points[(i + 1) % points.Count];
                var length = Distance(start, end);
                if (length < MinColumnSideMm || length > MaxColumnSideMm)
                    return false;

                edges.Add((start, end, length, GetAngleDegrees(start, end)));
            }

            if (!AreOppositeSidesEqual(edges[0].Length, edges[2].Length) ||
                !AreOppositeSidesEqual(edges[1].Length, edges[3].Length))
                return false;

            for (int i = 0; i < edges.Count; i++)
            {
                var current = edges[i].Angle;
                var next = edges[(i + 1) % edges.Count].Angle;
                if (!IsRightAngle(current, next))
                    return false;
            }

            if (!TryGetAxisAlignedDimensions(edges, out width, out height, out rotationDegrees))
                return false;

            center = new Point2D(points.Average(p => p.X), points.Average(p => p.Y));
            return true;
        }

        private static bool TryGetAxisAlignedDimensions(
            IReadOnlyList<(Point2D Start, Point2D End, double Length, double Angle)> edges,
            out double width,
            out double height,
            out double rotationDegrees)
        {
            width = height = rotationDegrees = 0;

            var horizontalEdges = edges
                .Where(e => IsHorizontal(e.Angle))
                .Select(e => e.Length)
                .ToList();

            var verticalEdges = edges
                .Where(e => IsVertical(e.Angle))
                .Select(e => e.Length)
                .ToList();

            if (horizontalEdges.Count == 2 && verticalEdges.Count == 2)
            {
                width = horizontalEdges.Average();
                height = verticalEdges.Average();
                rotationDegrees = 0;
                return true;
            }

            var xSize = edges
                .Select(e => Math.Abs(e.End.X - e.Start.X))
                .Where(v => v > PointToleranceMm)
                .DefaultIfEmpty(0)
                .Max();

            var ySize = edges
                .Select(e => Math.Abs(e.End.Y - e.Start.Y))
                .Where(v => v > PointToleranceMm)
                .DefaultIfEmpty(0)
                .Max();

            if (xSize < MinColumnSideMm || ySize < MinColumnSideMm)
                return false;

            width = xSize;
            height = ySize;
            rotationDegrees = 0;
            return true;
        }

        private static List<ColumnModel> Deduplicate(List<ColumnModel> columns)
        {
            var result = new List<ColumnModel>();

            foreach (var column in columns.OrderBy(c => c.CenterPoint.X).ThenBy(c => c.CenterPoint.Y))
            {
                var duplicate = result.Any(existing =>
                    Distance(existing.CenterPoint, column.CenterPoint) <= PointToleranceMm &&
                    Math.Abs(existing.Width - column.Width) <= PointToleranceMm &&
                    Math.Abs(existing.Height - column.Height) <= PointToleranceMm);

                if (!duplicate)
                    result.Add(column);
            }

            return result;
        }

        private static List<Point2D> RemoveConsecutiveDuplicates(List<Point2D> points)
        {
            var result = new List<Point2D>();

            foreach (var point in points)
            {
                if (result.Count == 0 || !AreSamePoint(result[^1], point))
                    result.Add(point);
            }

            return result;
        }

        private static bool AreSamePoint(Point2D a, Point2D b)
            => Distance(a, b) <= PointToleranceMm;

        private static bool AreOppositeSidesEqual(double a, double b)
            => Math.Abs(a - b) <= Math.Max(PointToleranceMm, Math.Min(a, b) * 0.05);

        private static bool IsRightAngle(double angleA, double angleB)
        {
            var delta = Math.Abs(NormalizeAngle(angleA - angleB));
            delta = Math.Min(delta, 180.0 - delta);
            return Math.Abs(delta - 90.0) <= OrthogonalToleranceDegrees;
        }

        private static bool IsHorizontal(double angle)
        {
            var normalized = NormalizeAngle(angle);
            return normalized <= OrthogonalToleranceDegrees ||
                   Math.Abs(normalized - 180.0) <= OrthogonalToleranceDegrees;
        }

        private static bool IsVertical(double angle)
        {
            var normalized = NormalizeAngle(angle);
            return Math.Abs(normalized - 90.0) <= OrthogonalToleranceDegrees;
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 180.0;
            if (angle < 0) angle += 180.0;
            return angle;
        }

        private static double GetAngleDegrees(Point2D start, Point2D end)
            => Math.Atan2(end.Y - start.Y, end.X - start.X) * 180.0 / Math.PI;

        private static double Distance(Point2D start, Point2D end)
            => Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));

        private static Point2D ToPoint2D(XYZ point)
            => new(
                UnitUtils.ConvertFromInternalUnits(point.X, UnitTypeId.Millimeters),
                UnitUtils.ConvertFromInternalUnits(point.Y, UnitTypeId.Millimeters));
    }
}
