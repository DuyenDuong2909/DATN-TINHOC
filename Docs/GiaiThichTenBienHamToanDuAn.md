# Giải thích tên biến, tên hàm trong toàn bộ dự án

Tài liệu này giải thích ý nghĩa tiếng Việt của các tên class, biến, property và hàm chính trong dự án `AutoCADToRevitApplication`.

Phạm vi tài liệu: các file nguồn chính `.cs` và `.xaml`, không tính thư mục build `bin`, `obj`, `.vs`.

## Quy ước đọc tên

| Từ trong tên | Ý nghĩa tiếng Việt |
|---|---|
| `Model` | dữ liệu trung gian đọc từ CAD, chưa phải element Revit thật |
| `Service` | lớp xử lý một nhóm chức năng |
| `Reader` | bộ đọc dữ liệu |
| `Creation` | tạo đối tượng Revit |
| `Result` | kết quả sau khi xử lý |
| `Layer` | layer trong CAD |
| `Grid` | lưới trục Revit |
| `Column` | cột |
| `Beam` | dầm |
| `Slab` | sàn |
| `Level` | cao độ/tầng trong Revit |
| `Point` | điểm tọa độ |
| `Loop` | vòng kín hình học |
| `Segment` | đoạn thẳng |
| `Offset` | độ lệch |
| `Thickness` | độ dày |
| `Width` | bề rộng |
| `Height` | chiều cao |
| `Created` | số lượng đã tạo |
| `Skipped` | số lượng bị bỏ qua |
| `Failed` | số lượng lỗi |
| `Ids` | danh sách mã định danh element |
| `Try...` | hàm thử làm việc gì đó, trả về đúng/sai |
| `Get...` | lấy dữ liệu |
| `Find...` | tìm dữ liệu |
| `Create...` | tạo dữ liệu hoặc element |
| `Read...` | đọc dữ liệu |
| `Parse...` | phân tích/chuyển đổi dữ liệu |
| `Normalize...` | chuẩn hóa dữ liệu |
| `Deduplicate...` | loại trùng |
| `Is...` | kiểm tra điều kiện đúng/sai |

## 1. File gốc add-in

### `App.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `App` | class | Điểm khởi động của add-in Revit |
| `IExternalApplication` | interface Revit | Cho phép add-in đăng ký khi Revit khởi động/tắt |
| `OnStartup` | hàm | Chạy khi add-in được Revit nạp |
| `OnShutdown` | hàm | Chạy khi Revit đóng hoặc add-in được gỡ |
| `application` | tham số | Đối tượng Revit UI dùng để thêm tab/panel/button |

Ý nghĩa chung: `App` tạo nút lệnh trên ribbon Revit để người dùng mở chức năng chuyển CAD sang Revit.

### `Command.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `Command` | class | Lệnh chính khi người dùng bấm nút add-in |
| `IExternalCommand` | interface Revit | Chuẩn lệnh được Revit gọi từ ribbon |
| `Execute` | hàm | Hàm chạy khi người dùng bấm lệnh |
| `commandData` | tham số | Chứa dữ liệu Revit truyền vào command |
| `message` | tham số | Chuỗi lỗi trả về cho Revit nếu lệnh thất bại |
| `elements` | tham số | Tập element liên quan tới lỗi nếu có |

Ý nghĩa chung: `Command.Execute` mở cửa sổ `MainWindow`.

## 2. Giao diện

### `Views/MainWindow.xaml`

| Tên/binding | Ý nghĩa |
|---|---|
| `MainWindow` | cửa sổ chính của add-in |
| `StatusMessage` | thông báo trạng thái ở đầu form |
| `StatusColor` | màu chữ thông báo |
| `FloorHeight` | chiều cao tầng 1 |
| `TypicalHeight` | chiều cao tầng điển hình |
| `NumberOfFloors` | số tầng |
| `SlabThickness` | độ dày sàn |
| `ColumnBaseOffset` | offset chân cột |
| `ColumnTopOffset` | offset đầu cột |
| `BeamWidth` | bề rộng dầm |
| `BeamHeight` | chiều cao dầm |
| `BeamLevelNames` | danh sách level cho combo box dầm/sàn |
| `SelectedBeamLevelName` | level đang chọn cho dầm/sàn |
| `BeamZOffset` | offset cao độ dầm/sàn |
| `XStartName` | tên bắt đầu của trục X |
| `YStartName` | tên bắt đầu của trục Y |
| `XNamingDirection` | hướng đánh tên trục X |
| `YNamingDirection` | hướng đánh tên trục Y |
| `CreateGrid` | checkbox có tạo lưới trục không |
| `CreateColumn` | checkbox có tạo cột không |
| `CreateBeam` | checkbox có tạo dầm không |
| `CreateSlab` | checkbox có tạo sàn không |
| `DwgFileName` | tên file DWG đang đọc |
| `Layers` | danh sách layer CAD trong bảng |
| `ApplyLayerCommand` | command khi bấm áp dụng gán layer |
| `ImportDwgCommand` | command khi bấm import DWG |
| `ReadCadCommand` | command khi bấm đọc CAD |
| `ConvertTo3DCommand` | command khi bấm chuyển đổi 3D |
| `IsFileLoaded` | trạng thái đã đọc CAD, dùng để bật/tắt nút |
| `GridCount`, `ColumnCount`, `BeamCount`, `SlabCount` | số lượng cấu kiện đọc được |

### `Views/MainWindow.xaml.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `MainWindow` | class | Code-behind của cửa sổ chính |
| `MainWindow(UIApplication uiApp)` | constructor | Tạo cửa sổ và nhận ứng dụng Revit hiện tại |
| `vm` | biến | ViewModel của cửa sổ |
| `DataContext` | property WPF | Nguồn dữ liệu để XAML binding |
| `BtnClose_Click` | hàm | Đóng cửa sổ khi bấm nút Đóng |
| `sender` | tham số | Control phát sinh sự kiện click |
| `e` | tham số | Dữ liệu sự kiện click |

## 3. ViewModel

### `ViewModels/MainViewModel.cs`

File này đã có tài liệu riêng chi tiết hơn tại:

`Docs/GiaiThichTenBienHamMainViewModel.md`

Tóm tắt các nhóm tên chính:

| Tên | Ý nghĩa |
|---|---|
| `MainViewModel` | ViewModel chính của màn hình |
| `_uiDoc` | tài liệu Revit phía giao diện |
| `_doc` | tài liệu Revit phía dữ liệu/model |
| `_currentDwgInstanceId` | id DWG hiện tại trong Revit |
| `_parsedGrids`, `_parsedColumns`, `_parsedBeams`, `_parsedSlabs` | dữ liệu đã đọc từ CAD |
| `Initialize` | khởi tạo ViewModel |
| `LoadLevelOptions` | nạp danh sách level |
| `preferredLevelName` | tên level ưu tiên chọn lại |
| `AutoDetectDwg` | tự tìm DWG trong model |
| `ImportDwg` | import CAD vào Revit |
| `ReadCad` | đọc CAD từ DWG |
| `ApplyLayer` | áp dụng mapping layer hiện tại |
| `ConvertTo3D` | tạo Revit 3D từ dữ liệu CAD |
| `ReadDwgFromRevit` | đọc DWG hiện tại trong Revit |
| `ParseGeometryByLayer` | phân tích hình học theo layer |
| `ReparseCurrentLayerMapping` | đọc lại theo mapping layer mới |
| `FocusCreatedElements` | zoom tới element vừa tạo |
| `FindPlanViewForActiveLevel` | tìm view mặt bằng theo level active |
| `GetLevelIndex` | tìm vị trí level theo tên |
| `UpdateCounts` | cập nhật thống kê |
| `ResetCounts` | reset thống kê |
| `GetCurrentDwgInstance` | lấy DWG hiện tại |
| `IsGridLayer`, `IsColumnLayer`, `IsBeamLayer...`, `IsSlabLayer...` | kiểm tra loại layer |
| `NormalizeText` | chuẩn hóa chữ để so sánh |
| `ParsePositiveDouble`, `ParseDouble`, `ParsePositiveInt` | đổi chuỗi nhập từ UI sang số |
| `SetStatus` | cập nhật thông báo trạng thái |
| `DwgReadResult` | gom kết quả đọc DWG |

## 4. Models/Elements

### `Models/Elements/GridModel.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `GridModel` | class | Dữ liệu lưới trục đọc từ CAD |
| `Name` | property | tên trục, ví dụ A, B, 1, 2 |
| `LayerName` | property | tên layer CAD chứa trục |
| `StartPoint` | property | điểm đầu của trục |
| `EndPoint` | property | điểm cuối của trục |
| `IsVertical` | property | trục đứng hay không |
| `Length` | property tính toán | chiều dài trục |
| `MidPoint` | property tính toán | điểm giữa trục |
| `ToString` | hàm | chuyển grid thành chuỗi để debug |
| `Point2D` | class | điểm 2D dùng chung cho dữ liệu CAD |
| `X` | property | tọa độ X |
| `Y` | property | tọa độ Y |

### `Models/Elements/ColumnModel.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `ColumnModel` | class | Dữ liệu cột đọc từ CAD |
| `LayerName` | property | layer CAD chứa cột |
| `CenterPoint` | property | tâm cột |
| `Width` | property | bề rộng cột |
| `Height` | property | chiều cao/kích thước cạnh còn lại của cột |
| `RotationDegrees` | property | góc xoay cột theo độ |
| `PrimaryAxis` | property tính toán | trục chính của cột, `H` nếu cao >= rộng, ngược lại `B` |
| `ToString` | hàm | chuyển cột thành chuỗi để debug |

### `Models/Elements/BeamModel.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `BeamModel` | class | Dữ liệu dầm đọc từ CAD |
| `LayerName` | property | layer CAD chứa dầm |
| `StartPoint` | property | điểm đầu tim dầm |
| `EndPoint` | property | điểm cuối tim dầm |
| `CenterPoint` | property | tâm dầm |
| `Width` | property | bề rộng dầm |
| `Height` | property | chiều cao dầm |
| `RotationDegrees` | property | góc xoay dầm |
| `DimensionText` | property | text kích thước đọc được từ CAD nếu có |
| `SourceType` | property | cách phát hiện dầm, ví dụ từ cặp biên và trục |
| `Length` | property tính toán | chiều dài dầm |
| `ToString` | hàm | chuyển dầm thành chuỗi để debug |

### `Models/Elements/SlabModel.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `SlabModel` | class | Dữ liệu sàn đọc từ CAD |
| `LayerName` | property | layer CAD chứa sàn |
| `OuterLoop` | property | vòng bao ngoài của sàn |
| `OpeningLoops` | property | danh sách vòng lỗ mở trong sàn |
| `Thickness` | property | độ dày sàn |
| `Area` | property | diện tích vòng sàn |
| `CenterPoint` | property | tâm sàn |
| `ToString` | hàm | chuyển sàn thành chuỗi để debug |

## 5. Models/Mapping

### `Models/Mapping/DwgLayer.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `DwgLayer` | class | Một layer CAD trong file DWG |
| `INotifyPropertyChanged` | interface | Cho UI biết property đã thay đổi |
| `LayerName` | property | tên layer CAD |
| `EntityCount` | property | số đối tượng hình học trong layer |
| `ElementType` | property | loại cấu kiện được gán: Lưới trục, Cột, Dầm, Sàn, Bỏ qua |
| `IsAutoMapped` | property | layer được gán tự động hay không |
| `MappingSource` | property tính toán | hiển thị `Tự động` hoặc `Thủ công` |
| `IsIgnored` | property tính toán | layer có đang bị bỏ qua không |
| `PropertyChanged` | event | sự kiện báo UI cập nhật |
| `OnPropertyChanged` | hàm | phát sự kiện khi property thay đổi |
| `name` | tham số | tên property vừa thay đổi |

### `Models/Mapping/LayerMappingRule.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `LayerMappingRule` | class | Quy tắc nhận diện layer CAD |
| `Keyword` | property | từ khóa cần tìm trong tên layer |
| `ElementType` | property | loại cấu kiện gán khi khớp keyword |
| `Priority` | property | độ ưu tiên của rule |

### `Models/Mapping/RevitCreationResult.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `CreationStatus` | enum | trạng thái tạo element |
| `Success` | enum value | tạo thành công |
| `Skipped` | enum value | bỏ qua |
| `Failed` | enum value | lỗi |
| `Conflict` | enum value | bị trùng/xung đột |
| `RevitCreationResult` | class | Kết quả tạo một element |
| `ElementType` | property | loại element |
| `ElementName` | property | tên element |
| `Status` | property | trạng thái tạo |
| `Message` | property | thông báo chi tiết |
| `Ok` | hàm static | tạo kết quả thành công |
| `Fail` | hàm static | tạo kết quả lỗi |
| `Skip` | hàm static | tạo kết quả bỏ qua |

## 6. Models/Results

Các file result có cấu trúc giống nhau: lưu số lượng tạo được, bỏ qua, lỗi và danh sách element đã tạo.

### `Models/Results/GridCreationResult.cs`

| Tên | Ý nghĩa |
|---|---|
| `GridCreationResult` | kết quả tạo lưới trục |
| `Created` | số lưới trục đã tạo |
| `Skipped` | số lưới trục bỏ qua |
| `Failed` | số lưới trục lỗi |
| `CreatedElementIds` | id các lưới trục đã tạo |
| `Messages` | thông báo chi tiết |

### `Models/Results\ColumnCreationResult.cs`

| Tên | Ý nghĩa |
|---|---|
| `ColumnCreationResult` | kết quả tạo cột |
| `Created` | số cột đã tạo |
| `Skipped` | số cột bỏ qua |
| `Failed` | số cột lỗi |
| `CreatedElementIds` | id các cột đã tạo |
| `Messages` | thông báo chi tiết |

### `Models/Results/BeamCreationResult.cs`

| Tên | Ý nghĩa |
|---|---|
| `BeamCreationResult` | kết quả tạo dầm |
| `Created` | số dầm đã tạo |
| `Skipped` | số dầm bỏ qua |
| `Failed` | số dầm lỗi |
| `CreatedElementIds` | id các dầm đã tạo |
| `Messages` | thông báo chi tiết |

### `Models/Results/SlabCreationResult.cs`

| Tên | Ý nghĩa |
|---|---|
| `SlabCreationResult` | kết quả tạo sàn |
| `Created` | số sàn đã tạo |
| `Skipped` | số sàn bỏ qua |
| `Failed` | số sàn lỗi |
| `CreatedElementIds` | id các sàn đã tạo |
| `Messages` | thông báo chi tiết |

### `Models/Results/LevelCreationResult.cs`

| Tên | Ý nghĩa |
|---|---|
| `LevelCreationResult` | kết quả tạo/cập nhật level |
| `Created` | số level tạo mới |
| `Updated` | số level cập nhật |
| `Failed` | số level lỗi |
| `Levels` | danh sách level sau khi xử lý |
| `CreatedElementIds` | id các level tạo mới |
| `Messages` | thông báo chi tiết |

## 7. Services/Import

### `Services/Import/CadImportService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `CadImportService` | class | Service import file CAD vào Revit |
| `_doc` | field | document Revit để import vào |
| `ImportDwg` | hàm | import file DWG/DXF |
| `filePath` | tham số | đường dẫn file CAD |
| `GetImportView` | hàm | lấy view hiện tại hoặc view phù hợp để import CAD |

## 8. Services/Parsing

### `Services/Parsing/LayerMappingService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `LayerMappingService` | class | Service tự gán layer CAD theo từ khóa |
| `DefaultRules` | field | danh sách rule mặc định |
| `AutoMap` | hàm | tự gán loại cấu kiện cho danh sách layer |
| `layers` | tham số | danh sách layer CAD |
| `FindBestMatch` | hàm | tìm rule khớp tốt nhất với tên layer |
| `layerName` | tham số | tên layer cần xét |
| `NormalizeText` | hàm | chuẩn hóa chữ để so sánh |
| `value` | tham số | chuỗi cần chuẩn hóa |
| `GetDefaultRules` | hàm | trả về danh sách rule mặc định |

### `Services/Parsing/RevitDwgReaderService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `RevitDwgReaderService` | class | Service đọc DWG đã import trong Revit |
| `_doc` | field | document Revit hiện tại |
| `FindDwgInstance` | hàm | tìm DWG import instance trong model |
| `GetLayersFromInstance` | hàm | lấy danh sách layer từ DWG |
| `dwgInstance` | tham số | element DWG trong Revit |
| `GetGeometryByLayer` | hàm | gom hình học DWG theo layer |
| `ReadGridLines` | hàm | đọc lưới trục từ hình học DWG |
| `ExtractGeometry` | hàm | đệ quy lấy geometry từ object Revit |
| `HasReadableText` | hàm | kiểm tra object có text đọc được không |
| `ReadGridFromGeometry` | hàm | đọc grid từ `Line` hoặc `PolyLine` |
| `CreateGrid` | hàm | tạo `GridModel` từ điểm đầu/cuối |
| `NameAndSortGrids` | hàm | sắp xếp và đặt tên trục |
| `ApplyNames` | hàm | gán tên tuần tự cho grid |
| `GetLayerName` | hàm | lấy tên layer của geometry object |
| `AddToLayer` | hàm | thêm geometry vào dictionary theo layer |
| `TryGetAxisDirection` | hàm | kiểm tra line gần ngang/dọc |
| `ToPoint2D` | hàm | đổi `XYZ` Revit sang `Point2D` mm |
| `Distance` | hàm | tính khoảng cách 2D |

#### `GridNamingOptions`

| Tên | Ý nghĩa |
|---|---|
| `GridNamingOptions` | tùy chọn đánh tên trục |
| `XStartName` | tên bắt đầu cho trục X |
| `YStartName` | tên bắt đầu cho trục Y |
| `XLeftToRight` | đánh tên X từ trái sang phải |
| `YBottomToTop` | đánh tên Y từ dưới lên trên |

#### `GridNameSequence`

| Tên | Ý nghĩa |
|---|---|
| `GridNameSequence` | bộ sinh tên trục tuần tự |
| `Current` | tên hiện tại |
| `FromStartName` | tạo sequence từ tên bắt đầu |
| `MoveNext` | chuyển sang tên tiếp theo |
| `LettersToNumber` | đổi chữ A/B/C thành số |
| `NumberToLetters` | đổi số thành chữ A/B/C |

### `Services/Parsing/ColumnReaderService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `ColumnReaderService` | class | Service đọc cột từ geometry CAD |
| `ReadColumns` | hàm | đọc danh sách cột từ các layer cột |
| `geometryByLayer` | tham số | geometry DWG nhóm theo layer |
| `columnLayerNames` | tham số | tên các layer cột |
| `ReadColumnFromGeometry` | hàm | đọc một cột từ một object hình học |
| `ToClosedPointList` | hàm | đổi polyline thành danh sách điểm khép kín |
| `TryReadRectangle` | hàm | thử đọc hình chữ nhật cột |
| `TryGetAxisAlignedDimensions` | hàm | thử lấy kích thước cột song song trục X/Y |
| `Deduplicate` | hàm | loại cột trùng |
| `RemoveConsecutiveDuplicates` | hàm | bỏ các điểm liên tiếp bị trùng |
| `AreSamePoint` | hàm | kiểm tra hai điểm gần nhau |
| `AreOppositeSidesEqual` | hàm | kiểm tra hai cạnh đối bằng nhau |
| `IsRightAngle` | hàm | kiểm tra góc vuông |
| `IsHorizontal` | hàm | kiểm tra cạnh ngang |
| `IsVertical` | hàm | kiểm tra cạnh dọc |
| `NormalizeAngle` | hàm | chuẩn hóa góc |
| `GetAngleDegrees` | hàm | lấy góc đoạn thẳng theo độ |
| `Distance` | hàm | tính khoảng cách |
| `ToPoint2D` | hàm | đổi `XYZ` sang `Point2D` |

### `Services/Parsing/BeamReaderService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `BeamReaderService` | class | Service đọc dầm từ CAD |
| `ReadBeams` | hàm | đọc danh sách dầm từ layer dầm và layer trục |
| `DetectBeamsFromBoundariesAndAxes` | hàm | phát hiện dầm từ cặp biên song song và trục giữa |
| `FindBestMiddleAxis` | hàm | tìm trục nằm giữa hai biên dầm |
| `GetParallelDistanceOrMax` | hàm | lấy khoảng cách song song hoặc giá trị rất lớn nếu lỗi |
| `ReadBeamSegments` | hàm | tách line/polyline thành các đoạn dầm |
| `CreateSegment` | hàm | tạo `BeamSegment` hợp lệ từ hai điểm |
| `DetectCenterLinesFromBoundaryPairs` | hàm | tìm đường tim từ các cặp biên |
| `CreateCenterLine` | hàm | tạo đường tim giữa hai đoạn biên |
| `CreateBeam` | hàm | tạo `BeamModel` từ một segment |
| `ReadDimensionNotes` | hàm | đọc ghi chú kích thước dầm |
| `CreateBeamsFromDimensionNotes` | hàm | gán kích thước từ text vào dầm gần nhất |
| `TryParseDimension` | hàm | đọc chuỗi kích thước kiểu `300x700` |
| `ParseNumber` | hàm | đổi chuỗi số sang double |
| `TryGetText` | hàm | thử lấy text từ geometry object |
| `TryGetGeometryCenter` | hàm | thử lấy tâm geometry |
| `Deduplicate` | hàm | loại dầm trùng |
| `AreSameLine` | hàm | kiểm tra hai line trùng nhau |
| `TryGetAxisDirection` | hàm | kiểm tra đoạn gần ngang/dọc |
| `AreParallel` | hàm | kiểm tra hai đoạn song song |
| `TryGetParallelDistance` | hàm | tính khoảng cách giữa hai đoạn song song |
| `HasEnoughOverlap` | hàm | kiểm tra hai đoạn chồng lấn đủ dài |
| `GetOverlapLength` | hàm | lấy chiều dài phần chồng lấn |
| `ProjectRange` | hàm | chiếu đoạn lên trục để so overlap |
| `OrderAlongMainAxis` | hàm | sắp xếp điểm đầu/cuối theo trục chính |
| `NormalizeAngle` | hàm | chuẩn hóa góc |
| `DistancePointToSegment` | hàm | khoảng cách từ điểm tới đoạn |
| `ToPoint2D` | hàm | đổi `XYZ` sang `Point2D` |
| `Distance` | hàm | tính khoảng cách 2D |
| `BeamSegment` | record | đoạn thẳng tạm dùng để nhận diện dầm |
| `BeamDimensionNote` | record | ghi chú kích thước dầm đọc từ CAD |

### `Services/Parsing/SlabReaderService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `SlabReaderService` | class | Service đọc sàn từ CAD |
| `ReadSlabs` | hàm | đọc danh sách sàn từ layer sàn |
| `ReadClosedPolylineLoops` | hàm | đọc các polyline khép kín thành vòng sàn |
| `ReadSegments` | hàm | tách geometry thành đoạn thẳng |
| `CreateSegment` | hàm | tạo đoạn sàn từ hai điểm |
| `BuildLoopsFromSegments` | hàm | nối các segment rời thành vòng kín |
| `BuildSlabsFromLoops` | hàm | tạo `SlabModel` từ các vòng kín |
| `DeduplicateLoops` | hàm | loại vòng sàn trùng |
| `NormalizeLoop` | hàm | chuẩn hóa thứ tự điểm trong vòng |
| `IsClosed` | hàm | kiểm tra vòng đã khép kín |
| `AreSamePoint` | hàm | kiểm tra hai điểm gần nhau |
| `IsPointInsidePolygon` | hàm | kiểm tra điểm nằm trong polygon |
| `GetSignedArea` | hàm | tính diện tích có dấu của polygon |
| `GetCentroid` | hàm | tính tâm polygon |
| `ToPoint2D` | hàm | đổi `XYZ` sang `Point2D` |
| `Distance` | hàm | tính khoảng cách |
| `SlabSegment` | record | đoạn thẳng tạm để dựng vòng sàn |
| `SlabLoop` | record | vòng kín sàn gồm điểm, diện tích, tâm |

## 9. Services/Creation

### `Services/Creation/LevelCreationService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `LevelCreationService` | class | Service tạo/cập nhật level Revit |
| `_doc` | field | document Revit hiện tại |
| `CreateOrUpdateLevels` | hàm | tạo hoặc cập nhật danh sách level theo số tầng |
| `numberOfFloors` | tham số | số tầng cần tạo |
| `firstFloorHeightMm` | tham số | chiều cao tầng 1 theo mm |
| `typicalFloorHeightMm` | tham số | chiều cao tầng điển hình theo mm |
| `GetOrCreateBaseLevel` | hàm | lấy hoặc tạo level gốc |
| `GetOrCreateLevel` | hàm | lấy hoặc tạo level theo tên/cao độ |
| `GetLevels` | hàm | lấy danh sách level hiện có |
| `TrySetLevelName` | hàm | thử đặt tên level |
| `MmToFeet` | hàm | đổi milimet sang feet Revit |

### `Services/Creation/GridCreationService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `GridCreationService` | class | Service tạo lưới trục Revit |
| `CreateGrids` | hàm | tạo grid từ danh sách `GridModel` |
| `gridModels` | tham số | danh sách lưới trục đọc từ CAD |
| `HideDwgImportsInAllViews` | hàm | ẩn DWG import trong các view |
| `GetActiveLevel` | hàm | lấy level của view hiện tại |
| `GetExistingGridNames` | hàm | lấy tên grid đã có |
| `GetExistingGridKeys` | hàm | lấy khóa hình học grid đã có |
| `TryGetExistingGridKey` | hàm | thử tạo khóa cho grid đã tồn tại |
| `CreateRevitLine` | hàm | tạo line Revit từ `GridModel` |
| `UpdateLevelExtentsInElevationViews` | hàm | cập nhật độ dài level trong view đứng |
| `TryUpdateLevelExtentInView` | hàm | thử cập nhật một level trong một view |
| `GetLevelLineInView` | hàm | lấy line biểu diễn level trong view |
| `SetLevelEndToViewSpecific` | hàm | chuyển đầu level sang chế độ view-specific |
| `SetLevelCurveInView` | hàm | đặt lại đường biểu diễn level trong view |
| `GetGridKey` | hàm | tạo khóa chống trùng grid |
| `CanHide` | hàm | kiểm tra element có thể ẩn trong view không |
| `RoundToTolerance` | hàm | làm tròn theo sai số |
| `MmToFeet`, `FeetToMm` | hàm | đổi đơn vị |
| `Clamp` | hàm | giới hạn giá trị trong khoảng min/max |
| `GridPlacement` | class phụ | thông tin đặt hệ tọa độ grid |
| `MinX`, `MaxX`, `MinY`, `MaxY` | property | biên nhỏ/lớn của lưới |
| `CenterX`, `CenterY` | property | tâm hệ lưới |

### `Services/Creation/ColumnCreationService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `ColumnCreationService` | class | Service tạo cột Revit |
| `CreateColumns` | hàm | tạo cột từ danh sách `ColumnModel` |
| `columnModels` | tham số | danh sách cột đọc từ CAD |
| `gridModels` | tham số | danh sách lưới trục để canh tọa độ |
| `baseLevel`, `topLevel` | tham số | level chân và level đỉnh cột |
| `floorHeightMm` | tham số | chiều cao tầng theo mm |
| `baseOffsetMm`, `topOffsetMm` | tham số | offset chân/đỉnh cột |
| `deleteExistingGenerated` | tham số | có xóa cột add-in đã tạo trước đó không |
| `GetBaseLevel` | hàm | lấy level gốc |
| `GetNextLevel` | hàm | lấy level kế tiếp |
| `FindBaseColumnSymbol` | hàm | tìm family type cột gốc |
| `GetOrCreateColumnType` | hàm | lấy hoặc nhân bản type cột theo kích thước |
| `GetColumnTypeName` | hàm | tạo tên type cột |
| `TrySetColumnDimensions` | hàm | thử set kích thước cột |
| `TrySetParameter` | hàm | thử set parameter theo danh sách tên |
| `SetColumnHeight` | hàm | set ràng buộc chiều cao cột |
| `RotateColumn` | hàm | xoay cột |
| `DeleteGeneratedColumns` | hàm | xóa cột do add-in tạo trước đó |
| `IsGeneratedColumn` | hàm | kiểm tra cột do add-in tạo |
| `MarkGeneratedColumn` | hàm | đánh dấu cột do add-in tạo |
| `GetExistingColumnKeys` | hàm | lấy khóa chống trùng cột |
| `GetColumnKey` | hàm | tạo khóa cột theo vị trí/level |
| `GetPointKey` | hàm | tạo khóa tọa độ |
| `TrySetElementIdParameter` | hàm | thử set parameter kiểu `ElementId` |
| `TrySetDoubleParameter` | hàm | thử set parameter kiểu số |
| `RoundToTolerance` | hàm | làm tròn theo sai số |
| `MmToFeet`, `FeetToMm` | hàm | đổi đơn vị |
| `ColumnPlacement` | class phụ | chuyển tọa độ CAD sang hệ đặt cột |
| `TryResolveColumnPoint` | hàm | thử bắt điểm cột về giao điểm lưới gần nhất |
| `TryFindMatchingCoordinate` | hàm | tìm tọa độ lưới gần tọa độ cột |

### `Services/Creation/BeamCreationService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `BeamCreationService` | class | Service tạo dầm Revit |
| `CreateBeams` | hàm | tạo dầm từ danh sách `BeamModel` |
| `beamModels` | tham số | danh sách dầm đọc từ CAD |
| `gridModels` | tham số | danh sách lưới trục để chuyển tọa độ |
| `beamLevelName` | tham số | tên level tạo dầm |
| `level` | tham số | level tạo dầm |
| `zOffset` | tham số | offset cao độ dầm |
| `deleteExistingGenerated` | tham số | có xóa dầm add-in đã tạo trước đó không |
| `GetBeamLevel` | hàm | tìm level dầm theo tên |
| `FindBaseBeamSymbol` | hàm | tìm family type dầm gốc |
| `GetOrCreateBeamType` | hàm | lấy hoặc nhân bản type dầm theo kích thước |
| `GetBeamTypeName` | hàm | tạo tên type dầm |
| `TrySetBeamDimensions` | hàm | thử set bề rộng/chiều cao dầm |
| `TrySetParameter` | hàm | thử set parameter theo tên |
| `SetBeamOffsets` | hàm | set offset đầu/cuối dầm |
| `DeleteGeneratedBeams` | hàm | xóa dầm do add-in tạo trước đó |
| `IsGeneratedBeam` | hàm | kiểm tra dầm do add-in tạo |
| `MarkGeneratedBeam` | hàm | đánh dấu dầm do add-in tạo |
| `GetExistingBeamKeys` | hàm | lấy khóa chống trùng dầm |
| `GetBeamKey` | hàm | tạo khóa dầm từ điểm đầu/cuối |
| `GetPointKey` | hàm | tạo khóa tọa độ |
| `TrySetElementIdParameter` | hàm | thử set parameter kiểu `ElementId` |
| `TrySetDoubleParameter` | hàm | thử set parameter kiểu số |
| `RoundToTolerance` | hàm | làm tròn theo sai số |
| `MmToFeet`, `FeetToMm` | hàm | đổi đơn vị |
| `BeamPlacement` | class phụ | chuyển tọa độ CAD sang tọa độ Revit khi đặt dầm |
| `ToRevitPoint` | hàm | chuyển `Point2D` CAD sang `XYZ` Revit |

### `Services/Creation/SlabCreationService.cs`

| Tên | Loại | Ý nghĩa |
|---|---|---|
| `SlabCreationService` | class | Service tạo sàn Revit |
| `CreateSlabs` | hàm | tạo sàn từ danh sách `SlabModel` |
| `slabModels` | tham số | danh sách sàn đọc từ CAD |
| `gridModels` | tham số | danh sách lưới để chuyển tọa độ |
| `slabLevelName` | tham số | tên level tạo sàn |
| `level` | tham số | level tạo sàn |
| `zOffset` | tham số | offset cao độ sàn |
| `deleteExistingGenerated` | tham số | có xóa sàn add-in đã tạo trước đó không |
| `GetSlabLevel` | hàm | tìm level sàn theo tên |
| `FindBaseFloorType` | hàm | tìm floor type gốc |
| `GetOrCreateFloorType` | hàm | lấy hoặc nhân bản floor type theo độ dày |
| `GetFloorTypeName` | hàm | tạo tên type sàn |
| `TrySetFloorThickness` | hàm | thử set độ dày sàn |
| `BuildProfile` | hàm | tạo profile sàn gồm biên ngoài và lỗ mở |
| `BuildCurveLoop` | hàm | tạo `CurveLoop` từ danh sách điểm |
| `SetSlabOffset` | hàm | set offset cao độ sàn |
| `DeleteGeneratedSlabs` | hàm | xóa sàn do add-in tạo trước đó |
| `IsGeneratedSlab` | hàm | kiểm tra sàn do add-in tạo |
| `MarkGeneratedSlab` | hàm | đánh dấu sàn do add-in tạo |
| `GetExistingSlabKeys` | hàm | lấy khóa chống trùng sàn |
| `GetExistingSlabKey` | hàm | tạo khóa từ sàn đã tồn tại |
| `GetSlabKey` | hàm | tạo khóa sàn theo biên dạng/level |
| `GetBoxKey` | hàm | tạo khóa từ bounding box |
| `GetFloorThicknessMm` | hàm | lấy độ dày floor type theo mm |
| `GetPointKey` | hàm | tạo khóa tọa độ 2D |
| `TrySetDoubleParameter` | hàm | thử set parameter kiểu số |
| `RoundToTolerance` | hàm | làm tròn theo sai số |
| `MmToFeet`, `FeetToMm` | hàm | đổi đơn vị |
| `SlabPlacement` | class phụ | chuyển tọa độ CAD sang tọa độ Revit khi tạo sàn |
| `ToRevitPoint` | hàm | chuyển `Point2D` CAD sang `XYZ` Revit |

## 10. Các biến cục bộ thường gặp trong service

| Tên biến | Ý nghĩa |
|---|---|
| `result` | kết quả trả về của hàm/service |
| `transaction` | giao dịch Revit để tạo/sửa element |
| `collector` | đối tượng thu thập element trong Revit |
| `symbol` | family symbol/type Revit |
| `baseSymbol` | type gốc để nhân bản |
| `floorType` | loại sàn Revit |
| `baseType` | loại sàn gốc để nhân bản |
| `levelResult` | kết quả tạo/cập nhật level |
| `gridResult`, `columnResult`, `beamResult`, `slabResult` | kết quả tạo từng loại cấu kiện |
| `existingKeys` | tập khóa element đã tồn tại để tránh tạo trùng |
| `createdElementIds` | danh sách id element đã tạo |
| `placement` | thông tin chuyển tọa độ CAD sang Revit |
| `start`, `end` | điểm đầu và điểm cuối |
| `startPoint`, `endPoint` | điểm đầu/cuối dạng `Point2D` |
| `centerPoint` | điểm tâm |
| `widthMm`, `heightMm`, `thicknessMm` | kích thước theo milimet |
| `elevation` | cao độ |
| `zOffset` | offset theo phương Z |
| `layerName` | tên layer CAD |
| `geometryObject` | một object hình học Revit/CAD |
| `polyLine` | polyline CAD/Revit |
| `points` | danh sách điểm |
| `segments` | danh sách đoạn thẳng |
| `loops` | danh sách vòng kín |
| `outer` | vòng bao ngoài |
| `opening` | vòng lỗ mở |
| `profile` | profile tạo sàn gồm các `CurveLoop` |
| `distance` | khoảng cách |
| `angle` | góc |
| `tolerance` | sai số cho phép |

## 11. Luồng tên theo chức năng

### Đọc CAD

```text
RevitDwgReaderService
    -> GetLayersFromInstance
    -> GetGeometryByLayer
    -> ReadGridLines

ColumnReaderService
    -> ReadColumns

BeamReaderService
    -> ReadBeams

SlabReaderService
    -> ReadSlabs
```

### Tạo Revit

```text
LevelCreationService
    -> CreateOrUpdateLevels

GridCreationService
    -> CreateGrids

ColumnCreationService
    -> CreateColumns

BeamCreationService
    -> CreateBeams

SlabCreationService
    -> CreateSlabs
```

### Dữ liệu trung gian

```text
GridModel
ColumnModel
BeamModel
SlabModel
```

### Kết quả xử lý

```text
GridCreationResult
ColumnCreationResult
BeamCreationResult
SlabCreationResult
LevelCreationResult
```

## 12. Ghi nhớ nhanh

- `UIDocument` là phía giao diện Revit.
- `Document` là dữ liệu/model thật trong file Revit.
- `Model` trong dự án là dữ liệu đọc từ CAD, chưa phải Revit element.
- `Service` là nơi xử lý logic.
- `Result` là nơi gom kết quả xử lý.
- `LayerMapping` là logic tự nhận diện layer.
- `CreationService` là logic tạo element Revit.
- `ReaderService` là logic đọc CAD/DWG.
