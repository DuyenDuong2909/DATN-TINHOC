using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using AutoCADToRevitApplication.Models.Elements;
using AutoCADToRevitApplication.Models.Results;

namespace AutoCADToRevitApplication.Services.Creation
{
    public class ColumnCreationService
    {
        private readonly Document _doc;
        private const double DuplicateToleranceMm = 50.0;
        private const double GridSnapToleranceMm = 20.0;
        private const double DefaultStoryHeightMm = 3000.0;
        private const string GeneratedColumnTypePrefix = "CAD_COL_";
        private const string GeneratedColumnMarker = "AutoCADToRevitApplication";
        private const string DefaultColumnFamilyName = "M_Concrete-Rectangular-Column";

        public ColumnCreationService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public ColumnCreationResult CreateColumns(
            IReadOnlyCollection<ColumnModel> columnModels,
            IReadOnlyCollection<GridModel> gridModels,
            Level? baseLevel,
            Level? topLevel,
            double fallbackStoryHeightMm,
            double baseOffsetMm,
            double topOffsetMm,
            bool deleteExistingGenerated)
        {
            var result = new ColumnCreationResult();

            if (columnModels.Count == 0)
            {
                result.Messages.Add("Chưa có dữ liệu cột để vẽ");
                return result;
            }

            if (gridModels.Count == 0)
            {
                result.Messages.Add("Cần có dữ liệu lưới trục");
                return result;
            }

            if (baseLevel == null)
            {
                result.Messages.Add("Không xác định được Level đặt cột");
                return result;
            }

            var baseSymbol = FindBaseColumnSymbol();
            if (baseSymbol == null)
            {
                result.Messages.Add($"Không tìm thấy family cột '{DefaultColumnFamilyName}'. Vui lòng chọn Family trước");
                return result;
            }

            var placement = ColumnPlacement.From(gridModels);
            var createdKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fallbackHeight = fallbackStoryHeightMm > 0 ? fallbackStoryHeightMm : DefaultStoryHeightMm;

            using var transaction = new Transaction(_doc, "Tạo cột từ file CAD");
            transaction.Start();

            if (deleteExistingGenerated)
                DeleteGeneratedColumns();

            var existingKeys = GetExistingColumnKeys();

            foreach (var columnModel in columnModels)
            {
                try
                {
                    if (!placement.TryResolveColumnPoint(columnModel.CenterPoint, out var resolvedPoint))
                    {
                        result.Failed++;
                        result.Messages.Add($"Cột tại {columnModel.CenterPoint} không nằm trên giao điểm lưới trục");
                        continue;
                    }

                    var key = GetColumnKey(resolvedPoint, placement, baseLevel);
                    if (existingKeys.Contains(key) || createdKeys.Contains(key))
                    {
                        result.Skipped++;
                        result.Messages.Add($"Bỏ qua cột tại {resolvedPoint} vì trùng tọa độ");
                        continue;
                    }

                    var symbol = GetOrCreateColumnType(baseSymbol, columnModel);
                    if (symbol == null)
                    {
                        result.Failed++;
                        result.Messages.Add($"Không tạo được Type cột {columnModel.Width:F0}x{columnModel.Height:F0}.");
                        continue;
                    }

                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        _doc.Regenerate();
                    }

                    var location = new XYZ(
                        MmToFeet(resolvedPoint.X - placement.CenterX),
                        MmToFeet(resolvedPoint.Y - placement.CenterY),
                        baseLevel.Elevation);

                    var instance = _doc.Create.NewFamilyInstance(location, symbol, baseLevel, StructuralType.Column);
                    MarkGeneratedColumn(instance);
                    SetColumnHeight(instance, baseLevel, topLevel, fallbackHeight, baseOffsetMm, topOffsetMm);
                    RotateColumn(instance.Id, location, columnModel.RotationDegrees);

                    createdKeys.Add(key);
                    result.Created++;
                    result.CreatedElementIds.Add(instance.Id);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Messages.Add($"Khong tao duoc cot {columnModel.Width:F0}x{columnModel.Height:F0}: {ex.Message}");
                }
            }

            transaction.Commit();
            return result;
        }

        private Level? GetNextLevel(Level baseLevel)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Where(l => l.Elevation > baseLevel.Elevation)
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();
        }

        private FamilySymbol? FindBaseColumnSymbol()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => string.Equals(
                    s.FamilyName,
                    DefaultColumnFamilyName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private FamilySymbol? GetOrCreateColumnType(FamilySymbol baseSymbol, ColumnModel columnModel)
        {
            var typeName = GetColumnTypeName(columnModel);
            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    string.Equals(s.FamilyName, DefaultColumnFamilyName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.Name, typeName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            var duplicate = baseSymbol.Duplicate(typeName) as FamilySymbol;
            if (duplicate == null) return null;

            return TrySetColumnDimensions(duplicate, columnModel.Width, columnModel.Height)
                ? duplicate
                : null;
        }

        private static string GetColumnTypeName(ColumnModel columnModel)
            => $"{Math.Round(columnModel.Width):F0} x {Math.Round(columnModel.Height):F0}mm";

        private static bool TrySetColumnDimensions(FamilySymbol symbol, double widthMm, double heightMm)
        {
            var widthSet = TrySetParameter(symbol, new[] { "b", "B", "Width", "WIDTH" }, widthMm);
            var heightSet = TrySetParameter(symbol, new[] { "h", "H", "Depth", "DEPTH", "Height", "HEIGHT" }, heightMm);
            return widthSet && heightSet;
        }

        private static bool TrySetParameter(Element element, IEnumerable<string> names, double valueMm)
        {
            foreach (var name in names)
            {
                var parameter = element.LookupParameter(name);
                if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
                    continue;

                parameter.Set(MmToFeet(valueMm));
                return true;
            }

            return false;
        }

        private void SetColumnHeight(
            FamilyInstance instance,
            Level baseLevel,
            Level? topLevel,
            double fallbackStoryHeightMm,
            double baseOffsetMm,
            double topOffsetMm)
        {
            var nextLevel = topLevel ?? GetNextLevel(baseLevel);
            TrySetElementIdParameter(instance, BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, baseLevel.Id);
            TrySetDoubleParameter(instance, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM, MmToFeet(baseOffsetMm));

            if (nextLevel != null)
            {
                TrySetElementIdParameter(instance, BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, nextLevel.Id);
                TrySetDoubleParameter(instance, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM, MmToFeet(topOffsetMm));
                return;
            }

            TrySetDoubleParameter(
                instance,
                BuiltInParameter.INSTANCE_LENGTH_PARAM,
                MmToFeet(fallbackStoryHeightMm + topOffsetMm - baseOffsetMm));
        }

        private void RotateColumn(ElementId elementId, XYZ location, double rotationDegrees)
        {
            var radians = rotationDegrees * Math.PI / 180.0;
            if (Math.Abs(radians) < 1e-6) return;

            var axis = Line.CreateBound(location, location + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(_doc, elementId, axis, radians);
        }

        private void DeleteGeneratedColumns()
        {
            var generatedColumnIds = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilyInstance>()
                .Where(IsGeneratedColumn)
                .Select(i => i.Id)
                .ToList();

            if (generatedColumnIds.Count > 0)
                _doc.Delete(generatedColumnIds);
        }

        private static bool IsGeneratedColumn(FamilyInstance instance)
        {
            if (instance.Symbol?.Name.StartsWith(
                    GeneratedColumnTypePrefix,
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            var comments = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            return string.Equals(
                comments?.AsString(),
                GeneratedColumnMarker,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void MarkGeneratedColumn(FamilyInstance instance)
        {
            var comments = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments == null || comments.IsReadOnly) return;

            comments.Set(GeneratedColumnMarker);
        }

        private HashSet<string> GetExistingColumnKeys()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilyInstance>()
                .Select(i => i.Location as LocationPoint)
                .Where(lp => lp != null)
                .Select(lp => GetPointKey(FeetToMm(lp!.Point.X), FeetToMm(lp.Point.Y), FeetToMm(lp.Point.Z)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string GetColumnKey(Point2D point, ColumnPlacement placement, Level baseLevel)
        {
            return GetPointKey(
                point.X - placement.CenterX,
                point.Y - placement.CenterY,
                FeetToMm(baseLevel.Elevation));
        }

        private static string GetPointKey(double xMm, double yMm, double zMm)
        {
            return $"{RoundToTolerance(xMm)}:{RoundToTolerance(yMm)}:{RoundToTolerance(zMm)}";
        }

        private static bool TrySetElementIdParameter(Element element, BuiltInParameter builtInParameter, ElementId value)
        {
            var parameter = element.get_Parameter(builtInParameter);
            if (parameter == null || parameter.IsReadOnly) return false;
            parameter.Set(value);
            return true;
        }

        private static bool TrySetDoubleParameter(Element element, BuiltInParameter builtInParameter, double value)
        {
            var parameter = element.get_Parameter(builtInParameter);
            if (parameter == null || parameter.IsReadOnly) return false;
            parameter.Set(value);
            return true;
        }

        private static double RoundToTolerance(double value)
            => Math.Round(value / DuplicateToleranceMm) * DuplicateToleranceMm;

        private static double MmToFeet(double value)
            => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);

        private static double FeetToMm(double value)
            => UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters);

        private class ColumnPlacement
        {
            public double CenterX { get; private set; }
            public double CenterY { get; private set; }
            private List<double> VerticalGridXs { get; set; } = new();
            private List<double> HorizontalGridYs { get; set; } = new();

            public static ColumnPlacement From(IReadOnlyCollection<GridModel> gridModels)
            {
                var minX = gridModels.SelectMany(g => new[] { g.StartPoint.X, g.EndPoint.X }).Min();
                var maxX = gridModels.SelectMany(g => new[] { g.StartPoint.X, g.EndPoint.X }).Max();
                var minY = gridModels.SelectMany(g => new[] { g.StartPoint.Y, g.EndPoint.Y }).Min();
                var maxY = gridModels.SelectMany(g => new[] { g.StartPoint.Y, g.EndPoint.Y }).Max();

                return new ColumnPlacement
                {
                    CenterX = (minX + maxX) / 2.0,
                    CenterY = (minY + maxY) / 2.0,
                    VerticalGridXs = gridModels
                        .Where(g => g.IsVertical)
                        .Select(g => g.MidPoint.X)
                        .OrderBy(x => x)
                        .ToList(),
                    HorizontalGridYs = gridModels
                        .Where(g => !g.IsVertical)
                        .Select(g => g.MidPoint.Y)
                        .OrderBy(y => y)
                        .ToList()
                };
            }

            public bool TryResolveColumnPoint(Point2D cadCenter, out Point2D resolvedPoint)
            {
                resolvedPoint = new Point2D();

                if (!TryFindMatchingCoordinate(VerticalGridXs, cadCenter.X, out var gridX) ||
                    !TryFindMatchingCoordinate(HorizontalGridYs, cadCenter.Y, out var gridY))
                {
                    return false;
                }

                resolvedPoint = new Point2D(gridX, gridY);
                return true;
            }

            private static bool TryFindMatchingCoordinate(
                IReadOnlyCollection<double> coordinates,
                double value,
                out double matchedValue)
            {
                matchedValue = 0;
                if (coordinates.Count == 0) return false;

                var nearest = coordinates
                    .Select(c => new { Value = c, Distance = Math.Abs(c - value) })
                    .OrderBy(x => x.Distance)
                    .First();

                if (nearest.Distance > GridSnapToleranceMm)
                    return false;

                matchedValue = nearest.Value;
                return true;
            }
        }
    }
}
