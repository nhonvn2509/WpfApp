# Tham chiếu mã nguồn

Bản đồ từng lớp theo vai trò. 169 tệp C# trong `WpfApp/WpfApp/`.

## Quy ước đặt tên

| Quy ước | Ý nghĩa |
| --- | --- |
| `I` + Tên | Interface |
| `XxxService` / `XxxCoordinator` | Lớp dịch vụ (logic nghiệp vụ, async) |
| `SqlTableLoadService` + `Xxx` | Lớp truy vấn CSDL |
| `...ViewModel` | ViewModel MVVM |
| `...Row`, `...Result`, `...Query` | Kiểu dữ liệu thuần (record/class) |
| `IFilePreviewStrategy` | Chiến lược theo loại tệp |
| File `Foo` + `Foo.Parts.cs` | Tách lớp lớn thành nhiều tệp cùng `partial class` |

Ngôn ngữ: thông báo người dùng và tài liệu viết bằng **tiếng Việt**, có dấu.

**Mã nguồn không chứa comment** (không `///`, `//`, `<!-- -->`). Giải thích và ghi chú
nằm trong `Docs/` — xem [KienTruc.md §9](KienTruc.md).

## 1. Điểm khởi động

| Tệp | Vai trò |
| --- | --- |
| `App.xaml` | Nối chuỗi tài nguyên (xem [KienTruc §6.2](KienTruc.md)) |
| `App.xaml.cs` | `ApplyVietnameseLanguage`, `StartAsync`, xử lý lỗi kết nối |
| `ApplicationServiceCollectionExtensions.cs` | Đăng ký toàn bộ DI |
| `Services/JsonAppSettings.cs` | Đọc `appsettings.json` + biến môi trường |
| `Services/Settings/*` | Cấu hình người dùng + lớp trang trí ghi đè (xem [CaiDat.md](CaiDat.md)) |
| `Services/DataBootstrapper.cs` | Mở kết nối, đọc số tham số, cập nhật trạng thái |
| `Views/LoadingWindow.xaml(.cs)` | Cửa sổ chờ hiện lúc khởi động |
| `Views/MainWindow.xaml(.cs)` | Bố cục 4 vùng + thanh trạng thái |

> `IAppSettings` trong DI trỏ tới `UserFirstAppSettings`, không trỏ thẳng
> `JsonAppSettings`. Nơi đọc cấu hình vẫn dùng `IAppSettings` như cũ.

## 2. Lớp CSDL nền tảng

| Tệp | Vai trò |
| --- | --- |
| `Services/Data/ISqlConnectionFactory.cs` | Tạo `SqlConnection` |
| `Services/Data/SqlConnectionFactory.cs` | Cài đặt — đọc chuỗi kết nối từ `IAppSettings` |
| `Services/Data/SqlReaderExtensions.cs` | Đọc cột an toàn (`GetStringOrEmpty`, `GetInt32OrNull`...) tránh lỗi kiểu |
| `Services/Data/ITableRowCountProvider.cs` | Đếm dòng qua DMV |
| `Services/Data/TableRowCountProvider.cs` | Cài đặt, có đệm ngắn |
| `Services/Data/DatabaseInsightsService.cs` | Dung lượng bảng, câu lệnh chậm, chỉ mục |
| `Services/Data/IDatabaseInsightsService.cs` | Interface |

## 3. Lớp truy vấn bảng

### 3.1. Khung dùng chung

| Tệp | Vai trò |
| --- | --- |
| `Services/Tables/TableLoadServiceBase.cs` | `SqlTableLoadService` — `OpenAsync` an toàn |
| `Services/Tables/SqlConditionBuilder.cs` | Ghép `WHERE` + tạo tham số `@Name` |
| `Services/Tables/FilesSqlQueryBuilder.cs` | Dựng SQL tệp + **`DayFgrScope`** (khoá vùng ngày) |
| `Services/Tables/TablePage.cs` | Trang, `OFFSET`/`FETCH` |
| `Services/Tables/PagedResult.cs` | `{ Items, HasMoreRows, TotalCount }` |
| `Services/Tables/ITableLoadService.cs` | Interface chung |
| `Services/Tables/ChainsQuery.cs`, `SessionsQuery.cs`, `FilesQuery.cs`, `ContactsQuery.cs` | Record mô tả điều kiện |
| `Services/Tables/SessionMetadataParser.cs` | Cắt `VAL_XML` thành bảng con, giữ nhóm Nguồn phát/Vị trí và trường XML lồng nhau |
| `Services/Tables/SessionFileTreeParser.cs` | Lấy danh sách tệp từ `VAL_XML` |

> `DayFgrScope` sinh tập `DAY_FGR IN (...)` — điều kiện bắt buộc để các câu truy vấn
> dùng được index. Chi tiết ở [KienTruc.md §7.2](KienTruc.md).

### 3.2. Từng bảng

| Tệp | Bảng | Ghi chú |
| --- | --- | --- |
| `ChainsLoadService.cs` / `IChainsLoadService.cs` | `main.CHAINS` | Có phạm vi phiên `EXISTS`, đã khoá vùng ngày |
| `SessionsLoadService.cs` / `ISessionsLoadService.cs` | `msg.ViewSessionsNC`, `msg.CONTACTS`, `msg.FILES` | Dùng view có sẵn và nạp IP/điện thoại theo lô; bộ đếm phiên/tệp áp dụng cùng bộ lọc hiện tại bằng `SELECT`/`EXISTS`, không sửa CSDL |
| `FilesLoadService.cs` / `IFilesLoadService.cs` | `msg.FILES` | Phân trang bằng con trỏ, có đệm |
| `FilesSqlQueryBuilder.cs` | — | Dựng SQL tệp; `EXISTS` thay JOIN; khoá vùng ngày |
| `ContactsLoadService.cs` / `IContactsLoadService.cs` | `msg.CONTACTS`, `info.TYPES_CONTACT` | Bảng liên hệ độc lập; liên hệ của phiên đọc từ `VAL_XML` |
| `Services/Tables/FileExtensionCatalog.cs` | — | **Ủy quyền** cho `Services.Files.FileExtensionCatalog` |

> **Lưu ý khi đọc câu tệp:** `EXISTS` thay `INNER JOIN` với `msg.SESSIONS`, vì điều kiện nối
> chỉ trên `ID_S` nên JOIN sẽ nhân bản dòng khi cùng `ID_S` có ở nhiều `DAY_FGR`, làm sai
> `COUNT` và phân trang. Chi tiết ở [KienTruc.md](KienTruc.md).


## 4. Lớp xem trước tệp

| Tệp | Vai trò |
| --- | --- |
| `Services/Files/Preview/IFilePreviewStrategy.cs` | Interface chiến lược theo `FilePreviewKind` |
| `Services/Files/Preview/FilePreviewContentFactory.cs` | Chọn đúng chiến lược theo loại |
| `Services/Files/Preview/FileTypeDetector.cs` | Đoán loại từ đuôi + chữ ký đầu tệp |
| `Services/Files/Preview/FilePreviewLimits.cs` | Các hạn mức đọc (xem [FilePreview.md](FilePreview.md)) |
| `Services/Files/Preview/ArchivePreviewStrategy.cs` | Chiến lược gói nén (zip/rar/7z/tar/gz) |
| `Services/Files/Preview/ImagePreviewStrategy.cs` | Chiến lược ảnh |
| `Services/Files/Preview/MediaPreviewStrategy.cs` | Chiến lược âm thanh/video |
| `Services/Files/Preview/TextPreviewStrategy.cs` | Chiến lược văn bản |
| `Services/Files/Preview/DocumentPreviewStrategy.cs` | Chiến lược tài liệu |
| `Services/Files/Preview/BinaryPreviewStrategy.cs` | Chiến lược nhị phân |
| `Services/Files/Preview/TablePreviewStrategy.cs` | Chiến lược bảng dữ liệu |
| `Services/Files/Preview/DocumentTextExtractor.cs` | Trích văn bản docx/xlsx/pptx/pdf/rtf/epub |
| `Services/Files/Preview/DelimitedTextReader.cs` | Đọc CSV/TSV |
| `Services/Files/Preview/SqlitePreviewReader.cs` | Đọc SQLite |
| `Services/Files/Preview/StataDtaReader.cs` | Đọc Stata `.dta` (định dạng 118) |
| `Services/Files/Preview/GpsTrackReader.cs` | Đọc track GPS (NMEA, GPX, KML) |
| `Services/Files/Preview/BinaryFormatReader.cs` | Chuỗi nhị phân nhúng (dùng chung) |
| `Services/Files/Preview/HexDumpBuilder.cs` | Dựng bảng hex |
| `Services/Files/Preview/ImagePreviewService.cs` | Giải mã ảnh bằng NetVips/libvips |
| `Services/Files/Preview/ImageInfoReader.cs` | Đọc kích thước/không gian màu |
| `Services/Files/Preview/MediaInfoReader.cs` | Đọc thông tin media (bitrate, thời lượng) |
| `Services/Files/Preview/MediaPlaybackSupport.cs` | WPF `MediaElement` có phát được không |
| `Services/Files/Preview/PrintableStringReader.cs` | Chuỗi in được |
| `Services/Files/Preview/SampleFileGenerator.cs` | Sinh 64 tệp mẫu |
| `Services/Files/Preview/SampleFileBuilder.cs` | Dựng từng tệp mẫu |

## 5. Lớp báo cáo và xuất

| Tệp | Vai trò |
| --- | --- |
| `Services/Files/WordDocumentBuilder.cs` / `IWordDocumentBuilder.cs` | Sinh tài liệu `.docx` |
| `Services/Files/FileExportService.cs` | Điều phối xuất |
| `Services/Files/FilePathResolver.cs` | Ghép `SHORT_NAME` + `EXT` + thư mục gốc |
| `Services/Files/FileDetail*.cs` | Báo cáo chi tiết tệp |

## 6. ViewModel

| Tệp | Vai trò |
| --- | --- |
| `ViewModel/MainWindowViewModel.cs` | Điều phối 4 ViewModel con, thanh trạng thái, đếm tổng, mở cửa sổ Cài đặt |
| `ViewModel/Tables/PagedTableViewModel.cs` + `.Loading` `.Paging` `.Selection` | Bảng phân trang dùng chung |
| `ViewModel/Tables/SessionsTableViewModel.cs` | Bảng Phiên |
| `ViewModel/Tables/ChainsTableViewModel.cs` | Bảng Chuỗi, đánh dấu đã khai thác |
| `ViewModel/Tables/ContactsTableViewModel.cs` | Bảng Liên hệ trong tab chính |
| `ViewModel/Tables/SessionFilesViewModel.cs` + `.Preview` | Cây tệp + điều phối xem trước |
| `ViewModel/FileExtensionsViewModel.cs` + `.Selection` `.Statistics` | Bộ lọc đuôi |
| `ViewModel/FileExtensionFilterRowViewModel.cs` | Một dòng trong bộ lọc đuôi |
| `ViewModel/FilePreviewViewModel.cs` | Trạng thái khung xem trước |
| `ViewModel/PropertyDetailViewModel.cs` | Thuộc tính của đối tượng được chọn |
| `ViewModel/SidebarItemViewModel.cs` | Mục cây bên trái |
| `ViewModel/Workflows/ChainExploitationCoordinator.cs` | **Điều phối khai thác chuỗi** |
| `ViewModel/Settings/SettingsViewModel.cs` + `.SettingsNavItem.cs` | Cửa sổ Cài đặt: 4 nhóm tuỳ chọn, validate, lệnh lưu/đặt lại/kiểm tra |

## 7. View (XAML)

| Tệp | Vai trò |
| --- | --- |
| `Views/MainWindow.xaml(.cs)` | Bố cục 4 vùng, nút bánh răng mở Cài đặt |
| `Views/SettingsWindow.xaml(.cs)` | Cửa sổ Cài đặt (4 nhóm tuỳ chọn) |
| `Views/LoadingWindow.xaml(.cs)` | Cửa sổ chờ |
| `Views/FilePreviewWindow.xaml(.cs)` | Cửa sổ xem trước riêng |
| `Views/Controls/SessionsTableView.xaml(.cs)` | Bảng Phiên |
| `Views/Controls/ChainsTableView.xaml(.cs)` | Bảng Chuỗi |
| `Views/Controls/SessionFilesPane.xaml(.cs)` | Khung chi tiết phiên (khung phải): thẻ Dossier + mỗi phần VAL_XML một thẻ |
| `Views/Controls/LocationMapPane.xaml(.cs)` | Bản đồ Mapsui offline (MBTiles) cho tọa độ trong metadata Vị trí; ưu tiên gói Việt Nam + Biển Đông nếu có |
| `Views/Controls/ShortbreadMbTilesTileSource.cs` | Đọc và vẽ vector tile Shortbread (PBF) từ MBTiles offline |
| `Views/Controls/FilePreviewPane.xaml(.cs)` | Khung xem trước (khung dưới) |
| `Views/Controls/Previews/*.xaml(.cs)` | 7 panel theo định dạng |
| `Views/Controls/WindowCaptionBar.xaml(.cs)` | Thanh tiêu đề tự vẽ: nút Thu nhỏ / Phóng to / Đóng, đổi màu theo theme |
| `Views/Controls/Docking/DockWorkspace.xaml(.cs)` | Khung neo 4 panel (AvalonDock): gán kích thước ban đầu sau khi nạp |
| `Views/Controls/Docking/AppDockTheme.cs` | Theme neo của ứng dụng, trỏ tới `DockTheme.xaml` |
| `Views/Resources/Icons.xaml` | Icon Lucide dùng chung |
| `Views/Resources/Metrics.xaml` | Khoảng cách, bo góc, cỡ chữ, chiều cao control (tầng metric) |
| `Views/Resources/TableStyles.xaml` | Style dùng chung: bảng, nút, panel, chip, thanh cuộn |
| `Views/Themes/ThemeMidnight.xaml` … `ThemeCarbon.xaml` | 5 bảng màu, mỗi theme một tệp |
| `Views/Themes/DockStyles.xaml` | Ghi đè style tab/splitter của AvalonDock (xem [KienTruc.md §10.7](KienTruc.md)) |
| `Views/Themes/CommonControls.xaml` | Icon lịch dùng chung, không phụ thuộc màu |
| `Models/Appearance/AppTheme.cs` | Record mô tả một theme |
| `Services/Appearance/AppThemeCatalog.cs` | Danh mục theme, tra cứu và chuẩn hoá khoá |
| `Services/Appearance/AppThemeService.cs` | Thay thế dictionary lúc chạy, lưu vào cấu hình |

## 8. Enum và Presentation

| Tệp | Vai trò |
| --- | --- |
| `Enums/FilePreviewKind.cs` | `None/Image/Text/Media/Document/Binary/Archive/Table` |
| `Enums/FilePreviewLayout.cs` | Bố cục panel hiển thị |
| `Enums/ExportType.cs` | Kiểu xuất (Word) |
| `Presentation/IconCatalog.cs` | Ánh xạ dữ liệu → icon Lucide + tên brush |
| `Presentation/ResourceKeyToBrush.cs` | Converter tên brush → `Brush` |
| `Presentation/QualityStar.cs`, `PriorityBadge.cs` | Sao chất lượng, badge ưu tiên |

## 9. Công cụ dòng lệnh

`tools/SampleFiles.Cli/Program.cs` — sinh 64 tệp mẫu rồi chạy thử khung xem trước
trên từng tệp, in kết quả và trả mã thoát khác 0 nếu có tệp không xem trước được.
Chi tiết ở [KiemThu.md](KiemThu.md).
