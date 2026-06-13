using Autodesk.Revit.DB;
using AutoCADToRevitApplication.Models.Elements;
using AutoCADToRevitApplication.Models.Results;

namespace AutoCADToRevitApplication.Services.Creation
{
    public class SlabCreationService
    {
        private readonly Document _doc;
        private const double DuplicateToleranceMm = 50.0;
        private const double MinLoopAreaMm2 = 100000.0;
        private const string GeneratedSlabMarker = "AutoCADToRevitApplication";

        public SlabCreationService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public SlabCreationResult CreateSlabs(
            IReadOnlyCollection<SlabModel> slabModels,
            IReadOnlyCollection<GridModel> gridModels,
            string slabLevelName,
            double zOffsetMm)
        {
            return CreateSlabs(
                slabModels,
                gridModels,
                GetSlabLevel(slabLevelName),
                zOffsetMm,
                true);
        }

        public SlabCreationResult CreateSlabs(
            IReadOnlyCollection<SlabModel> slabModels,
            IReadOnlyCollection<GridModel> gridModels,
            Level? level,
            double zOffsetMm,
            bool deleteExistingGenerated)
        {
            var result = new SlabCreationResult();

            if (slabModels.Count == 0)
            {
                result.Messages.Add("Chua co du lieu san de ve.");
                return result;
            }

            if (gridModels.Count == 0)
            {
                result.Messages.Add("Can co du lieu luoi truc de dat san dung toa do CAD.");
                return result;
            }

            if (level == null)
            {
                result.Messages.Add("Khong xac dinh duoc Level dat san.");
                return result;
            }

            var baseType = FindBaseFloorType();
            if (baseType == null)
            {
                result.Messages.Add("Khong tim thay Floor Type de tao san. Vui long load san truoc khi ve.");
                return result;
            }

            var placement = SlabPlacement.From(gridModels);
            var createdKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingKeys = GetExistingSlabKeys();
            var zOffset = MmToFeet(zOffsetMm);

            using var transaction = new Transaction(_doc, "Create slabs from CAD");
            transaction.Start();

            if (deleteExistingGenerated)
                DeleteGeneratedSlabs();

            foreach (var slabModel in slabModels)
            {
                try
                {
                    if (slabModel.OuterLoop.Count < 3 || slabModel.Area < MinLoopAreaMm2)
                    {
                        result.Skipped++;
                        result.Messages.Add($"Bo qua san khong hop le tai {slabModel.CenterPoint}.");
                        continue;
                    }

                    var floorType = GetOrCreateFloorType(baseType, slabModel.Thickness);
                    if (floorType == null)
                    {
                        result.Failed++;
                        result.Messages.Add($"Khong tao duoc Floor Type day {slabModel.Thickness:F0}mm.");
                        continue;
                    }

                    var profile = BuildProfile(slabModel, placement, level.Elevation);
                    if (profile.Count == 0)
                    {
                        result.Skipped++;
                        result.Messages.Add($"Bo qua san khong tao duoc duong bao tai {slabModel.CenterPoint}.");
                        continue;
                    }

                    var key = GetSlabKey(slabModel, placement, level, floorType);
                    if (existingKeys.Contains(key) || createdKeys.Contains(key))
                    {
                        result.Skipped++;
                        result.Messages.Add($"Bo qua san trung tai {slabModel.CenterPoint}.");
                        continue;
                    }

                    var floor = Floor.Create(_doc, profile, floorType.Id, level.Id);
                    MarkGeneratedSlab(floor);
                    SetSlabOffset(floor, zOffset);

                    createdKeys.Add(key);
                    result.Created++;
                    result.CreatedElementIds.Add(floor.Id);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Messages.Add($"Khong tao duoc san day {slabModel.Thickness:F0}mm: {ex.Message}");
                }
            }

            transaction.Commit();
            return result;
        }

        private Level? GetSlabLevel(string slabLevelName)
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            var selected = levels.FirstOrDefault(l =>
                string.Equals(l.Name, slabLevelName, StringComparison.OrdinalIgnoreCase));

            if (selected != null)
                return selected;

            var levelOne = levels.FirstOrDefault(l =>
                string.Equals(l.Name, "Level 1", StringComparison.OrdinalIgnoreCase));

            if (levelOne != null)
                return levelOne;

            return levels
                .OrderBy(l => Math.Abs(l.Elevation))
                .FirstOrDefault();
        }

        private FloorType? FindBaseFloorType()
        {
            var genericType = new FilteredElementCollector(_doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(t =>
                    !t.IsFoundationSlab &&
                    t.Name.Contains("Generic", StringComparison.OrdinalIgnoreCase));

            if (genericType != null)
                return genericType;

            var defaultTypeId = Floor.GetDefaultFloorType(_doc, false);
            var defaultType = _doc.GetElement(defaultTypeId) as FloorType;
            if (defaultType != null)
                return defaultType;

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(t => !t.IsFoundationSlab);
        }

        private FloorType? GetOrCreateFloorType(FloorType baseType, double thicknessMm)
        {
            var typeName = GetFloorTypeName(thicknessMm);
            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            var duplicate = baseType.Duplicate(typeName) as FloorType;
            if (duplicate == null)
                return null;

            TrySetFloorThickness(duplicate, thicknessMm);
            return duplicate;
        }

        private static string GetFloorTypeName(double thicknessMm)
            => $"CAD_SLAB_{Math.Round(thicknessMm):F0}mm";

        private static void TrySetFloorThickness(FloorType floorType, double thicknessMm)
        {
            if (TrySetDoubleParameter(floorType, BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM, MmToFeet(thicknessMm)))
                return;

            var structure = floorType.GetCompoundStructure();
            if (structure == null)
                return;

            var layerIndex = structure.GetFirstCoreLayerIndex();
            if (layerIndex < 0 && structure.LayerCount > 0)
                layerIndex = 0;

            if (layerIndex < 0)
                return;

            structure.SetLayerWidth(layerIndex, MmToFeet(thicknessMm));
            floorType.SetCompoundStructure(structure);
        }

        private static List<CurveLoop> BuildProfile(
            SlabModel slabModel,
            SlabPlacement placement,
            double elevation)
        {
            var profile = new List<CurveLoop>();
            var outer = BuildCurveLoop(slabModel.OuterLoop, placement, elevation);
            if (outer == null)
                return profile;

            profile.Add(outer);

            foreach (var opening in slabModel.OpeningLoops)
            {
                var openingLoop = BuildCurveLoop(opening, placement, elevation);
                if (openingLoop != null)
                    profile.Add(openingLoop);
            }

            return profile;
        }

        private static CurveLoop? BuildCurveLoop(
            IReadOnlyList<Point2D> points,
            SlabPlacement placement,
            double elevation)
        {
            if (points.Count < 3)
                return null;

            var curveLoop = new CurveLoop();

            for (int i = 0; i < points.Count; i++)
            {
                var start = placement.ToRevitPoint(points[i], elevation);
                var end = placement.ToRevitPoint(points[(i + 1) % points.Count], elevation);

                if (start.DistanceTo(end) <= MmToFeet(1.0))
                    continue;

                curveLoop.Append(Line.CreateBound(start, end));
            }

            return curveLoop.IsOpen() ? null : curveLoop;
        }

        private static void SetSlabOffset(Floor floor, double zOffset)
        {
            TrySetDoubleParameter(floor, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, zOffset);
        }

        private void DeleteGeneratedSlabs()
        {
            var generatedSlabIds = new FilteredElementCollector(_doc)
                .OfClass(typeof(Floor))
                .OfCategory(BuiltInCategory.OST_Floors)
                .Cast<Floor>()
                .Where(IsGeneratedSlab)
                .Select(f => f.Id)
                .ToList();

            if (generatedSlabIds.Count > 0)
                _doc.Delete(generatedSlabIds);
        }

        private static bool IsGeneratedSlab(Floor floor)
        {
            var comments = floor.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            return string.Equals(
                comments?.AsString(),
                GeneratedSlabMarker,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void MarkGeneratedSlab(Floor floor)
        {
            var comments = floor.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments == null || comments.IsReadOnly) return;

            comments.Set(GeneratedSlabMarker);
        }

        private HashSet<string> GetExistingSlabKeys()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Floor))
                .OfCategory(BuiltInCategory.OST_Floors)
                .Cast<Floor>()
                .Where(IsGeneratedSlab)
                .Select(GetExistingSlabKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string GetExistingSlabKey(Floor floor)
        {
            var box = floor.get_BoundingBox(null);
            if (box == null)
                return string.Empty;

            var thicknessMm = GetFloorThicknessMm(floor.FloorType);
            return GetBoxKey(
                floor.LevelId,
                FeetToMm(box.Min.X),
                FeetToMm(box.Min.Y),
                FeetToMm(box.Max.X),
                FeetToMm(box.Max.Y),
                thicknessMm);
        }

        private static string GetSlabKey(
            SlabModel slabModel,
            SlabPlacement placement,
            Level level,
            FloorType floorType)
        {
            var points = slabModel.OuterLoop
                .Select(point => placement.ToRevitPoint(point, level.Elevation))
                .ToList();

            return GetBoxKey(
                level.Id,
                points.Min(p => FeetToMm(p.X)),
                points.Min(p => FeetToMm(p.Y)),
                points.Max(p => FeetToMm(p.X)),
                points.Max(p => FeetToMm(p.Y)),
                GetFloorThicknessMm(floorType));
        }

        private static string GetBoxKey(
            ElementId levelId,
            double minX,
            double minY,
            double maxX,
            double maxY,
            double thicknessMm)
        {
            return $"{levelId.Value}:{RoundToTolerance(minX)}:{RoundToTolerance(minY)}:{RoundToTolerance(maxX)}:{RoundToTolerance(maxY)}:{RoundToTolerance(thicknessMm)}";
        }

        private static double GetFloorThicknessMm(FloorType floorType)
        {
            var parameter = floorType.get_Parameter(BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM);
            if (parameter != null && parameter.StorageType == StorageType.Double)
                return FeetToMm(parameter.AsDouble());

            return 0;
        }

        private static string GetPointKey(double xMm, double yMm)
            => $"{RoundToTolerance(xMm)}:{RoundToTolerance(yMm)}";

        private static bool TrySetDoubleParameter(Element element, BuiltInParameter builtInParameter, double value)
        {
            var parameter = element.get_Parameter(builtInParameter);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
                return false;

            parameter.Set(value);
            return true;
        }

        private static double RoundToTolerance(double value)
            => Math.Round(value / DuplicateToleranceMm) * DuplicateToleranceMm;

        private static double MmToFeet(double value)
            => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);

        private static double FeetToMm(double value)
            => UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters);

        private class SlabPlacement
        {
            public double CenterX { get; private set; }
            public double CenterY { get; private set; }

            public static SlabPlacement From(IReadOnlyCollection<GridModel> gridModels)
            {
                var minX = gridModels.SelectMany(g => new[] { g.StartPoint.X, g.EndPoint.X }).Min();
                var maxX = gridModels.SelectMany(g => new[] { g.StartPoint.X, g.EndPoint.X }).Max();
                var minY = gridModels.SelectMany(g => new[] { g.StartPoint.Y, g.EndPoint.Y }).Min();
                var maxY = gridModels.SelectMany(g => new[] { g.StartPoint.Y, g.EndPoint.Y }).Max();

                return new SlabPlacement
                {
                    CenterX = (minX + maxX) / 2.0,
                    CenterY = (minY + maxY) / 2.0
                };
            }

            public XYZ ToRevitPoint(Point2D point, double elevation)
            {
                return new XYZ(
                    MmToFeet(point.X - CenterX),
                    MmToFeet(point.Y - CenterY),
                    elevation);
            }
        }
    }
}
