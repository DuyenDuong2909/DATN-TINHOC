# Giải thích tên biến, tên hàm và kiểu dữ liệu

Tài liệu này dùng để giải thích với giáo viên **vì sao code đặt tên như hiện tại** và **vì sao chọn kiểu dữ liệu đó thay vì kiểu khác**.

Mục tiêu chính:

- Tên biến, tên hàm phải bám theo **nghĩa kỹ thuật trong bài toán CAD -> Revit**.
- Kiểu dữ liệu phải bám theo **bản chất dữ liệu đang lưu**: chữ, số nguyên, số đo, danh sách, trạng thái đúng/sai, đối tượng Revit.
- Các lớp `Model` chỉ lưu **thông tin đã đọc từ CAD**, chưa phải cấu kiện Revit thật.
- Các lớp `Service` thực hiện **một nhóm chức năng riêng**: đọc CAD, tạo Revit, import CAD, gán layer.
- Các lớp `Result` trả về **kết quả thực hiện**: tạo được bao nhiêu, bỏ qua bao nhiêu, lỗi bao nhiêu, id phần tử nào đã tạo.

## 1. Quy tắc đặt tên chung

### 1.1. Tên lớp

Tên lớp được đặt theo mẫu:

`Tên đối tượng + Vai trò`

Ví dụ:

- `GridModel`: dữ liệu lưới trục đọc từ CAD.
- `ColumnModel`: dữ liệu cột đọc từ CAD.
- `BeamModel`: dữ liệu dầm đọc từ CAD.
- `SlabModel`: dữ liệu sàn đọc từ CAD.
- `GridCreationService`: service chuyên tạo lưới trục Revit.
- `ColumnReaderService`: service chuyên đọc cột từ CAD.
- `LevelCreationResult`: kết quả tạo/cập nhật level.

**Vì sao đặt như vậy?**

Tên lớp cần trả lời được 2 câu hỏi:

- Nó liên quan đến đối tượng nào? `Grid`, `Column`, `Beam`, `Slab`, `Level`.
- Nó làm nhiệm vụ gì? `Model`, `ReaderService`, `CreationService`, `Result`.

Vì vậy nhìn tên `BeamReaderService` là hiểu ngay: **service đọc dầm**, không phải service vẽ dầm.

### 1.2. Hậu tố `Model`

Vị trí code:

- `Models/Elements/GridModel.cs`
- `Models/Elements/ColumnModel.cs`
- `Models/Elements/BeamModel.cs`
- `Models/Elements/SlabModel.cs`

`Model` nghĩa là **mô hình dữ liệu trung gian**.

Ví dụ `GridModel` không phải là `Autodesk.Revit.DB.Grid`. Nó chỉ là dữ liệu đọc được từ CAD, gồm:

- `Name`: tên trục.
- `LayerName`: layer CAD gốc.
- `StartPoint`: điểm đầu.
- `EndPoint`: điểm cuối.
- `IsVertical`: trục dọc hay ngang.

**Vì sao không dùng thẳng Revit `Grid`?**

Vì lúc đọc CAD, lưới trục **chưa được tạo trong Revit**. Nếu dùng `Autodesk.Revit.DB.Grid` ngay từ đầu thì sai bản chất, vì Revit `Grid` chỉ tồn tại sau khi gọi:

```csharp
Grid.Create(_doc, line)
```

Do đó luồng đúng là:

```text
CAD geometry -> GridModel -> GridCreationService -> Revit Grid
```

Tương tự:

```text
CAD geometry -> ColumnModel -> ColumnCreationService -> Revit FamilyInstance
CAD geometry -> BeamModel   -> BeamCreationService   -> Revit FamilyInstance
CAD geometry -> SlabModel   -> SlabCreationService   -> Revit Floor
```

### 1.3. Hậu tố `Service`

Vị trí code:

- `Services/Parsing/RevitDwgReaderService.cs`
- `Services/Parsing/ColumnReaderService.cs`
- `Services/Parsing/BeamReaderService.cs`
- `Services/Parsing/SlabReaderService.cs`
- `Services/Creation/GridCreationService.cs`
- `Services/Creation/ColumnCreationService.cs`
- `Services/Creation/BeamCreationService.cs`
- `Services/Creation/SlabCreationService.cs`
- `Services/Creation/LevelCreationService.cs`

`Service` là lớp **xử lý nghiệp vụ**.

Ví dụ:

- `ColumnReaderService`: đọc hình học CAD để tìm cột.
- `ColumnCreationService`: dùng `ColumnModel` để tạo cột trong Revit.

**Vì sao tách reader và creation?**

Vì đọc CAD và tạo Revit là 2 việc khác nhau:

- Đọc CAD chỉ phân tích đường line, polyline, layer, text.
- Tạo Revit cần transaction, family, level, offset, parameter.

Nếu gộp chung sẽ khó kiểm tra và khó giải thích theo lưu đồ.

### 1.4. Hậu tố `Result`

Vị trí code:

- `Models/Results/GridCreationResult.cs`
- `Models/Results/ColumnCreationResult.cs`
- `Models/Results/BeamCreationResult.cs`
- `Models/Results/SlabCreationResult.cs`
- `Models/Results/LevelCreationResult.cs`

`Result` là đối tượng trả về sau khi chạy một chức năng tạo cấu kiện.

Ví dụ:

```csharp
public class GridCreationResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<ElementId> CreatedElementIds { get; } = new();
    public List<string> Messages { get; } = new();
}
```

**Vì sao cần class result thay vì chỉ trả về `true/false`?**

Nếu chỉ trả về `bool` thì chỉ biết thành công hay thất bại. Nhưng chương trình cần biết thêm:

- Tạo được bao nhiêu cấu kiện.
- Bỏ qua bao nhiêu cấu kiện.
- Lỗi bao nhiêu cấu kiện.
- Phần tử Revit nào vừa tạo để zoom/focus.
- Thông báo lỗi cụ thể là gì.

Vì vậy `Result` phù hợp hơn `bool`.

## 2. Quy tắc chọn kiểu dữ liệu

### 2.1. `string`

`string` dùng cho **chữ, tên, text người dùng nhập, tên layer, tên level**.

Ví dụ trong `ViewModels/MainViewModel.cs`:

```csharp
[ObservableProperty] private string _floorHeight = "4300";
[ObservableProperty] private string _numberOfFloors = "7";
[ObservableProperty] private string _beamWidth = "300";
[ObservableProperty] private string _selectedBeamLevelName = string.Empty;
```

**Vì sao các ô nhập số trên giao diện vẫn để `string`?**

Vì ô nhập WPF `TextBox` nhận dữ liệu người dùng dưới dạng chữ. Khi người dùng đang gõ, dữ liệu có thể tạm thời chưa phải số hợp lệ, ví dụ:

- Rỗng.
- Đang gõ dở.
- Có ký tự sai.

Nếu để thẳng `int` hoặc `double`, binding giao diện dễ lỗi ngay khi nhập. Vì vậy ViewModel giữ giá trị nhập là `string`, sau đó khi bấm chạy mới chuyển đổi:

```csharp
var numberOfFloors = ParsePositiveInt(NumberOfFloors, 1);
var firstFloorHeight = ParsePositiveDouble(FloorHeight, 3000.0);
var baseOffset = ParseDouble(ColumnBaseOffset, 0.0);
```

Giải thích ngắn gọn:

```text
Trên form: dùng string để nhận chữ người dùng nhập.
Khi xử lý: parse sang int/double để tính toán.
```

### 2.2. `int`

`int` dùng cho **số nguyên đếm được**, không có phần thập phân.

Ví dụ:

- `GridCount`
- `ColumnCount`
- `BeamCount`
- `SlabCount`
- `Created`
- `Skipped`
- `Failed`
- `numberOfFloors`

**Vì sao `Created` là `int` mà không phải `string`?**

Vì `Created` là số lượng cấu kiện đã tạo. Số lượng phải cộng/trừ/so sánh được:

```csharp
result.Created++;
```

Nếu dùng `string`, mỗi lần cộng phải chuyển đổi qua lại, dễ sai và không đúng bản chất dữ liệu.

**Vì sao `numberOfFloors` là `int`?**

Vì số tầng là số đếm: 1 tầng, 2 tầng, 3 tầng. Không có 2.5 tầng trong logic tạo level.

### 2.3. `double`

`double` dùng cho **tọa độ, chiều dài, chiều rộng, chiều cao, offset, góc xoay, diện tích**.

Ví dụ:

- `Point2D.X`, `Point2D.Y`
- `ColumnModel.Width`, `ColumnModel.Height`
- `BeamModel.Width`, `BeamModel.Height`
- `BeamModel.RotationDegrees`
- `SlabModel.Thickness`, `SlabModel.Area`
- `firstFloorHeightMm`, `typicalFloorHeightMm`

**Vì sao dùng `double` mà không dùng `int` dù đơn vị là mm?**

Vì dữ liệu CAD/Revit có thể có số lẻ. Revit cũng dùng đơn vị nội bộ là feet, khi đổi sang mm có thể ra số thập phân.

Ví dụ:

```csharp
UnitUtils.ConvertFromInternalUnits(point.X, UnitTypeId.Millimeters)
```

Hàm chuyển đơn vị này trả về số thực. Nếu dùng `int`, chương trình sẽ làm tròn mất dữ liệu, gây lệch tọa độ.

Giải thích ngắn gọn:

```text
Số đo hình học dùng double để giữ chính xác tọa độ và kích thước.
Số lượng cấu kiện dùng int vì chỉ là số đếm.
```

### 2.4. `bool`

`bool` dùng cho **đúng/sai, có/không, bật/tắt**.

Ví dụ:

```csharp
[ObservableProperty] private bool _createGrid = true;
[ObservableProperty] private bool _createColumn = true;
[ObservableProperty] private bool _createBeam = true;
[ObservableProperty] private bool _createSlab = true;
```

Các biến này nối với checkbox trên giao diện:

- Tích `Tạo Lưới trục`: `CreateGrid = true`.
- Bỏ tích: `CreateGrid = false`.

Ví dụ trong `ConvertTo3D()`:

```csharp
if (CreateGrid)
{
    var gridResult = gridService.CreateGrids(_parsedGrids);
}
```

**Vì sao dùng `bool` mà không dùng `string` "Có"/"Không"?**

Vì code cần kiểm tra điều kiện. `bool` là kiểu đúng nhất cho câu hỏi có/không. Nếu dùng `string`, dễ sai do viết khác nhau: `"Có"`, `"co"`, `"yes"`, `"true"`.

### 2.5. `List<T>`

`List<T>` dùng cho **danh sách có nhiều phần tử và cần thêm/sắp xếp/lọc**.

Ví dụ:

```csharp
private List<GridModel> _parsedGrids = new();
private List<ColumnModel> _parsedColumns = new();
private List<BeamModel> _parsedBeams = new();
private List<SlabModel> _parsedSlabs = new();
```

**Vì sao dùng `List<GridModel>` thay vì một `GridModel`?**

Vì một bản vẽ có nhiều lưới trục. Mỗi đường trục là một `GridModel`, toàn bộ bản vẽ là `List<GridModel>`.

Ví dụ:

```text
GridModel = 1 đường trục
List<GridModel> = nhiều đường trục
```

### 2.6. `ObservableCollection<T>`

`ObservableCollection<T>` dùng cho danh sách hiển thị trên giao diện WPF.

Ví dụ:

```csharp
[ObservableProperty] private ObservableCollection<DwgLayer> _layers = new();
[ObservableProperty] private ObservableCollection<string> _beamLevelNames = new();
```

**Vì sao `Layers` không dùng `List<DwgLayer>`?**

Vì `Layers` hiển thị trong bảng giao diện. Khi thêm/xóa/sửa layer, giao diện cần tự cập nhật. `ObservableCollection` có cơ chế báo cho WPF biết danh sách đã thay đổi.

Nếu dùng `List`, dữ liệu có thể đã thay đổi trong code nhưng giao diện không tự refresh đúng lúc.

### 2.7. `Dictionary<string, List<GeometryObject>>`

Vị trí code:

- `Services/Parsing/RevitDwgReaderService.cs`
- `Services/Parsing/ColumnReaderService.cs`
- `Services/Parsing/BeamReaderService.cs`
- `Services/Parsing/SlabReaderService.cs`

Ví dụ:

```csharp
public Dictionary<string, List<GeometryObject>> GetGeometryByLayer(Element dwgInstance)
```

Kiểu này có nghĩa:

```text
Key   = tên layer CAD
Value = danh sách hình học nằm trong layer đó
```

Ví dụ dữ liệu:

```text
"Net Truc"      -> [line1, line2, line3]
"Column-800x600" -> [polyline1, polyline2]
"BEAM"          -> [line1, polyline1]
```

**Vì sao dùng `Dictionary`?**

Vì thuật toán luôn cần đọc theo layer. `Dictionary` giúp truy cập nhanh:

```csharp
geometryByLayer[layerName]
```

Nếu chỉ dùng một `List<GeometryObject>`, code sẽ phải quét toàn bộ hình học nhiều lần để tìm layer, chậm và khó quản lý.

### 2.8. `HashSet<T>`

`HashSet` dùng khi cần **kiểm tra có tồn tại hay chưa** và **loại trùng**.

Ví dụ:

```csharp
var layerSet = new HashSet<string>(columnLayerNames, StringComparer.OrdinalIgnoreCase);
if (!layerSet.Contains(layerName)) continue;
```

**Vì sao dùng `HashSet` thay vì `List`?**

Vì `HashSet.Contains()` nhanh và hợp với ý nghĩa "tập hợp không trùng".

Trong bài toán CAD, cùng một layer hoặc cùng một grid có thể bị kiểm tra nhiều lần. Dùng `HashSet` giúp code rõ nghĩa:

```text
Tập layer hợp lệ -> kiểm tra layer hiện tại có thuộc tập đó không.
```

### 2.9. `IReadOnlyCollection<T>`

Vị trí code:

```csharp
public GridCreationResult CreateGrids(IReadOnlyCollection<GridModel> gridModels)
public ColumnCreationResult CreateColumns(IReadOnlyCollection<ColumnModel> columnModels, IReadOnlyCollection<GridModel> gridModels, ...)
```

`IReadOnlyCollection<T>` nghĩa là service **chỉ đọc danh sách đầu vào**, không tự ý thêm/xóa phần tử trong danh sách đó.

**Vì sao không dùng `List<T>` ở tham số hàm tạo Revit?**

Vì khi đã đọc xong CAD, service tạo Revit chỉ cần dùng danh sách đó để vẽ. Nó không nên thay đổi danh sách gốc. Dùng `IReadOnlyCollection<T>` giúp thể hiện rõ ý đồ:

```text
Đầu vào chỉ để đọc, không sửa.
```

### 2.10. Kiểu nullable: `Level?`, `Element?`, `string?`

Dấu `?` nghĩa là biến **có thể null**.

Ví dụ:

```csharp
private UIDocument? _uiDoc;
private Document? _doc;
private ElementId? _currentDwgInstanceId;
private Level? GetActiveLevel()
```

**Vì sao cần nullable?**

Vì trong Revit có trường hợp chưa có document, chưa import CAD, hoặc không tìm thấy level. Nếu ép kiểu không null, code dễ lỗi.

Ví dụ:

```csharp
if (_doc == null)
{
    SetStatus("Không có Revit document đang mở.", StatusType.Error);
    return;
}
```

Đây là câu điều kiện bảo vệ chương trình trước khi gọi Revit API.

## 3. Giải thích các biến chính trong ViewModel

Vị trí code: `ViewModels/MainViewModel.cs`

`MainViewModel` là nơi nối giữa **giao diện** và **logic xử lý**.

### 3.1. Biến `_doc`

```csharp
private Document? _doc;
```

`Document` là tài liệu Revit đang mở.

**Vì sao dùng `Document?`?**

Vì khi add-in mới mở, có thể chưa lấy được document. Do đó biến có thể null.

Biến này rất quan trọng vì mọi thao tác Revit API như tạo grid, cột, dầm, sàn đều cần `_doc`.

### 3.2. Biến `_uiDoc`

```csharp
private UIDocument? _uiDoc;
```

`UIDocument` là phần document liên quan đến giao diện Revit, ví dụ chọn phần tử, focus view.

**Khác với `_doc`:**

- `_doc`: thao tác dữ liệu/model Revit.
- `_uiDoc`: thao tác giao diện Revit.

### 3.3. Biến `_currentDwgInstanceId`

```csharp
private ElementId? _currentDwgInstanceId;
```

Biến này lưu `ElementId` của file CAD đã import vào Revit.

**Vì sao lưu `ElementId` mà không lưu trực tiếp object CAD?**

Trong Revit API, `ElementId` là mã định danh ổn định để tìm lại element trong document:

```csharp
var element = _doc.GetElement(_currentDwgInstanceId);
```

Nếu lưu trực tiếp object, có thể object cũ không còn hợp lệ sau transaction hoặc sau khi document thay đổi.

### 3.4. Các biến `_parsedGrids`, `_parsedColumns`, `_parsedBeams`, `_parsedSlabs`

```csharp
private List<GridModel> _parsedGrids = new();
private List<ColumnModel> _parsedColumns = new();
private List<BeamModel> _parsedBeams = new();
private List<SlabModel> _parsedSlabs = new();
```

`parsed` nghĩa là **đã phân tích/đã đọc từ CAD**.

Ví dụ:

- `_parsedGrids`: các lưới trục đã đọc từ CAD.
- `_parsedColumns`: các cột đã đọc từ CAD.

**Vì sao không đặt là `_grids`?**

Nếu đặt `_grids`, dễ hiểu nhầm là lưới trục Revit đã tạo. Từ `parsed` nhấn mạnh đây mới là dữ liệu đọc từ CAD, chưa chắc đã vẽ ra Revit.

## 4. Giải thích các Model

### 4.1. `Point2D`

Vị trí code: `Models/Elements/GridModel.cs`

```csharp
public class Point2D
{
    public double X { get; set; }
    public double Y { get; set; }
}
```

`Point2D` lưu tọa độ phẳng CAD theo 2 trục X và Y.

**Vì sao không dùng Revit `XYZ` luôn?**

Vì đọc CAD đang xử lý mặt bằng 2D. Nếu dùng `XYZ`, code phải mang thêm Z dù chưa cần. `Point2D` giúp thuật toán đọc CAD đơn giản hơn:

- Tính khoảng cách 2D.
- Tìm tâm.
- Tính chiều dài.
- So sánh tọa độ X/Y.

Khi vẽ sang Revit, lúc đó mới chuyển từ `Point2D` sang `XYZ`.

### 4.2. `GridModel`

Vị trí code: `Models/Elements/GridModel.cs`

Các biến chính:

- `Name`: tên trục, ví dụ A, B, C hoặc 1, 2, 3.
- `LayerName`: tên layer CAD chứa đường trục.
- `StartPoint`: điểm đầu đường trục.
- `EndPoint`: điểm cuối đường trục.
- `IsVertical`: đúng nếu là trục dọc.
- `Length`: độ dài tính từ điểm đầu đến điểm cuối.
- `MidPoint`: trung điểm của đường trục.

**Vì sao có `StartPoint` và `EndPoint`?**

Vì lưới trục là một đường thẳng. Để vẽ lại trong Revit, cần biết 2 điểm đầu/cuối:

```csharp
Line.CreateBound(start, end)
```

**Vì sao `Length` không lưu bằng biến thường?**

Trong code, `Length` là property tính toán:

```csharp
public double Length => Math.Sqrt(...);
```

Nó phụ thuộc vào `StartPoint` và `EndPoint`. Nếu lưu thành biến riêng, có nguy cơ điểm đã đổi nhưng length chưa cập nhật. Tính trực tiếp giúp dữ liệu luôn đúng.

### 4.3. `ColumnModel`

Vị trí code: `Models/Elements/ColumnModel.cs`

Các biến chính:

- `LayerName`: layer CAD của cột.
- `CenterPoint`: tâm cột.
- `Width`: chiều rộng B.
- `Height`: chiều cao H.
- `RotationDegrees`: góc xoay.
- `PrimaryAxis`: trục chính của cột.

**Vì sao cột dùng `CenterPoint`?**

Theo lưu đồ, cột được xác định từ polyline khép kín và lấy vị trí tâm. Khi vẽ Revit `FamilyInstance`, điểm đặt cột chính là tâm:

```text
polyline CAD -> tính tâm -> đặt FamilyInstance tại tâm
```

**Vì sao `Width`, `Height` là `double`?**

Vì kích thước cột là số đo hình học, có thể có số lẻ sau khi đọc/đổi đơn vị.

### 4.4. `BeamModel`

Vị trí code: `Models/Elements/BeamModel.cs`

Các biến chính:

- `StartPoint`: điểm đầu tim dầm.
- `EndPoint`: điểm cuối tim dầm.
- `CenterPoint`: tâm đoạn dầm.
- `Width`: bề rộng dầm B.
- `Height`: chiều cao dầm H.
- `RotationDegrees`: hướng dầm.
- `DimensionText`: text kích thước nếu đọc từ CAD.
- `SourceType`: nguồn phát hiện dầm.

**Vì sao dầm cần `StartPoint` và `EndPoint`, không chỉ cần tâm?**

Dầm là cấu kiện dạng đường. Revit tạo dầm bằng một đường line:

```csharp
_doc.Create.NewFamilyInstance(line, symbol, level, StructuralType.Beam)
```

Vì vậy cần điểm đầu và điểm cuối để tạo line. Tâm chỉ dùng để so sánh, tìm gần text, kiểm tra trùng.

### 4.5. `SlabModel`

Vị trí code: `Models/Elements/SlabModel.cs`

Các biến chính:

- `OuterLoop`: vòng biên ngoài của sàn.
- `OpeningLoops`: danh sách vòng lỗ mở.
- `Thickness`: chiều dày sàn.
- `Area`: diện tích sàn sau khi trừ lỗ.
- `CenterPoint`: tâm vùng sàn.

**Vì sao `OuterLoop` là `List<Point2D>`?**

Vì sàn là một vùng kín, không chỉ có 2 điểm. Cần nhiều điểm nối lại thành polygon.

**Vì sao `OpeningLoops` là `List<List<Point2D>>`?**

Vì một sàn có thể có nhiều lỗ. Mỗi lỗ là một danh sách điểm. Nhiều lỗ là danh sách của danh sách:

```text
OpeningLoops
  -> lỗ 1: [P1, P2, P3, P4]
  -> lỗ 2: [P1, P2, P3, P4]
```

## 5. Giải thích tên hàm theo nhóm

### 5.1. Nhóm `Read...`

Ví dụ:

- `ReadCad()`
- `ReadDwgFromRevit()`
- `ReadGridLines(...)`
- `ReadColumns(...)`
- `ReadBeams(...)`
- `ReadSlabs(...)`

`Read` nghĩa là **đọc dữ liệu**, chưa tạo cấu kiện Revit.

Ví dụ:

```csharp
var columns = new ColumnReaderService().ReadColumns(geometryByLayer, columnLayerNames);
```

Câu này nghĩa là:

```text
Lấy hình học CAD đã chia theo layer -> đọc ra danh sách ColumnModel.
```

**Vì sao không đặt là `CreateColumns`?**

Vì hàm này chưa tạo cột trong Revit. Nó chỉ đọc CAD. Nếu đặt `CreateColumns` sẽ nhầm với service vẽ cột.

### 5.2. Nhóm `Create...`

Ví dụ:

- `CreateGrids(...)`
- `CreateColumns(...)`
- `CreateBeams(...)`
- `CreateSlabs(...)`
- `CreateOrUpdateLevels(...)`

`Create` nghĩa là **tạo đối tượng trong Revit**.

Ví dụ:

```csharp
var gridResult = gridService.CreateGrids(_parsedGrids);
```

Câu này nghĩa là:

```text
Dùng dữ liệu GridModel đã đọc từ CAD để tạo Grid thật trong Revit.
```

### 5.3. Nhóm `Get...`

Ví dụ:

- `GetGeometryByLayer(...)`
- `GetLayersFromInstance(...)`
- `GetActiveLevel()`
- `GetExistingGridNames()`
- `GetOrCreateLevel(...)`

`Get` nghĩa là **lấy dữ liệu đang có**.

Ví dụ:

```csharp
GetGeometryByLayer(dwgInstance)
```

Hàm này lấy toàn bộ hình học CAD và nhóm theo layer.

### 5.4. Nhóm `Find...`

Ví dụ:

- `FindDwgInstance()`
- `FindPlanViewForActiveLevel()`
- `FindBaseBeamSymbol()`
- `FindBaseColumnSymbol()`

`Find` nghĩa là **tìm kiếm**. Kết quả có thể tìm thấy hoặc không.

Vì vậy các hàm `Find...` thường trả về kiểu nullable, ví dụ `Element?`, `FamilySymbol?`, `ViewPlan?`.

### 5.5. Nhóm `Try...`

Ví dụ:

- `TryReadRectangle(...)`
- `TryParseDimension(...)`
- `TryGetAxisDirection(...)`
- `TrySetParameter(...)`
- `TrySetDoubleParameter(...)`

`Try` nghĩa là **thử làm một việc có thể thất bại nhưng không coi là lỗi nghiêm trọng**.

Ví dụ:

```csharp
if (!TryReadRectangle(points, out var width, out var height, out var center, out var rotationDegrees))
    return null;
```

Giải thích:

```text
Thử đọc polyline thành hình chữ nhật.
Nếu không đúng hình chữ nhật thì bỏ qua, không crash chương trình.
Nếu đúng thì lấy ra width, height, center, rotationDegrees.
```

**Vì sao dùng `Try...` thay vì ném exception?**

Trong CAD có nhiều hình không phải cấu kiện. Việc đọc không được một hình là bình thường. Dùng `Try...` giúp chương trình bỏ qua đối tượng không hợp lệ và tiếp tục đọc đối tượng khác.

### 5.6. Nhóm `Is...`

Ví dụ:

- `IsGridLayer(...)`
- `IsColumnLayer(...)`
- `IsBeamLayerRobust(...)`
- `IsSlabLayerRobust(...)`
- `IsClosed(...)`
- `IsVertical(...)`
- `IsHorizontal(...)`

`Is` trả về `bool`, dùng cho câu hỏi đúng/sai.

Ví dụ:

```csharp
if (!layerSet.Contains(layerName)) continue;
```

hoặc:

```csharp
if (!IsClosed(points) || points.Count < 4)
    yield break;
```

Đây là các câu điều kiện để kiểm tra dữ liệu có hợp lệ theo lưu đồ hay không.

### 5.7. Nhóm `Parse...`

Ví dụ:

- `ParsePositiveDouble(...)`
- `ParseDouble(...)`
- `ParsePositiveInt(...)`
- `TryParseDimension(...)`

`Parse` nghĩa là **chuyển chữ thành số hoặc dữ liệu có cấu trúc**.

Ví dụ:

```csharp
var zOffset = ParseDouble(BeamZOffset, 0.0);
```

Giải thích:

```text
BeamZOffset lấy từ ô nhập giao diện là string.
Khi vẽ dầm/sàn, cần số để tính toán.
Vì vậy phải parse sang double.
```

### 5.8. Nhóm `Build...`

Ví dụ:

- `BuildLoopsFromSegments(...)`
- `BuildSlabsFromLoops(...)`

`Build` nghĩa là **ghép dữ liệu nhỏ thành dữ liệu lớn hơn**.

Ví dụ với sàn:

```text
Các đoạn line rời -> ghép thành loop kín -> tạo SlabModel
```

Vì vậy dùng `Build`, không dùng `Read`, vì lúc này dữ liệu đã được đọc rồi, code đang dựng cấu trúc mới từ dữ liệu đó.

### 5.9. Nhóm `Deduplicate...`

Ví dụ:

- `Deduplicate(columns)`
- `Deduplicate(beams)`
- `DeduplicateLoops(loops)`

`Deduplicate` nghĩa là **loại bỏ phần tử trùng**.

CAD import có thể tạo ra dữ liệu lặp do polyline, block hoặc nhiều segment trùng nhau. Hàm này giúp danh sách kết quả không bị nhân đôi.

## 6. Giải thích tên biến trong thuật toán đọc CAD

### 6.1. `geometryByLayer`

Vị trí thường gặp:

```csharp
var geometryByLayer = reader.GetGeometryByLayer(instance);
```

Tên này nghĩa là:

```text
geometry = hình học CAD
byLayer = được nhóm theo layer
```

Kiểu dữ liệu:

```csharp
Dictionary<string, List<GeometryObject>>
```

Giải thích:

```text
Tên layer -> danh sách đối tượng hình học trong layer đó
```

### 6.2. `layerSet`

Ví dụ trong `ColumnReaderService`:

```csharp
var layerSet = new HashSet<string>(columnLayerNames, StringComparer.OrdinalIgnoreCase);
```

`layerSet` là tập layer được phép đọc.

**Vì sao có biến này?**

Vì người dùng hoặc auto mapping đã quyết định layer nào là cột, dầm, sàn. Code chỉ xử lý các layer hợp lệ đó.

### 6.3. `geometryObject`

`GeometryObject` là đối tượng hình học thô do Revit API trả về từ CAD import.

Nó có thể là:

- `Line`
- `PolyLine`
- text geometry
- geometry khác

Ví dụ:

```csharp
if (geometryObject is Line line)
```

Câu này là điều kiện:

```text
Nếu đối tượng hình học hiện tại là đường thẳng thì xử lý như line.
```

### 6.4. `points`

`points` là danh sách điểm lấy từ `PolyLine`.

Ví dụ:

```csharp
var points = polyLine.GetCoordinates()
    .Select(ToPoint2D)
    .ToList();
```

Giải thích:

```text
GetCoordinates() lấy các điểm gốc của polyline từ CAD.
ToPoint2D chuyển điểm Revit XYZ sang điểm 2D đơn vị mm.
ToList() gom lại thành danh sách để kiểm tra hình học.
```

### 6.5. `startPoint`, `endPoint`

Hai biến này là điểm đầu và điểm cuối của line/segment.

Ví dụ:

```csharp
var startPoint = ToPoint2D(start);
var endPoint = ToPoint2D(end);
```

**Vì sao cần 2 điểm?**

Đường thẳng trong CAD và Revit được xác định bằng 2 điểm. Từ 2 điểm có thể tính:

- Chiều dài.
- Góc.
- Phương ngang/dọc.
- Trung điểm.
- Đường tim.

### 6.6. `width`, `height`

`width` và `height` là kích thước B và H của cấu kiện.

Ví dụ với cột:

```csharp
TryReadRectangle(points, out var width, out var height, out var center, out var rotationDegrees)
```

Giải thích:

```text
Từ polyline hình chữ nhật, code tính được chiều rộng, chiều cao, tâm và góc xoay.
```

Ví dụ với dầm:

```csharp
ReadBeams(..., defaultWidth, defaultHeight)
```

`defaultWidth`, `defaultHeight` lấy từ ô người dùng nhập trên giao diện.

### 6.7. `center`, `centerPoint`, `CenterPoint`

Tên này dùng cho tâm hình học.

Ví dụ:

- Tâm cột: trung bình tọa độ các đỉnh hình chữ nhật.
- Tâm dầm: trung điểm đường tim dầm.
- Tâm sàn: centroid của polygon.

**Vì sao cần tâm?**

Tâm dùng để:

- Đặt cột đúng vị trí.
- So sánh trùng.
- Xác định lỗ sàn nằm trong biên sàn.
- Tìm text kích thước gần dầm.

### 6.8. `rotationDegrees`

`rotationDegrees` là góc xoay tính theo độ.

**Vì sao dùng độ mà không dùng radian?**

Vì đọc hiểu và giải thích dễ hơn. Tuy nhiên khi gọi Revit API xoay phần tử, code sẽ chuyển sang radian nếu API yêu cầu.

### 6.9. `tolerance`

Các biến như:

- `PointToleranceMm`
- `AxisToleranceDegrees`
- `DuplicateToleranceMm`
- `BeamWidthToleranceMm`

`Tolerance` là sai số cho phép.

**Vì sao cần tolerance?**

CAD/Revit thường có sai số số học hoặc điểm không khớp tuyệt đối. Nếu so sánh bằng tuyệt đối, code dễ bỏ sót.

Ví dụ:

```text
Khoảng cách lý thuyết: 300 mm
Khoảng cách đọc được: 300.000001 mm
```

Về bản chất vẫn đúng, nên cần sai số cho phép.

## 7. Giải thích tên biến trong thuật toán tạo Revit

### 7.1. `baseLevel`, `topLevel`, `level`

Các biến này liên quan đến level Revit.

- `baseLevel`: level chân cấu kiện.
- `topLevel`: level đỉnh cấu kiện.
- `level`: level đang thao tác chung.

Ví dụ với cột:

```csharp
SetColumnHeight(instance, baseLevel, topLevel, baseOffsetMm, topOffsetMm);
```

Giải thích:

```text
Đặt cột bắt đầu từ baseLevel, kết thúc tại topLevel, có offset chân và offset đỉnh.
```

### 7.2. `baseOffset`, `topOffset`, `zOffset`

Các biến offset đều là độ lệch theo phương Z.

- `baseOffset`: lệch chân cột so với base level.
- `topOffset`: lệch đầu cột so với top level.
- `zOffset`: lệch dầm/sàn so với level đang vẽ.

**Vì sao dùng `double`?**

Vì offset là số đo, có thể âm, dương hoặc có số lẻ.

### 7.3. `symbol`

`symbol` là `FamilySymbol`, tức là **type family** trong Revit.

Ví dụ:

```csharp
var symbol = GetOrCreateColumnType(baseSymbol, columnModel);
```

Giải thích:

```text
Tìm hoặc tạo type cột có đúng kích thước B x H theo CAD.
```

**Vì sao không tạo FamilyInstance luôn?**

Trong Revit, muốn tạo cột/dầm phải có type trước. Type chứa kích thước B/H. Sau đó mới tạo instance đặt vào model.

### 7.4. `instance`

`instance` là `FamilyInstance`, tức là **cấu kiện thật đã đặt trong Revit**.

Ví dụ:

```csharp
var instance = _doc.Create.NewFamilyInstance(location, symbol, baseLevel, StructuralType.Column);
```

Giải thích:

```text
Dùng vị trí, type family và level để tạo một cột thật trong Revit.
```

### 7.5. `createdElementIds`

```csharp
var createdElementIds = new List<ElementId>(levelResult.CreatedElementIds);
```

Danh sách này lưu id các phần tử vừa tạo.

**Vì sao cần lưu id?**

Sau khi tạo xong, chương trình gọi:

```csharp
FocusCreatedElements(createdElementIds);
```

Mục đích là chọn/hiển thị các cấu kiện vừa tạo trong Revit để người dùng thấy kết quả.

## 8. Vì sao chọn kiểu hàm trả về như hiện tại?

### 8.1. Hàm đọc trả về `List<Model>`

Ví dụ:

```csharp
public List<ColumnModel> ReadColumns(...)
```

**Vì sao không trả về một `ColumnModel`?**

Vì CAD có nhiều cột. Hàm đọc phải trả về danh sách cột.

**Vì sao không trả về `FamilyInstance`?**

Vì đọc CAD chưa tạo Revit. `FamilyInstance` chỉ có sau khi vẽ.

### 8.2. Hàm tạo trả về `CreationResult`

Ví dụ:

```csharp
public ColumnCreationResult CreateColumns(...)
```

**Vì sao không trả về `List<FamilyInstance>`?**

Vì kết quả cần nhiều thông tin hơn danh sách instance:

- Tạo được bao nhiêu.
- Bỏ qua bao nhiêu.
- Lỗi bao nhiêu.
- Id nào đã tạo.
- Thông báo cụ thể.

Ngoài ra, không phải lần nào cũng tạo được tất cả cấu kiện. Có cấu kiện có thể bị bỏ qua vì trùng, thiếu family, thiếu level.

### 8.3. Hàm kiểm tra trả về `bool`

Ví dụ:

```csharp
private static bool IsClosed(IReadOnlyList<Point2D> points)
```

Hàm này chỉ trả lời câu hỏi:

```text
Loop này có khép kín không?
```

Do đó trả về `bool` là đúng nhất.

### 8.4. Hàm tìm kiếm trả về nullable

Ví dụ:

```csharp
private Level? GetActiveLevel()
private FamilySymbol? FindBaseColumnSymbol()
```

Vì tìm kiếm có thể thất bại. Không phải model Revit nào cũng có level hoặc family đúng tên.

Nếu không tìm thấy thì trả về `null`, sau đó code kiểm tra:

```csharp
if (activeLevel == null)
{
    result.Messages.Add("Không xác định được Level hiện hành từ Active View.");
    return result;
}
```

## 9. Ví dụ giải thích khi bị hỏi trực tiếp

### Câu hỏi: Vì sao `GridModel` là class riêng mà không dùng `Grid` của Revit?

Trả lời:

`GridModel` là dữ liệu trung gian đọc từ CAD, còn `Grid` của Revit là đối tượng thật chỉ có sau khi tạo. Luồng code là đọc CAD trước, lưu vào `GridModel`, sau đó `GridCreationService` mới dùng `Grid.Create()` để tạo Revit Grid. Nếu dùng `Grid` ngay từ bước đọc CAD thì sai vì lúc đó chưa có grid trong Revit.

### Câu hỏi: Vì sao các kích thước dùng `double`?

Trả lời:

Vì tọa độ và kích thước đọc từ Revit/CAD là số đo hình học. Khi đổi đơn vị từ feet nội bộ của Revit sang mm có thể có số lẻ, nên dùng `double` để tránh mất chính xác. `int` chỉ phù hợp cho số lượng như số tầng hoặc số cấu kiện.

### Câu hỏi: Vì sao ô nhập số tầng trên giao diện lại là `string`?

Trả lời:

Vì dữ liệu từ `TextBox` là chữ. Người dùng có thể đang nhập dở hoặc nhập sai. ViewModel giữ dạng `string` để giao diện không lỗi, sau đó khi bấm chuyển đổi mới dùng `ParsePositiveInt()` để chuyển sang `int`.

### Câu hỏi: Vì sao hàm đọc cột trả về `List<ColumnModel>`?

Trả lời:

Vì một bản vẽ CAD có nhiều cột. Mỗi cột đọc được là một `ColumnModel`, toàn bộ kết quả là `List<ColumnModel>`. Hàm đọc chưa tạo Revit, nên không trả về `FamilyInstance`.

### Câu hỏi: Vì sao có `Created`, `Skipped`, `Failed` trong result?

Trả lời:

Vì khi tạo cấu kiện, không phải đối tượng nào cũng tạo thành công. Có đối tượng được tạo, có đối tượng bị bỏ qua do trùng, có đối tượng lỗi do thiếu family hoặc dữ liệu sai. Ba biến này giúp thống kê rõ kết quả và hiển thị lại cho người dùng.

### Câu hỏi: Vì sao có `TryReadRectangle`?

Trả lời:

Vì không phải polyline nào trong CAD cũng là cột. Hàm này thử kiểm tra polyline có phải hình chữ nhật hợp lệ không. Nếu đúng thì trả ra kích thước, tâm, góc xoay. Nếu sai thì trả `false` và bỏ qua, chương trình không bị dừng.

## 10. Bảng tóm tắt nhanh

| Tên/kiểu | Ý nghĩa | Vì sao dùng |
|---|---|---|
| `GridModel` | Dữ liệu lưới trục đọc từ CAD | Chưa phải Revit Grid thật |
| `ColumnModel` | Dữ liệu cột đọc từ CAD | Lưu tâm, kích thước, góc xoay trước khi vẽ |
| `BeamModel` | Dữ liệu dầm đọc từ CAD | Dầm cần điểm đầu, điểm cuối và B x H |
| `SlabModel` | Dữ liệu sàn đọc từ CAD | Sàn cần loop ngoài và loop lỗ |
| `Point2D` | Điểm 2D X/Y đơn vị mm | Đọc mặt bằng CAD không cần Z |
| `string` | Chữ, tên, input giao diện | TextBox và tên layer/level là chữ |
| `int` | Số lượng | Dùng cho đếm: số tầng, Created, Failed |
| `double` | Số đo hình học | Cần chính xác tọa độ/kích thước |
| `bool` | Đúng/sai | Dùng cho checkbox, điều kiện kiểm tra |
| `List<T>` | Danh sách nhiều phần tử | Một bản vẽ có nhiều grid/cột/dầm/sàn |
| `ObservableCollection<T>` | Danh sách hiển thị UI | Giao diện tự cập nhật khi danh sách đổi |
| `Dictionary<string, List<GeometryObject>>` | Hình học CAD nhóm theo layer | Đọc theo layer nhanh và rõ |
| `HashSet<T>` | Tập hợp không trùng | Kiểm tra tồn tại nhanh |
| `IReadOnlyCollection<T>` | Danh sách chỉ đọc | Service tạo Revit không sửa danh sách đầu vào |
| `ElementId` | Id phần tử Revit | Dùng để tìm lại/chọn/focus phần tử |
| `Level?`, `Element?` | Có thể không tìm thấy | Tránh lỗi khi Revit thiếu dữ liệu |

## 11. Cách nói tổng quát khi bảo vệ

Có thể trình bày ngắn gọn như sau:

```text
Em đặt tên biến và hàm theo đúng vai trò trong luồng CAD sang Revit.
Các lớp Model lưu dữ liệu trung gian đọc từ CAD, chưa phải cấu kiện Revit.
Các lớp ReaderService chỉ đọc và phân tích hình học CAD.
Các lớp CreationService mới tạo cấu kiện thật trong Revit.
Các hàm Read trả về danh sách Model vì một bản vẽ có nhiều cấu kiện.
Các hàm Create trả về Result vì cần thống kê tạo được, bỏ qua, lỗi và các ElementId đã tạo.
Với kiểu dữ liệu, em dùng string cho dữ liệu nhập và tên, int cho số lượng, double cho kích thước/tọa độ, bool cho điều kiện đúng sai, List cho danh sách nhiều cấu kiện, Dictionary để nhóm hình học theo layer và HashSet để kiểm tra trùng nhanh.
```

## 12. Phụ lục: danh sách tên biến, property, hàm theo từng file

Phần này liệt kê các tên chính đang có trong project để bạn tra cứu nhanh khi bị hỏi. Phạm vi liệt kê là các file code chính trong `ViewModels`, `Models`, `Services`. Không liệt kê file sinh tự động trong `bin`, `obj`, `.vs`.

Ghi chú:

- **Biến/property**: dữ liệu được lưu trong class.
- **Hằng số**: giá trị cố định dùng làm ngưỡng, tên family, tên marker.
- **Hàm/method**: hành động xử lý.
- **Record/class nội bộ**: kiểu dữ liệu phụ chỉ dùng bên trong service.

### 12.1. `ViewModels/MainViewModel.cs`

Class:

- `MainViewModel`
- `DwgReadResult`
- `StatusType`

Biến private:

- `_mappingService`
- `_uiDoc`
- `_doc`
- `_currentDwgInstanceId`
- `_parsedGrids`
- `_parsedColumns`
- `_parsedBeams`
- `_parsedSlabs`

Event:

- `RequestClose`

Property sinh từ `[ObservableProperty]`:

- `DwgFileName`
- `IsFileLoaded`
- `StatusMessage`
- `StatusColor`
- `Layers`
- `GridCount`
- `GridLayerCount`
- `ColumnCount`
- `BeamCount`
- `SlabCount`
- `FloorHeight`
- `TypicalHeight`
- `NumberOfFloors`
- `ColumnBaseOffset`
- `ColumnTopOffset`
- `SlabThickness`
- `BeamWidth`
- `BeamHeight`
- `BeamZOffset`
- `BeamLevelNames`
- `SelectedBeamLevelName`
- `XStartName`
- `YStartName`
- `XNamingDirection`
- `YNamingDirection`
- `CreateGrid`
- `CreateColumn`
- `CreateBeam`
- `CreateSlab`

Hàm:

- `Initialize`
- `LoadLevelOptions`
- `AutoDetectDwg`
- `ImportDwg`
- `ReadCad`
- `ApplyLayer`
- `ConvertTo3D`
- `ReadDwgFromRevit`
- `ParseGeometryByLayer`
- `ReparseCurrentLayerMapping`
- `FocusCreatedElements`
- `FindPlanViewForActiveLevel`
- `GetLevelIndex`
- `UpdateCounts`
- `ResetCounts`
- `GetCurrentDwgInstance`
- `IsGridLayer`
- `IsColumnLayer`
- `IsBeamLayer`
- `IsBeamLayerRobust`
- `IsSlabLayerRobust`
- `NormalizeText`
- `ParsePositiveDouble`
- `ParseDouble`
- `ParsePositiveInt`
- `SetStatus`

Property trong `DwgReadResult`:

- `Layers`
- `Grids`
- `Columns`
- `Beams`
- `Slabs`

### 12.2. `Models/Elements/GridModel.cs`

Class:

- `GridModel`
- `Point2D`

Property của `GridModel`:

- `Name`
- `LayerName`
- `StartPoint`
- `EndPoint`
- `IsVertical`
- `Length`
- `MidPoint`

Hàm của `GridModel`:

- `ToString`

Property của `Point2D`:

- `X`
- `Y`

Hàm của `Point2D`:

- `Point2D`
- `ToString`

### 12.3. `Models/Elements/ColumnModel.cs`

Class:

- `ColumnModel`

Property:

- `LayerName`
- `CenterPoint`
- `Width`
- `Height`
- `RotationDegrees`
- `PrimaryAxis`

Hàm:

- `ToString`

### 12.4. `Models/Elements/BeamModel.cs`

Class:

- `BeamModel`

Property:

- `LayerName`
- `StartPoint`
- `EndPoint`
- `CenterPoint`
- `Width`
- `Height`
- `RotationDegrees`
- `DimensionText`
- `SourceType`
- `Length`

Hàm:

- `ToString`

### 12.5. `Models/Elements/SlabModel.cs`

Class:

- `SlabModel`

Property:

- `LayerName`
- `OuterLoop`
- `OpeningLoops`
- `Thickness`
- `Area`
- `CenterPoint`

Hàm:

- `ToString`

### 12.6. `Models/Mapping/DwgLayer.cs`

Class:

- `DwgLayer`

Biến private:

- `_elementType`
- `_isAutoMapped`

Property:

- `LayerName`
- `EntityCount`
- `ElementType`
- `IsAutoMapped`
- `MappingSource`
- `IsIgnored`

Event:

- `PropertyChanged`

Hàm:

- `OnPropertyChanged`

### 12.7. `Models/Mapping/LayerMappingRule.cs`

Class:

- `LayerMappingRule`

Property:

- `Keyword`
- `ElementType`
- `Priority`

Hàm:

- `LayerMappingRule`

### 12.8. `Models/Mapping/RevitCreationResult.cs`

Enum:

- `CreationStatus`

Giá trị enum:

- `Success`
- `Skipped`
- `Failed`
- `Conflict`

Class:

- `RevitCreationResult`

Property:

- `ElementType`
- `ElementName`
- `Status`
- `Message`

Hàm:

- `Ok`
- `Fail`
- `Skip`

### 12.9. `Models/Results/*CreationResult.cs`

Các class:

- `GridCreationResult`
- `ColumnCreationResult`
- `BeamCreationResult`
- `SlabCreationResult`
- `LevelCreationResult`

Property dùng chung trong `GridCreationResult`, `ColumnCreationResult`, `BeamCreationResult`, `SlabCreationResult`:

- `Created`
- `Skipped`
- `Failed`
- `CreatedElementIds`
- `Messages`

Property riêng của `LevelCreationResult`:

- `Created`
- `Updated`
- `Failed`
- `Levels`
- `CreatedElementIds`
- `Messages`

### 12.10. `Services/Import/CadImportService.cs`

Class:

- `CadImportService`

Biến private:

- `_doc`

Hàm:

- `CadImportService`
- `ImportDwg`
- `GetImportView`

Tham số quan trọng:

- `filePath`

### 12.11. `Services/Parsing/RevitDwgReaderService.cs`

Class:

- `RevitDwgReaderService`
- `GridNamingOptions`
- `GridNameSequence`

Biến private/hằng số:

- `_doc`
- `AxisToleranceDegrees`
- `MinGridLengthMm`
- `_isNumeric`
- `_number`
- `_letters`

Property của `GridNamingOptions`:

- `XStartName`
- `YStartName`
- `XLeftToRight`
- `YBottomToTop`

Property của `GridNameSequence`:

- `Current`

Hàm:

- `RevitDwgReaderService`
- `FindDwgInstance`
- `GetAllDwgInstances`
- `GetLayersFromInstance`
- `GetGeometryByLayer`
- `ReadGridLines`
- `ExtractGeometry`
- `HasReadableText`
- `ReadGridFromGeometry`
- `CreateGrid`
- `NameAndSortGrids`
- `ApplyNames`
- `GetLayerName`
- `AddToLayer`
- `TryGetAxisDirection`
- `ToPoint2D`
- `Distance`
- `GridNameSequence`
- `FromStartName`
- `MoveNext`
- `LettersToNumber`
- `NumberToLetters`

Tham số/tên dữ liệu quan trọng:

- `dwgInstance`
- `gridLayerNames`
- `namingOptions`
- `geomElem`
- `result`
- `obj`
- `geometryObject`
- `layerName`
- `start`
- `end`
- `grids`
- `startName`
- `dict`
- `layerKey`
- `isVertical`
- `point`
- `value`

### 12.12. `Services/Parsing/LayerMappingService.cs`

Class:

- `LayerMappingService`

Biến/hằng số:

- `DefaultRules`

Hàm:

- `AutoMap`
- `FindBestMatch`
- `NormalizeText`
- `GetDefaultRules`

Tham số/tên dữ liệu quan trọng:

- `layers`
- `layerName`
- `value`

### 12.13. `Services/Parsing/ColumnReaderService.cs`

Class:

- `ColumnReaderService`

Hằng số:

- `PointToleranceMm`
- `OrthogonalToleranceDegrees`
- `MinColumnSideMm`
- `MaxColumnSideMm`

Hàm:

- `ReadColumns`
- `ReadColumnFromGeometry`
- `ToClosedPointList`
- `TryReadRectangle`
- `TryGetAxisAlignedDimensions`
- `Deduplicate`
- `RemoveConsecutiveDuplicates`
- `AreSamePoint`
- `AreOppositeSidesEqual`
- `IsRightAngle`
- `IsHorizontal`
- `IsVertical`
- `NormalizeAngle`
- `GetAngleDegrees`
- `Distance`
- `ToPoint2D`

Tham số/tên dữ liệu quan trọng:

- `geometryByLayer`
- `columnLayerNames`
- `geometryObject`
- `layerName`
- `polyLine`
- `points`
- `width`
- `height`
- `center`
- `rotationDegrees`
- `edges`
- `horizontalEdges`
- `verticalEdges`
- `columns`
- `a`
- `b`
- `angleA`
- `angleB`
- `angle`
- `start`
- `end`
- `point`

### 12.14. `Services/Parsing/BeamReaderService.cs`

Class/record:

- `BeamReaderService`
- `BeamSegment`
- `BeamDimensionNote`

Hằng số:

- `MinBeamLengthMm`
- `MaxBeamWidthMm`
- `MinBeamWidthMm`
- `AxisToleranceDegrees`
- `DuplicateToleranceMm`
- `TextSearchMaxDistanceMm`
- `BeamWidthToleranceMm`

Property của `BeamSegment`:

- `LayerName`
- `Start`
- `End`
- `Center`
- `Length`
- `AngleDegrees`

Property của `BeamDimensionNote`:

- `Text`
- `Location`
- `Width`
- `Height`

Hàm:

- `ReadBeams`
- `DetectBeamsFromBoundariesAndAxes`
- `FindBestMiddleAxis`
- `GetParallelDistanceOrMax`
- `ReadBeamSegments`
- `CreateSegment`
- `DetectCenterLinesFromBoundaryPairs`
- `CreateCenterLine`
- `CreateBeam`
- `ReadDimensionNotes`
- `CreateBeamsFromDimensionNotes`
- `TryParseDimension`
- `ParseNumber`
- `TryGetText`
- `TryGetGeometryCenter`
- `Deduplicate`
- `AreSameLine`
- `TryGetAxisDirection`
- `AreParallel`
- `TryGetParallelDistance`
- `HasEnoughOverlap`
- `GetOverlapLength`
- `OrderAlongMainAxis`
- `NormalizeAngle`
- `DistancePointToSegment`
- `ToPoint2D`
- `Distance`

Tham số/tên dữ liệu quan trọng:

- `geometryByLayer`
- `beamLayerNames`
- `axisLayerNames`
- `defaultWidth`
- `defaultHeight`
- `layerSet`
- `axisLayerSet`
- `beamSegments`
- `axisSegments`
- `boundarySegments`
- `beamWidth`
- `beamHeight`
- `centerLine`
- `firstBoundary`
- `secondBoundary`
- `axis`
- `first`
- `second`
- `distance`
- `segment`
- `segments`
- `sourceType`
- `notes`
- `text`
- `width`
- `height`
- `geometryObject`
- `beams`
- `a1`
- `a2`
- `b1`
- `b2`
- `isVertical`
- `angleA`
- `angleB`
- `overlap`
- `point`
- `start`
- `end`
- `value`

### 12.15. `Services/Parsing/SlabReaderService.cs`

Class/record:

- `SlabReaderService`
- `SlabSegment`
- `SlabLoop`

Hằng số:

- `PointToleranceMm`
- `MinSegmentLengthMm`
- `MinLoopAreaMm2`

Property của `SlabSegment`:

- `LayerName`
- `Start`
- `End`

Property của `SlabLoop`:

- `LayerName`
- `Points`
- `Area`
- `CenterPoint`

Hàm:

- `ReadSlabs`
- `ReadClosedPolylineLoops`
- `ReadSegments`
- `CreateSegment`
- `BuildLoopsFromSegments`
- `BuildSlabsFromLoops`
- `DeduplicateLoops`
- `NormalizeLoop`
- `IsClosed`
- `AreSamePoint`
- `IsPointInsidePolygon`
- `GetSignedArea`
- `GetCentroid`
- `ToPoint2D`
- `Distance`

Tham số/tên dữ liệu quan trọng:

- `geometryByLayer`
- `slabLayerNames`
- `thicknessMm`
- `layerSet`
- `allLoops`
- `geometryObject`
- `layerName`
- `segments`
- `polyLine`
- `points`
- `start`
- `end`
- `loops`
- `unused`
- `current`
- `outer`
- `openings`
- `candidate`
- `opening`
- `point`
- `polygon`
- `area`
- `signedArea`

### 12.16. `Services/Creation/LevelCreationService.cs`

Class:

- `LevelCreationService`

Biến/hằng số:

- `_doc`
- `ElevationToleranceMm`
- `FirstLevelName`
- `RoofLevelName`

Hàm:

- `CreateOrUpdateLevels`
- `GetOrCreateBaseLevel`
- `GetOrCreateLevel`
- `GetLevels`
- `TrySetLevelName`
- `MmToFeet`

Tham số/tên dữ liệu quan trọng:

- `numberOfFloors`
- `firstFloorHeightMm`
- `typicalFloorHeightMm`
- `result`
- `baseLevel`
- `baseElevation`
- `index`
- `name`
- `elevationMm`
- `levelName`
- `targetElevation`
- `level`
- `value`

### 12.17. `Services/Creation/GridCreationService.cs`

Class/class nội bộ:

- `GridCreationService`
- `GridPlacement`

Biến/hằng số:

- `_doc`
- `LevelEndExtensionMm`
- `VerticalLevelExtensionMm`
- `DuplicateToleranceMm`

Property của `GridPlacement`:

- `MinX`
- `MaxX`
- `MinY`
- `MaxY`
- `CenterX`
- `CenterY`

Hàm:

- `CreateGrids`
- `HideDwgImportsInAllViews`
- `GetActiveLevel`
- `GetExistingGridNames`
- `GetExistingGridKeys`
- `TryGetExistingGridKey`
- `CreateRevitLine`
- `GetVerticalExtents`
- `UpdateLevelExtentsInElevationViews`
- `TryUpdateLevelExtentInView`
- `GetLevelLineInView`
- `SetLevelEndToViewSpecific`
- `SetLevelCurveInView`
- `GetGridKey`
- `CanHide`
- `RoundToTolerance`
- `MmToFeet`
- `FeetToMm`
- `Clamp`
- `From`

Tham số/tên dữ liệu quan trọng:

- `gridModels`
- `result`
- `activeLevel`
- `existingNames`
- `createdGridKeys`
- `existingGridKeys`
- `placement`
- `verticalExtents`
- `model`
- `gridKey`
- `line`
- `grid`
- `view`
- `elementId`
- `level`
- `extentType`
- `end`
- `value`
- `min`
- `max`

### 12.18. `Services/Creation/ColumnCreationService.cs`

Class/class nội bộ:

- `ColumnCreationService`
- `ColumnPlacement`

Biến/hằng số:

- `_doc`
- `DuplicateToleranceMm`
- `GridSnapToleranceMm`
- `DefaultStoryHeightMm`
- `GeneratedColumnTypePrefix`
- `GeneratedColumnMarker`
- `DefaultColumnFamilyName`

Property của `ColumnPlacement`:

- `CenterX`
- `CenterY`
- `VerticalGridXs`
- `HorizontalGridYs`

Hàm:

- `CreateColumns`
- `GetBaseLevel`
- `GetNextLevel`
- `FindBaseColumnSymbol`
- `GetOrCreateColumnType`
- `GetColumnTypeName`
- `TrySetColumnDimensions`
- `TrySetParameter`
- `SetColumnHeight`
- `RotateColumn`
- `DeleteGeneratedColumns`
- `IsGeneratedColumn`
- `MarkGeneratedColumn`
- `GetExistingColumnKeys`
- `GetColumnKey`
- `GetPointKey`
- `TrySetElementIdParameter`
- `TrySetDoubleParameter`
- `RoundToTolerance`
- `MmToFeet`
- `FeetToMm`
- `From`
- `TryResolveColumnPoint`
- `TryFindMatchingCoordinate`

Tham số/tên dữ liệu quan trọng:

- `columnModels`
- `gridModels`
- `baseLevel`
- `topLevel`
- `baseOffsetMm`
- `topOffsetMm`
- `result`
- `baseSymbol`
- `columnModel`
- `symbol`
- `location`
- `instance`
- `rotationDegrees`
- `elementId`
- `point`
- `placement`
- `coordinates`
- `cadValue`
- `matched`
- `cadCenter`
- `resolvedPoint`
- `gridX`
- `gridY`
- `xMm`
- `yMm`
- `zMm`
- `value`

### 12.19. `Services/Creation/BeamCreationService.cs`

Class/class nội bộ:

- `BeamCreationService`
- `BeamPlacement`

Biến/hằng số:

- `_doc`
- `DuplicateToleranceMm`
- `MinBeamLengthMm`
- `GeneratedBeamMarker`
- `DefaultBeamFamilyName`

Property của `BeamPlacement`:

- `CenterX`
- `CenterY`

Hàm:

- `CreateBeams`
- `GetBeamLevel`
- `FindBaseBeamSymbol`
- `GetOrCreateBeamType`
- `GetBeamTypeName`
- `TrySetBeamDimensions`
- `TrySetParameter`
- `SetBeamOffsets`
- `DeleteGeneratedBeams`
- `IsGeneratedBeam`
- `MarkGeneratedBeam`
- `GetExistingBeamKeys`
- `GetBeamKey`
- `GetPointKey`
- `TrySetElementIdParameter`
- `TrySetDoubleParameter`
- `RoundToTolerance`
- `MmToFeet`
- `FeetToMm`
- `From`
- `ToRevitPoint`

Tham số/tên dữ liệu quan trọng:

- `beamModels`
- `gridModels`
- `beamLevelName`
- `zOffsetMm`
- `beamLevel`
- `result`
- `baseSymbol`
- `beamModel`
- `symbol`
- `level`
- `zOffset`
- `start`
- `end`
- `line`
- `instance`
- `element`
- `names`
- `valueMm`
- `builtInParameter`
- `value`
- `xMm`
- `yMm`
- `zMm`
- `point`
- `elevation`

### 12.20. `Services/Creation/SlabCreationService.cs`

Class/class nội bộ:

- `SlabCreationService`
- `SlabPlacement`

Biến/hằng số:

- `_doc`
- `DuplicateToleranceMm`
- `MinLoopAreaMm2`
- `GeneratedSlabMarker`

Property của `SlabPlacement`:

- `CenterX`
- `CenterY`

Hàm:

- `CreateSlabs`
- `GetSlabLevel`
- `FindBaseFloorType`
- `GetOrCreateFloorType`
- `GetFloorTypeName`
- `TrySetFloorThickness`
- `BuildProfile`
- `BuildCurveLoop`
- `SetSlabOffset`
- `DeleteGeneratedSlabs`
- `IsGeneratedSlab`
- `MarkGeneratedSlab`
- `GetExistingSlabKeys`
- `GetExistingSlabKey`
- `GetSlabKey`
- `GetBoxKey`
- `GetFloorThicknessMm`
- `GetPointKey`
- `TrySetDoubleParameter`
- `RoundToTolerance`
- `MmToFeet`
- `FeetToMm`
- `From`
- `ToRevitPoint`

Tham số/tên dữ liệu quan trọng:

- `slabModels`
- `gridModels`
- `slabLevelName`
- `zOffsetMm`
- `slabLevel`
- `result`
- `baseType`
- `thicknessMm`
- `floorType`
- `slabModel`
- `placement`
- `elevation`
- `points`
- `loop`
- `floor`
- `zOffset`
- `outerLoop`
- `openingLoops`
- `existingKeys`
- `key`
- `minX`
- `maxX`
- `minY`
- `maxY`
- `xMm`
- `yMm`
- `value`
- `point`

