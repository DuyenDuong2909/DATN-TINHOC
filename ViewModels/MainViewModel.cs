using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoCADToRevitApplication.Models.Elements;
using AutoCADToRevitApplication.Models.Mapping;
using AutoCADToRevitApplication.Services.Creation;
using AutoCADToRevitApplication.Services.Import;
using AutoCADToRevitApplication.Services.Parsing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace AutoCADToRevitApplication.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly LayerMappingService _mappingService = new();
        private UIDocument? _uiDoc;
        private Document? _doc;
        private ElementId? _currentDwgInstanceId;
        private List<GridModel> _parsedGrids = new();
        private List<ColumnModel> _parsedColumns = new();
        private List<BeamModel> _parsedBeams = new();

        public event Action? RequestClose;

        [ObservableProperty] private string _dwgFileName = "Chưa tìm thấy file DWG...";
        [ObservableProperty] private bool _isFileLoaded;
        [ObservableProperty] private string _statusMessage = "Chưa đọc file CAD. Vui lòng nhấn 'Đọc CAD' để bắt đầu.";
        [ObservableProperty] private Brush _statusColor = new SolidColorBrush(MediaColor.FromRgb(0x78, 0x90, 0x9C));
        [ObservableProperty] private ObservableCollection<DwgLayer> _layers = new();
        [ObservableProperty] private int _gridCount;
        [ObservableProperty] private int _gridLayerCount;
        [ObservableProperty] private int _columnCount;
        [ObservableProperty] private int _beamCount;
        [ObservableProperty] private int _slabCount;
        [ObservableProperty] private string _floorHeight = "4300";
        [ObservableProperty] private string _typicalHeight = "3900";
        [ObservableProperty] private string _numberOfFloors = "7";
        [ObservableProperty] private string _columnBaseOffset = "0";
        [ObservableProperty] private string _columnTopOffset = "0";
        [ObservableProperty] private string _slabThickness = "130";
        [ObservableProperty] private string _beamWidth = "300";
        [ObservableProperty] private string _beamHeight = "700";
        [ObservableProperty] private string _beamZOffset = "0";
        [ObservableProperty] private ObservableCollection<string> _beamLevelNames = new();
        [ObservableProperty] private string _selectedBeamLevelName = string.Empty;
        [ObservableProperty] private string _xStartName = "A";
        [ObservableProperty] private string _yStartName = "1";
        [ObservableProperty] private string _xNamingDirection = "Trái sang phải";
        [ObservableProperty] private string _yNamingDirection = "Dưới lên trên";
        [ObservableProperty] private bool _createGrid = true;
        [ObservableProperty] private bool _createColumn = true;
        [ObservableProperty] private bool _createBeam = true;
        [ObservableProperty] private bool _createSlab = true;

        public void Initialize(UIApplication uiApp)
        {
            _uiDoc = uiApp.ActiveUIDocument;
            _doc = _uiDoc?.Document;

            if (_doc != null)
            {
                LoadLevelOptions();
                AutoDetectDwg();
            }
        }

        private void LoadLevelOptions()
        {
            if (_doc == null) return;

            BeamLevelNames.Clear();

            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            foreach (var level in levels)
                BeamLevelNames.Add(level.Name);

            SelectedBeamLevelName = levels.FirstOrDefault(l =>
                    string.Equals(l.Name, "Level 1", StringComparison.OrdinalIgnoreCase))
                ?.Name ?? levels.FirstOrDefault()?.Name ?? string.Empty;
        }

        private void AutoDetectDwg()
        {
            if (CreateBeam && _parsedBeams.Count == 0)
            {
                SetStatus("Da tich ve dam nhung chua doc duoc du lieu dam tu CAD.", StatusType.Error);
                return;
            }

            try
            {
                var reader = new RevitDwgReaderService(_doc!);
                var instance = reader.FindDwgInstance();

                if (instance != null)
                {
                    _currentDwgInstanceId = instance.Id;
                    DwgFileName = instance.Category?.Name ?? instance.Name ?? "DWG File";
                    SetStatus($"Tìm thấy file DWG: {DwgFileName}. Nhấn 'Đọc CAD' để đọc dữ liệu.", StatusType.Info);
                }
                else
                {
                    SetStatus("Không tìm thấy file DWG trong model. Vui lòng Import file DWG vào Revit trước.", StatusType.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tìm DWG: {ex.Message}", StatusType.Error);
            }
        }

        [RelayCommand]
        private void ImportDwg()
        {
            if (_doc == null)
            {
                SetStatus("Không có Revit document đang mở.", StatusType.Error);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Chọn file CAD để import",
                Filter = "CAD files (*.dwg;*.dxf)|*.dwg;*.dxf|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var importService = new CadImportService(_doc);
                _currentDwgInstanceId = importService.ImportDwg(dialog.FileName);
                DwgFileName = Path.GetFileName(dialog.FileName);
                Layers.Clear();
                ResetCounts();
                IsFileLoaded = false;
                SetStatus($"Đã import CAD: {DwgFileName}. Nhấn 'Đọc CAD' để đọc layer và thông số.", StatusType.Success);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi import CAD: {ex.Message}", StatusType.Error);
            }
        }

        [RelayCommand]
        private async Task ReadCad()
        {
            if (_doc == null)
            {
                SetStatus("Không có Revit document đang mở.", StatusType.Error);
                return;
            }

            SetStatus("Đang đọc file DWG từ Revit...", StatusType.Pending);
            Layers.Clear();
            ResetCounts();

            try
            {
                var result = await Task.Run(ReadDwgFromRevit);
                if (result == null)
                {
                    SetStatus("Không tìm thấy file DWG trong model. Vui lòng Import file DWG vào Revit.", StatusType.Error);
                    return;
                }

                _parsedGrids = result.Grids;
                _parsedColumns = result.Columns;
                _parsedBeams = result.Beams;

                foreach (var layer in result.Layers)
                    Layers.Add(layer);

                UpdateCounts();
                SetStatus($"Đã đọc DWG thành công! Lưới trục: {_parsedGrids.Count}, Cột: {_parsedColumns.Count}", StatusType.Success);
                SetStatus($"Da doc CAD: Luoi truc {_parsedGrids.Count}, Cot {_parsedColumns.Count}, Dam {_parsedBeams.Count}.", StatusType.Success);
                IsFileLoaded = true;
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi đọc DWG: {ex.Message}", StatusType.Error);
            }
        }

        [RelayCommand]
        private void ApplyLayer()
        {
            ReparseCurrentLayerMapping();
            UpdateCounts();
            SetStatus("Đã áp dụng gán layer. Kiểm tra thống kê bên dưới.", StatusType.Success);
        }

        [RelayCommand]
        private void ConvertTo3D()
        {
            if (_doc == null)
            {
                SetStatus("Không có Revit document đang mở.", StatusType.Error);
                return;
            }

            if (!CreateGrid && !CreateColumn && !CreateBeam)
            {
                SetStatus("Chưa chọn cấu kiện để vẽ. Vui lòng tích Lưới trục hoặc Cột.", StatusType.Error);
                return;
            }

            if ((CreateGrid || CreateColumn || CreateBeam) && _parsedGrids.Count == 0)
            {
                SetStatus("Chưa có dữ liệu lưới trục. Vui lòng đọc CAD trước.", StatusType.Error);
                return;
            }

            if (CreateColumn && _parsedColumns.Count == 0)
            {
                SetStatus("Đã tích vẽ cột nhưng chưa đọc được dữ liệu cột từ CAD.", StatusType.Error);
                return;
            }

            try
            {
                var gridService = new GridCreationService(_doc);
                var createdElementIds = new List<ElementId>();
                var gridCreated = 0;
                var gridSkipped = 0;
                var columnCreated = 0;
                var columnSkipped = 0;
                var beamCreated = 0;
                var beamSkipped = 0;

                if (CreateGrid)
                {
                    var gridResult = gridService.CreateGrids(_parsedGrids);
                    gridCreated = gridResult.Created;
                    gridSkipped = gridResult.Skipped;
                    createdElementIds.AddRange(gridResult.CreatedElementIds);

                    if (gridResult.Failed > 0)
                    {
                        SetStatus($"Đã tạo {gridResult.Created} lưới trục, bỏ qua {gridResult.Skipped}, lỗi {gridResult.Failed}.", StatusType.Error);
                        return;
                    }
                }

                if (CreateColumn)
                {
                    var columnService = new ColumnCreationService(_doc);
                    var columnResult = columnService.CreateColumns(
                        _parsedColumns,
                        _parsedGrids,
                        ParsePositiveDouble(FloorHeight, 3000.0),
                        ParseDouble(ColumnBaseOffset, 0.0),
                        ParseDouble(ColumnTopOffset, 0.0));

                    columnCreated = columnResult.Created;
                    columnSkipped = columnResult.Skipped;
                    createdElementIds.AddRange(columnResult.CreatedElementIds);

                    if (columnResult.Failed > 0)
                    {
                        SetStatus($"Đã tạo {gridCreated} lưới trục, {columnCreated} cột; lỗi cột {columnResult.Failed}.", StatusType.Error);
                        return;
                    }
                }

                if (CreateBeam)
                {
                    var beamService = new BeamCreationService(_doc);
                    var beamResult = beamService.CreateBeams(
                        _parsedBeams,
                        _parsedGrids,
                        SelectedBeamLevelName,
                        ParseDouble(BeamZOffset, 0.0));

                    beamCreated = beamResult.Created;
                    beamSkipped = beamResult.Skipped;
                    createdElementIds.AddRange(beamResult.CreatedElementIds);

                    if (beamResult.Failed > 0)
                    {
                        SetStatus($"Da tao {gridCreated} luoi truc, {columnCreated} cot, {beamCreated} dam; loi dam {beamResult.Failed}.", StatusType.Error);
                        return;
                    }
                }

                if (createdElementIds.Count == 0)
                {
                    SetStatus($"Không tạo thêm cấu kiện mới. Lưới bỏ qua: {gridSkipped}, cột bỏ qua: {columnSkipped}.", StatusType.Info);
                    return;
                }

                FocusCreatedElements(createdElementIds);
                gridService.HideDwgImportsInAllViews();
                FocusCreatedElements(createdElementIds);
                SetStatus($"Đã tạo {gridCreated} lưới trục và {columnCreated} cột trong Revit. Bỏ qua lưới: {gridSkipped}, cột: {columnSkipped}.", StatusType.Success);
                SetStatus($"Da tao {gridCreated} luoi truc, {columnCreated} cot, {beamCreated} dam trong Revit. Bo qua luoi: {gridSkipped}, cot: {columnSkipped}, dam: {beamSkipped}.", StatusType.Success);
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi chuyển đổi sang 3D: {ex.Message}", StatusType.Error);
            }
        }

        private DwgReadResult? ReadDwgFromRevit()
        {
            var reader = new RevitDwgReaderService(_doc!);
            var instance = GetCurrentDwgInstance(reader);
            if (instance == null) return null;

            var layers = reader.GetLayersFromInstance(instance);
            _mappingService.AutoMap(layers);

            var result = ParseGeometryByLayer(reader, instance, layers);
            DwgFileName = instance.Category?.Name ?? instance.Name ?? "DWG File";
            result.Layers = layers;
            return result;
        }

        private DwgReadResult ParseGeometryByLayer(
            RevitDwgReaderService reader,
            Element instance,
            IEnumerable<DwgLayer> layers)
        {
            var layerList = layers.ToList();
            var gridLayerNames = layerList.Where(IsGridLayer).Select(l => l.LayerName).ToList();
            var columnLayerNames = layerList.Where(IsColumnLayer).Select(l => l.LayerName).ToList();
            var beamLayerNames = layerList.Where(IsBeamLayerRobust).Select(l => l.LayerName).ToList();

            var grids = reader.ReadGridLines(
                instance,
                gridLayerNames,
                new GridNamingOptions
                {
                    XStartName = XStartName,
                    YStartName = YStartName,
                    XLeftToRight = XNamingDirection == "Trái sang phải",
                    YBottomToTop = YNamingDirection == "Dưới lên trên"
                });

            var geometryByLayer = reader.GetGeometryByLayer(instance);
            var columns = new ColumnReaderService().ReadColumns(geometryByLayer, columnLayerNames);
            var beams = new BeamReaderService().ReadBeams(
                geometryByLayer,
                beamLayerNames,
                gridLayerNames,
                ParsePositiveDouble(BeamWidth, 300.0),
                ParsePositiveDouble(BeamHeight, 700.0));

            return new DwgReadResult
            {
                Grids = grids,
                Columns = columns,
                Beams = beams
            };
        }

        private void ReparseCurrentLayerMapping()
        {
            if (_doc == null || Layers.Count == 0) return;

            var reader = new RevitDwgReaderService(_doc);
            var instance = GetCurrentDwgInstance(reader);
            if (instance == null) return;

            var result = ParseGeometryByLayer(reader, instance, Layers);
            _parsedGrids = result.Grids;
            _parsedColumns = result.Columns;
            _parsedBeams = result.Beams;
        }

        private void FocusCreatedElements(ICollection<ElementId> createdElementIds)
        {
            if (_uiDoc == null || _doc == null || createdElementIds.Count == 0)
                return;

            var planView = FindPlanViewForActiveLevel();
            if (planView != null && _uiDoc.ActiveView.Id != planView.Id)
                _uiDoc.ActiveView = planView;

            _uiDoc.ShowElements(createdElementIds);
        }

        private ViewPlan? FindPlanViewForActiveLevel()
        {
            if (_doc == null) return null;

            var level = _doc.ActiveView.GenLevel;
            if (level == null) return _doc.ActiveView as ViewPlan;

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate)
                .Where(v => v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan)
                .FirstOrDefault(v => v.GenLevel != null && v.GenLevel.Id == level.Id);
        }

        private void UpdateCounts()
        {
            GridCount = _parsedGrids.Count;
            GridLayerCount = Layers.Count(IsGridLayer);
            ColumnCount = _parsedColumns.Count;
            BeamCount = _parsedBeams.Count;
            SlabCount = 0;
        }

        private void ResetCounts()
        {
            GridCount = GridLayerCount = ColumnCount = BeamCount = SlabCount = 0;
            _parsedGrids.Clear();
            _parsedColumns.Clear();
            _parsedBeams.Clear();
        }

        private Element? GetCurrentDwgInstance(RevitDwgReaderService reader)
        {
            if (_doc != null && _currentDwgInstanceId != null)
            {
                var element = _doc.GetElement(_currentDwgInstanceId);
                if (element is ImportInstance || element is RevitLinkInstance)
                    return element;
            }

            var detected = reader.FindDwgInstance();
            _currentDwgInstanceId = detected?.Id;
            return detected;
        }

        private static bool IsGridLayer(DwgLayer layer)
            => string.Equals(layer.ElementType, "Lưới trục", StringComparison.OrdinalIgnoreCase);

        private static bool IsColumnLayer(DwgLayer layer)
            => string.Equals(layer.ElementType, "Cột", StringComparison.OrdinalIgnoreCase);

        private static bool IsBeamLayer(DwgLayer layer)
            => string.Equals(layer.ElementType, "Dầm", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(layer.ElementType, "Dáº§m", StringComparison.OrdinalIgnoreCase);

        private static bool IsBeamLayerRobust(DwgLayer layer)
        {
            var elementType = layer.ElementType?.Trim() ?? string.Empty;
            return elementType.StartsWith("D", StringComparison.OrdinalIgnoreCase) ||
                   elementType.Contains("DAM", StringComparison.OrdinalIgnoreCase) ||
                   elementType.Contains("BEAM", StringComparison.OrdinalIgnoreCase);
        }

        private static double ParsePositiveDouble(string value, double fallback)
            => double.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

        private static double ParseDouble(string value, double fallback)
            => double.TryParse(value, out var parsed) ? parsed : fallback;

        private enum StatusType { Info, Success, Error, Pending }

        private void SetStatus(string message, StatusType type)
        {
            StatusMessage = message;
            StatusColor = type switch
            {
                StatusType.Success => new SolidColorBrush(MediaColor.FromRgb(0x2E, 0x7D, 0x32)),
                StatusType.Error => new SolidColorBrush(MediaColor.FromRgb(0xC6, 0x28, 0x28)),
                StatusType.Pending => new SolidColorBrush(MediaColor.FromRgb(0xF5, 0x7F, 0x17)),
                _ => new SolidColorBrush(MediaColor.FromRgb(0x78, 0x90, 0x9C))
            };
        }

        private class DwgReadResult
        {
            public List<DwgLayer> Layers { get; set; } = new();
            public List<GridModel> Grids { get; set; } = new();
            public List<ColumnModel> Columns { get; set; } = new();
            public List<BeamModel> Beams { get; set; } = new();
        }
    }
}
