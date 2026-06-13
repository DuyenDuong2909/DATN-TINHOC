using Autodesk.Revit.DB;
using AutoCADToRevitApplication.Models.Elements;

namespace AutoCADToRevitApplication.Services.Parsing
{
    public class SlabReaderService
    {
        private const double PointToleranceMm = 20.0;
        private const double MinSegmentLengthMm = 100.0;
        private const double MinLoopAreaMm2 = 100000.0;

        public List<SlabModel> ReadSlabs(
            Dictionary<string, List<GeometryObject>> geometryByLayer,
            IEnumerable<string> slabLayerNames,
            double thicknessMm)
        {
            var layerSet = new HashSet<string>(slabLayerNames, StringComparer.OrdinalIgnoreCase);
            if (layerSet.Count == 0) return new List<SlabModel>();

            var allLoops = new List<SlabLoop>();

            foreach (var (layerName, geometryObjects) in geometryByLayer)
            {
                if (!layerSet.Contains(layerName)) continue;

                var segments = new List<SlabSegment>();

                foreach (var geometryObject in geometryObjects)
                {
                    allLoops.AddRange(ReadClosedPolylineLoops(geometryObject, layerName));
                    segments.AddRange(ReadSegments(geometryObject, layerName));
                }

                allLoops.AddRange(BuildLoopsFromSegments(segments, layerName));
            }

            return BuildSlabsFromLoops(DeduplicateLoops(allLoops), thicknessMm);
        }

        private static IEnumerable<SlabLoop> ReadClosedPolylineLoops(
            GeometryObject geometryObject,
            string layerName)
        {
            if (geometryObject is not PolyLine polyLine)
                yield break;

            var points = NormalizeLoop(polyLine.GetCoordinates().Select(ToPoint2D).ToList());
            if (!IsClosed(points) || points.Count < 4)
                yield break;

            points.RemoveAt(points.Count - 1);

            var area = Math.Abs(GetSignedArea(points));
            if (area < MinLoopAreaMm2)
                yield break;

            yield return new SlabLoop(layerName, points, area, GetCentroid(points));
        }

        private static IEnumerable<SlabSegment> ReadSegments(
            GeometryObject geometryObject,
            string layerName)
        {
            if (geometryObject is Line line)
            {
                var segment = CreateSegment(line.GetEndPoint(0), line.GetEndPoint(1), layerName);
                if (segment != null) yield return segment;
                yield break;
            }

            if (geometryObject is PolyLine polyLine)
            {
                var points = polyLine.GetCoordinates();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var segment = CreateSegment(points[i], points[i + 1], layerName);
                    if (segment != null) yield return segment;
                }
            }
        }

        private static SlabSegment? CreateSegment(XYZ start, XYZ end, string layerName)
        {
            var startPoint = ToPoint2D(start);
            var endPoint = ToPoint2D(end);
            if (Distance(startPoint, endPoint) < MinSegmentLengthMm)
                return null;

            return new SlabSegment(layerName, startPoint, endPoint);
        }

        private static List<SlabLoop> BuildLoopsFromSegments(List<SlabSegment> segments, string layerName)
        {
            var loops = new List<SlabLoop>();
            var unused = segments.ToList();

            while (unused.Count > 0)
            {
                var first = unused[0];
                unused.RemoveAt(0);

                var points = new List<Point2D> { first.Start, first.End };
                var current = first.End;

                while (!AreSamePoint(current, points[0]))
                {
                    var nextIndex = unused.FindIndex(s =>
                        AreSamePoint(s.Start, current) || AreSamePoint(s.End, current));

                    if (nextIndex < 0)
                        break;

                    var next = unused[nextIndex];
                    unused.RemoveAt(nextIndex);

                    current = AreSamePoint(next.Start, current) ? next.End : next.Start;
                    points.Add(current);

                    if (points.Count > segments.Count + 1)
                        break;
                }

                if (!IsClosed(points) || points.Count < 4)
                    continue;

                points = NormalizeLoop(points);
                points.RemoveAt(points.Count - 1);

                var area = Math.Abs(GetSignedArea(points));
                if (area < MinLoopAreaMm2)
                    continue;

                loops.Add(new SlabLoop(layerName, points, area, GetCentroid(points)));
            }

            return loops;
        }

        private static List<SlabModel> BuildSlabsFromLoops(List<SlabLoop> loops, double thicknessMm)
        {
            var orderedLoops = loops
                .OrderByDescending(l => l.Area)
                .ToList();

            var slabs = new List<SlabModel>();
            var usedAsOpening = new HashSet<SlabLoop>();

            foreach (var outer in orderedLoops)
            {
                if (usedAsOpening.Contains(outer))
                    continue;

                var openings = orderedLoops
                    .Where(candidate => !ReferenceEquals(candidate, outer))
                    .Where(candidate => !usedAsOpening.Contains(candidate))
                    .Where(candidate => candidate.Area < outer.Area)
                    .Where(candidate => IsPointInsidePolygon(candidate.CenterPoint, outer.Points))
                    .ToList();

                foreach (var opening in openings)
                    usedAsOpening.Add(opening);

                slabs.Add(new SlabModel
                {
                    LayerName = outer.LayerName,
                    OuterLoop = outer.Points,
                    OpeningLoops = openings.Select(o => o.Points).ToList(),
                    Thickness = thicknessMm,
                    Area = outer.Area - openings.Sum(o => o.Area),
                    CenterPoint = outer.CenterPoint
                });
            }

            return slabs;
        }

        private static List<SlabLoop> DeduplicateLoops(List<SlabLoop> loops)
        {
            var result = new List<SlabLoop>();

            foreach (var loop in loops.OrderByDescending(l => l.Area))
            {
                var duplicate = result.Any(existing =>
                    Math.Abs(existing.Area - loop.Area) <= MinLoopAreaMm2 &&
                    Distance(existing.CenterPoint, loop.CenterPoint) <= PointToleranceMm);

                if (!duplicate)
                    result.Add(loop);
            }

            return result;
        }

        private static List<Point2D> NormalizeLoop(List<Point2D> points)
        {
            var result = new List<Point2D>();

            foreach (var point in points)
            {
                if (result.Count == 0 || !AreSamePoint(result[^1], point))
                    result.Add(point);
            }

            return result;
        }

        private static bool IsClosed(IReadOnlyList<Point2D> points)
            => points.Count > 2 && AreSamePoint(points[0], points[^1]);

        private static bool AreSamePoint(Point2D a, Point2D b)
            => Distance(a, b) <= PointToleranceMm;

        private static bool IsPointInsidePolygon(Point2D point, IReadOnlyList<Point2D> polygon)
        {
            var inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                var intersects = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                                 (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + 1e-9) + pi.X);

                if (intersects)
                    inside = !inside;
            }

            return inside;
        }

        private static double GetSignedArea(IReadOnlyList<Point2D> points)
        {
            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var current = points[i];
                var next = points[(i + 1) % points.Count];
                area += current.X * next.Y - next.X * current.Y;
            }

            return area / 2.0;
        }

        private static Point2D GetCentroid(IReadOnlyList<Point2D> points)
        {
            var signedArea = GetSignedArea(points);
            if (Math.Abs(signedArea) < 1e-9)
                return new Point2D(points.Average(p => p.X), points.Average(p => p.Y));

            double cx = 0;
            double cy = 0;

            for (int i = 0; i < points.Count; i++)
            {
                var current = points[i];
                var next = points[(i + 1) % points.Count];
                var factor = current.X * next.Y - next.X * current.Y;
                cx += (current.X + next.X) * factor;
                cy += (current.Y + next.Y) * factor;
            }

            return new Point2D(cx / (6.0 * signedArea), cy / (6.0 * signedArea));
        }

        private static Point2D ToPoint2D(XYZ point)
            => new(
                UnitUtils.ConvertFromInternalUnits(point.X, UnitTypeId.Millimeters),
                UnitUtils.ConvertFromInternalUnits(point.Y, UnitTypeId.Millimeters));

        private static double Distance(Point2D start, Point2D end)
            => Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));

        private record SlabSegment(string LayerName, Point2D Start, Point2D End);
        private record SlabLoop(string LayerName, List<Point2D> Points, double Area, Point2D CenterPoint);
    }
}
