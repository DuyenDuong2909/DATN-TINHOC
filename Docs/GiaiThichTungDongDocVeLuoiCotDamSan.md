# Giải thích các chức năng đọc và vẽ lưới trục, cột, dầm, sàn

Tài liệu này giải thích các file code chính liên quan đến luồng:

```text
Import/đọc CAD
    -> lấy layer và geometry từ DWG đã import vào Revit
    -> đọc lưới trục, cột, dầm, sàn thành model trung gian
    -> tạo/cập nhật Level
    -> tạo Grid, Column, Beam, Floor trong Revit
```

Ghi chú quan trọng:

- Revit API dùng feet làm đơn vị nội bộ.
- CAD trong dự án được đọc và xử lý theo milimet.
- Vì vậy code thường có hai hàm đổi đơn vị: `MmToFeet()` và `FeetToMm()`.
- Các model như `GridModel`, `ColumnModel`, `BeamModel`, `SlabModel` chỉ là dữ liệu trung gian, chưa phải element Revit thật.

---

## 1. Luồng tổng quan trong MainViewModel.cs

`ViewModels/MainViewModel.cs` là nơi nối giao diện với toàn bộ phần xử lý.

### 1.1. Các biến dữ liệu chính

| Code | Ý nghĩa | Vì sao quan trọng | Liên quan |
|---|---|---|---|
| `_mappingService` | Service tự động gán layer CAD vào loại cấu kiện. | Quyết định layer nào là trục, cột, dầm, sàn. | `LayerMappingService`, `DwgLayer` |
| `_uiDoc` | Document giao diện Revit hiện tại. | Cho biết người dùng đang thao tác trên file Revit nào. | Revit API |
| `_doc` | Document Revit dùng để đọc và tạo element. | Hầu hết service đều cần `Document`. | Các service import, parsing, creation |
| `_currentDwgInstanceId` | Id của DWG đã import hoặc link trong Revit. | Giúp đọc lại đúng file CAD hiện tại. | `RevitDwgReaderService.FindDwgInstance()` |
| `_parsedGrids` | Danh sách lưới trục đã đọc từ CAD. | Là mốc tọa độ cho cột, dầm, sàn. | `GridModel`, `GridCreationService` |
| `_parsedColumns` | Danh sách cột đã đọc từ CAD. | Đầu vào để tạo Structural Column. | `ColumnModel`, `ColumnCreationService` |
| `_parsedBeams` | Danh sách dầm đã đọc từ CAD. | Đầu vào để tạo Structural Framing. | `BeamModel`, `BeamCreationService` |
| `_parsedSlabs` | Danh sách sàn đã đọc từ CAD. | Đầu vào để tạo Floor. | `SlabModel`, `SlabCreationService` |

### 1.2. Hàm `ImportDwg()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `if (_doc == null)` | Kiểm tra có document Revit đang mở không. | Không có document thì không thể import CAD. |
| `OpenFileDialog` | Mở hộp thoại chọn file `.dwg` hoặc `.dxf`. | Đây là đầu vào của chức năng import. |
| `dialog.ShowDialog() != true` | Nếu người dùng hủy chọn file thì dừng. | Tránh xử lý khi chưa có đường dẫn file. |
| `new CadImportService(_doc)` | Tạo service chuyên import CAD. | ViewModel không trực tiếp gọi quá nhiều Revit API. |
| `importService.ImportDwg(dialog.FileName)` | Import CAD vào Revit và trả về `ElementId`. | Id này dùng để đọc layer và geometry sau đó. |
| `Layers.Clear(); ResetCounts();` | Xóa dữ liệu cũ. | Tránh dùng nhầm dữ liệu của file CAD trước. |
| `SetStatus(...)` | Cập nhật trạng thái trên giao diện. | Người dùng biết import thành công hay lỗi. |

### 1.3. Hàm `ReadCad()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `SetStatus("Đang đọc...", Pending)` | Báo giao diện đang xử lý. | Đọc geometry có thể mất thời gian. |
| `Layers.Clear(); ResetCounts();` | Xóa kết quả đọc cũ. | Thống kê mới không bị lẫn với file trước. |
| `Task.Run(ReadDwgFromRevit)` | Chạy đọc CAD ở nền. | Giảm khả năng treo giao diện WPF. |
| `result == null` | Không tìm thấy DWG trong model. | Dừng sớm và báo lỗi rõ ràng. |
| `_parsedGrids = result.Grids` | Lưu lưới trục đọc được. | Dùng cho bước tạo Grid và canh tọa độ. |
| `_parsedColumns = result.Columns` | Lưu cột đọc được. | Dùng cho bước tạo cột. |
| `_parsedBeams = result.Beams` | Lưu dầm đọc được. | Dùng cho bước tạo dầm. |
| `_parsedSlabs = result.Slabs` | Lưu sàn đọc được. | Dùng cho bước tạo sàn. |
| `UpdateCounts()` | Cập nhật số lượng trục, cột, dầm, sàn. | Hiển thị thống kê lên giao diện. |

### 1.4. Hàm `ReadDwgFromRevit()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `new RevitDwgReaderService(_doc!)` | Tạo service đọc DWG từ Revit. | Đây là điểm vào để lấy layer và geometry. |
| `GetCurrentDwgInstance(reader)` | Lấy DWG hiện tại. | Có thể là CAD vừa import hoặc CAD đã có sẵn. |
| `reader.GetLayersFromInstance(instance)` | Lấy danh sách layer. | Cần layer để biết đâu là trục, cột, dầm, sàn. |
| `_mappingService.AutoMap(layers)` | Tự động gán loại cấu kiện theo tên layer. | Giảm thao tác chọn layer thủ công. |
| `ParseGeometryByLayer(...)` | Đọc geometry thành model trung gian. | Đây là bước phân tích CAD chính. |
| `result.Layers = layers` | Gắn danh sách layer vào kết quả. | UI dùng để hiển thị và cho phép sửa mapping. |

### 1.5. Hàm `ParseGeometryByLayer()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `gridLayerNames` | Lấy các layer được xem là lưới trục. | Đầu vào cho hàm đọc Grid. |
| `columnLayerNames` | Lấy các layer được xem là cột. | Đầu vào cho `ColumnReaderService`. |
| `beamLayerNames` | Lấy các layer được xem là dầm. | Đầu vào cho `BeamReaderService`. |
| `slabLayerNames` | Lấy các layer được xem là sàn. | Đầu vào cho `SlabReaderService`. |
| `reader.ReadGridLines(...)` | Đọc line/polyline trục từ CAD. | Tạo danh sách `GridModel`. |
| `reader.GetGeometryByLayer(instance)` | Lấy toàn bộ geometry đã gom theo layer. | Cột, dầm, sàn dùng chung dữ liệu này. |
| `ReadColumns(...)` | Đọc polyline hình chữ nhật thành cột. | Tạo `ColumnModel`. |
| `ReadBeams(...)` | Đọc cặp biên dầm và trục giữa thành dầm. | Tạo `BeamModel`. |
| `ReadSlabs(...)` | Đọc loop kín thành sàn. | Tạo `SlabModel`. |

### 1.6. Hàm `ConvertTo3D()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| Kiểm tra `_doc == null` | Không có document thì dừng. | Tạo element Revit bắt buộc cần `Document`. |
| Kiểm tra checkbox `CreateGrid`, `CreateColumn`, `CreateBeam`, `CreateSlab` | Xem người dùng muốn tạo loại cấu kiện nào. | Tránh chạy khi chưa chọn gì. |
| Kiểm tra `_parsedGrids.Count == 0` | Yêu cầu phải có lưới trục. | Lưới trục là mốc đặt tọa độ cho các cấu kiện. |
| `LevelCreationService.CreateOrUpdateLevels(...)` | Tạo/cập nhật Level trước. | Cột, dầm, sàn cần Level để đặt cao độ. |
| `GridCreationService.CreateGrids(...)` | Tạo Grid Revit. | Vẽ lưới trục. |
| Vòng lặp tạo cột theo tầng | Gọi `CreateColumns()` nhiều lần. | Một mặt bằng CAD được phát triển lên nhiều tầng. |
| Vòng lặp tạo dầm theo Level | Gọi `CreateBeams()` cho các Level. | Dầm được nhân theo tầng. |
| Vòng lặp tạo sàn theo Level | Gọi `CreateSlabs()` cho các Level. | Sàn được nhân theo tầng. |
| Tổng hợp `Created`, `Skipped`, `Failed` | Đếm kết quả tạo element. | Hiển thị thông báo cuối cùng. |

---

## 2. Import CAD: CadImportService.cs

File `Services/Import/CadImportService.cs` chịu trách nhiệm import file CAD vào Revit.

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `private readonly Document _doc` | Lưu document Revit hiện tại. | Mọi thao tác import phải thực hiện trong document này. |
| `CadImportService(Document doc)` | Constructor nhận document. | Nếu `doc` null thì báo lỗi ngay. |
| `ImportDwg(string filePath)` | Hàm import CAD chính. | Trả về `ElementId` của CAD vừa import. |
| `string.IsNullOrWhiteSpace(filePath)` | Kiểm tra đường dẫn rỗng. | Tránh gọi API với input sai. |
| `File.Exists(filePath)` | Kiểm tra file có tồn tại không. | Báo lỗi rõ nếu đường dẫn sai. |
| `GetImportView()` | Tìm view hợp lệ để import CAD. | Revit cần view để import. |
| `DWGImportOptions` | Cấu hình import CAD. | Ảnh hưởng đơn vị, vị trí, layer, màu. |
| `Placement = ImportPlacement.Origin` | Đặt CAD tại gốc tọa độ. | Giữ tọa độ CAD ổn định để đọc. |
| `Unit = ImportUnit.Millimeter` | Đọc CAD theo milimet. | Phù hợp với bản vẽ kết cấu. |
| `VisibleLayersOnly = false` | Import cả layer đang ẩn. | Không mất dữ liệu cần đọc. |
| `Transaction` | Mở giao dịch Revit. | Mọi thay đổi model phải nằm trong transaction. |
| `_doc.Import(...)` | Gọi Revit API để import CAD. | Đây là dòng tạo CAD element trong model. |
| `transaction.Commit()` | Xác nhận import thành công. | CAD tồn tại trong Revit sau dòng này. |

---

## 3. Đọc DWG và lưới trục: RevitDwgReaderService.cs

File `Services/Parsing/RevitDwgReaderService.cs` làm ba việc chính:

1. Tìm DWG đã import hoặc link trong Revit.
2. Lấy danh sách layer và geometry theo layer.
3. Đọc các line/polyline trên layer trục thành `GridModel`.

### 3.1. Hằng số

| Code | Ý nghĩa |
|---|---|
| `AxisToleranceDegrees = 10.0` | Line lệch ngang/dọc tối đa 10 độ vẫn được xem là trục. |
| `MinGridLengthMm = 300.0` | Line ngắn hơn 300 mm bị bỏ qua. |

### 3.2. Hàm `FindDwgInstance()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `FilteredElementCollector(_doc)` | Quét element trong Revit document. | Cách chuẩn để tìm element bằng Revit API. |
| `.OfClass(typeof(ImportInstance))` | Tìm CAD import trực tiếp. | DWG import thường là `ImportInstance`. |
| `FirstOrDefault()` | Lấy CAD đầu tiên tìm được. | Dự án hiện xử lý một CAD chính. |
| `.OfClass(typeof(RevitLinkInstance))` | Nếu không có import thì tìm link. | Hỗ trợ trường hợp file được link. |

### 3.3. Hàm `GetLayersFromInstance()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `GetGeometryByLayer(dwgInstance)` | Lấy geometry theo layer. | Dùng để đếm số đối tượng trên mỗi layer. |
| `dwgInstance.Category.SubCategories` | Lấy subcategory của CAD. | Revit thường lưu layer CAD dưới dạng subcategory. |
| `geometryByLayer.TryGetValue(...)` | Tìm geometry tương ứng layer. | Tính `EntityCount`. |
| `result.Add(new DwgLayer { ... })` | Tạo object mô tả layer. | UI dùng để hiển thị và mapping. |
| `OrderByDescending(l => l.EntityCount)` | Sắp xếp layer nhiều đối tượng lên trước. | Người dùng dễ nhìn layer quan trọng. |

### 3.4. Hàm `GetGeometryByLayer()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `Dictionary<string, List<GeometryObject>>` | Gom geometry theo tên layer. | Các reader cột, dầm, sàn dùng chung dữ liệu này. |
| `Options { DetailLevel = Fine }` | Đọc geometry chi tiết cao. | Giảm thiếu dữ liệu hình học. |
| `IncludeNonVisibleObjects = true` | Lấy cả object không hiển thị. | Tránh mất layer/đường đang ẩn. |
| `dwgInstance.get_Geometry(options)` | Lấy geometry của CAD. | Đây là nguồn dữ liệu hình học chính. |
| `ExtractGeometry(...)` | Tách geometry và layer. | Xử lý cả block/instance lồng nhau. |

### 3.5. Hàm `ExtractGeometry()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `case GeometryInstance` | Gặp block hoặc instance lồng trong CAD. | CAD thường có block nên cần đọc đệ quy. |
| `GetInstanceGeometry()` | Lấy geometry bên trong block. | Nếu bỏ qua, có thể mất nhiều đối tượng CAD. |
| `case Curve` | Gặp line/curve. | Trục, biên dầm, cạnh sàn có thể là curve. |
| `case PolyLine` | Gặp polyline. | Cột và sàn thường được vẽ bằng polyline. |
| `GetLayerName(obj)` | Lấy layer từ `GraphicsStyleId`. | Biết object CAD thuộc layer nào. |
| `AddToLayer(...)` | Thêm object vào dictionary theo layer. | Đây là bước gom dữ liệu để các reader xử lý. |

### 3.6. Hàm `ReadGridLines()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `HashSet<string>(gridLayerNames, ...)` | Tạo tập layer trục. | Tra cứu nhanh, không phân biệt hoa/thường. |
| `GetGeometryByLayer(dwgInstance)` | Lấy geometry CAD theo layer. | Nguồn để đọc trục. |
| `if (!layerSet.Contains(layerName)) continue` | Bỏ qua layer không phải trục. | Tránh đọc nhầm. |
| `ReadGridFromGeometry(...)` | Chuyển line/polyline thành `GridModel`. | Mỗi đoạn hợp lệ là một trục. |
| `NameAndSortGrids(...)` | Sắp xếp và đặt tên trục. | Tạo tên A/B/C hoặc 1/2/3 trước khi vẽ. |

### 3.7. Hàm `CreateGrid()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `TryGetAxisDirection(...)` | Kiểm tra line gần ngang hoặc gần dọc. | Lọc đường chéo, nét rác. |
| `ToPoint2D(...)` | Đổi `XYZ` Revit sang điểm 2D milimet. | Tính toán CAD dùng milimet. |
| `Distance(...)` | Tính chiều dài trục. | Loại trục quá ngắn. |
| `new GridModel { ... }` | Tạo model trung gian của trục. | Đầu vào cho `GridCreationService`. |

### 3.8. Đặt tên trục

| Code | Ý nghĩa |
|---|---|
| `xGrids = grids.Where(g => g.IsVertical)` | Tách nhóm trục dọc. |
| `yGrids = grids.Where(g => !g.IsVertical)` | Tách nhóm trục ngang. |
| `OrderBy(g => g.MidPoint.X)` | Sắp xếp trục dọc theo X. |
| `OrderBy(g => g.MidPoint.Y)` | Sắp xếp trục ngang theo Y. |
| `ApplyNames(...)` | Gán tên lần lượt cho từng trục. |
| `GridNameSequence` | Sinh chuỗi tên liên tiếp như A, B, C hoặc 1, 2, 3. |

---

## 4. Model lưới trục: GridModel.cs

| Property | Ý nghĩa | Dùng ở đâu |
|---|---|---|
| `Name` | Tên trục, ví dụ A, B, 1, 2. | `GridCreationService` gán vào `grid.Name`. |
| `LayerName` | Layer CAD gốc. | Debug và truy vết dữ liệu. |
| `StartPoint`, `EndPoint` | Hai đầu trục theo tọa độ CAD milimet. | Tạo line Revit. |
| `IsVertical` | Trục dọc hay ngang. | Sắp xếp, đặt tên, chống trùng. |
| `Length` | Chiều dài trục. | Kiểm tra hoặc hiển thị. |
| `MidPoint` | Trung điểm trục. | Sắp xếp và tạo key chống trùng. |
| `Point2D` | Điểm 2D dùng chung cho X/Y. | Dùng cho tất cả model cấu kiện. |

---

## 5. Vẽ lưới trục: GridCreationService.cs

File này nhận `GridModel` và tạo `Autodesk.Revit.DB.Grid`.

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `_doc` | Document Revit đang thao tác. | Grid được tạo vào document này. |
| `DuplicateToleranceMm = 50.0` | Sai số chống trùng trục. | Tránh tạo hai Grid cùng tọa độ. |
| `CreateGrids(...)` | Hàm tạo Grid chính. | Được gọi từ `ConvertTo3D()`. |
| `GetActiveLevel()` | Lấy Level đang dùng. | Grid cần cao độ để tạo line. |
| `GetExistingGridNames()` | Lấy tên Grid đã tồn tại. | Revit không cho trùng tên datum. |
| `GetExistingGridKeys()` | Lấy key tọa độ Grid đã có. | Chống trùng theo tọa độ. |
| `GridPlacement.From(gridModels)` | Tính khung bao và tâm CAD. | Dùng để đưa tọa độ CAD về quanh gốc Revit. |
| `CreateRevitLine(...)` | Đổi `GridModel` thành `Line` Revit. | Đây là hình học dùng để tạo Grid. |
| `Grid.Create(_doc, line)` | Tạo Grid Revit thật. | Dòng quan trọng nhất của service. |
| `grid.Name = model.Name` | Gán tên trục. | Tên hiển thị trên đầu Grid. |
| `grid.SetVerticalExtents(...)` | Đặt phạm vi đứng của Grid. | Giúp Grid hiện qua các Level. |
| `UpdateLevelExtentsInElevationViews(...)` | Điều chỉnh chiều dài Level trong view đứng. | Làm Level khớp phạm vi lưới trục. |

---

## 6. Đọc cột: ColumnReaderService.cs

File này đọc polyline hình chữ nhật trên layer cột và tạo `ColumnModel`.

### 6.1. Hằng số

| Hằng số | Ý nghĩa |
|---|---|
| `PointToleranceMm = 20.0` | Sai số khi so sánh điểm. |
| `OrthogonalToleranceDegrees = 10.0` | Sai số khi kiểm tra góc vuông. |
| `MinColumnSideMm = 100.0` | Cạnh cột nhỏ nhất được chấp nhận. |
| `MaxColumnSideMm = 3000.0` | Cạnh cột lớn nhất được chấp nhận. |

### 6.2. Hàm `ReadColumns()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `geometryByLayer` | Geometry CAD đã gom theo layer. | Nguồn dữ liệu đầu vào. |
| `columnLayerNames` | Danh sách layer cột. | Chỉ đọc đúng layer cột. |
| `HashSet<string>` | Tập layer để tra cứu nhanh. | Không phân biệt hoa/thường. |
| `ReadColumnFromGeometry(...)` | Thử đọc một geometry thành cột. | Chỉ polyline hợp lệ mới tạo model. |
| `Deduplicate(columns)` | Loại cột trùng. | CAD có thể có block hoặc nét lặp. |

### 6.3. Hàm `ReadColumnFromGeometry()`

| Code | Ý nghĩa |
|---|---|
| `geometryObject is not PolyLine` | Chỉ nhận polyline. |
| `ToClosedPointList(polyLine)` | Đổi polyline thành danh sách điểm 2D và bỏ điểm trùng. |
| `points.Count != 4` | Cột phải có 4 đỉnh. |
| `TryReadRectangle(...)` | Kiểm tra 4 điểm có tạo hình chữ nhật không. |
| `new ColumnModel { ... }` | Tạo model cột gồm tâm, rộng, cao, góc xoay. |

### 6.4. Hàm `TryReadRectangle()`

| Code | Ý nghĩa |
|---|---|
| Tạo danh sách `edges` | Mỗi cạnh gồm điểm đầu, điểm cuối, chiều dài, góc. |
| Kiểm tra chiều dài cạnh | Loại cạnh quá nhỏ hoặc quá lớn. |
| `AreOppositeSidesEqual(...)` | Hai cạnh đối phải gần bằng nhau. |
| `IsRightAngle(...)` | Các cạnh liền kề phải gần vuông góc. |
| `TryGetAxisAlignedDimensions(...)` | Lấy width và height của cột. |
| `center = Average(X/Y)` | Tâm cột là trung bình tọa độ 4 đỉnh. |

---

## 7. Model cột: ColumnModel.cs

| Property | Ý nghĩa | Dùng ở đâu |
|---|---|---|
| `LayerName` | Layer CAD gốc. | Debug nguồn dữ liệu. |
| `CenterPoint` | Tâm cột theo tọa độ CAD milimet. | Vị trí đặt FamilyInstance. |
| `Width` | Bề rộng tiết diện cột. | Tạo type cột. |
| `Height` | Chiều cao/cạnh còn lại của tiết diện. | Tạo type cột. |
| `RotationDegrees` | Góc xoay cột. | `ColumnCreationService.RotateColumn()`. |
| `PrimaryAxis` | Cho biết cạnh nào lớn hơn. | Chủ yếu để mô tả/debug. |

---

## 8. Vẽ cột: ColumnCreationService.cs

File này nhận `ColumnModel`, snap tâm cột vào giao điểm lưới trục, tạo type cột đúng kích thước, rồi tạo `FamilyInstance` cột.

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `DefaultColumnFamilyName` | Tên family cột mặc định. | Nếu chưa load family này thì không tạo được cột. |
| `GridSnapToleranceMm = 20.0` | Sai số snap vào giao điểm trục. | Đảm bảo cột nằm đúng trục. |
| `CreateColumns(...)` | Hàm tạo cột chính. | Được gọi theo từng tầng. |
| `FindBaseColumnSymbol()` | Tìm family symbol cột mẫu. | Cần symbol để tạo instance. |
| `ColumnPlacement.From(gridModels)` | Lấy danh sách tọa độ trục dọc/ngang. | Dùng để snap tâm cột. |
| `TryResolveColumnPoint(...)` | Tìm giao điểm trục gần tâm cột nhất. | Nếu lệch quá 20 mm thì bỏ qua. |
| `GetOrCreateColumnType(...)` | Lấy hoặc tạo type cột theo kích thước. | Cột 300x500 cần type riêng. |
| `symbol.Activate()` | Kích hoạt symbol nếu cần. | Revit yêu cầu symbol active trước khi tạo instance. |
| `NewFamilyInstance(...)` | Tạo cột Revit thật. | Dòng tạo element chính. |
| `SetColumnHeight(...)` | Gán base level, top level và offset. | Cột cao đúng tầng. |
| `RotateColumn(...)` | Xoay cột nếu có góc xoay. | Hỗ trợ cột không song song trục. |
| `MarkGeneratedColumn(...)` | Ghi marker vào Comments. | Biết cột nào do add-in tạo. |

---

## 9. Đọc dầm: BeamReaderService.cs

File này đọc dầm theo logic:

```text
2 đường biên dầm song song
    + khoảng cách giữa 2 biên bằng bề rộng dầm
    + có đường trục nằm giữa
    -> tạo BeamModel theo đường tim dầm
```

### 9.1. Hằng số

| Hằng số | Ý nghĩa |
|---|---|
| `MinBeamLengthMm = 300.0` | Dầm ngắn hơn 300 mm bị bỏ. |
| `AxisToleranceDegrees = 10.0` | Sai số để xem line gần ngang/dọc/song song. |
| `DuplicateToleranceMm = 20.0` | Sai số loại dầm trùng. |
| `BeamWidthToleranceMm = 10.0` | Sai số khi so khoảng cách hai biên với bề rộng dầm. |

### 9.2. Hàm `ReadBeams()`

| Code | Ý nghĩa |
|---|---|
| `beamLayerNames` | Layer chứa biên dầm. |
| `axisLayerNames` | Layer chứa trục/tim dầm. |
| `defaultWidth`, `defaultHeight` | Kích thước dầm nhập từ UI. |
| `beamSegments` | Các đoạn biên dầm. |
| `axisSegments` | Các đoạn trục dùng để xác nhận tim dầm. |
| `ReadBeamSegments(...)` | Tách line/polyline thành các đoạn thẳng. |
| `DetectBeamsFromBoundariesAndAxes(...)` | Nhận dạng dầm từ biên và trục. |
| `Deduplicate(...)` | Loại dầm trùng. |

### 9.3. Hàm `DetectBeamsFromBoundariesAndAxes()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `axisSegments.Count == 0` | Không có trục thì không đọc dầm. | Thiết kế hiện tại cần trục giữa để xác nhận dầm. |
| Hai vòng `for` | Thử mọi cặp biên dầm. | Dầm được nhận từ 2 biên song song. |
| `AreParallel(...)` | Hai biên phải song song. | Điều kiện cơ bản của dầm. |
| `TryGetParallelDistance(...)` | Tính khoảng cách vuông góc giữa hai biên. | Khoảng cách này phải bằng bề rộng dầm. |
| `HasEnoughOverlap(...)` | Hai biên phải chồng nhau đủ dài. | Tránh ghép hai line không liên quan. |
| `CreateCenterLine(...)` | Tạo đường tim giữa hai biên. | Đây là line để tạo Beam Revit. |
| `FindBestMiddleAxis(...)` | Tìm axis nằm giữa hai biên. | Xác nhận đây là dầm thật. |
| `new BeamModel { ... }` | Tạo model dầm. | Đầu vào cho `BeamCreationService`. |

---

## 10. Model dầm: BeamModel.cs

| Property | Ý nghĩa | Dùng ở đâu |
|---|---|---|
| `LayerName` | Layer CAD gốc. | Debug nguồn dầm. |
| `StartPoint`, `EndPoint` | Hai đầu đường tim dầm. | Tạo line FamilyInstance beam. |
| `CenterPoint` | Trung điểm dầm. | Thông báo lỗi, debug, chống trùng. |
| `Width`, `Height` | Kích thước tiết diện dầm. | Tạo type dầm. |
| `RotationDegrees` | Góc dầm trên mặt bằng. | Dùng để mô tả/debug. |
| `DimensionText` | Text kích thước nếu có. | Hiện tại chưa dùng nhiều. |
| `SourceType` | Cách nhận dạng dầm. | Ví dụ `BoundaryPairWithAxis`. |
| `Length` | Chiều dài dầm. | Bỏ qua dầm quá ngắn. |

---

## 11. Vẽ dầm: BeamCreationService.cs

File này nhận `BeamModel`, tạo type dầm đúng kích thước, rồi tạo Structural Framing theo đường tim dầm.

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `DefaultBeamFamilyName` | Tên family dầm mặc định. | Nếu chưa load family thì không tạo được dầm. |
| `CreateBeams(...)` | Hàm tạo dầm chính. | Được gọi theo Level. |
| `FindBaseBeamSymbol()` | Tìm family symbol dầm mẫu. | Cần symbol để tạo instance. |
| `BeamPlacement.From(gridModels)` | Tính tâm CAD theo lưới trục. | Đưa tọa độ CAD về hệ Revit. |
| `GetExistingBeamKeys()` | Lấy key các dầm đã có. | Chống tạo trùng. |
| `GetOrCreateBeamType(...)` | Lấy hoặc tạo type theo width/height. | Quản lý kích thước dầm. |
| `placement.ToRevitPoint(...)` | Đổi điểm CAD sang `XYZ` Revit. | Chuyển milimet sang feet và trừ tâm CAD. |
| `Line.CreateBound(start, end)` | Tạo line tim dầm. | Beam Revit được đặt theo line này. |
| `NewFamilyInstance(line, symbol, level, StructuralType.Beam)` | Tạo dầm Revit thật. | Dòng tạo element chính. |
| `SetBeamOffsets(...)` | Gán Level và offset Z. | Dầm nằm đúng cao độ. |
| `MarkGeneratedBeam(...)` | Ghi marker vào Comments. | Biết dầm nào do add-in tạo. |

---

## 12. Đọc sàn: SlabReaderService.cs

File này đọc sàn theo hai cách:

1. Đọc polyline đã khép kín thành loop sàn.
2. Ghép các line/polyline rời thành loop kín.

Sau đó code xác định loop nào là biên ngoài, loop nào là lỗ mở.

### 12.1. Hằng số

| Hằng số | Ý nghĩa |
|---|---|
| `PointToleranceMm = 20.0` | Sai số khi so sánh điểm. |
| `MinSegmentLengthMm = 100.0` | Đoạn ngắn hơn 100 mm bị bỏ. |
| `MinLoopAreaMm2 = 100000.0` | Loop quá nhỏ bị bỏ. |

### 12.2. Hàm `ReadSlabs()`

| Code | Ý nghĩa |
|---|---|
| `geometryByLayer` | Geometry CAD đã gom theo layer. |
| `slabLayerNames` | Danh sách layer sàn. |
| `thicknessMm` | Chiều dày sàn nhập từ UI. |
| `ReadClosedPolylineLoops(...)` | Đọc polyline kín thành loop. |
| `ReadSegments(...)` | Tách line/polyline thành segment. |
| `BuildLoopsFromSegments(...)` | Ghép segment thành loop kín. |
| `DeduplicateLoops(...)` | Loại loop trùng. |
| `BuildSlabsFromLoops(...)` | Tạo `SlabModel` và nhận diện opening. |

### 12.3. Hàm `ReadClosedPolylineLoops()`

| Code | Ý nghĩa |
|---|---|
| `geometryObject is not PolyLine` | Chỉ xử lý polyline. |
| `NormalizeLoop(...)` | Xóa điểm trùng liên tiếp. |
| `IsClosed(points)` | Kiểm tra loop kín. |
| `points.RemoveAt(points.Count - 1)` | Bỏ điểm cuối trùng điểm đầu. |
| `GetSignedArea(points)` | Tính diện tích polygon. |
| `new SlabLoop(...)` | Tạo loop nội bộ để xử lý tiếp. |

### 12.4. Hàm `BuildLoopsFromSegments()`

| Code | Ý nghĩa |
|---|---|
| `unused = segments.ToList()` | Danh sách segment chưa dùng. |
| Chọn segment đầu tiên | Bắt đầu tạo một loop. |
| Tìm segment nối với `current` | Ghép các đoạn đầu-cuối. |
| `IsClosed(points)` | Nếu không khép kín thì bỏ. |
| `GetSignedArea(points)` | Tính diện tích loop. |
| `loops.Add(...)` | Lưu loop hợp lệ. |

### 12.5. Hàm `BuildSlabsFromLoops()`

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `OrderByDescending(l => l.Area)` | Loop lớn xét trước. | Loop lớn thường là biên ngoài. |
| `usedAsOpening` | Ghi nhớ loop đã dùng làm lỗ mở. | Tránh tạo lỗ mở thành sàn riêng. |
| `candidate.Area < outer.Area` | Opening phải nhỏ hơn biên ngoài. | Điều kiện hình học cơ bản. |
| `IsPointInsidePolygon(...)` | Kiểm tra tâm loop nằm trong biên ngoài. | Xác định opening. |
| `new SlabModel { ... }` | Tạo model sàn. | Đầu vào cho `SlabCreationService`. |

---

## 13. Model sàn: SlabModel.cs

| Property | Ý nghĩa | Dùng ở đâu |
|---|---|---|
| `LayerName` | Layer CAD gốc. | Debug nguồn sàn. |
| `OuterLoop` | Vòng biên ngoài của sàn. | Bắt buộc để tạo Floor. |
| `OpeningLoops` | Danh sách lỗ mở. | Tạo profile Floor có opening. |
| `Thickness` | Chiều dày sàn. | Tạo FloorType. |
| `Area` | Diện tích sàn sau khi trừ opening. | Thống kê/debug. |
| `CenterPoint` | Tâm sàn. | Thông báo lỗi, chống trùng. |

---

## 14. Vẽ sàn: SlabCreationService.cs

File này nhận `SlabModel`, tạo FloorType đúng chiều dày, rồi tạo `Floor` theo profile biên ngoài và lỗ mở.

| Code | Ý nghĩa | Vì sao quan trọng |
|---|---|---|
| `FindBaseFloorType()` | Tìm FloorType mẫu. | Cần type mẫu để duplicate. |
| `GetOrCreateFloorType(...)` | Lấy hoặc tạo FloorType theo chiều dày. | Sàn 130 mm, 150 mm cần type riêng. |
| `TrySetFloorThickness(...)` | Set chiều dày FloorType. | Có thể set parameter hoặc compound structure. |
| `SlabPlacement.From(gridModels)` | Tính tâm CAD theo lưới trục. | Đưa tọa độ CAD về hệ Revit. |
| `BuildProfile(...)` | Tạo danh sách `CurveLoop`. | `Floor.Create` cần profile kín. |
| `BuildCurveLoop(...)` | Nối các điểm thành line Revit. | Nếu loop hở, Revit sẽ lỗi. |
| `Floor.Create(_doc, profile, floorType.Id, level.Id)` | Tạo Floor Revit thật. | Dòng tạo element chính. |
| `SetSlabOffset(...)` | Set offset sàn so với Level. | Đặt đúng cao độ sàn. |
| `MarkGeneratedSlab(...)` | Ghi marker vào Comments. | Biết sàn nào do add-in tạo. |

---

## 15. Các file kết quả tạo element

Các file:

- `GridCreationResult.cs`
- `ColumnCreationResult.cs`
- `BeamCreationResult.cs`
- `SlabCreationResult.cs`
- `LevelCreationResult.cs`

| Property | Ý nghĩa |
|---|---|
| `Created` | Số element tạo thành công. |
| `Skipped` | Số element bị bỏ qua do trùng hoặc không hợp lệ. |
| `Failed` | Số element bị lỗi khi tạo. |
| `CreatedElementIds` | Id các element mới tạo. |
| `Messages` | Thông báo chi tiết để debug. |

---

## 16. Quan hệ giữa các file

```text
MainViewModel
    -> CadImportService.ImportDwg()
    -> RevitDwgReaderService.GetLayersFromInstance()
    -> RevitDwgReaderService.ReadGridLines()
    -> ColumnReaderService.ReadColumns()
    -> BeamReaderService.ReadBeams()
    -> SlabReaderService.ReadSlabs()
    -> LevelCreationService.CreateOrUpdateLevels()
    -> GridCreationService.CreateGrids()
    -> ColumnCreationService.CreateColumns()
    -> BeamCreationService.CreateBeams()
    -> SlabCreationService.CreateSlabs()
```

| Chức năng | File đọc | Model trung gian | File vẽ | Element Revit tạo ra |
|---|---|---|---|---|
| Lưới trục | `RevitDwgReaderService.cs` | `GridModel` | `GridCreationService.cs` | `Grid` |
| Cột | `ColumnReaderService.cs` | `ColumnModel` | `ColumnCreationService.cs` | Structural Column |
| Dầm | `BeamReaderService.cs` | `BeamModel` | `BeamCreationService.cs` | Structural Framing |
| Sàn | `SlabReaderService.cs` | `SlabModel` | `SlabCreationService.cs` | `Floor` |
| Level | Input từ UI | Không có model riêng | `LevelCreationService.cs` | `Level` |

---

## 17. Điểm cần nhớ

1. Chương trình không tạo Revit element trực tiếp từ CAD ngay, mà đọc CAD thành model trung gian trước.
2. Lưới trục rất quan trọng vì là mốc tọa độ cho cột, dầm, sàn.
3. Cột chỉ được tạo khi tâm cột snap được vào giao điểm lưới trục.
4. Dầm được nhận dạng từ hai biên song song và một trục nằm giữa.
5. Sàn được nhận dạng từ loop kín; loop nhỏ nằm trong loop lớn được xem là lỗ mở.
6. Mọi thao tác tạo/sửa element Revit đều nằm trong `Transaction`.
7. Tọa độ được tính bằng milimet khi đọc CAD và đổi sang feet khi tạo element Revit.
8. Các service creation đều có cơ chế chống trùng.
9. Cột, dầm, sàn được đánh dấu trong `Comments = "AutoCADToRevitApplication"` để nhận biết element do add-in tạo.
10. Nếu chưa load family cột/dầm mặc định, chương trình sẽ báo lỗi và không tạo được cấu kiện tương ứng.
