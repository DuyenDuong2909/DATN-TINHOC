using Autodesk.Revit.DB;

namespace AutoCADToRevitApplication.Services.Import
{
    public class CadImportService
    {
        private readonly Document _doc;

        public CadImportService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public ElementId ImportDwg(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Đường dẫn file CAD không hợp lệ", nameof(filePath));

            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException("Không tìm thấy file CAD", filePath);

            var view = GetImportView();
            var options = new DWGImportOptions
            {
                Placement = ImportPlacement.Origin,
                Unit = ImportUnit.Millimeter,
                ThisViewOnly = false,
                OrientToView = false,
                VisibleLayersOnly = false,
                AutoCorrectAlmostVHLines = true,
                ColorMode = ImportColorMode.Preserved
            };

            using var transaction = new Transaction(_doc, "Import CAD");
            transaction.Start();

            var imported = _doc.Import(filePath, options, view, out var importedElementId);
            if (!imported || importedElementId == ElementId.InvalidElementId)
            {
                transaction.RollBack();
                throw new InvalidOperationException("Revit không import được file CAD đã chọn");
            }

            transaction.Commit();
            return importedElementId;
        }

        private View GetImportView()
        {
            if (_doc.ActiveView != null && !_doc.ActiveView.IsTemplate)
                return _doc.ActiveView;

            var viewPlan = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => !v.IsTemplate);

            if (viewPlan != null)
                return viewPlan;

            throw new InvalidOperationException("Không tìm thấy view hợp lệ để import CAD");
        }
    }
}
