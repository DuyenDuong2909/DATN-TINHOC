param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$content = @'
# Äá»ŒC HIá»‚U CODE - Báº¢N Dá»„ HIá»‚U

Má»¥c tiĂªu cá»§a tĂ i liá»‡u nĂ y lĂ  giáº£i thĂ­ch **ngáº¯n gá»n, dá»… hiá»ƒu, Ä‘Ăºng luá»“ng** cĂ¡c pháº§n code Ä‘ang dĂ¹ng trong project. TĂ i liá»‡u tĂ¡ch riĂªng tá»«ng chá»©c nÄƒng: **Ä‘á»c lÆ°á»›i trá»¥c**, **váº½ lÆ°á»›i trá»¥c**, **Ä‘á»c cá»™t**, **váº½ cá»™t**.

# 1. Äá»ŒC LÆ¯á»I TRá»¤C Tá»ª CAD

## Má»¥c Ä‘Ă­ch

Chá»©c nÄƒng nĂ y Ä‘á»c cĂ¡c Ä‘Æ°á»ng lÆ°á»›i trá»¥c trong file CAD Ä‘Ă£ import vĂ o Revit. Code khĂ´ng Ä‘á»c text tĂªn trá»¥c trong CAD, mĂ  Ä‘á»c **hĂ¬nh há»c Ä‘Æ°á»ng tháº³ng** gá»“m `Line` vĂ  `PolyLine`.

## Vá»‹ trĂ­ trong project

- **File chĂ­nh:** `Services/Parsing/RevitDwgReaderService.cs`
- **Model lÆ°u káº¿t quáº£:** `Models/Elements/GridModel.cs`

## HĂ m chĂ­nh

```csharp
public List<GridModel> ReadGridLines(
    Element dwgInstance,
    IEnumerable<string> gridLayerNames,
    GridNamingOptions namingOptions)
```

HĂ m nĂ y nháº­n CAD Ä‘Ă£ import, danh sĂ¡ch layer Ä‘Æ°á»£c gĂ¡n lĂ  **LÆ°á»›i trá»¥c**, vĂ  quy táº¯c Ä‘áº·t tĂªn X/Y. Káº¿t quáº£ tráº£ vá» lĂ  danh sĂ¡ch `GridModel`.

## Lá»‡nh Revit API quan trá»ng

```csharp
var geomElem = dwgInstance.get_Geometry(options);
```

Lá»‡nh nĂ y láº¥y toĂ n bá»™ hĂ¬nh há»c CAD mĂ  Revit Ä‘á»c Ä‘Æ°á»£c tá»« file import, vĂ­ dá»¥: `Line`, `PolyLine`, `Curve`, `GeometryInstance`.

## Code Ä‘á»c lÆ°á»›i báº±ng gĂ¬?

- **Äá»c báº±ng 2 Ä‘iá»ƒm Ä‘áº§u/cuá»‘i cá»§a Ä‘Æ°á»ng CAD.**
- KhĂ´ng Ä‘á»c dimension trong CAD.
- KhĂ´ng Ä‘á»c text tĂªn trá»¥c trong CAD.
- TĂªn trá»¥c A/B/C hoáº·c 1/2/3 Ä‘Æ°á»£c táº¡o tá»« thĂ´ng tin ngÆ°á»i dĂ¹ng nháº­p trĂªn giao diá»‡n.

Vá»›i `Line`, code láº¥y 2 Ä‘áº§u mĂºt:

```csharp
line.GetEndPoint(0)
line.GetEndPoint(1)
```

Vá»›i `PolyLine`, code láº¥y danh sĂ¡ch Ä‘iá»ƒm:

```csharp
var points = polyLine.GetCoordinates();
```

## CĂ¡ch xĂ¡c Ä‘á»‹nh Ä‘Æ°á»ng ngang/dá»c

```csharp
TryGetAxisDirection(XYZ start, XYZ end, out bool isVertical)
```

Code tĂ­nh gĂ³c cá»§a Ä‘Æ°á»ng tá»« 2 Ä‘iá»ƒm Ä‘áº§u/cuá»‘i:

- GĂ³c gáº§n **0Â° hoáº·c 180Â°** -> Ä‘Æ°á»ng ngang.
- GĂ³c gáº§n **90Â°** -> Ä‘Æ°á»ng dá»c.
- Sai sá»‘ hiá»‡n táº¡i: **10Â°**.

## CĂ¡ch xĂ¡c Ä‘á»‹nh layer lÆ°á»›i trá»¥c

Code láº¥y tĂªn layer thĂ´ng qua `GraphicsStyleId`, sau Ä‘Ă³ chá»‰ Ä‘á»c nhá»¯ng layer Ä‘ang Ä‘Æ°á»£c gĂ¡n lĂ  **LÆ°á»›i trá»¥c**.

```csharp
if (!layerSet.Contains(layerName)) continue;
```

**Ă chĂ­nh:** Code nháº­n biáº¿t lÆ°á»›i trá»¥c báº±ng **layer CAD**, khĂ´ng pháº£i báº±ng text hay kĂ½ hiá»‡u bubble.

# 2. Váº¼ LÆ¯á»I TRá»¤C TRONG REVIT

## Má»¥c Ä‘Ă­ch

Chá»©c nÄƒng nĂ y dĂ¹ng dá»¯ liá»‡u `GridModel` Ä‘Ă£ Ä‘á»c tá»« CAD Ä‘á»ƒ táº¡o **Revit Grid tháº­t** trong model.

## Vá»‹ trĂ­ trong project

- **File chĂ­nh:** `Services/Creation/GridCreationService.cs`
- **Model káº¿t quáº£:** `Models/Results/GridCreationResult.cs`

## HĂ m chĂ­nh

```csharp
public GridCreationResult CreateGrids(IReadOnlyCollection<GridModel> gridModels)
```

Äáº§u vĂ o lĂ  danh sĂ¡ch lÆ°á»›i trá»¥c Ä‘Ă£ Ä‘á»c tá»« CAD. Má»—i `GridModel` cĂ³ `StartPoint`, `EndPoint`, `Name`, `IsVertical`.

## Lá»‡nh Revit API Ä‘á»ƒ táº¡o Grid

```csharp
var line = Line.CreateBound(startXYZ, endXYZ);
var grid = Grid.Create(_doc, line);
```

- `Line.CreateBound(...)`: táº¡o Ä‘Æ°á»ng Revit tá»« 2 Ä‘iá»ƒm.
- `Grid.Create(...)`: táº¡o Revit Grid tá»« Ä‘Æ°á»ng Ä‘Ă³.

## CĂ¡ch Ä‘áº·t vá»‹ trĂ­ Grid

- Láº¥y `StartPoint` vĂ  `EndPoint` tá»« CAD.
- TĂ­nh tĂ¢m toĂ n bá»™ há»‡ lÆ°á»›i báº±ng `GridPlacement`.
- Dá»‹ch há»‡ tá»a Ä‘á»™ CAD vá» gáº§n gá»‘c Revit.
- Äá»•i Ä‘Æ¡n vá»‹ tá»« **mm** sang **feet** vĂ¬ Revit API dĂ¹ng feet ná»™i bá»™.

```csharp
UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters)
```

## CĂ¡ch trĂ¡nh táº¡o trĂ¹ng Grid

Code kiá»ƒm tra 2 loáº¡i trĂ¹ng:

- **TrĂ¹ng tĂªn:** bá» qua náº¿u Revit Ä‘Ă£ cĂ³ Grid cĂ¹ng tĂªn.
- **TrĂ¹ng tá»a Ä‘á»™:** bá» qua náº¿u Ä‘Ă£ cĂ³ Grid gáº§n cĂ¹ng vá»‹ trĂ­.

Sai sá»‘ kiá»ƒm tra trĂ¹ng hiá»‡n táº¡i lĂ  **50mm**.

## CĂ¡ch gáº¯n Grid vá»›i Level

```csharp
grid.SetVerticalExtents(verticalExtents.Bottom, verticalExtents.Top);
```

Lá»‡nh nĂ y kĂ©o Grid theo phÆ°Æ¡ng Ä‘á»©ng Ä‘á»ƒ Grid hiá»ƒn thá»‹ qua cĂ¡c Level.

```csharp
UpdateLevelExtentsInElevationViews(placement)
```

HĂ m nĂ y kĂ©o chiá»u dĂ i Level trong cĂ¡c máº·t Ä‘á»©ng Ä‘á»ƒ Level phá»§ Ä‘Æ°á»£c toĂ n bá»™ há»‡ lÆ°á»›i.

## CĂ¡ch áº©n CAD sau khi táº¡o

```csharp
view.HideElements(idsToHide)
```

Sau khi táº¡o xong Grid, code áº©n CAD import Ä‘á»ƒ trĂ¡nh báº£n CAD chá»“ng lĂªn Revit Grid.

# 3. Äá»ŒC Cá»˜T Tá»ª CAD

## Má»¥c Ä‘Ă­ch

Chá»©c nÄƒng nĂ y Ä‘á»c footprint cá»™t tá»« CAD, xĂ¡c Ä‘á»‹nh **tĂ¢m cá»™t** vĂ  kĂ­ch thÆ°á»›c **b x h** Ä‘á»ƒ chuáº©n bá»‹ váº½ cá»™t trong Revit.

## Vá»‹ trĂ­ trong project

- **File chĂ­nh:** `Services/Parsing/ColumnReaderService.cs`
- **Model lÆ°u káº¿t quáº£:** `Models/Elements/ColumnModel.cs`

## HĂ m chĂ­nh

```csharp
public List<ColumnModel> ReadColumns(
    Dictionary<string, List<GeometryObject>> geometryByLayer,
    IEnumerable<string> columnLayerNames)
```

HĂ m nĂ y chá»‰ Ä‘á»c cĂ¡c layer Ä‘Æ°á»£c gĂ¡n lĂ  **Cá»™t**.

## Dá»¯ liá»‡u cá»™t gá»“m gĂ¬?

```csharp
LayerName
CenterPoint
Width
Height
RotationDegrees
```

- **CenterPoint:** tĂ¢m cá»™t Ä‘á»c tá»« CAD, Ä‘Æ¡n vá»‹ mm.
- **Width:** kĂ­ch thÆ°á»›c theo phÆ°Æ¡ng X CAD.
- **Height:** kĂ­ch thÆ°á»›c theo phÆ°Æ¡ng Y CAD.
- **RotationDegrees:** gĂ³c xoay; vá»›i cá»™t song song lÆ°á»›i hiá»‡n lĂ  0.

## Luá»“ng Ä‘á»c cá»™t

1. Chá»‰ xá»­ lĂ½ Ä‘á»‘i tÆ°á»£ng `PolyLine`.
2. Láº¥y cĂ¡c Ä‘iá»ƒm cá»§a polyline.
3. Bá» Ä‘iá»ƒm trĂ¹ng náº¿u cĂ³.
4. Chá»‰ nháº­n polyline cĂ³ Ä‘Ăºng 4 Ä‘iá»ƒm.
5. Kiá»ƒm tra 4 cáº¡nh táº¡o thĂ nh hĂ¬nh chá»¯ nháº­t.
6. TĂ­nh tĂ¢m cá»™t.
7. TĂ­nh kĂ­ch thÆ°á»›c theo phÆ°Æ¡ng X/Y CAD.
8. Lá»c cá»™t trĂ¹ng.

Lá»‡nh kiá»ƒm tra loáº¡i hĂ¬nh há»c:

```csharp
if (geometryObject is not PolyLine polyLine) return null;
```

Lá»‡nh láº¥y Ä‘iá»ƒm polyline:

```csharp
polyLine.GetCoordinates()
```

## CĂ¡ch tĂ­nh tĂ¢m cá»™t

TĂ¢m cá»™t Ä‘Æ°á»£c tĂ­nh báº±ng trung bĂ¬nh tá»a Ä‘á»™ 4 Ä‘iá»ƒm:

```csharp
center = new Point2D(
    points.Average(p => p.X),
    points.Average(p => p.Y));
```

**Ă quan trá»ng:** Theo lÆ°u Ä‘á»“, **tĂ¢m cá»™t lĂ  dá»¯ liá»‡u chĂ­nh Ä‘á»ƒ xĂ¡c Ä‘á»‹nh vá»‹ trĂ­ cá»™t**.

## CĂ¡ch tĂ­nh b x h hiá»‡n táº¡i

TrÆ°á»›c Ä‘Ă¢y code tá»«ng láº¥y:

- `Width = cáº¡nh ngáº¯n`
- `Height = cáº¡nh dĂ i`

CĂ¡ch Ä‘Ă³ cĂ³ thá»ƒ lĂ m cá»™t náº±m ngang trong CAD nhÆ°ng sang Revit bá»‹ dá»±ng dá»c.

Hiá»‡n táº¡i Ä‘Ă£ sá»­a:

- **KĂ­ch thÆ°á»›c theo phÆ°Æ¡ng X CAD -> Width**
- **KĂ­ch thÆ°á»›c theo phÆ°Æ¡ng Y CAD -> Height**

HĂ m xá»­ lĂ½:

```csharp
TryGetAxisAlignedDimensions(
    edges,
    out width,
    out height,
    out rotationDegrees)
```

## CĂ¡ch lá»c cá»™t trĂ¹ng

```csharp
Deduplicate(columns)
```

Cá»™t bá»‹ xem lĂ  trĂ¹ng náº¿u tĂ¢m vĂ  kĂ­ch thÆ°á»›c gáº§n nhau trong sai sá»‘ **20mm**.

# 4. Váº¼ Cá»˜T TRONG REVIT

## Má»¥c Ä‘Ă­ch

Chá»©c nÄƒng nĂ y táº¡o **Structural Column** trong Revit tá»« dá»¯ liá»‡u cá»™t Ä‘Ă£ Ä‘á»c tá»« CAD. Cá»™t pháº£i Ä‘áº·t theo **lÆ°á»›i trá»¥c Ä‘Ă£ váº½ trÆ°á»›c Ä‘Ă³**.

## Vá»‹ trĂ­ trong project

- **File chĂ­nh:** `Services/Creation/ColumnCreationService.cs`
- **Model káº¿t quáº£:** `Models/Results/ColumnCreationResult.cs`
- **NÆ¡i gá»i service:** `ViewModels/MainViewModel.cs`

## HĂ m chĂ­nh

```csharp
public ColumnCreationResult CreateColumns(
    IReadOnlyCollection<ColumnModel> columnModels,
    IReadOnlyCollection<GridModel> gridModels,
    double fallbackStoryHeightMm,
    double baseOffsetMm,
    double topOffsetMm)
```

HĂ m nĂ y táº¡o cá»™t tá»« dá»¯ liá»‡u CAD vĂ  dá»¯ liá»‡u lÆ°á»›i trá»¥c.

## CĂ¡ch xĂ¡c Ä‘á»‹nh vá»‹ trĂ­ cá»™t

**Cá»™t khĂ´ng Ä‘Æ°á»£c Ä‘áº·t tá»± do. Cá»™t pháº£i phĂ¡t triá»ƒn tá»« lÆ°á»›i trá»¥c.**

Luá»“ng xá»­ lĂ½:

1. Láº¥y tĂ¢m cá»™t tá»« CAD: `ColumnModel.CenterPoint`.
2. TĂ¬m trá»¥c dá»c cĂ³ tá»a Ä‘á»™ X trĂ¹ng vá»›i tĂ¢m cá»™t.
3. TĂ¬m trá»¥c ngang cĂ³ tá»a Ä‘á»™ Y trĂ¹ng vá»›i tĂ¢m cá»™t.
4. Giao Ä‘iá»ƒm 2 trá»¥c lĂ  Ä‘iá»ƒm Ä‘áº·t cá»™t trong Revit.

HĂ m xá»­ lĂ½:

```csharp
placement.TryResolveColumnPoint(
    columnModel.CenterPoint,
    out var resolvedPoint)
```

Náº¿u khĂ´ng tĂ¬m Ä‘Æ°á»£c giao Ä‘iá»ƒm lÆ°á»›i, code **khĂ´ng váº½ cá»™t** vĂ  bĂ¡o lá»—i.

## Family cá»™t máº·c Ä‘á»‹nh

Family máº·c Ä‘á»‹nh:

```text
M_Concrete-Rectangular-Column
```

Khai bĂ¡o trong code:

```csharp
private const string DefaultColumnFamilyName =
    "M_Concrete-Rectangular-Column";
```

Náº¿u project chÆ°a load family nĂ y, code bĂ¡o lá»—i Ä‘á»ƒ ngÆ°á»i dĂ¹ng load/chá»n Ä‘Ăºng family. Code khĂ´ng tá»± láº¥y family khĂ¡c Ä‘á»ƒ trĂ¡nh sai hÆ°á»›ng local vĂ  sai parameter.

## CĂ¡ch táº¡o type theo b x h

Náº¿u type Ä‘Ăºng kĂ­ch thÆ°á»›c chÆ°a cĂ³, code duplicate type má»›i theo chuáº©n family.

VĂ­ dá»¥ tĂªn type:

```text
400 x 500mm
```

HĂ m Ä‘áº·t tĂªn type:

```csharp
private static string GetColumnTypeName(ColumnModel columnModel)
```

## CĂ¡ch set kĂ­ch thÆ°á»›c b x h

Code thá»­ set cĂ¡c parameter phá»• biáº¿n:

```csharp
TrySetParameter(
    symbol,
    new[] { "b", "B", "Width", "WIDTH" },
    widthMm)
```

```csharp
TrySetParameter(
    symbol,
    new[] { "h", "H", "Depth", "DEPTH", "Height", "HEIGHT" },
    heightMm)
```

Ă nghÄ©a:

- `Width` tá»« CAD set vĂ o `b` hoáº·c `Width`.
- `Height` tá»« CAD set vĂ o `h`, `Depth` hoáº·c `Height`.

## Base Level, Top Level vĂ  Offset

- **Base Level:** Æ°u tiĂªn `Level 1`.
- **Top Level:** láº¥y level káº¿ tiáº¿p phĂ­a trĂªn Base Level.
- **Base Offset:** ngÆ°á»i dĂ¹ng nháº­p trĂªn giao diá»‡n, Ä‘Æ¡n vá»‹ mm.
- **Top Offset:** ngÆ°á»i dĂ¹ng nháº­p trĂªn giao diá»‡n, Ä‘Æ¡n vá»‹ mm.

CĂ¡c parameter Revit API Ä‘ang dĂ¹ng:

```csharp
BuiltInParameter.FAMILY_BASE_LEVEL_PARAM
BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM
BuiltInParameter.FAMILY_TOP_LEVEL_PARAM
BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM
```

## CĂ¡ch xĂ³a/cáº­p nháº­t cá»™t cÅ©

Cá»™t do tool táº¡o Ä‘Æ°á»£c Ä‘Ă¡nh dáº¥u báº±ng comment:

```csharp
private const string GeneratedColumnMarker =
    "AutoCADToRevitApplication";
```

Láº§n cháº¡y sau, code xĂ³a cĂ¡c cá»™t Ä‘Ă£ Ä‘Ă¡nh dáº¥u Ä‘á»ƒ táº¡o láº¡i Ä‘Ăºng vá»‹ trĂ­ vĂ  kĂ­ch thÆ°á»›c má»›i.

# 5. LUá»’NG CHUYá»‚N Äá»”I Tá»ª GIAO DIá»†N

## Vá»‹ trĂ­ trong project

- **ViewModel:** `ViewModels/MainViewModel.cs`
- **Giao diá»‡n:** `Views/MainWindow.xaml`

## Khi báº¥m Chuyá»ƒn Ä‘á»•i sang 3D

HĂ m xá»­ lĂ½:

```csharp
private void ConvertTo3D()
```

Luá»“ng cháº¡y:

1. Kiá»ƒm tra Ä‘Ă£ Ä‘á»c CAD.
2. Kiá»ƒm tra Ä‘Ă£ cĂ³ dá»¯ liá»‡u lÆ°á»›i trá»¥c.
3. Táº¡o Grid trÆ°á»›c báº±ng `GridCreationService`.
4. Náº¿u báº­t táº¡o cá»™t, táº¡o cá»™t báº±ng `ColumnCreationService`.
5. Truyá»n `Base Offset` vĂ  `Top Offset` tá»« giao diá»‡n xuá»‘ng service.
6. áº¨n CAD import sau khi táº¡o xong.
7. Focus cĂ¡c pháº§n tá»­ vá»«a táº¡o Ä‘á»ƒ ngÆ°á»i dĂ¹ng tháº¥y káº¿t quáº£.

## CĂ¡c Ă´ nháº­p liĂªn quan Ä‘áº¿n cá»™t

- **Base Offset cot:** Ä‘á»™ lá»‡ch chĂ¢n cá»™t, Ä‘Æ¡n vá»‹ mm.
- **Top Offset cot:** Ä‘á»™ lá»‡ch Ä‘á»‰nh cá»™t, Ä‘Æ¡n vá»‹ mm.

# 6. TĂ“M Táº®T NGáº®N Gá»ŒN

- **LÆ°á»›i trá»¥c:** Ä‘á»c `Line/PolyLine` theo layer, láº¥y 2 Ä‘iá»ƒm Ä‘áº§u/cuá»‘i, táº¡o Revit Grid.
- **Cá»™t:** Ä‘á»c `PolyLine` hĂ¬nh chá»¯ nháº­t, láº¥y tĂ¢m vĂ  kĂ­ch thÆ°á»›c theo X/Y CAD.
- **Vá»‹ trĂ­ cá»™t:** pháº£i náº±m trĂªn giao Ä‘iá»ƒm lÆ°á»›i trá»¥c.
- **Family cá»™t:** dĂ¹ng `M_Concrete-Rectangular-Column`.
- **Level cá»™t:** xuáº¥t phĂ¡t tá»« `Level 1`, top lĂ  level phĂ­a trĂªn.
- **ÄÆ¡n vá»‹ nháº­p:** mm; trÆ°á»›c khi gá»i Revit API sáº½ Ä‘á»•i sang feet.
'@

function Escape-XmlText {
    param([string]$Text)
    return [System.Security.SecurityElement]::Escape($Text)
}

function RunXml {
    param(
        [string]$Text,
        [bool]$Bold = $false,
        [bool]$Code = $false
    )

    $escaped = Escape-XmlText $Text
    $props = ""
    if ($Bold -or $Code) {
        $items = ""
        if ($Bold) { $items += "<w:b/>" }
        if ($Code) {
            $items += "<w:rFonts w:ascii=`"Consolas`" w:hAnsi=`"Consolas`"/>"
            $items += "<w:color w:val=`"1F2937`"/>"
        }
        $props = "<w:rPr>$items</w:rPr>"
    }

    return "<w:r>$props<w:t xml:space=`"preserve`">$escaped</w:t></w:r>"
}

function ParagraphXml {
    param(
        [string]$RunXml,
        [string]$Style = "Normal",
        [bool]$Bullet = $false
    )

    $styleXml = ""
    $indentXml = ""
    if ($Style -ne "Normal") { $styleXml = "<w:pStyle w:val=`"$Style`"/>" }
    if ($Bullet) { $indentXml = "<w:ind w:left=`"720`" w:hanging=`"360`"/>" }
    $pPr = ""
    if ($styleXml -or $indentXml) { $pPr = "<w:pPr>$styleXml$indentXml</w:pPr>" }
    return "<w:p>$pPr$RunXml</w:p>"
}

function RunsFromInlineMarkdown {
    param(
        [string]$Text,
        [bool]$Code = $false
    )

    if ($Code) {
        return RunXml -Text $Text -Code $true
    }

    $runs = ""
    $parts = [regex]::Split($Text, '(\*\*[^*]+\*\*)')
    foreach ($part in $parts) {
        if ($part.Length -eq 0) { continue }
        if ($part.StartsWith("**") -and $part.EndsWith("**")) {
            $runs += RunXml -Text $part.Substring(2, $part.Length - 4) -Bold $true
        }
        else {
            $runs += RunXml -Text $part
        }
    }
    return $runs
}

$paragraphs = New-Object System.Collections.Generic.List[string]
$inCode = $false

foreach ($rawLine in ($content -split "`r?`n")) {
    $line = $rawLine.TrimEnd()

    if ($line.StartsWith('```')) {
        $inCode = -not $inCode
        continue
    }

    if ($line.Trim().Length -eq 0) {
        $run = RunXml -Text ''
        $xml = ParagraphXml -RunXml $run
        [void]$paragraphs.Add($xml)
        continue
    }

    if ($inCode) {
        $run = RunsFromInlineMarkdown -Text $line -Code $true
        $xml = ParagraphXml -Style 'CodeBlock' -RunXml $run
        [void]$paragraphs.Add($xml)
        continue
    }

    if ($line.StartsWith('# ')) {
        $headingText = $line.Substring(2)
        $run = RunXml -Text $headingText -Bold $true
        $xml = ParagraphXml -Style 'Heading1' -RunXml $run
        [void]$paragraphs.Add($xml)
        continue
    }

    if ($line.StartsWith('## ')) {
        $headingText = $line.Substring(3)
        $run = RunXml -Text $headingText -Bold $true
        $xml = ParagraphXml -Style 'Heading2' -RunXml $run
        [void]$paragraphs.Add($xml)
        continue
    }

    if ($line.StartsWith('- ')) {
        $bulletText = $line.Substring(2)
        $run = RunsFromInlineMarkdown -Text $bulletText
        $xml = ParagraphXml -Bullet $true -RunXml $run
        [void]$paragraphs.Add($xml)
        continue
    }

    if ($line -match '^\d+\. ') {
        $run = RunsFromInlineMarkdown -Text $line
        $xml = ParagraphXml -Bullet $true -RunXml $run
        [void]$paragraphs.Add($xml)
        continue
    }

    $run = RunsFromInlineMarkdown -Text $line
    $xml = ParagraphXml -RunXml $run
    [void]$paragraphs.Add($xml)
}

$documentXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    $($paragraphs -join "`n")
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="720" w:footer="720" w:gutter="0"/>
    </w:sectPr>
  </w:body>
</w:document>
"@

$stylesXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
    <w:name w:val="Normal"/>
    <w:pPr><w:spacing w:after="120" w:line="276" w:lineRule="auto"/></w:pPr>
    <w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="22"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading1">
    <w:name w:val="Heading 1"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:spacing w:before="360" w:after="180"/></w:pPr>
    <w:rPr><w:b/><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="32"/><w:color w:val="1F4E79"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading2">
    <w:name w:val="Heading 2"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:spacing w:before="240" w:after="120"/></w:pPr>
    <w:rPr><w:b/><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="26"/><w:color w:val="2F5597"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="CodeBlock">
    <w:name w:val="Code Block"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:spacing w:after="80"/><w:ind w:left="360"/></w:pPr>
    <w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="20"/><w:color w:val="1F2937"/></w:rPr>
  </w:style>
</w:styles>
"@

$contentTypesXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
</Types>
"@

$relsXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
"@

$documentRelsXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>
"@

if (Test-Path -LiteralPath $OutputPath) {
    Remove-Item -LiteralPath $OutputPath -Force
}

$outputDir = Split-Path -Parent $OutputPath
if (!(Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$zip = [System.IO.Compression.ZipFile]::Open($OutputPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $entries = @{
        "[Content_Types].xml" = $contentTypesXml
        "_rels/.rels" = $relsXml
        "word/document.xml" = $documentXml
        "word/styles.xml" = $stylesXml
        "word/_rels/document.xml.rels" = $documentRelsXml
    }

    foreach ($name in $entries.Keys) {
        $entry = $zip.CreateEntry($name)
        $writer = New-Object System.IO.StreamWriter($entry.Open(), [System.Text.UTF8Encoding]::new($false))
        $writer.Write($entries[$name])
        $writer.Dispose()
    }
}
finally {
    $zip.Dispose()
}

Write-Output $OutputPath
