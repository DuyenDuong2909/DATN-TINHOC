using Autodesk.Revit.DB;
using AutoCADToRevitApplication.Models.Elements;
using AutoCADToRevitApplication.Models.Results;

namespace AutoCADToRevitApplication.Services.Creation
{
    public class GridCreationService
    {
        private readonly Document _doc;
        private const double LevelEndExtensionMm = 500.0;
        private const double VerticalLevelExtensionMm = 500.0;
        private const double DuplicateToleranceMm = 50.0;

        public GridCreationService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public GridCreationResult CreateGrids(IReadOnlyCollection<GridModel> gridModels)
        {
            var result = new GridCreationResult();

            if (gridModels.Count == 0)
            {
                result.Messages.Add("Chưa có dữ liệu lưới trục để vẽ.");
                return result;
            }

            var activeLevel = GetActiveLevel();
            if (activeLevel == null)
            {
                result.Messages.Add("Không xác định được Level hiện hành từ Active View.");
                return result;
            }

            var existingNames = GetExistingGridNames();
            var createdGridKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingGridKeys = GetExistingGridKeys();
            var placement = GridPlacement.From(gridModels);
            var verticalExtents = GetVerticalExtents();

            using var transaction = new Transaction(_doc, "Tạo lưới trục từ file CAD");
            transaction.Start();

            foreach (var model in gridModels)
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    result.Skipped++;
                    result.Messages.Add("Bỏ qua một lưới trục chưa có tên.");
                    continue;
                }

                if (existingNames.Contains(model.Name))
                {
                    result.Skipped++;
                    result.Messages.Add($"Bỏ qua lưới trục '{model.Name}' vì tên đã tồn tại trong Revit.");
                    continue;
                }

                try
                {
                    var gridKey = GetGridKey(model, placement);
                    if (existingGridKeys.Contains(gridKey) || createdGridKeys.Contains(gridKey))
                    {
                        result.Skipped++;
                        result.Messages.Add($"Bỏ qua lưới trục '{model.Name}' vì trùng tọa độ với lưới trục khác.");
                        continue;
                    }

                    var line = CreateRevitLine(model, activeLevel.Elevation, placement);
                    var grid = Grid.Create(_doc, line);
                    grid.Name = model.Name;
                    grid.SetVerticalExtents(verticalExtents.Bottom, verticalExtents.Top);
                    existingNames.Add(model.Name);
                    createdGridKeys.Add(gridKey);
                    result.Created++;
                    result.CreatedElementIds.Add(grid.Id);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Messages.Add($"Không tạo được lưới trục '{model.Name}': {ex.Message}");
                }
            }

            UpdateLevelExtentsInElevationViews(placement);

            transaction.Commit();
            return result;
        }

        private Level? GetActiveLevel()
        {
            if (_doc.ActiveView.GenLevel != null)
                return _doc.ActiveView.GenLevel;

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation))
                .FirstOrDefault();
        }

        private HashSet<string> GetExistingGridNames()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Select(g => g.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> GetExistingGridKeys()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Select(TryGetExistingGridKey)
                .Where(key => key != null)
                .Select(key => key!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private string? TryGetExistingGridKey(Grid grid)
        {
            if (grid.Curve is not Line line) return null;

            var start = line.GetEndPoint(0);
            var end = line.GetEndPoint(1);
            var dx = Math.Abs(end.X - start.X);
            var dy = Math.Abs(end.Y - start.Y);

            if (dy >= dx)
            {
                var xMm = FeetToMm((start.X + end.X) / 2.0);
                return $"V:{RoundToTolerance(xMm)}";
            }

            var yMm = FeetToMm((start.Y + end.Y) / 2.0);
            return $"H:{RoundToTolerance(yMm)}";
        }

        private static Line CreateRevitLine(GridModel model, double elevation, GridPlacement placement)
        {
            return Line.CreateBound(
                new XYZ(
                    MmToFeet(model.StartPoint.X - placement.CenterX),
                    MmToFeet(model.StartPoint.Y - placement.CenterY),
                    elevation),
                new XYZ(
                    MmToFeet(model.EndPoint.X - placement.CenterX),
                    MmToFeet(model.EndPoint.Y - placement.CenterY),
                    elevation));
        }

        private (double Bottom, double Top) GetVerticalExtents()
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            if (levels.Count == 0)
            {
                var margin = MmToFeet(VerticalLevelExtensionMm);
                return (-margin, margin);
            }

            var minLevel = levels.Min(l => l.Elevation);
            var maxLevel = levels.Max(l => l.Elevation);
            var bottom = minLevel - MmToFeet(VerticalLevelExtensionMm);
            var top = maxLevel + MmToFeet(VerticalLevelExtensionMm);

            if (top <= bottom)
                top = bottom + MmToFeet(VerticalLevelExtensionMm);

            return (bottom, top);
        }

        private void UpdateLevelExtentsInElevationViews(GridPlacement placement)
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            if (levels.Count == 0) return;

            var elevationViews = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType == ViewType.Elevation)
                .ToList();

            foreach (var view in elevationViews)
            {
                foreach (var level in levels)
                    TryUpdateLevelExtentInView(level, view, placement);
            }
        }

        private void TryUpdateLevelExtentInView(Level level, View view, GridPlacement placement)
        {
            try
            {
                var existingLine = GetLevelLineInView(level, view);
                if (existingLine == null) return;

                var direction = existingLine.Direction.Normalize();
                var midpoint = (existingLine.GetEndPoint(0) + existingLine.GetEndPoint(1)) / 2.0;
                var levelZ = level.Elevation;
                Line newLine;

                if (Math.Abs(direction.X) >= Math.Abs(direction.Y))
                {
                    newLine = Line.CreateBound(
                        new XYZ(MmToFeet(placement.MinX - placement.CenterX - LevelEndExtensionMm), midpoint.Y, levelZ),
                        new XYZ(MmToFeet(placement.MaxX - placement.CenterX + LevelEndExtensionMm), midpoint.Y, levelZ));
                }
                else
                {
                    newLine = Line.CreateBound(
                        new XYZ(midpoint.X, MmToFeet(placement.MinY - placement.CenterY - LevelEndExtensionMm), levelZ),
                        new XYZ(midpoint.X, MmToFeet(placement.MaxY - placement.CenterY + LevelEndExtensionMm), levelZ));
                }

                SetLevelCurveInView(level, view, DatumExtentType.Model, newLine);
                SetLevelEndToViewSpecific(level, view, DatumEnds.End0);
                SetLevelEndToViewSpecific(level, view, DatumEnds.End1);
                SetLevelCurveInView(level, view, DatumExtentType.ViewSpecific, newLine);
            }
            catch
            {
                // Some elevation views do not display a given level or do not allow datum extent edits.
            }
        }

        private static Line? GetLevelLineInView(Level level, View view)
        {
            var viewSpecific = level.GetCurvesInView(DatumExtentType.ViewSpecific, view)
                .OfType<Line>()
                .FirstOrDefault();

            if (viewSpecific != null) return viewSpecific;

            return level.GetCurvesInView(DatumExtentType.Model, view)
                .OfType<Line>()
                .FirstOrDefault();
        }

        private static void SetLevelEndToViewSpecific(Level level, View view, DatumEnds end)
        {
            try
            {
                level.SetDatumExtentType(end, view, DatumExtentType.ViewSpecific);
            }
            catch
            {
                // Keep the existing datum extent type when Revit does not allow changing it in this view.
            }
        }

        private static void SetLevelCurveInView(Level level, View view, DatumExtentType extentType, Line line)
        {
            try
            {
                level.SetCurveInView(extentType, view, line);
            }
            catch
            {
                // Some views only allow one datum extent type to be edited.
            }
        }

        private static string GetGridKey(GridModel model, GridPlacement placement)
        {
            if (model.IsVertical)
            {
                var x = model.MidPoint.X - placement.CenterX;
                return $"V:{RoundToTolerance(x)}";
            }

            var y = model.MidPoint.Y - placement.CenterY;
            return $"H:{RoundToTolerance(y)}";
        }

        private bool CanHide(View view, ElementId elementId)
        {
            var element = _doc.GetElement(elementId);
            return element?.CanBeHidden(view) == true && !element.IsHidden(view);
        }

        private static double RoundToTolerance(double value)
            => Math.Round(value / DuplicateToleranceMm) * DuplicateToleranceMm;

        private static double MmToFeet(double value)
            => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);

        private static double FeetToMm(double value)
            => UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters);

        private static double Clamp(double value, double min, double max)
            => Math.Min(Math.Max(value, min), max);

        private class GridPlacement
        {
            public double MinX { get; private set; }
            public double MaxX { get; private set; }
            public double MinY { get; private set; }
            public double MaxY { get; private set; }
            public double CenterX => (MinX + MaxX) / 2.0;
            public double CenterY => (MinY + MaxY) / 2.0;

            public static GridPlacement From(IReadOnlyCollection<GridModel> gridModels)
            {
                var minX = gridModels.SelectMany(g => new[] { g.StartPoint.X, g.EndPoint.X }).Min();
                var maxX = gridModels.SelectMany(g => new[] { g.StartPoint.X, g.EndPoint.X }).Max();
                var minY = gridModels.SelectMany(g => new[] { g.StartPoint.Y, g.EndPoint.Y }).Min();
                var maxY = gridModels.SelectMany(g => new[] { g.StartPoint.Y, g.EndPoint.Y }).Max();

                return new GridPlacement
                {
                    MinX = minX,
                    MaxX = maxX,
                    MinY = minY,
                    MaxY = maxY
                };
            }
        }
    }

}
