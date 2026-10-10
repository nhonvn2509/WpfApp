# Kiến trúc phần mềm

## 1. Tổng quan

Ứng dụng WPF theo mô hình **MVVM** với kiến trúc nhiều tầng, dùng DI của
`Microsoft.Extensions.DependencyInjection`.

```
┌──────────────────────────────────────────────────────────────┐
│  Views (XAML)                                               │
│  MainWindow · bảng Chuỗi/Phiên · khung xem trước · panel    │
└───────────────────────────┬──────────────────────────────────┘
                            │ DataBinding + ICommand
┌───────────────────────────▼──────────────────────────────────┐
│  ViewModel (CommunityToolkit.Mvvm)                          │
│  MainWindowViewModel · PagedTableViewModel · Workflows      │
└───────────────────────────┬──────────────────────────────────┘
                            │ gọi dịch vụ (async)
┌───────────────────────────▼──────────────────────────────────┐
│  Services                                                    │
│  Services/Tables  — truy vấn CSDL theo bảng                  │
│  Services/Files   — xem trước tệp, xuất Word, báo cáo         │
│  Services/Data    — kết nối, đếm dòng, thống kê DMV          │
└───────────────────────────┬──────────────────────────────────┘
                            │ Microsoft.Data.SqlClient / System.IO
┌───────────────────────────▼──────────────────────────────────┐
│  SQL Server — main · msg · info · stor · app · admin · jobs  │
└──────────────────────────────────────────────────────────────┘
```

**Quy tắc phụ thuộc:** mũi tên chỉ đi một chiều xuống. View không gọi trực tiếp
SQL; Service không tham chiếu View; ViewModel chỉ biết interface của Service.

## 2. Khởi động ứng dụng

Trình tự trong `App.xaml.cs`:

1. `ApplyVietnameseLanguage()` — đặt culture `vi-VN` mặc định và nạp bản dịch tiếng
   Việt cho HandyControl (nhúng resource `HandyControlLang.resx` với LogicalName
   `HandyControl.Properties.Langs.Lang.resources`).
2. `StartAsync()`:
   - Dựng `ServiceCollection`, gọi `AddApplicationServices(new WpfDialogService())`.
   - `BuildServiceProvider(ValidateOnBuild: true, ValidateScopes: true)` — lỗi DI
     lộ ra lúc chạy chứ không phải lúc bấm nút.
   - Tạo scope, kiểm tra có chuỗi kết nối không.
   - `DataBootstrapper.InitializeAsync()` mở kết nối, đọc số tham số `admin.CONFIG`,
     cập nhật dòng trạng thái trên `LoadingWindow`.
   - Hiện `MainWindow` với `MainWindowViewModel`, đóng `LoadingWindow`.
3. Nếu kết nối hỏng, hiện hộp thoại hỏi có thử lại không.

> **Ghi chú triển khai:** `LoadingWindow` hiện *trước* khi chờ CSDL để người dùng không
> thấy màn hình trắng. `ShutdownMode` đổi từ `OnExplicitShutdown` sang
> `OnMainWindowClose` sau khi nạp xong.

## 3. Đăng ký Dependency Injection

`ApplicationServiceCollectionExtensions.AddApplicationServices`:

| Lifetime | Dịch vụ | Ghi chú |
| --- | --- | --- |
| Singleton | `IUserSettings` | Cấu hình người dùng ở `%APPDATA%\DeltaBat\settings.json` |
| Singleton | `JsonAppSettings` | Cấu hình kèm theo (`appsettings.json` + biến môi trường) |
| Singleton | `IAppSettings` → `UserFirstAppSettings` | Lớp trang trí: cấu hình người dùng đứng trên cấu hình kèm theo |
| Singleton | `ISqlConnectionFactory` | Tạo `SqlConnection`, kiểm tra cú pháp, mở thử |
| Singleton | `ITableRowCountProvider`, `IDatabaseInsightsService` | Đệm số đếm dòng, tránh hỏi CSDL liên tục |
| Singleton | `IFilePathResolver` | Ghép `SHORT_NAME` + `EXT` + thư mục gốc thành đường dẫn |
| Singleton | `IWaveformEnvelopeExtractor`, `IWaveformRenderer` | Vẽ sóng âm |
| Singleton | `IFileTypeDetector` | Bảng tra đuôi + đoán bằng chữ ký đầu tệp |
| Singleton | `DocumentTextExtractor` | Trích văn bản docx/xlsx/pptx/pdf/rtf/epub |
| Singleton | 7 × `IFilePreviewStrategy` | Chiến lược theo loại tệp, tra cứu theo `Kind` |
| Singleton | `IFilePreviewService` (`FilePreviewCoordinator`) | Điều phối các chiến lược |
| Singleton | `IFileDetail*`, `IWord*`, `IFileExportService` | Báo cáo chi tiết, xuất Word |
| Singleton | `IDialogService` | `WpfDialogService`, gắn cửa sổ chủ |
| Transient | `SettingsViewModel` | Mỗi lần mở cửa sổ Cài đặt một bản mới |
| Scoped | ViewModel: `ChainsTableViewModel`, `SessionsTableViewModel`, `ContactsTableViewModel`, `FileExtensionsViewModel`, `SessionFilesViewModel` | Mỗi lần mở cửa sổ một bộ |
| Scoped | `IChainExploitationCoordinator`, `MainWindowViewModel` | Điều phối khai thác |

> Vì các ViewModel dùng `SemaphoreSlim` và `CancellationTokenSource` nên chúng là
> **Scoped**, không phải Singleton — tránh trạng thái tải dở dang dính giữa các cửa sổ.

## 4. Lớp truy vấn CSDL

Các bảng Phiên, Chuỗi, Tệp, Liên hệ dùng chung một bộ khung:

| Thành phần | Vai trò |
| --- | --- |
| `XxxQuery` (record) | Mô tả điều kiện truy vấn: `Search`, `FromDate`, `ToDate`, `ChainIds`, `Extensions`, `Page` |
| `XxxLoadService` | Dựng câu SQL, gắn tham số, đọc `SqlDataReader` thành đối tượng Row |
| `SqlConditionBuilder` | Gom điều kiện `WHERE` và tạo tham số `@Name` |
| `SqlTableLoadService` (abstract) | `OpenAsync` — mở và đóng kết nối an toàn |
| `PagedResult<T>` | `{ Items, HasMoreRows, TotalCount }` |
| `TablePage` | `First/MaxPageSize`, tính `OFFSET`/`FETCH NEXT` |

### 4.1. Ghép câu SQL an toàn

Mọi giá trị người dùng nhập đều đi qua **tham số** (`@Search`, `@ChainId`...), không
ghép chuỗi. `SqlConditionBuilder` có các phương thức:

- `AppendLike(...)` — tìm `LIKE` trên nhiều cột, bỏ qua nếu chuỗi rỗng.
- `AddInList(...)` — sinh `cột IN (@P0, @P1, ...)`, có bản chuyên biệt cho `int` và `string`.
- `AddDateRange(...)` — `@ToDate` được cộng thêm một ngày thành `@ToDateExclusive`
  để `BETWEEN` bao trọn cả ngày cuối (tránh lỗi mất dữ liệu do độ tròn milliseconds).
- `WithRecompile(...)` — ép SQL Server biên dịch lại vì câu động thay đổi theo bộ lọc.

Câu đọc đều kèm `WITH (NOLOCK)` để không chặn tiến trình ghi của hệ thống thu thập.

### 4.2. Phân trang

`TablePage` tính `OFFSET = (Page - 1) × PageSize` và `FETCH NEXT PageSize ROWS ONLY`.
`MaxFilesPerSession = 1000` — khung bên phải chỉ hiện được vài chục dòng, nạp hết một
phiên rất lớn không giúp ích mà chỉ tốn bộ nhớ.

## 5. Luồng dữ liệu chính

### 5.1. Bảng có phân trang (`PagedTableViewModel`)

Tách thành nhiều tệp cùng một phần lớp:

| Tệp | Phụ trách |
| --- | --- |
| `PagedTableViewModel.cs` | Thuộc tính dùng chung, `Items`, bộ lọc |
| `.Loading.cs` | Nạp trang đầu, `CancellationTokenSource`, chống nạp chồng |
| `.Paging.cs` | Trang trước/sau/đầu/cuối, đổi kích thước trang |
| `.Selection.cs` | Tick dòng, chọn tất cả, `SelectedItems` |

Mỗi lần đổi bộ lọc hoặc trang đều hủy yêu cầu đang chạy trước bằng
`CancellationTokenSource`, tránh kết quả cũ ghi đè kết quả mới.

### 5.2. Khai thác chuỗi (`ChainExploitationCoordinator`)

Đây là nghiệp vụ trung tâm, nối bốn ViewModel:

```
Tick chuỗi → bấm "Khai thác"
        ↓
  hủy lần chạy trước (CancellationTokenSource)
        ↓
  SemaphoreSlim(1,1) — chỉ một lần khai thác chạy
        ↓
  ┌──────────────────────────────────────────────┐
  │ Có tick chuỗi?                                 │
  │  ├─ Có     → nạp phiên của riêng các chuỗi đó  │
  │  └─ Không  → chỉ tính thống kê đuôi toàn hệ    │
  └──────────────────────────────────────────────┘
        ↓
  SessionsTableViewModel nạp phiên
        ↓
  Sự kiện SessionsReady → SessionFilesViewModel nạp cây tệp
        ↓
  FileExtensionsViewModel tính danh mục đuôi
        ↓
  Chuỗi được đánh dấu "đã khai thác" (màu cam → nút "Khai thác" nổi lên)
```

Hủy giữa chừng an toàn: `finally` luôn giải phóng `SemaphoreSlim` nếu đã vào gate, và
chỉ cập nhật thông báo khi `CancellationTokenSource` hiện tại vẫn là của mình (tránh
hai lần khai thác chồng nhau ghi đè thông báo của nhau). Trước khi chạy, coordinator
kiểm tra `HasInvalidDateRange` và dừng lại kèm thông báo nếu ngày sai.

### 5.3. Xem trước tệp

Xem [FilePreview.md](FilePreview.md) — phần này mô tả chi tiết 7 panel theo định dạng
(ảnh, âm thanh, video, văn bản, bảng dữ liệu, gói nén, nhị phân) cùng panel dự phòng
cho định dạng chưa hỗ trợ.

## 6. View và tài nguyên

### 6.1. Bố cục `MainWindow`

Khung neo là `DockWorkspace`, dựng trên thư việc **AvalonDock** (`Dirkster.AvalonDock`, giấy phép MS-PL). Bốn vùng là bốn panel thật của thư viện nên kéo thả, neo lại, tách cửa sổ nổi và gom nhóm tab đều dùng được; vị trí khởi tạo giữ như cũ: trái (bộ lọc đuôi), giữa (tab Phiên / Chuỗi), phải (chi tiết phiên), dưới (khung xem trước), cộng thanh trạng thái.

Ba điều dễ sai khi đụng vào khung này:

1. **Khung giữa phải là `LayoutDocumentPane`, không phải `LayoutAnchorablePane`.** AvalonDock chỉ cho panel có chứa document co giãn (dấi `*`); nếu cả bốn đều là anchorable thì thư viện ép tất cả về `*` và các khung trái/phải/dưới sẽ chia đều màn hình.
2. **`DockWidth` / `DockHeight` khai trong XAML có thể không có tác dụng.** Thư viện dựng lưới ngay khi cây layout được gán, tức là trước khi trình đọc XAML gán xong các thuộc tính này. `DockWorkspace.ApplyInitialSizes()` gán lại sau khi nạp; giá trị trong XAML giữ lại làm mặc định khi lưu / khôi phục bố cục.
3. **Style thanh kéo phải trỏ `AvalonDock.Controls.LayoutGridResizerControl`** (kiểu con của `GridSplitter`). Trỏ nhầm `TargetType` làm ứng dụng văng ngay lúc dựng khung.

> Vì HandyControl vẽ nền sáng và animation chuyển tab gây vỡ màu trên nền tối, tab
> chính dùng `TabControl` thuần của WPF kèm style riêng `WorkspaceTabItem`.

### 6.2. Chuỗi tài nguyên XAML

`App.xaml` nối theo thứ tự — thứ tự này có ý nghĩa, style sau ghi đè style trước:

```
HandyControl SkinDark → Theme → brush gốc của Xceed
→ Icons.xaml → AppTheme.xaml → TableStyles.xaml → XceedDarkTheme.xaml
```

| Tệp | Nội dung |
| --- | --- |
| `Views/Resources/AppTheme.xaml` | Bảng màu nền tối (`RegionBrush`, `PrimaryBrush`, `WarningBrush`...) |
| `Views/Resources/TableStyles.xaml` | Style bảng, nút, thanh công cụ, pill trạng thái |
| `Views/Resources/Icons.xaml` | Converter `ResourceKeyToBrush` và mẫu ô bảng |
| `Views/Resources/XceedDarkTheme.xaml` | Ép nền tối cho bộ chọn ngày giờ của Xceed |
| `Views/Resources/DockTheme.xaml` | Theme neo: nạp theme gốc AvalonDock rồi ghi đè 6 brush của nó bằng bảng màu dự án (chỉ nạp trong `DockingManager`, không nạp ở `App.xaml` để không rò style thư viện ra toàn app) |

Mọi màu đều lấy qua `DynamicResource`, nên đổi bảng màu ở `AppTheme.xaml` là toàn bộ
ứng dụng đổi theo.

> **Cảnh báo khi thêm resource:** `HandyControlLang.resx` phải giữ LogicalName
> `HandyControl.Properties.Langs.Lang.resources` và tên tệp **không** có hậu tố culture
> (`.vi`). Nếu không, MSBuild tách nó thành satellite assembly `vi\WpfApp.resources.dll`
> và `ResourceManager` không tìm thấy → `MissingManifestResourceException` lúc khởi động.

### 6.3. Icon nhất quán

`IconCatalog` ánh xạ dữ liệu của DeltaBat sang icon Lucide:

- `IconCatalog.File(extension, typeName)` — theo đuôi trước, không có thì theo loại tệp.
- `IconCatalog.Status(statusName)` — `PARSED` → `CircleCheck`, `ERROR` → `TriangleAlert`...
- `IconCatalog.StatusBrushKey(statusName)` — trả *tên* brush, hiển thị qua
  `ResourceKeyToBrush` (không trả thẳng `Brush` để tránh rò bộ nhớ khi đổi theme).

## 7. Quyết định thiết kế đáng chú ý

### 7.1. Bộ lọc đuôi file đọc từ cấu hình, không đếm từ CSDL

Câu `SELECT EXT, COUNT_BIG(*) ... GROUP BY EXT` phải quét toàn bộ 23 triệu dòng
`msg.FILES`, đo được **70–86 giây**. Bản gốc DeltaBat không làm vậy mà đọc danh sách
đuôi từ cấu hình trong bộ nhớ. Ứng dụng giữ nguyên cách đó: `FileExtensionCatalog`
(Services/Tables) liệt kê tĩnh danh mục đuôi theo đúng `CoreConfig.DefaultExtentions()`.

> Số lượng tệp theo từng đuôi hiển thị là `0` vì lấy số đếm cũng đòi quét bảng. Thông
> báo trạng thái nói rõ điều này thay vì để người dùng tưởng là lỗi.

### 7.2. Không gắn `DAY_FGR` của phiên vào điều kiện tệp, nhưng **phải khoá vùng ngày**

Hai điều kiện này dễ nhầm là một, nhưng khác nhau:

| Điều kiện | Có dùng không | Vì sao |
| --- | --- | --- |
| `f.DAY_FGR = <DAY_FGR của phiên>` | **Không** | Dữ liệu thật không nhất quán: phiên `ID_S = 796924` có `DAY_FGR = 0` trong `msg.SESSIONS` nhưng tệp nằm ở `DAY_FGR = 1`. Gắn cứng sẽ âm thầm trả về 0 tệp. |
| `f.DAY_FGR IN (<các ngày đang xem>)` | **Có** | Khoá **tập vùng ngày** đang lọc. Đây là điều kiện giúp index dùng được. |

Vì sao điều kiện thứ hai quyết định tốc độ: index của `msg.FILES` đặt `DAY_FGR` ở cột
đầu (`IX_FS_ID_S (DAY_FGR, ID_S)`, `IX_FS_DT_START (DAY_FGR, ID_F, DT_START)`).
Câu chỉ lọc `ID_S` thiếu cột dẫn đầu nên SQL Server không seek được, rơi về quét cả
23 triệu dòng — đo được 45–130 giây. Thêm `DAY_FGR IN (...)` biến câu thành seek vào
từng vùng ngày.

Lớp `DayFgrScope` (trong `FilesSqlQueryBuilder.cs`) là nơi duy nhất sinh tập này, dùng
chung cho bảng Phiên, bảng Chuỗi và bảng Tệp. Tập luôn có mặt `0` vì dữ liệu không
phân vùng thì dồn vào ngày ảo — đúng như `msg.fnGetWhereDayFGR` của bản gốc.

**Chỉ khoá ngày được khi hai đầu rơi vào cùng một tháng.** `DAY_FGR` là *ngày trong
tháng* (`tinyint` 0–31), không phải ngày tuyệt đối. Khoảng 05/10–07/12 nếu sinh tập
"5..12" sẽ loại mất các ngày 13..31 của tháng 10 — người dùng tưởng không có dữ liệu.
`DayFgrScope.Build` trả mảng rỗng trong trường hợp đó, câu chỉ còn lọc bằng `DT_START`:
chậm hơn nhưng không sai. Đây là đánh đổi lấy **đúng** thay vì lấy **nhanh**.

Đã xác minh bằng kế hoạch thực thi thật: các câu nặng nhất dùng
`IX_FS_EXT`, `IX_FS_EXT_TYPE`, `IX_FS_DT_START`, `IX_SS_IDTRANSP`. Chạy lại bằng
`tools/SampleFiles.Cli` (xem [KiemThu.md](KiemThu.md)).

### 7.3. Dùng view có sẵn của bản gốc thay vì tự viết câu JOIN

Bảng Phiên dùng `msg.ViewSessionsNC` — view có sẵn của DeltaBat, đã `JOIN` sẵn
`info.TYPES_*` với `main.CHAINS`.

Cố ý **không** dùng `msg.ViewSessions`: bản đó gọi nhiều hàm vô hướng
(`fnGetContactValue`, `fnGetContactRemark`) cho **mỗi dòng**. Các cột IP/điện thoại
trong bảng Phiên được nạp bằng một truy vấn theo lô cho các phiên của trang hiện tại;
truy vấn này ghép theo cả `ID_S` và `DAY_FGR`, tránh đọc liên hệ lặp lại từng dòng
và tránh nhân đôi phiên hoặc làm sai phân trang.

### 7.4. `VAL_XML` nạp riêng, không lấy trong câu danh sách

Cột `VAL_XML` của phiên có thể dài vài megabyte. Trước đây câu danh sách chọn nó cho
**mọi dòng** chỉ để dựng cây tệp và khung chi tiết cho một phiên.

Nay câu danh sách **không chọn** cột đó; khi bấm một phiên mới gọi
`ISessionsLoadService.LoadSessionXmlAsync` lấy đúng một giá trị, có đệm 8 phiên gần
nhất trong `SessionsTableViewModel`. Cây tệp cũng chỉ dựng cho phiên đang chọn thay vì
cả bảng.

### 7.5. Phân trang bằng con trỏ thay vì `OFFSET`

`OFFSET` phải đọc rồi bỏ `(Page - 1) × PageSize` dòng, nên trang càng sâu càng chậm.
Bảng Phiên dùng **keyset pagination**: nhớ `(ID_S, DAY_FGR)` của dòng cuối trang trước
và thêm điều kiện `ID_S < @CursorId`. Vì `ORDER BY` trùng khớp clustered PK nên SQL
Server đi thẳng tới vị trí đó. Trang 1 vẫn dùng `OFFSET` (tức thì), các trang sau mới
dùng con trỏ. Bảng Tệp vốn đã làm theo cách này.

Con trỏ hỏng thì tự lùi về `OFFSET` — chậm hơn nhưng vẫn đúng.

**SQL Server không cho dùng `TOP` và `OFFSET` trong cùng một câu** (lỗi 10741: *"A TOP
can not be used in the same query or sub-query as a OFFSET"*). Vì vậy câu chọn đúng một
trong hai:

| Trang | Cách dựng câu |
| --- | --- |
| Trang 1 | `OFFSET 0 ROWS FETCH NEXT @Fetch ROWS ONLY` — không kèm `TOP` |
| Trang 2+ | `SELECT TOP (@Fetch)` + điều kiện con trỏ |

Trang 1 dùng `OFFSET` không tốn gì vì `OFFSET = 0` không phải đọc rồi bỏ dòng nào.

### 7.6. Quy ước lưu đuôi file được dò từ CSDL, không giả định cứng

Cột `msg.FILES.EXT` lưu **khác nhau giữa các bản DeltaBat**: bản tham chiếu lưu
`.wav`, còn bản dựng từ `SeedDeltaBat.sql` lưu `wav` (đã kiểm tra trực tiếp:
`SELECT '['+EXT+']' FROM msg.FILES` trả về `[pdf]`). Giả định sai thì điều kiện
`IN ('.pdf')` không khớp gì và bộ lọc luôn rỗng.

`DataBootstrapper` dò quy ước bằng một câu `TOP (1)` lúc khởi động và lưu vào
`FileExtensionCatalog.ExtStoredWithLeadingDot`; `NormalizeForSql` sinh điều kiện theo
đúng dạng đang lưu. Dòng trạng thái hiện dạng đang dùng để dễ kiểm tra.

### 7.7. Thống kê CSDL qua DMV thay vì `COUNT(*)`

`ITableRowCountProvider` và `DatabaseInsightsService` đọc `sys.dm_db_partition_stats`
và các DMV của SQL Server — gần như tức thì, không quét bảng. DMV có đệm ngắn nên thực
tế chỉ hỏi CSDL khoảng nửa phút một lần.

### 7.4. Vòng đời `CancellationTokenSource` trong ViewModel

Mỗi thao tác tải giữ một `CancellationTokenSource` riêng; yêu cầu mới hủy yêu cầu cũ
và `finally` chỉ dọn dẹp nếu nguồn đó vẫn là hiện tại. Nếu không có bước kiểm tra này,
lệnh hủy của lượt sau sẽ xóa nhầm nguồn của lượt đang chạy.

### 7.5. Chuẩn hoá đuôi file chỉ có một nơi định nghĩa

Từng có hai lớp trùng tên `FileExtensionCatalog` ở hai namespace, mỗi lớp một quy
tắc: một lớp thêm dấu chấm (`.wav`), lớp kia giữ đuôi trần (`wav`). Hệ quả là điều
kiện SQL sinh `IN ('.wav')` còn lọc tại chỗ so khớp `wav` với `.wav` — bộ lọc luôn ra
danh sách rỗng. Nay `Services.Tables.FileExtensionCatalog` **ủy quyền** về
`Services.Files.FileExtensionCatalog`, và có hai hàm rõ ràng:

- `Normalize(IEnumerable<string>)` → `["wav", "dta"]` (không dấu chấm) — dùng so khớp.
- `NormalizeWithDot(IEnumerable<string>)` → `[".wav", ".dta"]` — dùng cho cột `f.EXT`.

Lưu ý khi chuẩn hoá: `Path.GetExtension(".wav")` trả về **chuỗi rỗng** vì coi `.wav`
là tên tệp ẩn, nên phải tự cắt sau dấu chấm thay vì dùng hàm đó một mình.

## 8. Bảo mật

- Chuỗi kết nối nằm ngoài mã nguồn, trong `appsettings.json`; có thể ghi đè bằng biến
  môi trường `DELTABAT_CONNECTION_STRING` (nên dùng cho môi trường thật).
- Mọi giá trị người nhập đều là tham số SQL, không ghép chuỗi.
- Ứng dụng chỉ đọc dữ liệu; không có chức năng ghi vào CSDL.
- Mở tệp ngoài ứng dụng dùng `Process.Start` với `UseShellExecute = true` theo đường
  dẫn người dùng đã chọn từ CSDL.

## 9. Quy ước viết mã

Dự án **không dùng comment trong mã nguồn** — không XML documentation, không `//`,
không `<!-- -->` trong XAML. Mọi giải thích, lý do thiết kế và ghi chú vận hành nằm
trong thư mục `Docs/`.

| Cần ghi | Viết ở đâu |
| --- | --- |
| Lớp/tệp này làm gì | `Docs/MaNguon.md` |
| Vì sao chọn cách này, không chọn cách kia | `Docs/KienTruc.md` §7 |
| Quy tắc dữ liệu, tên cột, ràng buộc CSDL | `Docs/CoSoDuLieu.md` |
| Giao diện một màn hình | `Docs/FilePreview.md` hoặc `Docs/CaiDat.md` |
| Lệnh chạy, cấu hình, cách xử lý sự cố | `Docs/VanHanh.md` |
| Quy ước tên, kiểu dữ liệu | `Docs/MaNguon.md` |

Hệ quả cần nhớ:

- **Tên phải nói thay cho comment.** `Reset()` rõ hơn là `DeleteUserSettingsIfNotLocked()`.
- Muốn giải thích *vì sao* thì thêm mục vào `Docs/`, không chú thích trong tệp.
## 10. Hệ thống giao diện (theme)

Ứng dụng có 5 theme, đổi được lúc chạy từ cửa sổ Cài đặt. Xem phía người dùng ở
[CaiDat.md §2.1](CaiDat.md).

### 10.1. Vì sao không sửa XAML trực tiếp

Trước khi có theme, màu nằm cứng trong `AppTheme.xaml` + `DockTheme.xaml` +
`XceedDarkTheme.xaml` và `App.xaml` nạp thẳng `SkinDark` của HandyControl. Đổi màu đồng
nghĩa sửa 4 tệp rồi build lại — không làm được lúc đang dùng.

Cách nay: mỗi theme là **một tệp `ResourceDictionary` đầy đủ** trong `Views/Themes/`,
đổi theme = thay thế dictionary, không cần build.

### 10.2. Thứ tự merge là then chốt

`App.xaml` nạp 7 dictionary theo thứ tự cố định. Hai chỗ phụ thuộc theme nằm ở vị trí
cố định đó và `AppThemeService.Swap` tìm chúng bằng cách quét `Source`:

```
[0] HandyControl Skin        ← thay theo theme
[1] HandyControl Theme
[2] Xceed brushes
[3] Icons.xaml
[4] Views/Themes/ThemeXXX    ← thay theo theme
[5] TableStyles.xaml
[6] CommonControls.xaml
```

Bảng màu phải nằm **sau** `HandyControl/Theme.xaml` vì HandyControl cũng định nghĩa các
khoá trùng tên như `PrimaryBrush`, `RegionBrush`. Nếu đặt trước thì theme của HandyControl
sẽ đè lên màu của ứng dụng và mọi tuỳ chỉnh trong tệp theme trở nên vô dụng.

Vì vậy `Swap` tìm **hai vị trí riêng biệt** theo marker, chứ không giả định chúng
liền kề như lần triển khai đầu tiên.

> Marker phải khớp **cả** dạng tương đối lẫn tuyệt đối của URI. `App.xaml` khai báo
> `Views/Themes/ThemeMidnight.xaml` nên `Source.OriginalString` giữ nguyên dạng đó;
> tìm bằng `WpfApp;component/...` sẽ không khớp và theme không bao giờ đổi.

### 10.3. Khung neo AvalonDock

AvalonDock không đọc `Application.Current.Resources` mà có `DockingManager.Theme` riêng.
`AppDockTheme` dựng `ResourceDictionary` gồm `generic.xaml` của AvalonDock rồi thêm bảng
màu của theme hiện tại **sau** (thứ tự merge trong dictionary con cũng vậy — cái sau thắng).
`DockWorkspace.RefreshTheme()` gán lại `Manager.Theme` mỗi lần đổi.

`DockWorkspace` giữ tham chiếu tới chính nó trong `ActiveWorkspace` lúc `Loaded`;
`ThemeProvider.Current` là biến tĩnh để `AppDockTheme` đọc được theme hiện tại mà không
tạo phụ thuộc vòng giữa service và lớp View.

### 10.4. Thanh tiêu đề cửa sổ

Thanh tiêu đề mặc định của Windows do DWM vẽ, đọc màu từ giao diện hệ điều hành chứ không
đọc `Application.Current.Resources` — nên nó không thể đổi theo theme ứng dụng, và trên
Windows 10 còn **không vẽ lại** khi đổi thuộc tính `DWMWA_USE_IMMERSIVE_DARK_MODE` nếu
cửa sổ đang maximize. Vì vậy ứng dụng không dùng thanh này.

Bốn cửa sổ có khung chuẩn (`MainWindow`, `SettingsWindow`, `FilePreviewWindow`,
`FileDetailWindow`) khai `WindowChrome` với `CaptionHeight="32"`, `GlassFrameThickness="0"`
và `UseAeroCaptionButtons="False"`: khung Windows bị thay bằng nội dung WPF nhưng vùng 32px
trên cùng vẫn là vùng caption, nên kéo cửa sổ, nhấn đúp để phóng to và snap Aero vẫn
hoạt động như bình thường. Ba nút do `Views/Controls/WindowCaptionBar.xaml` tự vẽ bằng
brush `DynamicResource` nên tự đổi màu khi `AppThemeService.Apply` thay dictionary.

`LoadingWindow` không khai `WindowChrome` vì vốn đã `WindowStyle="None"`, không có khung.

Khi maximize, WPF nới cửa sổ vượt ra ngoài vùng làm việc `ResizeBorderThickness` (8px mỗi
cạnh) và phần bị cắt sẽ nuốt mất nút. `WindowCaptionBar` bù lại bằng cách đặt
`Margin = 8` cho phần nội dung gốc của cửa sổ khi maximize, và gỡ khi về cửa sổ thường.

### 10.5. Thêm theme mới

1. Copy `Views/Themes/ThemeMidnight.xaml` thành tệp mới, đổi màu.
2. Thêm mục vào `AppThemeCatalog.Themes` (kèm `FileName`, `RegionColor`, `AccentColor`).
3. Nếu dùng skin HandyControl khác, chọn trong `SkinDark` / `SkinDefault` / `SkinViolet`
   — đây là ba skin có sẵn trong HandyControl 3.5.1.

Không cần sửa `App.xaml`, không cần sửa View nào — thanh tiêu đề tự đổi theo vì nó đọc
cùng bộ brush đó.
- `Docs/README.md` là mục lục — thêm tài liệu mới thì cập nhật cả bảng đó.

### 10.6. Ba tầng tài nguyên: metric → theme → style

Giao diện chia làm ba tầng, mỗi tầng một việc:

| Tầng | Tệp | Chứa | Đổi khi |
| --- | --- | --- | --- |
| **Metric** | `Views/Resources/Metrics.xaml` | Khoảng cách, bo góc, cỡ chữ, chiều cao control, cỡ icon — `sys:Double` | Tinh chỉnh mật độ giao diện |
| **Theme** | `Views/Themes/Theme*.xaml` | Toàn bộ màu, mỗi theme một tệp | Đổi theme |
| **Style** | `Views/Resources/TableStyles.xaml` | Style dùng chung: bảng, nút, panel, chip, thanh cuộn | Tái dựng một loại control |

Quy tắc bắt buộc khi viết XAML mới:

1. **Không viết mã màu hex trong View.** Mọi màu lấy bằng `{DynamicResource TênBrush}`
   và phải có khoá đó trong cả 5 tệp theme. Màu nào chưa có khoá thì thêm vào theme
   trước, không tự chế `#1FFFFFFF` rồi hy vọng theme khác cũng hợp.
2. **Kích thước lấy từ `Metrics.xaml`.** Thay vì `Height="32"` dùng
   `Height="{StaticResource ControlHeight}"`.
3. **Style dùng chung đặt trong `TableStyles.xaml`**, đặt tên theo vai trò
   (`SurfaceCard`, `StatusBadge`, `CaptionButton`) chứ không theo vị trí
   (`SettingsFooterButton`) — vì cùng một vai trò có thể xuất hiện ở nhiều màn hình.

Bộ khoá màu chia theo **ngữ nghĩa** chứ không theo tên hiện tượng:

| Nhóm | Khoá tiêu biểu |
| --- | --- |
| Nền | `RegionBrush`, `DarkRegionBrush`, `SecondaryRegionBrush`, `TertiaryRegionBrush`, `ElevatedRegionBrush` |
| Viền | `BorderBrush`, `SubtleBorderBrush`, `StrongBorderBrush`, `GridLineBrush` |
| Chữ | `PrimaryTextBrush`, `ThirdlyTextBrush`, `DisabledTextBrush` |
| Nhấn | `PrimaryBrush`, `DarkPrimaryBrush`, `LightPrimaryBrush` |
| Trạng thái | `InfoBrush`, `SuccessBrush`, `WarningBrush`, `DangerBrush` + bản nền mờ `*SurfaceBrush` |
| Lớp phủ | `OverlayHoverBrush`, `OverlayPressedBrush`, `OverlaySelectedBrush` |
| Bảng | `ColumnHeaderBrush`, `RowHoverBrush`, `RowSelectedBrush`, `GridLineBrush` |
| Vùng chuyên dụng | `CodeBackgroundBrush`, `MediaBackgroundBrush`, `CheckerLightBrush`, `CheckerDarkBrush`, `WaveformBrush` |
| Khung neo | `DockPaneHeaderBrush`, `DockTabActiveBackgroundBrush`, `DockTabActiveForegroundBrush` |

Vì sao có cả nhóm *overlay* riêng: bản cũ dùng `#1FFFFFFF` cho nền nút. Màu đó
**vô hình trên theme sáng** (`Daylight`), nên giao diện sáng bị mất nền ở mọi nút
và thanh chọn. Nay theme sáng khai báo overlay bằng đen mờ (`#0F000000`) nên nhìn
đúng ở cả hai nhóm sáng/tối.

### 10.7. Ghi đè style của AvalonDock

`generic.xaml` của AvalonDock **không đọc bảng màu ứng dụng** cho phần tab: style
mặc định viết thẳng `<Setter Property="Background" Value="White" />` cho tab được
chọn. Chỉ thêm khoá màu vào theme không đủ — tab vẫn nền trắng chữ tối.

Vì vậy `Views/Themes/DockStyles.xaml` khai báo lại các style của thư viện, và
`AppDockTheme` merge nó **sau** bảng màu để thắng (cùng nguyên tắc thứ tự merge ở
§10.2):

| Style trong `DockStyles.xaml` | Ghi đè kiểu nào |
| --- | --- |
| `DocumentPaneTabItemStyle` | Tab tài liệu: nền theo token, gạch dưới màu nhấn khi chọn |
| `AnchorablePaneTabItemStyle` | Tab panel neo, cùng ngôn ngữ hình ảnh |
| `AnchorablePaneTitleStyle` | Tiêu đề panel: phẳng, nút ẩn/đóng vẽ bằng `Path` thay bitmap mặc định |
| `{x:Type LayoutGridResizerControl}` | Thanh chia ngăn: mảnh, bo tròn, đổi màu khi rê chuột và khi kéo |

Hai lưu ý khi sửa file này:

- Kiểu `ToolTip="{x:Static ...}"` của AvalonDock nằm ở namespace `AvalonDock.Properties`
  (`adProperties`), **không phải** `AvalonDock.Controls` — sai namespace sẽ báo
  *Cannot find the type*.
- Phải giữ nguyên `DocumentPaneTabPanel` / `AnchorablePaneTabPanel` trong template
  (kèm `IsItemsHost="True"`). Xoá chúng thì AvalonDock không còn chỗ đặt tab và danh
  sách tab biến mất hoàn toàn.

## 11. Quy ước comment trong mã nguồn

**Mã nguồn không chứa comment.** Không có `//`, `///` hay `<!-- -->` trong `.cs` và
`.xaml`. Mọi lý do, quyết định và cạm bẫy đều nằm trong tài liệu Markdown này.

Lý do: comment trong mã nhanh lỗi thời. Ai sửa code cũng ngại xoá comment của người khác
vì không dám chắc nó còn đúng, nên comment tích tụ thành những đoạn sai lệch. Đưa sang
Markdown thì có một chỗ duy nhất để sửa, có lịch sử (`git log`) để truy vết, và người đọc
tài liệu thấy cả bối cảnh thay vì một dòng trôi trong file.

Quy tắc khi gặp đoạn code khó hiểu:

1. Không thêm comment mới trong code.
2. Ghi lý do vào `Docs/KienTruc.md` (mục §7 *Quyết định thiết kế đáng chú ý* hoặc §11).
3. Tên biến, hàm và class phải tự nói lên điều gì chúng làm — đây mới là hình thức tài
   liệu được duy trì tốt nhất.

## 12. Những quyết định đã bỏ comment khỏi code

Danh sách dưới đây chính là các ghi chú từng nằm trong mã và giờ được ghi ở đây.

### 12.1. Cây tệp hiện mọi phiên, nạp tệp trễ

Cây tệp bên trái dựng **một gốc cho mọi phiên đang có trong bảng Phiên**, không chỉ phiên
đang bấm. Cây bám theo bảng nên tôn trọng bộ lọc ngày, tìm kiếm, trạng thái và chuỗi.

Không nạp tệp của mọi phiên cùng lúc vì `VAL_XML` có thể dài vài MB (§7.4). Cách làm:

- Tên phiên và số tệp lấy từ dòng bảng (`N_FILES` đã có sẵn trong câu truy vấn danh sách)
  nên hiện ngay, **không tốn truy vấn nào**.
- Danh sách tệp nạp khi người dùng mở rộng gốc đó (`SessionFilesViewModel.LoadTreeChildrenAsync`).
- Gốc đã nạp được tái dùng khi cây dựng lại (lọc, đổi trang) nên không mất dữ liệu đã xem.
- Phiên đang mở không hỏi CSDL lần hai — dùng lại `VAL_XML` đã có trong bộ nhớ.
- Khi lọc theo chuỗi (không có phiên cụ thể) cây dựng từ kết quả lọc thay vì từ bảng Phiên.

### 12.2. Chân cửa sổ kiểu VS Code

Chân cửa sổ gộp tổng số phiên/tệp, trạng thái và thanh tiến trình 2px. Thanh tiến trình
chạy **không xác định** cho tới khi biết tổng số dòng, vì lần tải đầu SQL Server thường đang
chạy `COUNT` ở nền (`TotalCount` chỉ chính xác khi `IsTotalCountKnown`). Bám theo cả lúc
đang tải bảng và lúc đang tải tệp của phiên.

### 12.3. Khung trái là hai tab, không phải hai khối thu gọn

Trước đây là hai khối xếp dọc kèm nút thu gọn; mỗi khối chỉ còn nửa chiều cao nên cây bị
bóp và phải cuộn liên tục. Nay là `TabControl` với hai tab *Cây tệp* / *Đuôi file*.
Tab đang chọn do `MainWindowViewModel.SelectedNavTabIndex` giữ nên có thể đổi bằng lệnh.

### 12.4. `EventSetter` không dùng được trong `ResourceDictionary`

Gắn sự kiện cho style trong `TableStyles.xaml` sẽ báo lỗi *MC6024* vì `ResourceDictionary`
không khai báo `x:Class`. Vì vậy style `SessionFileTreeItem` nằm ở tài nguyên, còn
`EventSetter` cho sự kiện `Expanded` khai ở `NavPane.xaml` với `BasedOn` trỏ về style gốc.

### 12.5. Trần an toàn khi nạp tệp

`SessionFilesViewModel.HardLoadLimit` = 200 000 tệp. `PageAccumulator` lặp trang cho tới
hết, dừng ở ngưỡng này và báo đã cắt, để một phiên vài trăm nghìn tệp không làm treo máy.

### 12.6. Không ghi tên package đã gỡ trong comment XAML

Trình biên dịch markup WPF quét cả phần comment để sinh lệnh `using`. Nếu comment nhắc
tên một package đã gỡ khỏi project, nó sinh ra `using ...;` không tồn tại và build hỏng.
Vì vậy nay comment XAML bị gỡ hoàn toàn — cũng là một lý do khác để giữ quy ước §11.
