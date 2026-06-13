# Giải thích code theo lưu đồ thuật toán

Tài liệu này dùng để giải thích với giáo viên cách code hiện tại bám theo các lưu đồ: **lưới trục**, **cột**, **dầm**, **sàn**, và phần **phát triển cấu kiện theo tầng / tạo Level**.

Mục tiêu của tài liệu:

- Nói được **luồng chạy tổng quan**: bắt đầu từ file nào, gọi đến file nào.
- Nói được **mỗi khối trong lưu đồ ứng với đoạn code nào**.
- Chỉ rõ **câu điều kiện** trong code dùng để kiểm tra gì.
- Chỉ rõ **câu xử lý** trong code lấy dữ liệu gì, tạo object gì, gửi đi đâu.
- Ghi riêng các phần **code có xử lý thêm ngoài lưu đồ** để giải thích khi bị hỏi.

---

## 0. Luồng tổng quan của chương trình

### 0.1. Luồng đọc CAD

Khi bấm nút **Đọc CAD**, code chạy theo luồng:

```text
Views/MainWindow.xaml
    -> ViewModels/MainViewModel.cs
        -> ReadCad()
        -> ReadDwgFromRevit()
        -> ParseGeometryByLayer()
            -> RevitDwgReaderService
            -> ColumnReaderService
            -> BeamReaderService
            -> SlabReaderService
```

Ý nghĩa từng file:

- `Views/MainWindow.xaml`: giao diện người dùng.
- `ViewModels/MainViewModel.cs`: nhận lệnh từ giao diện, kiểm tra dữ liệu, gọi service xử lý.
- `RevitDwgReaderService.cs`: đọc file CAD import trong Revit, lấy layer và geometry.
- `ColumnReaderService.cs`: đọc cột từ polyline CAD.
- `BeamReaderService.cs`: đọc dầm từ các line biên dầm và line trục.
- `SlabReaderService.cs`: đọc sàn từ polyline khép kín hoặc từ các line rời ghép thành loop.

Kết quả đọc được gom vào class nội bộ `DwgReadResult` trong `MainViewModel.cs`:

```csharp
private class DwgReadResult
{
    public List<DwgLayer> Layers { get; set; } = new();
    public List<GridModel> Grids { get; set; } = new();
    public List<ColumnModel> Columns { get; set; } = new();
    public List<BeamModel> Beams { get; set; } = new();
    public List<SlabModel> Slabs { get; set; } = new();
}
```

Hiểu đơn giản:

```text
DwgReadResult = gói kết quả sau khi đọc CAD
Layers = danh sách layer
Grids = lưới trục đã đọc
Columns = cột đã đọc
Beams = dầm đã đọc
Slabs = sàn đã đọc
```

### 0.2. Luồng chuyển đổi sang 3D

Khi bấm nút **Chuyển đổi sang 3D**, code chạy theo luồng:

```text
Views/MainWindow.xaml
    -> ViewModels/MainViewModel.cs
        -> ConvertTo3D()
            -> LevelCreationService.CreateOrUpdateLevels()
            -> GridCreationService.CreateGrids()
            -> ColumnCreationService.CreateColumns()
            -> BeamCreationService.CreateBeams()
            -> SlabCreationService.CreateSlabs()
```

Ý nghĩa:

- `ConvertTo3D()` là hàm điều phối chính.
- Hàm này kiểm tra người dùng đã tích chọn cấu kiện nào.
- Sau đó gọi đúng service để tạo cấu kiện tương ứng trong Revit.
- Nếu người dùng không tích loại cấu kiện nào thì không tạo loại đó.

---

## 1. Lưới trục

Lưới trục có 2 giai đoạn:

```text
Đọc CAD -> GridModel
GridModel -> Revit Grid
```

File đọc lưới trục:

```text
Services/Parsing/RevitDwgReaderService.cs
```

File tạo lưới trục:

```text
Services/Creation/GridCreationService.cs
```

Model dữ liệu:

```text
Models/Elements/GridModel.cs
```

### 1.1. Khối "Đọc dữ liệu từ file CAD import"

Trong `MainViewModel.cs`, hàm `ReadCad()` gọi:

```csharp
var result = await Task.Run(ReadDwgFromRevit);
```

Sau đó `ReadDwgFromRevit()` tạo reader:

```csharp
var reader = new RevitDwgReaderService(_doc!);
var instance = GetCurrentDwgInstance(reader);
```

Ý nghĩa:

- `reader`: object chuyên đọc CAD trong Revit.
- `instance`: file CAD import đang tồn tại trong Revit.
- Nếu `instance == null` thì không có CAD để đọc, code dừng và báo lỗi.

### 1.2. Khối "Nhập thông tin layer, tên lưới"

Thông tin lấy từ giao diện nằm trong `MainViewModel.cs`:

```csharp
XStartName
YStartName
XNamingDirection
YNamingDirection
Layers
```

Code gom thông tin này thành `GridNamingOptions`:

```csharp
new GridNamingOptions
{
    XStartName = XStartName,
    YStartName = YStartName,
    XLeftToRight = XNamingDirection == "Trái sang phải",
    YBottomToTop = YNamingDirection == "Dưới lên trên"
}
```

Ý nghĩa:

- `XStartName`: tên bắt đầu của nhóm trục X, ví dụ A.
- `YStartName`: tên bắt đầu của nhóm trục Y, ví dụ 1.
- `XLeftToRight`: có đánh tên X từ trái sang phải không.
- `YBottomToTop`: có đánh tên Y từ dưới lên trên không.

Đây là **câu xử lý**, không phải câu điều kiện. Nó lấy dữ liệu từ form và gửi sang hàm đọc lưới.

### 1.3. Khối "Duyệt từng đường line trong CAD"

Trong `RevitDwgReaderService.cs`, hàm:

```csharp
public List<GridModel> ReadGridLines(
    Element dwgInstance,
    IEnumerable<string> gridLayerNames,
    GridNamingOptions namingOptions)
```

Hàm này nhận:

- `dwgInstance`: file CAD import trong Revit.
- `gridLayerNames`: danh sách layer được gán là lưới trục.
- `namingOptions`: quy tắc đánh tên lưới.

Code lấy geometry theo layer:

```csharp
var geometryByLayer = GetGeometryByLayer(dwgInstance);
```

`geometryByLayer` có dạng:

```text
Tên layer -> danh sách GeometryObject trong layer đó
```

Ví dụ:

```text
"Net Truc" -> [line1, line2, line3]
"BEAM"     -> [line1, polyline1]
```

### 1.4. Khối "Kiểm tra layer có hợp lệ không"

Trong `ReadGridLines()`:

```csharp
var layerSet = new HashSet<string>(gridLayerNames, StringComparer.OrdinalIgnoreCase);
if (layerSet.Count == 0) return new List<GridModel>();
```

Ý nghĩa:

- `layerSet`: tập layer được phép đọc làm lưới trục.
- Nếu không có layer nào được chọn làm lưới trục thì trả về danh sách rỗng.

Khi duyệt từng layer:

```csharp
if (!layerSet.Contains(layerName)) continue;
```

Ý nghĩa:

- Nếu layer hiện tại không thuộc layer lưới trục thì bỏ qua.
- Đây là câu điều kiện đúng với khối "kiểm tra layer có hợp lệ không" trong lưu đồ.

### 1.5. Code lấy đường trong CAD như thế nào?

Hàm xử lý từng hình học:

```csharp
private IEnumerable<GridModel> ReadGridFromGeometry(
    GeometryObject geometryObject,
    string layerName)
```

Nếu CAD trả về `Line`:

```csharp
if (geometryObject is Line line)
{
    var grid = CreateGrid(line.GetEndPoint(0), line.GetEndPoint(1), layerName);
    if (grid != null) yield return grid;
    yield break;
}
```

Ý nghĩa:

- `line.GetEndPoint(0)`: lấy điểm đầu của line.
- `line.GetEndPoint(1)`: lấy điểm cuối của line.
- `CreateGrid(...)`: kiểm tra line có hợp lệ không rồi tạo `GridModel`.

Nếu CAD trả về `PolyLine`:

```csharp
if (geometryObject is PolyLine polyLine)
{
    var points = polyLine.GetCoordinates();
    for (int i = 0; i < points.Count - 1; i++)
    {
        var grid = CreateGrid(points[i], points[i + 1], layerName);
        if (grid != null) yield return grid;
    }
}
```

Ý nghĩa:

- `polyLine.GetCoordinates()`: lấy toàn bộ điểm của polyline.
- Mỗi cặp điểm liên tiếp `points[i]` và `points[i + 1]` được xem như một đoạn line.
- Mỗi đoạn hợp lệ được chuyển thành một `GridModel`.

Kết luận:

```text
Code không dùng lệnh đo kích thước của AutoCAD.
Code lấy trực tiếp điểm đầu/cuối của Line hoặc danh sách điểm của PolyLine từ Revit API.
```

### 1.6. Khối "Xác định phương của đường thẳng"

Trong `CreateGrid()`:

```csharp
if (!TryGetAxisDirection(start, end, out bool isVertical)) return null;
```

Ý nghĩa:

- `TryGetAxisDirection(...)`: thử xác định đường đang xét là ngang hay dọc.
- `out bool isVertical`: nếu hợp lệ, hàm trả thêm kết quả đường đó có phải trục dọc không.
- Dấu `!` nghĩa là phủ định.
- Nếu không xác định được hướng hợp lệ thì trả `null`, tức là bỏ qua line đó.

Hàm `TryGetAxisDirection()` tính góc từ 2 điểm:

```csharp
var dx = end.X - start.X;
var dy = end.Y - start.Y;
var angle = Math.Abs(Math.Atan2(dy, dx) * 180.0 / Math.PI);
```

Sau đó kiểm tra:

- Gần 0 độ hoặc 180 độ -> đường ngang.
- Gần 90 độ -> đường dọc.
- Khác các trường hợp trên -> không phải lưới trục hợp lệ.

### 1.7. Khối "Tính tọa độ và chiều dài"

Trong `CreateGrid()`:

```csharp
var startPoint = ToPoint2D(start);
var endPoint = ToPoint2D(end);
var length = Distance(startPoint, endPoint);
```

Ý nghĩa:

- `start`, `end` ban đầu là `XYZ` của Revit.
- `ToPoint2D(...)` chuyển sang `Point2D` của project.
- `Distance(...)` tính chiều dài đường theo đơn vị mm.

Lý do cần chuyển sang `Point2D`:

- Bản vẽ mặt bằng CAD đang xử lý theo 2D.
- Thuật toán chỉ cần X/Y, không cần Z.
- Revit dùng feet nội bộ, project thống nhất dùng mm, nên cần đổi đơn vị.

Sau đó code lọc đường quá ngắn:

```csharp
if (length < MinGridLengthMm) return null;
```

Ý nghĩa:

- Nếu line quá ngắn thì coi là đường rác hoặc ký hiệu, không phải lưới trục.

### 1.8. Khối "Đưa danh sách vào lưới trục X/Y"

Trong `NameAndSortGrids()`:

```csharp
var xGrids = grids
    .Where(g => g.IsVertical)
    .OrderBy(g => namingOptions.XLeftToRight ? g.MidPoint.X : -g.MidPoint.X)
    .ToList();
```

Ý nghĩa:

- `g.IsVertical == true`: trục dọc.
- Trục dọc được xếp vào nhóm X vì nó có tọa độ X cố định.
- Nếu đánh tên trái sang phải thì sắp xếp tăng dần theo X.
- Nếu đánh tên phải sang trái thì sắp xếp giảm dần theo X.

Nhóm Y:

```csharp
var yGrids = grids
    .Where(g => !g.IsVertical)
    .OrderBy(g => namingOptions.YBottomToTop ? g.MidPoint.Y : -g.MidPoint.Y)
    .ToList();
```

Ý nghĩa:

- `!g.IsVertical`: trục ngang.
- Trục ngang được xếp vào nhóm Y vì nó có tọa độ Y cố định.

### 1.9. Khối "Đánh tên theo phương tự động"

Code gọi:

```csharp
ApplyNames(xGrids, namingOptions.XStartName);
ApplyNames(yGrids, namingOptions.YStartName);
```

Trong `ApplyNames()`:

```csharp
var name = GridNameSequence.FromStartName(startName);

foreach (var grid in grids)
{
    grid.Name = name.Current;
    name.MoveNext();
}
```

Ý nghĩa:

- Nếu bắt đầu bằng chữ: A, B, C...
- Nếu bắt đầu bằng số: 1, 2, 3...
- Mỗi grid sau khi sắp xếp sẽ được gán tên theo thứ tự.

### 1.10. Tạo lưới trục trong Revit

Trong `GridCreationService.cs`, hàm chính:

```csharp
public GridCreationResult CreateGrids(IReadOnlyCollection<GridModel> gridModels)
```

Code kiểm tra đầu vào:

```csharp
if (gridModels.Count == 0)
{
    result.Messages.Add("Chưa có dữ liệu lưới trục để vẽ.");
    return result;
}
```

Sau đó lấy level hiện hành:

```csharp
var activeLevel = GetActiveLevel();
```

Tạo line Revit:

```csharp
var line = CreateRevitLine(model, activeLevel.Elevation, placement);
var grid = Grid.Create(_doc, line);
grid.Name = model.Name;
```

Ý nghĩa:

- `CreateRevitLine(...)`: chuyển `GridModel` thành `Line` của Revit.
- `Grid.Create(...)`: tạo Revit Grid thật.
- `grid.Name = model.Name`: gán tên trục đã đọc/đánh tự động.

### 1.11. Phần xử lý thêm ngoài lưu đồ của lưới trục

Code có thêm các bước:

- Chống trùng tên lưới trục.
- Chống trùng tọa độ lưới trục.
- Ẩn CAD import sau khi tạo.
- Kéo dài level trong các elevation view để bao phủ grid.

Giải thích khi bị hỏi:

> Lưu đồ chỉ mô tả luồng đọc và phân loại lưới. Code có thêm bước chống trùng và cập nhật hiển thị level để tránh tạo 2 Grid trùng nhau và để kết quả trong Revit dễ quan sát hơn. Đây là xử lý thực tế, không làm sai thuật toán chính.

---

## 2. Cột

Cột có 2 giai đoạn:

```text
Đọc CAD -> ColumnModel
ColumnModel -> Revit FamilyInstance
```

File đọc cột:

```text
Services/Parsing/ColumnReaderService.cs
```

File tạo cột:

```text
Services/Creation/ColumnCreationService.cs
```

Model dữ liệu:

```text
Models/Elements/ColumnModel.cs
```

### 2.1. Khối "Nhập thông tin vào form"

Thông tin cột lấy từ form trong `MainViewModel.cs`:

```csharp
NumberOfFloors
ColumnBaseOffset
ColumnTopOffset
CreateColumn
```

Trong `ConvertTo3D()`:

```csharp
var numberOfFloors = ParsePositiveInt(NumberOfFloors, 1);
var baseOffset = ParseDouble(ColumnBaseOffset, 0.0);
var topOffset = ParseDouble(ColumnTopOffset, 0.0);
```

Ý nghĩa:

- `NumberOfFloors`: số tầng cần phát triển.
- `ColumnBaseOffset`: offset chân cột.
- `ColumnTopOffset`: offset đầu cột.
- `Parse...`: chuyển dữ liệu từ chữ trên form sang số để tính toán.

### 2.2. Khối "Kiểm tra đã có đầy đủ thông tin chưa"

Trong `ConvertTo3D()`:

```csharp
if (CreateColumn && _parsedColumns.Count == 0)
{
    SetStatus("Đã tích vẽ cột nhưng chưa đọc được dữ liệu cột từ CAD.", StatusType.Error);
    return;
}
```

Ý nghĩa:

- Nếu người dùng tích vẽ cột nhưng chưa đọc được cột từ CAD thì dừng.
- Đây là câu điều kiện kiểm tra đầu vào đúng theo lưu đồ.

### 2.3. Khối "Đọc hình học CAD"

Trong `ColumnReaderService.cs`:

```csharp
public List<ColumnModel> ReadColumns(
    Dictionary<string, List<GeometryObject>> geometryByLayer,
    IEnumerable<string> columnLayerNames)
```

Hàm nhận:

- `geometryByLayer`: hình học CAD đã nhóm theo layer.
- `columnLayerNames`: các layer được gán là cột.

Code kiểm tra layer:

```csharp
var layerSet = new HashSet<string>(columnLayerNames, StringComparer.OrdinalIgnoreCase);
if (layerSet.Count == 0) return new List<ColumnModel>();
```

Khi duyệt:

```csharp
if (!layerSet.Contains(layerName)) continue;
```

Ý nghĩa:

- Chỉ đọc geometry thuộc layer cột.
- Các layer khác bị bỏ qua.

### 2.4. Khối "Kiểm tra polyline có hợp lệ?"

Trong `ReadColumnFromGeometry()`:

```csharp
if (geometryObject is not PolyLine polyLine)
    return null;
```

Ý nghĩa:

- Cột trong CAD đang được đọc từ polyline.
- Nếu không phải polyline thì không xử lý làm cột.

Sau đó:

```csharp
var points = ToClosedPointList(polyLine);
if (points.Count != 4)
    return null;
```

Ý nghĩa:

- Cột chữ nhật cần 4 điểm sau khi loại điểm trùng.
- Nếu không đúng 4 điểm thì không coi là cột chữ nhật hợp lệ.

### 2.5. Khối "Xác định trục chính, tính B và H"

Code gọi:

```csharp
if (!TryReadRectangle(points, out var width, out var height, out var center, out var rotationDegrees))
    return null;
```

Ý nghĩa:

- `TryReadRectangle(...)`: thử đọc polyline thành hình chữ nhật.
- Nếu không phải hình chữ nhật thì trả `false`, code bỏ qua.
- Nếu hợp lệ thì lấy ra:
  - `width`: chiều rộng B.
  - `height`: chiều cao H.
  - `center`: tâm cột.
  - `rotationDegrees`: góc xoay.

Trong `TryReadRectangle()`, code kiểm tra:

- Chiều dài từng cạnh có nằm trong ngưỡng hợp lý không.
- Hai cạnh đối có bằng nhau không.
- Các cạnh liên tiếp có vuông góc không.
- Xác định B/H theo cạnh ngang/dọc.

Ghi chú:

> Lưu đồ ghi "xác định trục chính". Code hiện tại đã xử lý ở mức cần thiết cho cột chữ nhật bằng cách xác định cạnh ngang/dọc, tính B/H và tâm. Với cột xoay phức tạp bất kỳ thì code chưa xử lý đầy đủ mọi trường hợp.

### 2.6. Khối "Xác định vị trí tâm"

Trong `TryReadRectangle()`:

```csharp
center = new Point2D(points.Average(p => p.X), points.Average(p => p.Y));
```

Ý nghĩa:

- Tâm cột được tính bằng trung bình tọa độ X và Y của các đỉnh.
- Đây là điểm dùng để đặt cột trong Revit.

### 2.7. Khối "Duplicate family type theo kích thước BxH"

Trong `ColumnCreationService.cs`:

```csharp
var symbol = GetOrCreateColumnType(baseSymbol, columnModel);
```

Hàm này:

- Tìm type cột có kích thước đúng B/H.
- Nếu chưa có thì duplicate từ family mặc định.
- Sau đó set parameter `b`, `h`, `Width`, `Height` nếu có.

Family mặc định:

```csharp
private const string DefaultColumnFamilyName = "M_Concrete-Rectangular-Column";
```

### 2.8. Tạo cột trong Revit

Trong `CreateColumns()`:

```csharp
var instance = _doc.Create.NewFamilyInstance(
    location,
    symbol,
    baseLevel,
    StructuralType.Column);
```

Ý nghĩa:

- `location`: vị trí đặt cột.
- `symbol`: type cột đúng kích thước.
- `baseLevel`: level chân cột.
- `StructuralType.Column`: tạo structural column.

Sau đó set chiều cao:

```csharp
SetColumnHeight(instance, baseLevel, topLevel, baseOffsetMm, topOffsetMm);
```

Ý nghĩa:

- Cột xuất phát từ base level.
- Cột kết thúc ở top level.
- Có áp dụng base offset và top offset theo form.

### 2.9. Cột phát triển dựa trên lưới trục

Trong `ColumnCreationService`, class nội bộ `ColumnPlacement` dùng grid để quy chiếu vị trí:

```csharp
public bool TryResolveColumnPoint(Point2D cadCenter, out Point2D resolvedPoint)
```

Ý nghĩa:

- `cadCenter`: tâm cột đọc từ CAD.
- Code tìm tọa độ grid gần tương ứng theo X/Y.
- Nếu tâm cột nằm đúng hệ grid thì trả ra `resolvedPoint`.
- Nếu không khớp grid thì bỏ qua hoặc báo trong result.

Giải thích khi bị hỏi:

> Yêu cầu của đồ án là cấu kiện phải phát triển dựa trên lưới trục. Vì vậy khi tạo cột, code không đặt tùy tiện theo CAD raw, mà quy chiếu tâm cột về hệ lưới trục đã đọc/tạo.

### 2.10. Phần xử lý thêm ngoài lưu đồ của cột

Code có thêm:

- Lọc trùng cột.
- Xóa/cập nhật cột đã sinh trước đó nếu chạy lại.
- Gắn marker vào comment để nhận biết cột do add-in tạo.
- Kiểm tra family mặc định có tồn tại không.

Giải thích:

> Các bước này không làm thay đổi thuật toán đọc cột. Chúng giúp chương trình chạy lại được nhiều lần và tránh tạo trùng cấu kiện.

---

## 3. Dầm

Dầm có 2 giai đoạn:

```text
Đọc CAD -> BeamModel
BeamModel -> Revit FamilyInstance
```

File đọc dầm:

```text
Services/Parsing/BeamReaderService.cs
```

File tạo dầm:

```text
Services/Creation/BeamCreationService.cs
```

Model dữ liệu:

```text
Models/Elements/BeamModel.cs
```

### 3.1. Khối "Nhập thông tin vào form"

Thông tin dầm lấy từ form:

```csharp
BeamWidth
BeamHeight
BeamZOffset
SelectedBeamLevelName
CreateBeam
```

Trong `ParseGeometryByLayer()`:

```csharp
var beams = new BeamReaderService().ReadBeams(
    geometryByLayer,
    beamLayerNames,
    gridLayerNames,
    ParsePositiveDouble(BeamWidth, 300.0),
    ParsePositiveDouble(BeamHeight, 700.0));
```

Ý nghĩa:

- `BeamWidth`: b dầm do người dùng nhập.
- `BeamHeight`: h dầm do người dùng nhập.
- `beamLayerNames`: layer dầm.
- `gridLayerNames`: layer lưới trục, dùng để tìm line trục nằm giữa hai biên dầm.

### 3.2. Khối "Kiểm tra đủ thông tin đầu vào"

Trong `ConvertTo3D()`:

```csharp
if (CreateBeam && _parsedBeams.Count == 0)
{
    SetStatus("Đã tích vẽ dầm nhưng chưa đọc được dữ liệu dầm từ CAD.", StatusType.Error);
    return;
}
```

Ý nghĩa:

- Nếu tích vẽ dầm nhưng chưa đọc được dầm thì dừng.

### 3.3. Khối "Lấy line trục dầm từ CAD"

Trong `BeamReaderService.cs`:

```csharp
var beamSegments = new List<BeamSegment>();
var axisSegments = new List<BeamSegment>();
```

Ý nghĩa:

- `beamSegments`: các đoạn line biên dầm.
- `axisSegments`: các đoạn line trục, lấy từ layer lưới trục.

Khi duyệt geometry:

```csharp
if (layerSet.Contains(layerName))
    beamSegments.AddRange(ReadBeamSegments(geometryObject, layerName));

if (axisLayerSet.Contains(layerName))
    axisSegments.AddRange(ReadBeamSegments(geometryObject, layerName));
```

Ý nghĩa:

- Nếu geometry nằm trên layer dầm thì đưa vào danh sách biên dầm.
- Nếu geometry nằm trên layer trục thì đưa vào danh sách trục.

### 3.4. Code lấy 2 điểm đầu/cuối của dầm như thế nào?

Trong `ReadBeamSegments()`:

```csharp
if (geometryObject is Line line)
{
    var segment = CreateSegment(line.GetEndPoint(0), line.GetEndPoint(1), layerName);
    if (segment != null) yield return segment;
    yield break;
}
```

Ý nghĩa:

- Nếu là line thì lấy điểm đầu/cuối bằng `GetEndPoint(0)` và `GetEndPoint(1)`.

Nếu là polyline:

```csharp
var points = polyLine.GetCoordinates();
for (int i = 0; i < points.Count - 1; i++)
{
    var segment = CreateSegment(points[i], points[i + 1], layerName);
    if (segment != null) yield return segment;
}
```

Ý nghĩa:

- Mỗi cặp điểm liên tiếp trong polyline là một segment.

### 3.5. Khối "Tìm line gần nhất có BxH nằm middle đường line"

Code hiện tại đọc dầm theo logic:

```text
Tìm 2 line biên dầm song song
-> Khoảng cách giữa 2 line bằng B dầm đã nhập
-> Tạo centerline ở giữa
-> Kiểm tra có line trục nằm giữa centerline không
-> Nếu có thì chấp nhận là dầm
```

Trong `DetectBeamsFromBoundariesAndAxes()`:

```csharp
if (!AreParallel(first.AngleDegrees, second.AngleDegrees))
    continue;
```

Ý nghĩa:

- Hai biên dầm phải song song.

Kiểm tra khoảng cách:

```csharp
if (!TryGetParallelDistance(first, second, out var distance) ||
    Math.Abs(distance - beamWidth) > BeamWidthToleranceMm)
    continue;
```

Ý nghĩa:

- Khoảng cách hai biên phải gần bằng `beamWidth`.
- Sai số cho phép là `BeamWidthToleranceMm`.

Tạo tim dầm:

```csharp
var centerLine = CreateCenterLine(first, second);
```

Tìm trục nằm giữa:

```csharp
var axis = FindBestMiddleAxis(centerLine, first, second, axisSegments, beamWidth);
if (axis == null)
    continue;
```

Ý nghĩa:

- Nếu không có line trục nằm giữa hai biên dầm thì bỏ qua.
- Dầm chỉ được nhận khi có trục làm cơ sở.

### 3.6. Khối "Duplicate dầm theo text BxH"

Lưu đồ ghi duplicate theo text BxH. Code hiện tại **không lấy BxH từ text CAD làm nguồn chính**.

Code hiện tại lấy B/H từ form:

```csharp
ParsePositiveDouble(BeamWidth, 300.0)
ParsePositiveDouble(BeamHeight, 700.0)
```

Sau đó tạo type dầm theo B/H:

```csharp
var symbol = GetOrCreateBeamType(baseSymbol, beamModel);
```

Ghi chú cần nói rõ khi bảo vệ:

> Phần này chưa tuân thủ 100% lưu đồ gốc nếu lưu đồ bắt buộc lấy text note BxH trong CAD. Code hiện tại dùng B/H nhập tay trên form để ổn định hơn, vì text CAD khi import qua Revit API có thể không đọc được đều giữa các bản vẽ.

### 3.7. Tạo dầm trong Revit

Trong `BeamCreationService.cs`:

```csharp
var line = Line.CreateBound(start, end);
var instance = _doc.Create.NewFamilyInstance(line, symbol, level, StructuralType.Beam);
```

Ý nghĩa:

- `start`, `end`: điểm đầu/cuối tim dầm.
- `line`: đường đặt dầm trong Revit.
- `symbol`: type dầm đúng kích thước.
- `level`: level dầm.

Set offset:

```csharp
SetBeamOffsets(instance, level, zOffset);
```

Trong đó:

- Reference Level = level người dùng chọn.
- Z Offset = offset dầm/sàn nhập trên form.
- Start Level Offset = 0.
- End Level Offset = 0.

---

## 4. Sàn

Sàn có 2 giai đoạn:

```text
Đọc CAD -> SlabModel
SlabModel -> Revit Floor
```

File đọc sàn:

```text
Services/Parsing/SlabReaderService.cs
```

File tạo sàn:

```text
Services/Creation/SlabCreationService.cs
```

Model dữ liệu:

```text
Models/Elements/SlabModel.cs
```

### 4.1. Khối "Nhập thông tin vào form"

Thông tin sàn lấy từ form:

```csharp
SlabThickness
BeamZOffset
SelectedBeamLevelName
CreateSlab
```

Trong `ParseGeometryByLayer()`:

```csharp
var slabs = new SlabReaderService().ReadSlabs(
    geometryByLayer,
    slabLayerNames,
    ParsePositiveDouble(SlabThickness, 130.0));
```

Ý nghĩa:

- `SlabThickness`: độ dày sàn.
- `slabLayerNames`: layer được gán là sàn.

### 4.2. Khối "Kiểm tra đủ thông tin đầu vào"

Trong `ConvertTo3D()`:

```csharp
if (CreateSlab && _parsedSlabs.Count == 0)
{
    SetStatus("Đã tích vẽ sàn nhưng chưa đọc được dữ liệu sàn từ CAD.", StatusType.Error);
    return;
}
```

Ý nghĩa:

- Nếu tích vẽ sàn nhưng chưa có dữ liệu sàn thì báo lỗi và dừng.

### 4.3. Khối "Tìm đường line, polyline khép kín theo layer"

Trong `SlabReaderService.cs`:

```csharp
public List<SlabModel> ReadSlabs(
    Dictionary<string, List<GeometryObject>> geometryByLayer,
    IEnumerable<string> slabLayerNames,
    double thicknessMm)
```

Code kiểm tra layer:

```csharp
var layerSet = new HashSet<string>(slabLayerNames, StringComparer.OrdinalIgnoreCase);
if (layerSet.Count == 0) return new List<SlabModel>();
```

Khi duyệt:

```csharp
if (!layerSet.Contains(layerName)) continue;
```

Ý nghĩa:

- Chỉ xử lý geometry thuộc layer sàn.

### 4.4. Code lấy polyline sàn như thế nào?

Trong `ReadClosedPolylineLoops()`:

```csharp
if (geometryObject is not PolyLine polyLine)
    yield break;
```

Ý nghĩa:

- Nhánh này chỉ xử lý polyline.
- Nếu không phải polyline thì không đọc theo cách polyline khép kín.

Lấy điểm:

```csharp
var points = NormalizeLoop(polyLine.GetCoordinates().Select(ToPoint2D).ToList());
```

Ý nghĩa:

- `GetCoordinates()`: lấy các điểm của polyline CAD.
- `ToPoint2D`: đổi sang tọa độ 2D đơn vị mm.
- `NormalizeLoop`: loại điểm trùng liên tiếp.

Kiểm tra khép kín:

```csharp
if (!IsClosed(points) || points.Count < 4)
    yield break;
```

Ý nghĩa:

- Nếu loop không khép kín thì không thể tạo sàn.
- Nếu quá ít điểm thì không đủ tạo vùng sàn.

Tính diện tích:

```csharp
var area = Math.Abs(GetSignedArea(points));
if (area < MinLoopAreaMm2)
    yield break;
```

Ý nghĩa:

- Loop quá nhỏ thì bỏ qua, tránh nhận nhầm ký hiệu.

### 4.5. Trường hợp CAD vẽ sàn bằng line rời

Trong `ReadSegments()`:

```csharp
if (geometryObject is Line line)
{
    var segment = CreateSegment(line.GetEndPoint(0), line.GetEndPoint(1), layerName);
    if (segment != null) yield return segment;
    yield break;
}
```

Ý nghĩa:

- Nếu CAD là line rời thì lấy điểm đầu/cuối để tạo `SlabSegment`.

Sau đó ghép các segment thành loop:

```csharp
allLoops.AddRange(BuildLoopsFromSegments(segments, layerName));
```

Trong `BuildLoopsFromSegments()`:

- Bắt đầu từ một segment.
- Tìm segment tiếp theo có điểm đầu/cuối trùng với điểm hiện tại.
- Ghép đến khi quay lại điểm đầu.
- Nếu không khép kín thì bỏ qua.

### 4.6. Khối "Phân loại loop ngoài và loop lỗ"

Trong `BuildSlabsFromLoops()`:

```csharp
var orderedLoops = loops
    .OrderByDescending(l => l.Area)
    .ToList();
```

Ý nghĩa:

- Loop lớn hơn thường là biên ngoài.
- Loop nhỏ nằm bên trong loop lớn là lỗ mở.

Kiểm tra loop lỗ:

```csharp
.Where(candidate => candidate.Area < outer.Area)
.Where(candidate => IsPointInsidePolygon(candidate.CenterPoint, outer.Points))
```

Ý nghĩa:

- Loop lỗ phải nhỏ hơn loop ngoài.
- Tâm của loop lỗ phải nằm bên trong loop ngoài.

### 4.7. Khối "Duplicate sàn theo chiều dày đã nhập"

Trong `SlabCreationService.cs`:

```csharp
var floorType = GetOrCreateFloorType(baseType, slabModel.Thickness);
```

Ý nghĩa:

- Tìm type sàn đúng chiều dày.
- Nếu chưa có thì duplicate từ floor type mặc định.
- Sau đó set thickness.

### 4.8. Khối "Tạo các lỗ mở"

Trong `SlabCreationService.cs`, code tạo profile:

```csharp
var profile = BuildProfile(slabModel, placement, level.Elevation);
```

Sau đó tạo floor:

```csharp
var floor = Floor.Create(_doc, profile, floorType.Id, level.Id);
```

Ghi chú:

> Lưu đồ ghi "tạo các lỗ mở" thành một khối riêng. Code không gọi hàm `CreateOpening` riêng, vì Revit API cho phép tạo sàn có lỗ bằng cách truyền nhiều `CurveLoop` vào `Floor.Create(...)`. Loop đầu là biên ngoài, các loop sau là lỗ.

---

## 5. Tạo Level và phát triển cấu kiện theo tầng

File chính:

```text
Services/Creation/LevelCreationService.cs
ViewModels/MainViewModel.cs
```

### 5.1. Khối "Nhập số tầng và chiều cao tầng"

Trong `MainViewModel.cs`:

```csharp
var numberOfFloors = ParsePositiveInt(NumberOfFloors, 1);
var firstFloorHeight = ParsePositiveDouble(FloorHeight, 3000.0);
var typicalFloorHeight = ParsePositiveDouble(TypicalHeight, firstFloorHeight);
```

Ý nghĩa:

- `numberOfFloors`: số tầng.
- `firstFloorHeight`: chiều cao tầng 1.
- `typicalFloorHeight`: chiều cao tầng điển hình.

### 5.2. Khối "Tạo hoặc cập nhật Level"

Code gọi:

```csharp
var levelResult = levelService.CreateOrUpdateLevels(
    numberOfFloors,
    firstFloorHeight,
    typicalFloorHeight);
```

Trong `LevelCreationService.cs`, hàm chính:

```csharp
public LevelCreationResult CreateOrUpdateLevels(
    int numberOfFloors,
    double firstFloorHeightMm,
    double typicalFloorHeightMm)
```

Ý nghĩa:

- Kiểm tra số tầng và chiều cao có hợp lệ không.
- Tìm hoặc tạo `Level 1`.
- Tạo các level tiếp theo.
- Level cuối cùng đặt tên là `Level mái`.

### 5.3. Logic tìm Level 1

Trong `GetOrCreateBaseLevel()`:

```csharp
var existing = GetLevels()
    .FirstOrDefault(l => string.Equals(l.Name, FirstLevelName, StringComparison.OrdinalIgnoreCase));
```

Ý nghĩa:

- Tìm level có tên `Level 1`.
- Nếu có thì dùng luôn.

Nếu không có `Level 1`:

```csharp
existing = GetLevels()
    .OrderBy(l => Math.Abs(l.Elevation))
    .FirstOrDefault();
```

Ý nghĩa:

- Tìm level gần cao độ 0 nhất.
- Dùng nó làm level gốc.

Nếu không có level nào:

```csharp
var created = Level.Create(_doc, 0);
TrySetLevelName(created, FirstLevelName);
```

Ý nghĩa:

- Tạo mới `Level 1` tại cao độ 0.

### 5.4. Logic tạo các level trên

Trong vòng lặp:

```csharp
for (int index = 2; index <= numberOfFloors + 1; index++)
```

Ý nghĩa:

- Bắt đầu từ level 2.
- Chạy đến `numberOfFloors + 1` vì level cuối là level mái.

Tên level:

```csharp
var name = index == numberOfFloors + 1
    ? RoofLevelName
    : $"Level {index}";
```

Ý nghĩa:

- Nếu là level cuối thì tên là `Level mái`.
- Còn lại là `Level 2`, `Level 3`, ...

Cao độ:

```csharp
var elevationMm = firstFloorHeightMm;
if (index > 2)
    elevationMm += (index - 2) * typicalFloorHeightMm;
```

Ý nghĩa:

- Level 2 cao bằng chiều cao tầng 1.
- Từ Level 3 trở đi cộng thêm chiều cao tầng điển hình.

### 5.5. Logic nếu level đã tồn tại

Trong `GetOrCreateLevel()`:

```csharp
var existingByName = GetLevels()
    .FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));
```

Nếu có level đúng tên:

```csharp
if (Math.Abs(existingByName.Elevation - targetElevation) > MmToFeet(ElevationToleranceMm))
{
    existingByName.Elevation = targetElevation;
    result.Updated++;
}
```

Ý nghĩa:

- Nếu level đúng tên nhưng sai cao độ thì cập nhật cao độ.

Nếu không có level đúng tên, code tìm level đúng cao độ:

```csharp
var existingByElevation = GetLevels()
    .FirstOrDefault(l => Math.Abs(l.Elevation - targetElevation) <= MmToFeet(ElevationToleranceMm));
```

Nếu có:

```csharp
TrySetLevelName(existingByElevation, levelName);
result.Updated++;
```

Ý nghĩa:

- Nếu có level đúng cao độ nhưng sai tên thì đổi tên nếu có thể.

Nếu không có cả tên và cao độ:

```csharp
var created = Level.Create(_doc, targetElevation);
TrySetLevelName(created, levelName);
```

Ý nghĩa:

- Tạo level mới đúng cao độ mục tiêu.

### 5.6. Phát triển cấu kiện theo tầng

Trong `ConvertTo3D()`, sau khi có `levelResult.Levels`, code tạo cấu kiện theo các level:

- Lưới trục: tạo một lần.
- Cột: lặp theo từng tầng.
- Dầm: tạo từ level người dùng chọn trở lên.
- Sàn: tạo từ level người dùng chọn trở lên.

Ví dụ cột:

```csharp
for (int floorIndex = 0; floorIndex < levelResult.Levels.Count - 1; floorIndex++)
{
    var columnResult = columnService.CreateColumns(
        _parsedColumns,
        _parsedGrids,
        levelResult.Levels[floorIndex],
        levelResult.Levels[floorIndex + 1],
        baseOffset,
        topOffset);
}
```

Ý nghĩa:

- Mỗi tầng có một đoạn cột riêng.
- Cột tầng hiện tại bắt đầu từ level hiện tại và kết thúc ở level kế tiếp.

---

## 6. Tổng kết mức độ tuân thủ lưu đồ

### 6.1. Phần tuân thủ đúng lưu đồ

Các phần chính đang bám lưu đồ:

- Lấy dữ liệu từ CAD import và document Revit.
- Nhập thông tin từ form.
- Kiểm tra đầu vào trước khi xử lý.
- Đọc layer theo loại cấu kiện.
- Đọc line/polyline từ CAD.
- Tính hình học từ điểm CAD: chiều dài, tâm, phương, diện tích.
- Phân loại lưới trục X/Y.
- Đọc cột từ polyline chữ nhật.
- Đọc dầm từ hai biên song song và line trục ở giữa.
- Đọc sàn từ loop khép kín.
- Duplicate type theo kích thước hoặc chiều dày.
- Tạo cấu kiện Revit bằng Revit API.

### 6.2. Phần xử lý thêm ngoài lưu đồ

Code có thêm một số xử lý thực tế:

- Chống trùng grid/cột/dầm/sàn khi chạy lại.
- Ẩn CAD import sau khi tạo.
- Kéo dài level trong view để bao phủ grid.
- Gắn marker vào cấu kiện do add-in tạo.
- Ghép line rời thành loop sàn.
- Với dầm, B/H hiện lấy từ form thay vì bắt buộc lấy từ text CAD.

Giải thích khi bị hỏi:

> Phần chính tuân thủ lưu đồ. Tuy nhiên code có thêm một số xử lý thực tế như chống trùng, ghép line rời thành loop, ẩn CAD import và dùng B/H dầm từ form. Đây là các workaround để chương trình ổn định với CAD import trong Revit, vì dữ liệu CAD thực tế không phải lúc nào cũng sạch và text CAD không phải lúc nào cũng đọc ổn định bằng Revit API.

### 6.3. Câu trả lời ngắn gọn khi bị hỏi code đọc CAD bằng cách nào

Có thể trả lời:

> Code không dùng lệnh đo kích thước của AutoCAD. Code đọc geometry của file CAD import thông qua Revit API. Với `Line`, code lấy 2 điểm đầu/cuối bằng `GetEndPoint(0)` và `GetEndPoint(1)`. Với `PolyLine`, code lấy danh sách điểm bằng `GetCoordinates()`. Sau đó code tự tính chiều dài, phương, tâm, diện tích, B/H và kiểm tra điều kiện hình học để quyết định đối tượng đó có phải lưới trục, cột, dầm hoặc sàn hay không.

### 6.4. Câu trả lời ngắn gọn khi bị hỏi làm sao biết đã lấy đúng

Có thể trả lời:

> Code không chấp nhận mọi đường CAD. Mỗi cấu kiện đều có điều kiện kiểm tra: đúng layer, đúng loại hình học, đủ kích thước tối thiểu, đúng phương, đúng số điểm, đúng khoảng cách, có loop kín, có grid làm cơ sở. Nếu không đạt điều kiện thì bị bỏ qua hoặc báo lỗi. Vì vậy dữ liệu được lọc theo đúng tiêu chí của lưu đồ trước khi tạo Revit.

