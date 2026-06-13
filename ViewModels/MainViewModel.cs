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
        private List<SlabModel> _parsedSlabs = new();

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
        [ObservableProperty] private string _beamHeight = "700";
        [ObservableProperty] private string _beamZOffset = "0";
        [ObservableProperty] private ObservableCollection<string> _beamLevelNames = new();
        [ObservableProperty] private string _selectedBeamLevelName = string.Empty;
        [ObservableProperty] private string _xStartName = "A";
        [ObservableProperty] private string _yStartName = "1";
        [ObservableProperty] private string _xNamingDirection = "Trái sang phải";
        [ObservableProperty] private string _yNamingDirection = "Dưới lên trên";
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

        private void LoadLevelOptions(string? preferredLevelName = null)
        {
            var currentSelection = string.IsNullOrWhiteSpace(preferredLevelName)
                ? SelectedBeamLevelName
                : preferredLevelName;
            var numberOfFloors = ParsePositiveInt(NumberOfFloors, 1);

            BeamLevelNames.Clear();

            for (var floorNumber = 1; floorNumber <= numberOfFloors; floorNumber++)
                BeamLevelNames.Add($"Level {floorNumber}");

            SelectedBeamLevelName = BeamLevelNames.FirstOrDefault(levelName =>
                    string.Equals(levelName, currentSelection, StringComparison.OrdinalIgnoreCase))
                ?? BeamLevelNames.FirstOrDefault() ?? string.Empty;
        }

        partial void OnNumberOfFloorsChanged(string value)
        {
            LoadLevelOptions(SelectedBeamLevelName);
        }

        private void AutoDetectDwg()
        {
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
                _parsedSlabs = result.Slabs;

                foreach (var layer in result.Layers)
                    Layers.Add(layer);

                UpdateCounts();
                SetStatus($"Đã đọc CAD: Lưới trục {_parsedGrids.Count}, Cột {_parsedColumns.Count}, Dầm {_parsedBeams.Count}, Sàn {_parsedSlabs.Count}.", StatusType.Success);
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

            if (_parsedGrids.Count == 0)
            {
                SetStatus("Chưa có dữ liệu lưới trục. Vui lòng đọc CAD trước.", StatusType.Error);
                return;
            }

            if (CreateBeam && !TryParsePositiveDouble(BeamHeight, out _))
            {
                SetStatus("Chiều cao dầm phải là số dương. Bề rộng dầm sẽ được lấy theo khoảng cách hai biên trên bản vẽ CAD.", StatusType.Error);
                return;
            }

            if (CreateBeam)
            {
                ReparseCurrentLayerMapping();
                UpdateCounts();
            }

            if (CreateColumn && _parsedColumns.Count == 0)
            {
                SetStatus("Đã tích vẽ cột nhưng chưa đọc được dữ liệu cột từ CAD.", StatusType.Error);
                return;
            }

            if (CreateBeam && _parsedBeams.Count == 0)
            {
                SetStatus("Đã tích vẽ dầm nhưng chưa đọc được dữ liệu dầm từ CAD.", StatusType.Error);
                return;
            }

            if (CreateSlab && _parsedSlabs.Count == 0)
            {
                SetStatus("Đã tích vẽ sàn nhưng chưa đọc được dữ liệu sàn từ CAD.", StatusType.Error);
                return;
            }

            try
            {
                var numberOfFloors = ParsePositiveInt(NumberOfFloors, 1);
                var firstFloorHeight = ParsePositiveDouble(FloorHeight, 3000.0);
                var typicalFloorHeight = ParsePositiveDouble(TypicalHeight, firstFloorHeight);
                var baseOffset = ParseDouble(ColumnBaseOffset, 0.0);
                var topOffset = ParseDouble(ColumnTopOffset, 0.0);
                var zOffset = ParseDouble(BeamZOffset, 0.0);
                var selectedBeamSlabLevelName = SelectedBeamLevelName;

                var levelService = new LevelCreationService(_doc);
                var levelResult = levelService.CreateOrUpdateLevels(
                    numberOfFloors,
                    firstFloorHeight,
                    typicalFloorHeight);

                if (levelResult.Failed > 0 || levelResult.Levels.Count < numberOfFloors + 1)
                {
                    SetStatus($"Không tạo được danh sách Level. Lỗi: {levelResult.Failed}.", StatusType.Error);
                    return;
                }

                var beamSlabStartIndex = GetLevelIndex(levelResult.Levels, selectedBeamSlabLevelName);
                if (beamSlabStartIndex < 0)
                    beamSlabStartIndex = 0;

                var gridService = new GridCreationService(_doc);
                var createdElementIds = new List<ElementId>(levelResult.CreatedElementIds);
                var gridCreated = 0;
                var gridSkipped = 0;
                var columnCreated = 0;
                var columnSkipped = 0;
                var beamCreated = 0;
                var beamSkipped = 0;
                var slabCreated = 0;
                var slabSkipped = 0;

                var gridResult = gridService.CreateGrids(_parsedGrids);
                gridCreated = gridResult.Created;
                gridSkipped = gridResult.Skipped;
                createdElementIds.AddRange(gridResult.CreatedElementIds);

                if (gridResult.Failed > 0)
                {
                    SetStatus($"Đã tạo {gridResult.Created} lưới trục, bỏ qua {gridResult.Skipped}, lỗi {gridResult.Failed}.", StatusType.Error);
                    return;
                }

                if (CreateColumn)
                {
                    var columnService = new ColumnCreationService(_doc);
                    for (int floorIndex = 0; floorIndex < numberOfFloors; floorIndex++)
                    {
                        var columnResult = columnService.CreateColumns(
                            _parsedColumns,
                            _parsedGrids,
                            levelResult.Levels[floorIndex],
                            levelResult.Levels[floorIndex + 1],
                            firstFloorHeight,
                            baseOffset,
                            topOffset,
                            false);

                        columnCreated += columnResult.Created;
                        columnSkipped += columnResult.Skipped;
                        createdElementIds.AddRange(columnResult.CreatedElementIds);

                        if (columnResult.Failed > 0)
                        {
                            SetStatus($"Đã tạo {gridCreated} lưới trục, {columnCreated} cột; lỗi cột {columnResult.Failed} tại tầng {floorIndex + 1}.", StatusType.Error);
                            return;
                        }
                    }
                }

                if (CreateBeam)
                {
                    var beamService = new BeamCreationService(_doc);
                    var beamSlabEndIndex = Math.Min(levelResult.Levels.Count, beamSlabStartIndex + numberOfFloors);
                    for (int levelIndex = beamSlabStartIndex; levelIndex < beamSlabEndIndex; levelIndex++)
                    {
                        var beamResult = beamService.CreateBeams(
                            _parsedBeams,
                            _parsedGrids,
                            levelResult.Levels[levelIndex],
                            zOffset,
                            false);

                        beamCreated += beamResult.Created;
                        beamSkipped += beamResult.Skipped;
                        createdElementIds.AddRange(beamResult.CreatedElementIds);

                        if (beamResult.Failed > 0)
                        {
                            SetStatus($"Đã tạo {gridCreated} lưới trục, {columnCreated} cột, {beamCreated} dầm; lỗi dầm {beamResult.Failed} tại {levelResult.Levels[levelIndex].Name}.", StatusType.Error);
                            return;
                        }
                    }
                }

                if (CreateSlab)
                {
                    var slabService = new SlabCreationService(_doc);
                    var beamSlabEndIndex = Math.Min(levelResult.Levels.Count, beamSlabStartIndex + numberOfFloors);
                    for (int levelIndex = beamSlabStartIndex; levelIndex < beamSlabEndIndex; levelIndex++)
                    {
                        var slabResult = slabService.CreateSlabs(
                            _parsedSlabs,
                            _parsedGrids,
                            levelResult.Levels[levelIndex],
                            zOffset,
                            false);

                        slabCreated += slabResult.Created;
                        slabSkipped += slabResult.Skipped;
                        createdElementIds.AddRange(slabResult.CreatedElementIds);

                        if (slabResult.Failed > 0)
                        {
                            SetStatus($"Đã tạo {gridCreated} lưới trục, {columnCreated} cột, {beamCreated} dầm, {slabCreated} sàn; lỗi sàn {slabResult.Failed} tại {levelResult.Levels[levelIndex].Name}.", StatusType.Error);
                            return;
                        }
                    }
                }

                if (createdElementIds.Count == 0)
                {
                    SetStatus($"Không tạo thêm cấu kiện mới. Lưới bỏ qua: {gridSkipped}, cột bỏ qua: {columnSkipped}, dầm: {beamSkipped}, sàn: {slabSkipped}.", StatusType.Info);
                    return;
                }

                SetStatus($"Đã tạo/cập nhật Level: mới {levelResult.Created}, cập nhật {levelResult.Updated}. Tạo {gridCreated} lưới trục, {columnCreated} cột, {beamCreated} dầm, {slabCreated} sàn. Bỏ qua lưới: {gridSkipped}, cột: {columnSkipped}, dầm: {beamSkipped}, sàn: {slabSkipped}.", StatusType.Success);
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
            var slabLayerNames = layerList.Where(IsSlabLayerRobust).Select(l => l.LayerName).ToList();

            var grids = reader.ReadGridLines(
                instance,
                gridLayerNames,
                new GridNamingOptions
                {
                    XStartName = XStartName,
                    YStartName = YStartName,
                    XLeftToRight = NormalizeText(XNamingDirection).Contains("TRAI"),
                    YBottomToTop = NormalizeText(YNamingDirection).Contains("DUOI")
                });

            var geometryByLayer = reader.GetGeometryByLayer(instance);
            var columns = new ColumnReaderService().ReadColumns(geometryByLayer, columnLayerNames);
            var beams = new BeamReaderService().ReadBeams(
                geometryByLayer,
                beamLayerNames,
                gridLayerNames,
                ParsePositiveDouble(BeamHeight, 700.0));
            var slabs = new SlabReaderService().ReadSlabs(
                geometryByLayer,
                slabLayerNames,
                ParsePositiveDouble(SlabThickness, 130.0));

            return new DwgReadResult
            {
                Grids = grids,
                Columns = columns,
                Beams = beams,
                Slabs = slabs
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
            _parsedSlabs = result.Slabs;
        }

        private static int GetLevelIndex(IReadOnlyList<Level> levels, string levelName)
        {
            for (var index = 0; index < levels.Count; index++)
            {
                if (string.Equals(levels[index].Name, levelName, StringComparison.OrdinalIgnoreCase))
                    return index;
            }

            return -1;
        }

        private void UpdateCounts()
        {
            GridCount = _parsedGrids.Count;
            GridLayerCount = Layers.Count(IsGridLayer);
            ColumnCount = _parsedColumns.Count;
            BeamCount = _parsedBeams.Count;
            SlabCount = _parsedSlabs.Count;
        }

        private void ResetCounts()
        {
            GridCount = GridLayerCount = ColumnCount = BeamCount = SlabCount = 0;
            _parsedGrids.Clear();
            _parsedColumns.Clear();
            _parsedBeams.Clear();
            _parsedSlabs.Clear();
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
        {
            var elementType = NormalizeText(layer.ElementType);
            return elementType.Contains("LUOI") ||
                   elementType.Contains("TRUC") ||
                   elementType.Contains("GRID") ||
                   elementType.Contains("AXIS");
        }

        private static bool IsColumnLayer(DwgLayer layer)
        {
            var elementType = NormalizeText(layer.ElementType);
            return elementType.Contains("COT") ||
                   elementType.Contains("COLUMN") ||
                   elementType.Contains("COL");
        }

        private static bool IsBeamLayerRobust(DwgLayer layer)
        {
            var elementType = NormalizeText(layer.ElementType);
            return elementType.StartsWith("D", StringComparison.OrdinalIgnoreCase) ||
                   elementType.Contains("DAM") ||
                   elementType.Contains("BEAM");
        }

        private static bool IsSlabLayerRobust(DwgLayer layer)
        {
            var elementType = NormalizeText(layer.ElementType);
            return elementType.StartsWith("S", StringComparison.OrdinalIgnoreCase) ||
                   elementType.Contains("SAN") ||
                   elementType.Contains("SLAB") ||
                   elementType.Contains("FLOOR");
        }

        private static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().Normalize(System.Text.NormalizationForm.FormD);
            var builder = new System.Text.StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    builder.Append(ch);
            }

            return builder
                .ToString()
                .Normalize(System.Text.NormalizationForm.FormC)
                .ToUpperInvariant();
        }

        private static double ParsePositiveDouble(string value, double fallback)
            => double.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

        private static bool TryParsePositiveDouble(string value, out double parsed)
            => double.TryParse(value, out parsed) && parsed > 0;

        private static double ParseDouble(string value, double fallback)
            => double.TryParse(value, out var parsed) ? parsed : fallback;

        private static int ParsePositiveInt(string value, int fallback)
            => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

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
            public List<SlabModel> Slabs { get; set; } = new();
        }
    }
}
