using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using AutoCADToRevitApplication.Models.Elements;
using AutoCADToRevitApplication.Models.Results;

namespace AutoCADToRevitApplication.Services.Creation
{
    public class BeamCreationService
    {
        private readonly Document _doc;
        private const double DuplicateToleranceMm = 50.0;
        private const double MinBeamLengthMm = 300.0;
        private const string GeneratedBeamMarker = "AutoCADToRevitApplication";
        private const string DefaultBeamFamilyName = "M_Concrete-Rectangular Beam";

        public BeamCreationService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public BeamCreationResult CreateBeams(
            IReadOnlyCollection<BeamModel> beamModels,
            IReadOnlyCollection<GridModel> gridModels,
            string beamLevelName,
            double zOffsetMm)
        {
            var result = new BeamCreationResult();

            if (beamModels.Count == 0)
            {
                result.Messages.Add("Chua co du lieu dam de ve.");
                return result;
            }

            if (gridModels.Count == 0)
            {
                result.Messages.Add("Can co du lieu luoi truc de dat dam dung toa do CAD.");
                return result;
            }

            var level = GetBeamLevel(beamLevelName);
            if (level == null)
            {
                result.Messages.Add("Khong xac dinh duoc Level dat dam.");
                return result;
            }

            var baseSymbol = FindBaseBeamSymbol();
            if (baseSymbol == null)
            {
                result.Messages.Add($"Khong tim thay family dam '{DefaultBeamFamilyName}'. Vui long load family nay truoc khi ve dam.");
                return result;
            }

            var placement = BeamPlacement.From(gridModels);
            var createdKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingKeys = GetExistingBeamKeys();
            var zOffset = MmToFeet(zOffsetMm);
            var elevation = level.Elevation;

            using var transaction = new Transaction(_doc, "Create beams from CAD");
            transaction.Start();

            DeleteGeneratedBeams();

            foreach (var beamModel in beamModels)
            {
                try
                {
                    if (beamModel.Length < MinBeamLengthMm)
                    {
                        result.Skipped++;
                        result.Messages.Add($"Bo qua dam qua ngan tai {beamModel.CenterPoint}.");
                        continue;
                    }

                    var symbol = GetOrCreateBeamType(baseSymbol, beamModel);
                    if (symbol == null)
                    {
                        result.Failed++;
                        result.Messages.Add($"Khong tao duoc type dam {beamModel.Width:F0}x{beamModel.Height:F0}.");
                        continue;
                    }

                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        _doc.Regenerate();
                    }

                    var start = placement.ToRevitPoint(beamModel.StartPoint, elevation);
                    var end = placement.ToRevitPoint(beamModel.EndPoint, elevation);
                    var key = GetBeamKey(start, end);

                    if (existingKeys.Contains(key) || createdKeys.Contains(key))
                    {
                        result.Skipped++;
                        result.Messages.Add($"Bo qua dam trung tai {beamModel.CenterPoint}.");
                        continue;
                    }

                    var line = Line.CreateBound(start, end);
                    var instance = _doc.Create.NewFamilyInstance(line, symbol, level, StructuralType.Beam);
                    MarkGeneratedBeam(instance);
                    SetBeamOffsets(instance, level, zOffset);

                    createdKeys.Add(key);
                    result.Created++;
                    result.CreatedElementIds.Add(instance.Id);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Messages.Add($"Khong tao duoc dam {beamModel.Width:F0}x{beamModel.Height:F0}: {ex.Message}");
                }
            }

            transaction.Commit();
            return result;
        }

        private Level? GetBeamLevel(string beamLevelName)
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            var selected = levels.FirstOrDefault(l =>
                string.Equals(l.Name, beamLevelName, StringComparison.OrdinalIgnoreCase));

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

        private FamilySymbol? FindBaseBeamSymbol()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => string.Equals(
                    s.FamilyName,
                    DefaultBeamFamilyName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private FamilySymbol? GetOrCreateBeamType(FamilySymbol baseSymbol, BeamModel beamModel)
        {
            var typeName = GetBeamTypeName(beamModel);
            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    string.Equals(s.FamilyName, DefaultBeamFamilyName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.Name, typeName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            var duplicate = baseSymbol.Duplicate(typeName) as FamilySymbol;
            if (duplicate == null) return null;

            return TrySetBeamDimensions(duplicate, beamModel.Width, beamModel.Height)
                ? duplicate
                : null;
        }

        private static string GetBeamTypeName(BeamModel beamModel)
            => $"{Math.Round(beamModel.Width):F0} x {Math.Round(beamModel.Height):F0}mm";

        private static bool TrySetBeamDimensions(FamilySymbol symbol, double widthMm, double heightMm)
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

        private static void SetBeamOffsets(FamilyInstance instance, Level level, double zOffset)
        {
            TrySetElementIdParameter(instance, BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM, level.Id);
            TrySetDoubleParameter(instance, BuiltInParameter.Z_OFFSET_VALUE, zOffset);
            TrySetDoubleParameter(instance, BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION, 0);
            TrySetDoubleParameter(instance, BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION, 0);
        }

        private void DeleteGeneratedBeams()
        {
            var generatedBeamIds = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilyInstance>()
                .Where(IsGeneratedBeam)
                .Select(i => i.Id)
                .ToList();

            if (generatedBeamIds.Count > 0)
                _doc.Delete(generatedBeamIds);
        }

        private static bool IsGeneratedBeam(FamilyInstance instance)
        {
            var comments = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            return string.Equals(
                comments?.AsString(),
                GeneratedBeamMarker,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void MarkGeneratedBeam(FamilyInstance instance)
        {
            var comments = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments == null || comments.IsReadOnly) return;

            comments.Set(GeneratedBeamMarker);
        }

        private HashSet<string> GetExistingBeamKeys()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilyInstance>()
                .Select(i => i.Location as LocationCurve)
                .Where(lc => lc?.Curve is Line)
                .Select(lc =>
                {
                    var line = (Line)lc!.Curve;
                    return GetBeamKey(line.GetEndPoint(0), line.GetEndPoint(1));
                })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string GetBeamKey(XYZ start, XYZ end)
        {
            var a = GetPointKey(FeetToMm(start.X), FeetToMm(start.Y), FeetToMm(start.Z));
            var b = GetPointKey(FeetToMm(end.X), FeetToMm(end.Y), FeetToMm(end.Z));
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0
                ? $"{a}|{b}"
                : $"{b}|{a}";
        }

        private static string GetPointKey(double xMm, double yMm, double zMm)
            => $"{RoundToTolerance(xMm)}:{RoundToTolerance(yMm)}:{RoundToTolerance(zMm)}";

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

        private class BeamPlacement
        {
            public double CenterX { get; private set; }
            public double CenterY { get; private set; }

            public static BeamPlacement From(IReadOnlyCollection<GridModel> gridModels)
            {
                var minX = gridModels.SelectMany(g => new[] { g.StartPoint.X, g.EndPoint.X }).Min();
                var maxX = gridModels.SelectMany(g => new[] { g.StartPoint.X, g.EndPoint.X }).Max();
                var minY = gridModels.SelectMany(g => new[] { g.StartPoint.Y, g.EndPoint.Y }).Min();
                var maxY = gridModels.SelectMany(g => new[] { g.StartPoint.Y, g.EndPoint.Y }).Max();

                return new BeamPlacement
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
