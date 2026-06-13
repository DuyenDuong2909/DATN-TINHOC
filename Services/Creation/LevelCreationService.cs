using Autodesk.Revit.DB;
using AutoCADToRevitApplication.Models.Results;

namespace AutoCADToRevitApplication.Services.Creation
{
    public class LevelCreationService
    {
        private readonly Document _doc;
        private const double ElevationToleranceMm = 1.0;
        private const string FirstLevelName = "Level 1";
        private const string RoofLevelName = "Level mái";

        public LevelCreationService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public LevelCreationResult CreateOrUpdateLevels(
            int numberOfFloors,
            double firstFloorHeightMm,
            double typicalFloorHeightMm)
        {
            var result = new LevelCreationResult();

            if (numberOfFloors < 1)
            {
                result.Failed++;
                result.Messages.Add("Số tầng phải lớn hơn hoặc bằng 1.");
                return result;
            }

            if (firstFloorHeightMm <= 0)
            {
                result.Failed++;
                result.Messages.Add("Chiều cao tầng 1 phải lớn hơn 0.");
                return result;
            }

            if (numberOfFloors > 1 && typicalFloorHeightMm <= 0)
            {
                result.Failed++;
                result.Messages.Add("Chiều cao tầng điển hình phải lớn hơn 0 khi số tầng > 1.");
                return result;
            }

            using var transaction = new Transaction(_doc, "Tạo level");
            transaction.Start();

            try
            {
                var baseLevel = GetOrCreateBaseLevel(result);
                result.Levels.Add(baseLevel);

                var baseElevation = baseLevel.Elevation;
                for (int index = 2; index <= numberOfFloors + 1; index++)
                {
                    var name = index == numberOfFloors + 1
                        ? RoofLevelName
                        : $"Level {index}";

                    var elevationMm = firstFloorHeightMm;
                    if (index > 2)
                        elevationMm += (index - 2) * typicalFloorHeightMm;

                    var level = GetOrCreateLevel(
                        name,
                        baseElevation + MmToFeet(elevationMm),
                        result);

                    result.Levels.Add(level);
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.RollBack();
                result.Failed++;
                result.Messages.Add($"Không tạo/cập nhật được Level: {ex.Message}");
            }

            return result;
        }

        private Level GetOrCreateBaseLevel(LevelCreationResult result)
        {
            var existing = GetLevels()
                .FirstOrDefault(l => string.Equals(l.Name, FirstLevelName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            existing = GetLevels()
                .OrderBy(l => Math.Abs(l.Elevation))
                .FirstOrDefault();

            if (existing != null)
                return existing;

            var created = Level.Create(_doc, 0);
            TrySetLevelName(created, FirstLevelName);
            result.Created++;
            result.CreatedElementIds.Add(created.Id);
            return created;
        }

        private Level GetOrCreateLevel(
            string levelName,
            double targetElevation,
            LevelCreationResult result)
        {
            var existingByName = GetLevels()
                .FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));

            if (existingByName != null)
            {
                if (Math.Abs(existingByName.Elevation - targetElevation) > MmToFeet(ElevationToleranceMm))
                {
                    existingByName.Elevation = targetElevation;
                    result.Updated++;
                }

                return existingByName;
            }

            var existingByElevation = GetLevels()
                .FirstOrDefault(l => Math.Abs(l.Elevation - targetElevation) <= MmToFeet(ElevationToleranceMm));

            if (existingByElevation != null)
            {
                TrySetLevelName(existingByElevation, levelName);
                result.Updated++;
                return existingByElevation;
            }

            var created = Level.Create(_doc, targetElevation);
            TrySetLevelName(created, levelName);
            result.Created++;
            result.CreatedElementIds.Add(created.Id);
            return created;
        }

        private List<Level> GetLevels()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();
        }

        private static void TrySetLevelName(Level level, string name)
        {
            try
            {
                level.Name = name;
            }
            catch
            {

            }
        }

        private static double MmToFeet(double value)
            => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
    }
}
