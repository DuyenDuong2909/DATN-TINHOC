using System.Reflection;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using AutoCADToRevitApplication.Models.Elements;

namespace AutoCADToRevitApplication.Services.Parsing
{
    public class BeamReaderService
    {
        private const double MinBeamLengthMm = 300.0;
        private const double MinBoundaryDistanceMm = 100.0;
        private const double MaxBoundaryDistanceMm = 2000.0;
        private const double AxisToleranceDegrees = 10.0;
        private const double DuplicateToleranceMm = 20.0;
        private const double BeamWidthToleranceMm = 10.0;

        public List<BeamModel> ReadBeams(
            Dictionary<string, List<GeometryObject>> geometryByLayer,
            IEnumerable<string> beamLayerNames,
            IEnumerable<string> axisLayerNames,
            double inputBeamHeight)
        {
            var layerSet = new HashSet<string>(beamLayerNames, StringComparer.OrdinalIgnoreCase);
            if (layerSet.Count == 0) return new List<BeamModel>();

            var beamSegments = new List<BeamSegment>();
            var axisLayerSet = new HashSet<string>(axisLayerNames, StringComparer.OrdinalIgnoreCase);
            var axisSegments = new List<BeamSegment>();

            foreach (var (layerName, geometryObjects) in geometryByLayer)
            {
                foreach (var geometryObject in geometryObjects)
                {
                    if (layerSet.Contains(layerName))
                        beamSegments.AddRange(ReadBeamSegments(geometryObject, layerName));

                    if (axisLayerSet.Contains(layerName))
                        axisSegments.AddRange(ReadBeamSegments(geometryObject, layerName));
                }
            }

            var candidates = DetectBeamsFromBoundariesAndAxes(
                    beamSegments,
                    axisSegments,
                    inputBeamHeight)
                .ToList();

            return Deduplicate(candidates);
        }

        private static IEnumerable<BeamModel> DetectBeamsFromBoundariesAndAxes(
            List<BeamSegment> boundarySegments,
            List<BeamSegment> axisSegments,
            double inputBeamHeight)
        {
            if (axisSegments.Count == 0)
                yield break;

            for (int i = 0; i < boundarySegments.Count; i++)
            {
                for (int j = i + 1; j < boundarySegments.Count; j++)
                {
                    var first = boundarySegments[i];
                    var second = boundarySegments[j];

                    if (!AreParallel(first.AngleDegrees, second.AngleDegrees))
                        continue;

                    if (!TryGetParallelDistance(first, second, out var boundaryDistance) ||
                        boundaryDistance < MinBoundaryDistanceMm ||
                        boundaryDistance > MaxBoundaryDistanceMm)
                        continue;

                    if (!HasEnoughOverlap(first, second))
                        continue;

                    var centerLine = CreateCenterLine(first, second);
                    var axis = FindBestMiddleAxis(
                        centerLine,
                        first,
                        second,
                        axisSegments,
                        boundaryDistance);
                    if (axis == null)
                        continue;

                    yield return new BeamModel
                    {
                        LayerName = first.LayerName,
                        StartPoint = centerLine.Start,
                        EndPoint = centerLine.End,
                        CenterPoint = centerLine.Center,
                        RotationDegrees = centerLine.AngleDegrees,
                        Width = boundaryDistance,
                        Height = inputBeamHeight,
                        SourceType = "BoundaryPairWithAxis"
                    };
                }
            }
        }

        private static BeamSegment? FindBestMiddleAxis(
            BeamSegment centerLine,
            BeamSegment firstBoundary,
            BeamSegment secondBoundary,
            List<BeamSegment> axisSegments,
            double boundaryDistance)
        {
            return axisSegments
                .Where(axis => AreParallel(axis.AngleDegrees, centerLine.AngleDegrees))
                .Select(axis => new
                {
                    Axis = axis,
                    CenterDistance = GetParallelDistanceOrMax(centerLine, axis),
                    FirstDistance = GetParallelDistanceOrMax(firstBoundary, axis),
                    SecondDistance = GetParallelDistanceOrMax(secondBoundary, axis),
                    Overlap = GetOverlapLength(axis, centerLine)
                })
                .Where(x => x.Overlap >= Math.Min(x.Axis.Length, centerLine.Length) * 0.3)
                .Where(x => Math.Abs(x.FirstDistance - boundaryDistance / 2.0) <= BeamWidthToleranceMm)
                .Where(x => Math.Abs(x.SecondDistance - boundaryDistance / 2.0) <= BeamWidthToleranceMm)
                .Where(x => x.CenterDistance <= BeamWidthToleranceMm)
                .OrderBy(x => x.CenterDistance)
                .ThenBy(x => Math.Abs(x.FirstDistance - x.SecondDistance))
                .Select(x => x.Axis)
                .FirstOrDefault();
        }

        private static double GetParallelDistanceOrMax(BeamSegment first, BeamSegment second)
            => TryGetParallelDistance(first, second, out var distance) ? distance : double.MaxValue;

        private static IEnumerable<BeamSegment> ReadBeamSegments(GeometryObject geometryObject, string layerName)
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

        private static BeamSegment? CreateSegment(XYZ start, XYZ end, string layerName)
        {
            if (!TryGetAxisDirection(start, end, out _)) return null;

            var startPoint = ToPoint2D(start);
            var endPoint = ToPoint2D(end);
            var length = Distance(startPoint, endPoint);

            if (length < MinBeamLengthMm) return null;

            return new BeamSegment(layerName, startPoint, endPoint);
        }

        private static BeamSegment CreateCenterLine(BeamSegment first, BeamSegment second)
        {
            var firstOrdered = OrderAlongMainAxis(first);
            var secondOrdered = OrderAlongMainAxis(second);

            var start = new Point2D(
                (firstOrdered.Start.X + secondOrdered.Start.X) / 2.0,
                (firstOrdered.Start.Y + secondOrdered.Start.Y) / 2.0);

            var end = new Point2D(
                (firstOrdered.End.X + secondOrdered.End.X) / 2.0,
                (firstOrdered.End.Y + secondOrdered.End.Y) / 2.0);

            return new BeamSegment(first.LayerName, start, end);
        }

        private static List<BeamModel> Deduplicate(List<BeamModel> beams)
        {
            var result = new List<BeamModel>();

            foreach (var beam in beams.OrderByDescending(b => b.DimensionText.Length)
                                      .ThenBy(b => b.CenterPoint.X)
                                      .ThenBy(b => b.CenterPoint.Y))
            {
                var duplicate = result.Any(existing =>
                    AreSameLine(existing.StartPoint, existing.EndPoint, beam.StartPoint, beam.EndPoint));

                if (!duplicate)
                    result.Add(beam);
            }

            return result;
        }

        private static bool AreSameLine(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
        {
            return (Distance(a1, b1) <= DuplicateToleranceMm && Distance(a2, b2) <= DuplicateToleranceMm) ||
                   (Distance(a1, b2) <= DuplicateToleranceMm && Distance(a2, b1) <= DuplicateToleranceMm);
        }

        private static bool TryGetAxisDirection(XYZ start, XYZ end, out bool isVertical)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var angle = Math.Abs(Math.Atan2(dy, dx) * 180.0 / Math.PI);

            if (angle <= AxisToleranceDegrees || Math.Abs(angle - 180) <= AxisToleranceDegrees)
            {
                isVertical = false;
                return true;
            }

            if (Math.Abs(angle - 90) <= AxisToleranceDegrees)
            {
                isVertical = true;
                return true;
            }

            isVertical = false;
            return false;
        }

        private static bool AreParallel(double angleA, double angleB)
        {
            var delta = Math.Abs(NormalizeAngle(angleA - angleB));
            return delta <= AxisToleranceDegrees || Math.Abs(delta - 180.0) <= AxisToleranceDegrees;
        }

        private static bool TryGetParallelDistance(
            BeamSegment first,
            BeamSegment second,
            out double distance)
        {
            var length = first.Length;
            distance = 0;

            if (length <= 0) return false;

            var dx = (first.End.X - first.Start.X) / length;
            var dy = (first.End.Y - first.Start.Y) / length;
            var normalX = -dy;
            var normalY = dx;

            distance = Math.Abs((second.Start.X - first.Start.X) * normalX +
                                (second.Start.Y - first.Start.Y) * normalY);
            return true;
        }

        private static bool HasEnoughOverlap(BeamSegment first, BeamSegment second)
        {
            var overlap = GetOverlapLength(first, second);
            return overlap >= Math.Min(first.Length, second.Length) * 0.5;
        }

        private static double GetOverlapLength(BeamSegment first, BeamSegment second)
        {
            var a = ProjectRange(first, first);
            var b = ProjectRange(second, first);
            return Math.Max(0, Math.Min(a.Max, b.Max) - Math.Max(a.Min, b.Min));
        }

        private static (double Min, double Max) ProjectRange(BeamSegment segment, BeamSegment axis)
        {
            var length = axis.Length;
            var dx = (axis.End.X - axis.Start.X) / length;
            var dy = (axis.End.Y - axis.Start.Y) / length;

            var start = (segment.Start.X - axis.Start.X) * dx + (segment.Start.Y - axis.Start.Y) * dy;
            var end = (segment.End.X - axis.Start.X) * dx + (segment.End.Y - axis.Start.Y) * dy;

            return (Math.Min(start, end), Math.Max(start, end));
        }

        private static BeamSegment OrderAlongMainAxis(BeamSegment segment)
        {
            if (Math.Abs(segment.End.X - segment.Start.X) >= Math.Abs(segment.End.Y - segment.Start.Y))
                return segment.Start.X <= segment.End.X
                    ? segment
                    : new BeamSegment(segment.LayerName, segment.End, segment.Start);

            return segment.Start.Y <= segment.End.Y
                ? segment
                : new BeamSegment(segment.LayerName, segment.End, segment.Start);
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 180.0;
            if (angle < 0) angle += 180.0;
            return angle;
        }

        private static Point2D ToPoint2D(XYZ point)
            => new(
                UnitUtils.ConvertFromInternalUnits(point.X, UnitTypeId.Millimeters),
                UnitUtils.ConvertFromInternalUnits(point.Y, UnitTypeId.Millimeters));

        private static double Distance(Point2D start, Point2D end)
            => Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));

        private record BeamSegment(string LayerName, Point2D Start, Point2D End)
        {
            public Point2D Center => new((Start.X + End.X) / 2.0, (Start.Y + End.Y) / 2.0);
            public double Length => Distance(Start, End);
            public double AngleDegrees => Math.Atan2(End.Y - Start.Y, End.X - Start.X) * 180.0 / Math.PI;
        }

        private record BeamDimensionNote(string Text, Point2D Location, double Width, double Height);
    }
}
