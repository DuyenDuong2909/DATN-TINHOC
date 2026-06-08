using Autodesk.Revit.DB;
using AutoCADToRevitApplication.Models.Elements;
using AutoCADToRevitApplication.Models.Mapping;

namespace AutoCADToRevitApplication.Services.Parsing
{
    public class RevitDwgReaderService
    {
        private readonly Document _doc;

        private const double AxisToleranceDegrees = 10.0;
        private const double MinGridLengthMm = 300.0;

        public RevitDwgReaderService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public Element? FindDwgInstance()
        {
            var imported = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .FirstOrDefault();

            if (imported != null) return imported;

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault();
        }

        public List<(string Name, Element Instance)> GetAllDwgInstances()
        {
            var result = new List<(string, Element)>();

            var imports = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Select(i => (i.Category?.Name ?? "DWG Import", (Element)i));

            var links = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Select(l => (l.Name ?? "DWG Link", (Element)l));

            result.AddRange(imports);
            result.AddRange(links);
            return result;
        }

        public List<DwgLayer> GetLayersFromInstance(Element dwgInstance)
        {
            var geometryByLayer = GetGeometryByLayer(dwgInstance);
            var result = new List<DwgLayer>();
            var category = dwgInstance.Category;

            if (category?.SubCategories != null)
            {
                foreach (Category sub in category.SubCategories)
                {
                    geometryByLayer.TryGetValue(sub.Name, out var geometry);
                    result.Add(new DwgLayer
                    {
                        LayerName = sub.Name,
                        EntityCount = geometry?.Count ?? 0
                    });
                }
            }

            foreach (var layer in geometryByLayer.Keys)
            {
                if (result.Any(l => string.Equals(l.LayerName, layer, StringComparison.OrdinalIgnoreCase)))
                    continue;

                result.Add(new DwgLayer
                {
                    LayerName = layer,
                    EntityCount = geometryByLayer[layer].Count
                });
            }

            return result
                .OrderByDescending(l => l.EntityCount)
                .ThenBy(l => l.LayerName)
                .ToList();
        }

        public Dictionary<string, List<GeometryObject>> GetGeometryByLayer(Element dwgInstance)
        {
            var result = new Dictionary<string, List<GeometryObject>>(StringComparer.OrdinalIgnoreCase);
            var options = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = true,
                ComputeReferences = false
            };

            var geomElem = dwgInstance.get_Geometry(options);
            if (geomElem == null) return result;

            ExtractGeometry(geomElem, result);
            return result;
        }

        public List<GridModel> ReadGridLines(
            Element dwgInstance,
            IEnumerable<string> gridLayerNames,
            GridNamingOptions namingOptions)
        {
            var layerSet = new HashSet<string>(gridLayerNames, StringComparer.OrdinalIgnoreCase);
            if (layerSet.Count == 0) return new List<GridModel>();

            var geometryByLayer = GetGeometryByLayer(dwgInstance);
            var grids = new List<GridModel>();

            foreach (var (layerName, geometryObjects) in geometryByLayer)
            {
                if (!layerSet.Contains(layerName)) continue;

                foreach (var geometryObject in geometryObjects)
                {
                    grids.AddRange(ReadGridFromGeometry(geometryObject, layerName));
                }
            }

            return NameAndSortGrids(grids, namingOptions);
        }

        private void ExtractGeometry(
            GeometryElement geomElem,
            Dictionary<string, List<GeometryObject>> result)
        {
            foreach (GeometryObject obj in geomElem)
            {
                switch (obj)
                {
                    case GeometryInstance gi:
                        var instanceGeom = gi.GetInstanceGeometry();
                        if (instanceGeom != null)
                            ExtractGeometry(instanceGeom, result);
                        break;

                    case Curve curve:
                        AddToLayer(result, GetLayerName(curve), curve);
                        break;

                    case PolyLine poly:
                        AddToLayer(result, GetLayerName(poly), poly);
                        break;
                }
            }
        }

        private IEnumerable<GridModel> ReadGridFromGeometry(GeometryObject geometryObject, string layerName)
        {
            if (geometryObject is Line line)
            {
                var grid = CreateGrid(line.GetEndPoint(0), line.GetEndPoint(1), layerName);
                if (grid != null) yield return grid;
                yield break;
            }

            if (geometryObject is PolyLine polyLine)
            {
                var points = polyLine.GetCoordinates();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var grid = CreateGrid(points[i], points[i + 1], layerName);
                    if (grid != null) yield return grid;
                }
            }
        }

        private GridModel? CreateGrid(XYZ start, XYZ end, string layerName)
        {
            if (!TryGetAxisDirection(start, end, out bool isVertical)) return null;

            var startPoint = ToPoint2D(start);
            var endPoint = ToPoint2D(end);
            var length = Distance(startPoint, endPoint);

            if (length < MinGridLengthMm) return null;

            return new GridModel
            {
                LayerName = layerName,
                StartPoint = startPoint,
                EndPoint = endPoint,
                IsVertical = isVertical
            };
        }

        private List<GridModel> NameAndSortGrids(List<GridModel> grids, GridNamingOptions namingOptions)
        {
            var xGrids = grids
                .Where(g => g.IsVertical)
                .OrderBy(g => namingOptions.XLeftToRight ? g.MidPoint.X : -g.MidPoint.X)
                .ToList();

            var yGrids = grids
                .Where(g => !g.IsVertical)
                .OrderBy(g => namingOptions.YBottomToTop ? g.MidPoint.Y : -g.MidPoint.Y)
                .ToList();

            ApplyNames(xGrids, namingOptions.XStartName);
            ApplyNames(yGrids, namingOptions.YStartName);

            return xGrids.Concat(yGrids).ToList();
        }

        private static void ApplyNames(List<GridModel> grids, string startName)
        {
            var name = GridNameSequence.FromStartName(startName);

            foreach (var grid in grids)
            {
                grid.Name = name.Current;
                name.MoveNext();
            }
        }

        private string GetLayerName(GeometryObject obj)
        {
            if (obj.GraphicsStyleId == ElementId.InvalidElementId)
                return "0";

            var graphicsStyle = _doc.GetElement(obj.GraphicsStyleId) as GraphicsStyle;
            return graphicsStyle?.GraphicsStyleCategory?.Name ?? "0";
        }

        private static void AddToLayer(
            Dictionary<string, List<GeometryObject>> dict,
            string layerKey,
            GeometryObject obj)
        {
            if (!dict.ContainsKey(layerKey))
                dict[layerKey] = new List<GeometryObject>();

            dict[layerKey].Add(obj);
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

        private static Point2D ToPoint2D(XYZ point)
            => new(
                UnitUtils.ConvertFromInternalUnits(point.X, UnitTypeId.Millimeters),
                UnitUtils.ConvertFromInternalUnits(point.Y, UnitTypeId.Millimeters));

        private static double Distance(Point2D start, Point2D end)
            => Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
    }

    public class GridNamingOptions
    {
        public string XStartName { get; set; } = "A";
        public string YStartName { get; set; } = "1";
        public bool XLeftToRight { get; set; } = true;
        public bool YBottomToTop { get; set; } = true;
    }

    internal class GridNameSequence
    {
        private readonly bool _isNumeric;
        private int _number;
        private int _letters;

        private GridNameSequence(string startName)
        {
            startName = string.IsNullOrWhiteSpace(startName) ? "1" : startName.Trim().ToUpperInvariant();

            if (int.TryParse(startName, out _number))
            {
                _isNumeric = true;
                Current = _number.ToString();
                return;
            }

            _isNumeric = false;
            _letters = LettersToNumber(startName);
            Current = NumberToLetters(_letters);
        }

        public string Current { get; private set; }

        public static GridNameSequence FromStartName(string startName) => new(startName);

        public void MoveNext()
        {
            if (_isNumeric)
            {
                _number++;
                Current = _number.ToString();
                return;
            }

            _letters++;
            Current = NumberToLetters(_letters);
        }

        private static int LettersToNumber(string value)
        {
            var result = 0;
            foreach (char c in value.Where(char.IsLetter))
            {
                result = result * 26 + (c - 'A' + 1);
            }

            return Math.Max(result, 1);
        }

        private static string NumberToLetters(int value)
        {
            var chars = new Stack<char>();

            while (value > 0)
            {
                value--;
                chars.Push((char)('A' + value % 26));
                value /= 26;
            }

            return new string(chars.ToArray());
        }
    }
}
