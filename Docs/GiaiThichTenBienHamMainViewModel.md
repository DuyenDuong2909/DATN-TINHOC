# Giải thích tên biến và tên hàm trong MainViewModel

Tài liệu này giải thích ý nghĩa tiếng Việt của các tên biến, tên hàm trong `ViewModels/MainViewModel.cs`.

`MainViewModel` là lớp trung gian giữa giao diện `MainWindow.xaml` và phần xử lý Revit/CAD. Giao diện binding vào các property trong `MainViewModel`; khi người dùng bấm nút, các command trong `MainViewModel` sẽ chạy.

## 1. Tên lớp chính

| Tên | Ý nghĩa tiếng Việt | Dùng để làm gì |
|---|---|---|
| `MainViewModel` | ViewModel chính của màn hình chính | Chứa dữ liệu hiển thị trên UI và xử lý các nút Import, Đọc CAD, Gán Layer, Chuyển đổi 3D |
| `ObservableObject` | Đối tượng có khả năng thông báo thay đổi | Giúp UI tự cập nhật khi property thay đổi |

## 2. Biến private lưu trạng thái nội bộ

| Tên biến | Ý nghĩa tiếng Việt | Giải thích |
|---|---|---|
| `_mappingService` | dịch vụ gán layer | Dùng để tự nhận diện layer CAD thuộc loại Grid, Column, Beam, Slab |
| `_uiDoc` | tài liệu Revit phía giao diện | Đại diện cho file Revit đang mở ở phía UI: selection, active view, show element |
| `_doc` | tài liệu Revit phía dữ liệu/model | Đại diện cho dữ liệu thật trong file Revit: đọc element, tạo Grid, Column, Beam, Floor |
| `_currentDwgInstanceId` | id của file DWG hiện tại trong Revit | Lưu `ElementId` của DWG đã import để lần sau tìm lại nhanh |
| `_parsedGrids` | danh sách lưới trục đã đọc | Lưu các `GridModel` đọc được từ CAD |
| `_parsedColumns` | danh sách cột đã đọc | Lưu các `ColumnModel` đọc được từ CAD |
| `_parsedBeams` | danh sách dầm đã đọc | Lưu các `BeamModel` đọc được từ CAD |
| `_parsedSlabs` | danh sách sàn đã đọc | Lưu các `SlabModel` đọc được từ CAD |

Dấu `_` ở đầu tên biến thường dùng cho field private của class.

Ví dụ:

```csharp
private Document? _doc;
```

Nghĩa là `_doc` chỉ dùng bên trong `MainViewModel`, và có thể bằng `null`.

## 3. Event

| Tên | Ý nghĩa tiếng Việt | Giải thích |
|---|---|---|
| `RequestClose` | yêu cầu đóng cửa sổ | Khi chuyển đổi 3D xong, ViewModel phát event này để `MainWindow` đóng lại |

## 4. Các property binding ra giao diện

Các dòng có dạng:

```csharp
[ObservableProperty] private string _statusMessage = "...";
```

Sẽ tự sinh property public:

```csharp
public string StatusMessage { get; set; }
```

Vì vậy XAML có thể binding:

```xml
Content="{Binding StatusMessage}"
```

| Field private | Property sinh ra | Ý nghĩa tiếng Việt | Dùng trên giao diện |
|---|---|---|---|
| `_dwgFileName` | `DwgFileName` | tên file DWG | Hiển thị tên file CAD đang đọc |
| `_isFileLoaded` | `IsFileLoaded` | đã đọc file CAD chưa | Bật/tắt nút chuyển đổi 3D |
| `_statusMessage` | `StatusMessage` | thông báo trạng thái | Hiển thị dòng báo lỗi/thành công trên đầu form |
| `_statusColor` | `StatusColor` | màu thông báo | Đổi màu chữ thông báo theo Info, Success, Error, Pending |
| `_layers` | `Layers` | danh sách layer CAD | Hiển thị trong bảng Layer CAD |
| `_gridCount` | `GridCount` | số lưới trục đọc được | Hiển thị phần thống kê |
| `_gridLayerCount` | `GridLayerCount` | số layer lưới trục | Hiển thị số layer được gán là lưới trục |
| `_columnCount` | `ColumnCount` | số cột đọc được | Hiển thị phần thống kê |
| `_beamCount` | `BeamCount` | số dầm đọc được | Hiển thị phần thống kê |
| `_slabCount` | `SlabCount` | số sàn đọc được | Hiển thị phần thống kê |
| `_floorHeight` | `FloorHeight` | chiều cao tầng 1 | Người dùng nhập trên form |
| `_typicalHeight` | `TypicalHeight` | chiều cao tầng điển hình | Người dùng nhập trên form |
| `_numberOfFloors` | `NumberOfFloors` | số tầng | Người dùng nhập trên form |
| `_columnBaseOffset` | `ColumnBaseOffset` | offset chân cột | Dịch điểm bắt đầu cột theo phương Z |
| `_columnTopOffset` | `ColumnTopOffset` | offset đầu cột | Dịch điểm kết thúc cột theo phương Z |
| `_slabThickness` | `SlabThickness` | độ dày sàn | Dùng khi tạo sàn |
| `_beamWidth` | `BeamWidth` | bề rộng dầm | Dùng khi đọc/tạo dầm |
| `_beamHeight` | `BeamHeight` | chiều cao dầm | Dùng khi đọc/tạo dầm |
| `_beamZOffset` | `BeamZOffset` | offset cao độ dầm/sàn | Dịch dầm/sàn lên hoặc xuống so với level |
| `_beamLevelNames` | `BeamLevelNames` | danh sách tên level cho dầm/sàn | Đổ dữ liệu vào ComboBox chọn level |
| `_selectedBeamLevelName` | `SelectedBeamLevelName` | level đang chọn cho dầm/sàn | Cho biết bắt đầu tạo dầm/sàn từ level nào |
| `_xStartName` | `XStartName` | tên trục X bắt đầu | Ví dụ `A` |
| `_yStartName` | `YStartName` | tên trục Y bắt đầu | Ví dụ `1` |
| `_xNamingDirection` | `XNamingDirection` | hướng đặt tên trục X | Trái sang phải hoặc phải sang trái |
| `_yNamingDirection` | `YNamingDirection` | hướng đặt tên trục Y | Dưới lên trên hoặc trên xuống dưới |
| `_createGrid` | `CreateGrid` | có tạo lưới trục không | Checkbox trên UI |
| `_createColumn` | `CreateColumn` | có tạo cột không | Checkbox trên UI |
| `_createBeam` | `CreateBeam` | có tạo dầm không | Checkbox trên UI |
| `_createSlab` | `CreateSlab` | có tạo sàn không | Checkbox trên UI |

## 5. Các hàm chính

| Tên hàm | Ý nghĩa tiếng Việt | Dùng để làm gì |
|---|---|---|
| `Initialize` | khởi tạo ViewModel | Nhận `UIApplication`, lấy `_uiDoc`, `_doc`, nạp level và tự tìm DWG |
| `LoadLevelOptions` | nạp danh sách level | Đọc các `Level` trong Revit để đưa vào ComboBox chọn level dầm/sàn |
| `AutoDetectDwg` | tự phát hiện DWG | Tìm DWG đã import sẵn trong model Revit |
| `ImportDwg` | import file DWG | Mở hộp thoại chọn file CAD và import vào Revit |
| `ReadCad` | đọc CAD | Đọc layer và hình học CAD từ DWG trong Revit |
| `ApplyLayer` | áp dụng gán layer | Đọc lại dữ liệu theo loại layer người dùng đã chỉnh trong bảng |
| `ConvertTo3D` | chuyển đổi sang 3D | Tạo Level, Grid, Column, Beam, Slab trong Revit |
| `ReadDwgFromRevit` | đọc DWG từ Revit | Lấy DWG hiện tại, đọc layer, tự map layer, parse hình học |
| `ParseGeometryByLayer` | phân tích hình học theo layer | Tách layer thành nhóm lưới, cột, dầm, sàn rồi gọi các service đọc tương ứng |
| `ReparseCurrentLayerMapping` | đọc lại theo mapping hiện tại | Khi người dùng đổi loại layer, hàm này parse lại dữ liệu CAD |
| `FocusCreatedElements` | đưa camera tới phần tử đã tạo | Chuyển view nếu cần và zoom tới các element vừa tạo |
| `FindPlanViewForActiveLevel` | tìm view mặt bằng theo level hiện tại | Tìm `ViewPlan` tương ứng với level của active view |
| `GetLevelIndex` | lấy vị trí level trong danh sách | Tìm index của level theo tên |
| `UpdateCounts` | cập nhật số lượng thống kê | Đếm Grid, Column, Beam, Slab sau khi đọc CAD |
| `ResetCounts` | xóa thống kê | Reset số lượng về 0 và xóa dữ liệu đã parse |
| `GetCurrentDwgInstance` | lấy DWG hiện tại | Ưu tiên lấy theo `_currentDwgInstanceId`, nếu không có thì tự tìm |
| `IsGridLayer` | kiểm tra layer lưới trục | Xem layer có phải Grid/Axis/Trục không |
| `IsColumnLayer` | kiểm tra layer cột | Xem layer có phải Column/Cột không |
| `IsBeamLayer` | kiểm tra layer dầm cơ bản | Xem layer có phải Beam/Dầm không |
| `IsBeamLayerRobust` | kiểm tra layer dầm linh hoạt hơn | Nhận cả các layer bắt đầu bằng `D`, `DAM`, `BEAM` |
| `IsSlabLayerRobust` | kiểm tra layer sàn linh hoạt hơn | Nhận layer bắt đầu bằng `S`, `SAN`, `SLAB`, `FLOOR` |
| `NormalizeText` | chuẩn hóa chữ | Bỏ dấu tiếng Việt, trim, chuyển sang chữ hoa để so sánh dễ hơn |
| `ParsePositiveDouble` | đổi chuỗi sang số thực dương | Nếu nhập sai hoặc <= 0 thì dùng giá trị dự phòng |
| `ParseDouble` | đổi chuỗi sang số thực | Nếu nhập sai thì dùng giá trị dự phòng |
| `ParsePositiveInt` | đổi chuỗi sang số nguyên dương | Dùng cho số tầng |
| `SetStatus` | đặt thông báo trạng thái | Cập nhật `StatusMessage` và `StatusColor` |

## 6. Ý nghĩa các tham số quan trọng

| Tên tham số | Ý nghĩa tiếng Việt | Nằm trong hàm |
|---|---|---|
| `uiApp` | ứng dụng Revit phía UI | `Initialize` |
| `preferredLevelName` | tên level muốn ưu tiên giữ lại | `LoadLevelOptions` |
| `reader` | đối tượng đọc DWG/Revit | `ParseGeometryByLayer`, `GetCurrentDwgInstance` |
| `instance` | đối tượng DWG import trong Revit | `ParseGeometryByLayer` |
| `layers` | danh sách layer CAD | `ParseGeometryByLayer` |
| `createdElementIds` | danh sách id element vừa tạo | `FocusCreatedElements` |
| `levels` | danh sách level Revit | `GetLevelIndex` |
| `levelName` | tên level cần tìm | `GetLevelIndex` |
| `layer` | một layer CAD | `IsGridLayer`, `IsColumnLayer`, `IsBeamLayer`, `IsSlabLayerRobust` |
| `value` | chuỗi cần chuẩn hóa hoặc parse | `NormalizeText`, `ParseDouble`, `ParsePositiveDouble`, `ParsePositiveInt` |
| `fallback` | giá trị dự phòng | Các hàm `Parse...` |
| `message` | nội dung thông báo | `SetStatus` |
| `type` | loại thông báo | `SetStatus` |

### Giải thích riêng `preferredLevelName`

```csharp
private void LoadLevelOptions(string? preferredLevelName = null)
```

`preferredLevelName` nghĩa là **tên level được ưu tiên chọn lại**.

Ví dụ trước khi tạo level, người dùng đang chọn:

```text
Level 2
```

Sau khi tạo/cập nhật lại danh sách level, ComboBox bị nạp lại. Nếu không lưu tên level cũ, chương trình có thể tự nhảy về `Level 1`. Vì vậy code truyền `preferredLevelName` để cố gắng chọn lại level cũ.

Trong hàm:

```csharp
var currentSelection = string.IsNullOrWhiteSpace(preferredLevelName)
    ? SelectedBeamLevelName
    : preferredLevelName;
```

Nghĩa là:

- nếu có `preferredLevelName` thì dùng nó;
- nếu không có thì dùng `SelectedBeamLevelName` hiện tại.

## 7. Biến cục bộ quan trọng trong `ConvertTo3D`

| Tên biến | Ý nghĩa tiếng Việt | Giải thích |
|---|---|---|
| `numberOfFloors` | số tầng | Lấy từ textbox `NumberOfFloors` |
| `firstFloorHeight` | chiều cao tầng 1 | Lấy từ `FloorHeight` |
| `typicalFloorHeight` | chiều cao tầng điển hình | Lấy từ `TypicalHeight` |
| `baseOffset` | offset chân cột | Lấy từ `ColumnBaseOffset` |
| `topOffset` | offset đầu cột | Lấy từ `ColumnTopOffset` |
| `zOffset` | offset dầm/sàn | Lấy từ `BeamZOffset` |
| `selectedBeamSlabLevelName` | level dầm/sàn đang chọn trước khi reload | Dùng để chọn lại level sau khi tạo/cập nhật level |
| `levelService` | service tạo level | Gọi `CreateOrUpdateLevels` |
| `levelResult` | kết quả tạo/cập nhật level | Chứa số level tạo mới, cập nhật, lỗi, danh sách level |
| `beamSlabStartIndex` | vị trí level bắt đầu tạo dầm/sàn | Tìm theo `SelectedBeamLevelName` |
| `gridService` | service tạo lưới trục | Tạo Grid và ẩn DWG |
| `createdElementIds` | id các element vừa tạo | Dùng để zoom/focus sau khi tạo xong |
| `gridCreated` | số lưới trục đã tạo | Thống kê kết quả |
| `gridSkipped` | số lưới trục bị bỏ qua | Thống kê kết quả |
| `columnCreated` | số cột đã tạo | Thống kê kết quả |
| `columnSkipped` | số cột bị bỏ qua | Thống kê kết quả |
| `beamCreated` | số dầm đã tạo | Thống kê kết quả |
| `beamSkipped` | số dầm bị bỏ qua | Thống kê kết quả |
| `slabCreated` | số sàn đã tạo | Thống kê kết quả |
| `slabSkipped` | số sàn bị bỏ qua | Thống kê kết quả |
| `floorIndex` | chỉ số tầng đang lặp | Dùng khi tạo cột từng tầng |
| `levelIndex` | chỉ số level đang lặp | Dùng khi tạo dầm/sàn theo level |
| `beamSlabEndIndex` | vị trí level kết thúc tạo dầm/sàn | Giới hạn số level cần tạo dầm/sàn |
| `columnService` | service tạo cột | Gọi `CreateColumns` |
| `columnResult` | kết quả tạo cột | Chứa Created, Skipped, Failed |
| `beamService` | service tạo dầm | Gọi `CreateBeams` |
| `beamResult` | kết quả tạo dầm | Chứa Created, Skipped, Failed |
| `slabService` | service tạo sàn | Gọi `CreateSlabs` |
| `slabResult` | kết quả tạo sàn | Chứa Created, Skipped, Failed |

## 8. Biến cục bộ trong phần đọc CAD

| Tên biến | Ý nghĩa tiếng Việt | Giải thích |
|---|---|---|
| `dialog` | hộp thoại chọn file | Dùng trong `ImportDwg` để chọn DWG/DXF |
| `importService` | service import CAD | Thực hiện import file CAD vào Revit |
| `result` | kết quả đọc CAD | Chứa layer, grid, column, beam, slab |
| `reader` | service đọc DWG trong Revit | Đọc layer, geometry, DWG instance |
| `instance` | DWG instance | Đối tượng DWG đã import trong Revit |
| `layerList` | danh sách layer dạng List | Chuyển từ `IEnumerable` sang `List` để lọc nhiều lần |
| `gridLayerNames` | tên các layer lưới trục | Truyền vào hàm đọc grid |
| `columnLayerNames` | tên các layer cột | Truyền vào hàm đọc cột |
| `beamLayerNames` | tên các layer dầm | Truyền vào hàm đọc dầm |
| `slabLayerNames` | tên các layer sàn | Truyền vào hàm đọc sàn |
| `grids` | dữ liệu lưới trục đọc được | Danh sách `GridModel` |
| `geometryByLayer` | hình học CAD nhóm theo layer | Dictionary: tên layer -> danh sách geometry |
| `columns` | dữ liệu cột đọc được | Danh sách `ColumnModel` |
| `beams` | dữ liệu dầm đọc được | Danh sách `BeamModel` |
| `slabs` | dữ liệu sàn đọc được | Danh sách `SlabModel` |

## 9. Kiểu enum và class phụ

### `StatusType`

```csharp
private enum StatusType { Info, Success, Error, Pending }
```

| Giá trị | Ý nghĩa tiếng Việt | Màu đang dùng |
|---|---|---|
| `Info` | thông tin bình thường | xám xanh |
| `Success` | thành công | xanh lá |
| `Error` | lỗi | đỏ |
| `Pending` | đang xử lý | vàng/cam |

### `DwgReadResult`

`DwgReadResult` là class nhỏ dùng để gom kết quả sau khi đọc DWG.

| Property | Ý nghĩa tiếng Việt |
|---|---|
| `Layers` | danh sách layer CAD |
| `Grids` | danh sách lưới trục đọc được |
| `Columns` | danh sách cột đọc được |
| `Beams` | danh sách dầm đọc được |
| `Slabs` | danh sách sàn đọc được |

## 10. Quy tắc đọc tên trong code

Một số hậu tố thường gặp:

| Hậu tố | Ý nghĩa |
|---|---|
| `Service` | lớp làm một nhóm công việc xử lý |
| `Result` | kết quả trả về sau khi xử lý |
| `Count` | số lượng |
| `Names` | danh sách tên |
| `Id` | mã định danh của element |
| `Ids` | danh sách mã định danh |
| `Index` | vị trí trong danh sách |
| `Layer` | layer CAD |
| `Model` | dữ liệu trung gian đọc từ CAD, chưa phải element Revit thật |
| `Create...` | tạo element Revit |
| `Read...` | đọc dữ liệu |
| `Parse...` | chuyển/diễn giải dữ liệu từ dạng này sang dạng khác |
| `Find...` | tìm một đối tượng |
| `Get...` | lấy dữ liệu |
| `Is...` | kiểm tra đúng/sai |
| `Update...` | cập nhật dữ liệu |
| `Reset...` | đưa dữ liệu về trạng thái ban đầu |
