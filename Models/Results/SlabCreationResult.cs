using Autodesk.Revit.DB;

namespace AutoCADToRevitApplication.Models.Results
{
    public class SlabCreationResult
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<ElementId> CreatedElementIds { get; } = new();
        public List<string> Messages { get; } = new();
    }
}
