using System.Reflection;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using AutoCADToRevitApplication.Models.Elements;

namespace AutoCADToRevitApplication.Services.Parsing
{
    public class BeamReaderService
    {
        private const double MinBeamLengthMm = 300.0;
        private const double MaxBeamWidthMm = 2000.0;
        private const double MinBeamWidthMm = 100.0;
        private const double AxisToleranceDegrees = 10.0;
        private const double DuplicateToleranceMm = 20.0;
        private const double TextSearchMaxDistanceMm = 2500.0;
        private const double BeamWidthToleranceMm = 10.0;

        public List<BeamModel> ReadBeams(
            Dictionary<string, List<GeometryObject>> geometryByLayer,
            IEnumerable<string> beamLayerNames,
            IEnumerable<string> axisLayerNames,
            double defaultWidth,
            double defaultHeight)
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
                    defaultWidth,
                    defaultHeight)
                .ToList();

            return Deduplicate(candidates);
        }

        private static IEnumerable<BeamModel> DetectBeamsFromBoundariesAndAxes(
            List<BeamSegment> boundarySegments,
            List<BeamSegment> axisSegments,
            double beamWidth,
            double beamHeight)
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

                    if (!TryGetParallelDistance(first, second, out var distance) ||
                        Math.Abs(distance - beamWidth) > BeamWidthToleranceMm)
                        continue;

                    if (!HasEnoughOverlap(first, second))
                        continue;

                    var centerLine = CreateCenterLine(first, second);
                    var axis = FindBestMiddleAxis(centerLine, first, second, axisSegments, beamWidth);
                    if (axis == null)
                        continue;

                    yield return new BeamModel
                    {
                        LayerName = first.LayerName,
                        StartPoint = centerLine.Start,
                        EndPoint = centerLine.End,
                        CenterPoint = centerLine.Center,
                        RotationDegrees = centerLine.AngleDegrees,
                        Width = beamWidth,
                        Height = beamHeight,
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
            double beamWidth)
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
                .Where(x => Math.Abs(x.FirstDistance - beamWidth / 2.0) <= BeamWidthToleranceMm)
                .Where(x => Math.Abs(x.SecondDistance - beamWidth / 2.0) <= BeamWidthToleranceMm)
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

        private static List<BeamSegment> DetectCenterLinesFromBoundaryPairs(List<BeamSegment> segments)
        {
            var result = new List<BeamSegment>();

            for (int i = 0; i < segments.Count; i++)
            {
                for (int j = i + 1; j < segments.Count; j++)
                {
                    var first = segments[i];
                    var second = segments[j];

                    if (!string.Equals(first.LayerName, second.LayerName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!AreParallel(first.AngleDegrees, second.AngleDegrees))
                        continue;

                    if (!TryGetParallelDistance(first, second, out var distance) ||
                        distance < MinBeamWidthMm ||
                        distance > MaxBeamWidthMm)
                        continue;

                    if (!HasEnoughOverlap(first, second))
                        continue;

                    result.Add(CreateCenterLine(first, second));
                }
            }

            return result;
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

        private static BeamModel CreateBeam(
            BeamSegment segment,
            double defaultWidth,
            double defaultHeight,
            string sourceType)
        {
            return new BeamModel
            {
                LayerName = segment.LayerName,
                StartPoint = segment.Start,
                EndPoint = segment.End,
                CenterPoint = segment.Center,
                RotationDegrees = segment.AngleDegrees,
                Width = defaultWidth,
                Height = defaultHeight,
                SourceType = sourceType
            };
        }

        private static List<BeamDimensionNote> ReadDimensionNotes(
            Dictionary<string, List<GeometryObject>> geometryByLayer)
        {
            var notes = new List<BeamDimensionNote>();

            foreach (var (_, geometryObjects) in geometryByLayer)
            {
                foreach (var geometryObject in geometryObjects)
                {
                    var text = TryGetText(geometryObject);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    if (!TryParseDimension(text, out var width, out var height)) continue;

                    var center = TryGetGeometryCenter(geometryObject);
                    if (center == null) continue;

                    notes.Add(new BeamDimensionNote(text.Trim(), center, width, height));
                }
            }

            return notes;
        }

        private static List<BeamModel> CreateBeamsFromDimensionNotes(
            List<BeamModel> candidates,
            List<BeamDimensionNote> notes)
        {
            var result = new List<BeamModel>();

            foreach (var note in notes)
            {
                var nearest = candidates
                    .Select(beam => new
                    {
                        Beam = beam,
                        Distance = DistancePointToSegment(note.Location, beam.StartPoint, beam.EndPoint),
                        MiddleDistance = Distance(note.Location, beam.CenterPoint)
                    })
                    .Where(x => x.Distance <= TextSearchMaxDistanceMm)
                    .OrderBy(x => x.Distance)
                    .ThenBy(x => x.MiddleDistance)
                    .FirstOrDefault();

                if (nearest == null) continue;

                result.Add(new BeamModel
                {
                    LayerName = nearest.Beam.LayerName,
                    StartPoint = nearest.Beam.StartPoint,
                    EndPoint = nearest.Beam.EndPoint,
                    CenterPoint = nearest.Beam.CenterPoint,
                    RotationDegrees = nearest.Beam.RotationDegrees,
                    SourceType = nearest.Beam.SourceType,
                    Width = note.Width,
                    Height = note.Height,
                    DimensionText = note.Text
                });
            }

            return result;
        }

        private static bool TryParseDimension(string text, out double width, out double height)
        {
            width = height = 0;

            var normalized = text
                .Replace(" ", string.Empty)
                .Replace("X", "x")
                .Replace("*", "x");

            var match = Regex.Match(
                normalized,
                @"(?:B|b)?(?<b>\d+(?:[.,]\d+)?)(?:x|X)(?:H|h)?(?<h>\d+(?:[.,]\d+)?)");

            if (!match.Success) return false;

            width = ParseNumber(match.Groups["b"].Value);
            height = ParseNumber(match.Groups["h"].Value);
            return width > 0 && height > 0;
        }

        private static double ParseNumber(string value)
            => double.Parse(value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);

        private static string? TryGetText(GeometryObject geometryObject)
        {
            var type = geometryObject.GetType();
            foreach (var propertyName in new[] { "Text", "TextString", "Contents", "Value" })
            {
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property?.PropertyType != typeof(string)) continue;

                return property.GetValue(geometryObject) as string;
            }

            return null;
        }

        private static Point2D? TryGetGeometryCenter(GeometryObject geometryObject)
        {
            var method = geometryObject.GetType().GetMethod("GetBoundingBox", Type.EmptyTypes);
            if (method?.Invoke(geometryObject, null) is not BoundingBoxXYZ box)
                return null;

            return ToPoint2D((box.Min + box.Max) / 2.0);
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

        private static double DistancePointToSegment(Point2D point, Point2D start, Point2D end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lengthSquared = dx * dx + dy * dy;

            if (lengthSquared <= 0) return Distance(point, start);

            var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));

            var projection = new Point2D(start.X + t * dx, start.Y + t * dy);
            return Distance(point, projection);
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
