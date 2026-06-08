using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoCADToRevitApplication.Models.Elements;
using AutoCADToRevitApplication.Models.Mapping;
using AutoCADToRevitApplication.Services.Creation;
using AutoCADToRevitApplication.Services.Parsing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace AutoCADToRevitApplication.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly LayerMappingService _mappingService = new();
        private UIDocument? _uiDoc;
        private Document? _doc;
        private List<GridModel> _parsedGrids = new();

        public event Action? RequestClose;

        [ObservableProperty] private string _dwgFileName = "Chưa tìm thấy file DWG...";
        [ObservableProperty] private bool _isFileLoaded;

        [ObservableProperty]
        private string _statusMessage = "Chưa đọc file CAD. Vui lòng nhấn 'Đọc CAD' để bắt đầu.";

        [ObservableProperty]
        private Brush _statusColor = new SolidColorBrush(MediaColor.FromRgb(0x78, 0x90, 0x9C));

        [ObservableProperty] private ObservableCollection<DwgLayer> _layers = new();

        [ObservableProperty] private int _gridCount;
        [ObservableProperty] private int _gridLayerCount;
        [ObservableProperty] private int _columnCount;
        [ObservableProperty] private int _beamCount;
        [ObservableProperty] private int _slabCount;

        [ObservableProperty] private string _floorHeight = "4300";
        [ObservableProperty] private string _typicalHeight = "3900";
        [ObservableProperty] private string _numberOfFloors = "7";
        [ObservableProperty] private string _slabThickness = "130";
        [ObservableProperty] private string _beamWidth = "300";
        [ObservableProperty] private string _beamHeight = "700";
        [ObservableProperty] private string _xStartName = "A";
        [ObservableProperty] private string _yStartName = "1";
        [ObservableProperty] private string _xNamingDirection = "Trái sang phải";
        [ObservableProperty] private string _yNamingDirection = "Dưới lên trên";

        [ObservableProperty] private bool _createWall = true;
        [ObservableProperty] private bool _createColumn = true;
        [ObservableProperty] private bool _createBeam = true;
        [ObservableProperty] private bool _createSlab = true;

        public void Initialize(UIApplication uiApp)
        {
            _uiDoc = uiApp.ActiveUIDocument;
            _doc = _uiDoc?.Document;

            if (_doc != null)
                AutoDetectDwg();
        }

        private void AutoDetectDwg()
        {
            try
            {
                var reader = new RevitDwgReaderService(_doc!);
                var instance = reader.FindDwgInstance();

                if (instance != null)
                {
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

            AutoDetectDwg();
            Layers.Clear();
            ResetCounts();
            IsFileLoaded = false;
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

                foreach (var layer in result.Layers)
                    Layers.Add(layer);

                UpdateCounts();
                SetStatus($"Đã đọc DWG thành công! Lưới trục: {_parsedGrids.Count}", StatusType.Success);
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

            if (_parsedGrids.Count == 0)
            {
                SetStatus("Chưa có dữ liệu lưới trục để vẽ. Vui lòng đọc CAD trước.", StatusType.Error);
                return;
            }

            try
            {
                var service = new GridCreationService(_doc);
                var result = service.CreateGrids(_parsedGrids);

                if (result.Failed > 0)
                {
                    SetStatus($"Đã tạo {result.Created} lưới trục, bỏ qua {result.Skipped}, lỗi {result.Failed}.", StatusType.Error);
                    return;
                }

                if (result.Created == 0)
                {
                    SetStatus($"Không tạo thêm lưới trục mới. Bỏ qua: {result.Skipped}.", StatusType.Info);
                    return;
                }

                FocusCreatedGrids(result.CreatedElementIds);
                var hiddenCadCount = service.HideDwgImportsInAllViews();
                FocusCreatedGrids(result.CreatedElementIds);
                SetStatus($"Đã tạo {result.Created} lưới trục trong Revit. Bỏ qua: {result.Skipped}.", StatusType.Success);
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi vẽ lưới trục: {ex.Message}", StatusType.Error);
            }
        }

        private DwgReadResult? ReadDwgFromRevit()
        {
            var reader = new RevitDwgReaderService(_doc!);
            var instance = reader.FindDwgInstance();
            if (instance == null) return null;

            var layers = reader.GetLayersFromInstance(instance);
            _mappingService.AutoMap(layers);

            var gridLayerNames = layers
                .Where(l => l.ElementType == "Lưới trục")
                .Select(l => l.LayerName)
                .ToList();

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

            DwgFileName = instance.Category?.Name ?? instance.Name ?? "DWG File";

            return new DwgReadResult
            {
                Layers = layers,
                Grids = grids
            };
        }

        private void FocusCreatedGrids(ICollection<ElementId> createdElementIds)
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
            GridLayerCount = Layers.Count(l => l.ElementType == "Lưới trục");
            ColumnCount = 0;
            BeamCount = 0;
            SlabCount = 0;
        }

        private void ResetCounts()
        {
            GridCount = GridLayerCount = ColumnCount = BeamCount = SlabCount = 0;
            _parsedGrids.Clear();
        }

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
        }
    }
}
