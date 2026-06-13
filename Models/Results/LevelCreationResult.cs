using Autodesk.Revit.DB;

namespace AutoCADToRevitApplication.Models.Results
{
    public class LevelCreationResult
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public List<Level> Levels { get; } = new();
        public List<ElementId> CreatedElementIds { get; } = new();
        public List<string> Messages { get; } = new();
    }
}
