# Kiểm thử

## 1. Bộ kiểm thử tệp mẫu

Công cụ `tools/SampleFiles.Cli` sinh **64 tệp mẫu** (58 đuôi khác nhau) rồi chạy
từng tệp qua **đúng đường đi của ứng dụng**: `FileTypeDetector` → chọn chiến lược theo
`FilePreviewKind` → nạp nội dung → so kết quả với kỳ vọng trong `SampleFileCatalog`.

Mục tiêu: kiểm tra logic khung xem trước **không cần mở cửa sổ WPF, không cần CSDL**.

### 1.1. Chạy

```powershell
dotnet run --project tools\SampleFiles.Cli\SampleFiles.Cli.csproj -- .\SampleFiles
```

Lệnh này vừa sinh tệp vừa kiểm tra, in kết quả từng dòng và trả về:

| Mã thoát | Ý nghĩa |
| --- | --- |
| `0` | Tất cả tệp xem trước được như mong đợi |
| `1` | Có tệp không cho kết quả như mong đợi |

### 1.2. Năm nhóm kiểm tra

Kết quả thực tế gần nhất (exit code `0`):

| Nhóm | Phạm vi | Kết quả |
| --- | --- | --- |
| Logic thuần | 21 ca | **21/21 đúng** |
| Xem trước tệp | 64 tệp | 64/64 đúng |
| Bố cục panel | 22 định dạng | 22/22 đúng |
| Lọc đuôi file | 3 ca | 3/3 đúng |
| Kế hoạch truy vấn | 5 câu SQL | 5/5 đúng |

Nhóm logic thuần kiểm thử các hàm không cần CSDL, nên chạy được ở mọi máy:

| Nhóm kiểm tra | Số ca | Vì sao quan trọng |
| --- | --- | --- |
| `DayFgrScope` | 10 | Quyết định tập ngày dùng để khoá index. Sai lặng — ra kết quả rỗng chứ không báo lỗi |
| Chuẩn hoá đuôi | 5 | Nguyên nhân lịch sử của bộ lọc rỗng |
| Phân trang | 5 | Tràn kích thước trang, chỉ số âm |
| Con trỏ phân trang | 1 | Con trỏ là chuỗi, phải đọc lại được |

Ca đáng chú ý nhất trong nhóm `DayFgrScope`:

```powershell
DayFgrScope.Build(05/10/2026, 07/12/2026)  # -> rỗng, KHÔNG phải [0,5,6,7]
```

`DAY_FGR` là **ngày trong tháng** (tinyint 0–31), nên tập "5..12" cho khoảng 05/10–07/12
sẽ loại mất các ngày 13..31 của tháng 10 — người dùng tưởng không có dữ liệu. Hàm trả
mảng rỗng trong trường hợp đó để chỉ lọc bằng `DT_START`: chậm hơn nhưng không sai.

Kết quả lọc đuôi (dữ liệu thật, gồm cả đuôi viết hoa để bắt lỗi so khớp):

```
chon [wav, dta]      -> lọc 3 đuôi: .wav, .WAV, .dta
chon [WAV, .DTA]     -> lọc đúng:     .wav, .WAV, .dta
khong chon đuôi      -> giữ nguyên 6 đuôi
```

Dòng thứ hai là ca quan trọng nhất: đầu vào `WAV` (hoa, không dấu chấm) và `.DTA`
(hoa, có dấu chấm) phải cho ra **cùng kết quả** với `wav`, `dta` — đúng như cách lọc
trong CSDL. Trường hợp này từng làm bộ lọc trả về danh sách rỗng.

### 1.3. Kiểm tra kế hoạch thực thi

Nhóm kiểm tra này chạy đúng các câu SQL mà ứng dụng sinh ra, rồi đọc kế hoạch thực
thi thật từ SQL Server (`SET STATISTICS XML ON`) để xem **index nào** được dùng và
**quét cả bao nhiêu dòng**.

Đây là bằng chứng cho các kỹ thuật tối ưu ở [KienTruc.md §7](KienTruc.md), không phải
kiểm tra logic từ định nghĩa. Kết quả gần nhất:

```
--- CSDL lưu EXT: pdf (không dấu chấm) ---
--- Kiểm tra kế hoạch thực thi của các câu truy vấn ---
+ tệp của phiên + khoá ngày      có index: [IX_FS_EXT], [PK_MSG_FILES_TBL], [PK_INFO_TYPES_STATUS]
+ tệp của phiên + đuôi           có index: [IX_FS_EXT], [PK_MSG_FILES_TBL], [PK_INFO_TYPES_STATUS]
+ tệp theo chuỗi (EXISTS)        có index: [IX_FS_EXT_TYPE], [IX_FS_DT_START], [IX_SS_IDTRANSP], ...
+ đếm tệp của phiên              có index: [IX_FS_EXT], [IX_FS_DT_START]
+ tệp không lọc gì (toàn bảng)   có index: [PK_MSG_FILES_TBL], [PK_INFO_TYPES_STATUS]
```

Đọc kết quả:

| Dòng | Ý nghĩa |
| --- | --- |
| `tệp của phiên + khoá ngày` | Câu nặng nhất hằng ngày — dùng `IX_FS_EXT` |
| `tệp theo chuỗi (EXISTS)` | Có cả index của `msg.SESSIONS` (`IX_SS_IDTRANSP`) nhờ mệnh đề `EXISTS` |
| `đếm tệp của phiên` | `COUNT` cũng seek được |
| `tệp không lọc gì (toàn bảng)` | Không áp đặt — trên CSDL nhỏ thì cả câu quét cả bảng cũng chạy index |

> **Lưu ý khi chạy:** câu bắt buộc dùng `MultipleActiveResultSets=true` (đọc kế hoạch
> qua `NextResult()`) và bỏ mệnh đề `OPTION (RECOMPILE)` — SQL Server không sinh kế hoạch
> cho câu có mệnh đề đó. Bỏ đi không ảnh hưởng tới việc chọn index.
>
> Nếu không nối được CSDL, nhóm này in "Bỏ qua kiểm tra truy vấn" và **không tính là
> lỗi**, để lệnh kiểm thử vẫn chạy được trên máy không cài SQL Server.

### 1.4. Đo thời gian thực tế

Kế hoạch thực thi cho biết câu *dùng index hay không*; phép đo này cho biết nó *chạy mất
bao lâu* — con số người dùng cảm nhận được. Chạy 5 lần lấy trung vị để loại ảnh hưởng
của lần biên dịch kế hoạch đầu tiên, và in ở lần cuối khi kế hoạch đã nằm trong cache.

```
--- Đo thời gian thực tế (5 lần, lấy trung vị) ---
+ tệp của 1 phiên        0 ms ·    6 dòng · reads 0 · tham số 3
+ bảng Phiên trang 1     0 ms ·   60 dòng · reads 0 · tham số 3
--- CSDL: msg.FILES 3,000 dòng · phiên mẫu #500 ---
```

> **Đọc số liệu này:** con số trên CSDL nhỏ (3.000 dòng) nên không đại diện cho CSDL thật
> (23 triệu dòng). Cái cần mang đi là **phương pháp**: chạy lệnh này trên CSDL thật để có
> con số chính xác, và so sánh với mốc 45–130 giây của bản chưa tối ưu.

### 1.5. Đối chiếu phân trang bằng con trỏ

Phân trang bằng con trỏ phải cho ra **đúng bằng** lấy một lần bằng `OFFSET` — không
trùng dòng nào, không bỏ sót dòng nào. Bộ kiểm thử duyệt hết bằng con trỏ (10 dòng mỗi
trang) rồi so với một câu `OFFSET` lấy hết:

```
--- Đối chiếu phân trang: con trỏ vs OFFSET ---
+ con trỏ khớp OFFSET: 60/60 dòng qua 6 trang
```

Kiểm thử bỏ qua (không tính lỗi) nếu CSDL không đủ dữ liệu để có hơn một trang.

> **Lỗi đã phát hiện nhờ kiểm thử này:** câu danh sách từng sinh ra `SELECT TOP (@Fetch)`
> kèm `OFFSET ... FETCH NEXT`, mà SQL Server không cho dùng cả hai trong một câu
> (`A TOP can not be used in the same query or sub-query as a OFFSET`). Bảng Phiên vì thế
> **luôn lỗi** khi tải trang đầu. Nay câu chọn một trong hai: trang đầu dùng `OFFSET`, các
> trang sau dùng con trỏ `TOP`. Xem [KienTruc.md §7.5](KienTruc.md).

## 2. Bộ kiểm thử logic (`dotnet test`)

Dự án `tests/WpfApp.Tests` kiểm thử **logic thuần của chính ứng dụng** bằng xUnit: gọi
trực tiếp lớp thật (`internal` mở qua `InternalsVisibleTo`) chứ không sao chép logic ra
bản riêng — sao chép thì có thể lệch mà không ai biết.

Không cần CSDL, không mở cửa sổ WPF, nên chạy được ở mọi máy.

### 2.1. Chạy

```powershell
dotnet test tests\WpfApp.Tests\WpfApp.Tests.csproj
```

### 2.2. Nhóm kiểm thử

| Lớp kiểm thử | Phạm vi | Vì sao quan trọng |
| --- | --- | --- |
| `SessionFileMatcherTests` | So khớp tệp giữa bảng "Tệp" và tệp đang mở | `PATH_NAME` là THƯ MỤC chứa tệp, so khớp bằng nó sẽ luôn tô sáng dòng đầu tiên |
| `SessionSelectionRegressionTests` | Hồi quy đúng hai ca đã gặp lỗi (`.avi` → `.flv`, `.7z` → `.rar`) | Bảng "Tệp" dựng từ VAL_XML còn tệp đang mở đọc từ CSDL — hai nguồn khác nhau |
| `SessionMetadataParserTests` | Cắt `VAL_XML` thành các bảng của khung chi tiết | Ẩn/hiện phần và cột; dựng `FileRows` cho bảng "Tệp" |
| `SessionFileTreeParserTests` | Dựng cây "Tệp tin" bên trái | Thẻ `<files>` từng bị tính nhầm là một tệp |
| `DayFgrScopeTests` | Tập ngày khoá index | Sai lặng: câu vẫn chạy, chỉ ra kết quả rỗng |
| `TablePageTests`, `PagedResultTests` | Phân trang, số trang, số thứ tự dòng | Trang âm, tràn kích thước, trang ngoài phạm vi |
| `FileExtensionCatalogTests` | Chuẩn hoá đuôi cho cả SQL lẫn lọc tại chỗ | Nguyên nhân lịch sử của bộ lọc luôn rỗng |
| `FilesQueryTests` | Mô tả câu truy vấn và cờ "không lọc" | Câu không lọc gì thì đếm bằng DMV cho nhanh |
| `UserSettingsTests`, `UserFirstAppSettingsTests`, `SettingsViewModelTests` | Cấu hình người dùng và cửa sổ Cài đặt | Ghi đè cấu hình sai thì lần khởi động sau không lên được ứng dụng |

Kết quả gần nhất: **145/145 đúng** (`Failed: 0, Passed: 145`).

### 2.3. Điểm cần biết

- Dự án test khai báo `RuntimeIdentifier=win-x64` và `UseWPF` để khớp với `WpfApp`;
  đổi RID của ứng dụng thì phải sửa cả hai.
- `FileExtensionCatalog.ExtStoredWithLeadingDot` là trạng thái tĩnh đọc từ CSDL; ca kiểm
  thử đổi nó rồi **hoàn nguyên trong `finally`** để không ảnh hưởng ca khác.
- Không so khớp câu chữ tiếng Việt trong kỳ vọng (`Describe()`); chỉ kiểm tra ý nghĩa để
  đổi văn bản hiển thị không làm hỏng test.

### 2.4. Nhóm hiệu năng

Các ca đo thời gian đều mang nhãn `Category=Performance`. Chạy riêng:

```powershell
dotnet test tests\WpfApp.Tests\WpfApp.Tests.csproj --filter Category=Performance
```

| Nhóm | Đo gì | Ngưỡng |
| --- | --- | --- |
| `LogicPerformanceTests` | So khớp 1000 tệp, cắt XML 1000 thẻ `<file>`, dò kiểu 200 lần, chuẩn hoá 10.000 đuôi | < 0,2–2 s |
| `DatabasePerformanceTests` | Câu thật của ứng dụng trên **CSDL thật**: tệp của phiên, tệp theo đuôi, danh sách phiên theo ngày, thống kê đuôi, nạp và cắt `VAL_XML` | < 2–15 s |

Nguyên tắc đặt ngưỡng:

- Ngưỡng **rộng**, chỉ để bắt hồi quy đột ngột (ví dụ một vòng lặp O(n²) mới thêm), không
  dùng để so sánh tốc độ máy — máy kiểm thử có thể chậm hơn nhiều.
- Câu CSDL đo qua **đúng service của ứng dụng** và **CSDL thật**, không dùng CSDL in-memory:
  kế hoạch thực thi và cache quyết định độ trễ, đo trên LocalDB rồi suy ra là sai.
- Thiếu CSDL thì ca được **bỏ qua** (`SkippableFact`), không phải báo đạt giả.
- Phiên dùng để đo được chọn bằng SQL sao cho **có tệp .avi**; câu lọc theo đuôi mà trả
  về 0 dòng thì đo thời gian không có ý nghĩa.

Kết quả đo gần nhất trên LocalDB (500 phiên / 3000 tệp / 25 chuỗi):

| Ca | Thời gian |
| --- | --- |
| Tệp của một phiên | 5 ms |
| Tệp của một phiên theo đuôi | 17 ms |
| Danh sách phiên theo khoảng ngày | 44 ms |
| Thống kê đuôi theo chuỗi | 14 ms |
| Nạp `VAL_XML` của phiên | 3 ms |
| Cắt `VAL_XML` thật | 35 ms |
| So khớp 1000 tệp × 100 lần | 14 ms |
| Cắt XML 1000 thẻ `<file>` | 6 ms |

## 3. Tệp mẫu phủ những gì

| Nhóm | Tệp đại diện |
| --- | --- |
| Ảnh | PNG, GIF, ICO hợp lệ (giải mã thật bằng NetVips/libvips) |
| Âm thanh | Sóng WAV tạo ra |
| Video | `video-mau.flv` — có chữ ký Flash Video thật, WPF không phát được |
| Văn bản | txt, log, csv, xml, json, base64, html, rtf, epub, odt |
| Tài liệu | `tai-lieu.docx`, `bao-cao.pdf` (trích văn bản thật) |
| Bảng dữ liệu | CSV, `du-lieu-mau.dta` (Stata 118), `track-1.gpsidt` (NMEA 0183), `track-2.gpx`, `du-lieu.sqlite` |
| Gói nén | zip, tar, gz, rar, 7z |
| Nhị phân | `du-lieu.sqlite`, `chuong-trinh.exe` (chữ ký PE), `rong.dat` |

### 3.1. Điểm cần biết về tệp mẫu

- **7z/rar chỉ có chữ ký, chưa phải kho nén hợp lệ.** Logic đọc đã dùng SharpCompress
  và sẽ chạy được trên kho thật, nhưng tệp mẫu chưa kiểm chứng đường giải nén thực sự.
  Nên thay bằng kho thật khi có thể.
- **FLV không phát được trong WPF** — đây là giới hạn của `MediaElement`, không phải
  lỗi. Tệp vẫn hiện thông tin chữ ký và nút mở trình phát ngoài.
- Tệp có nội dung thật (chữ ký đúng, dữ liệu hợp lệ), không phải tệp rỗng hay văn bản
  đặt tên giả.

## 4. Kiểm thử thủ công

Ba phần này **chưa** có kiểm thử tự động, cần kiểm tra tay với CSDL thật.

### 4.1. Khởi động

- [ ] Mở ứng dụng, dòng trạng thái hiện tên CSDL và số tham số.
- [ ] Cửa sổ chờ biến mất, `MainWindow` hiện đầy đủ.
- [ ] Không có `MissingManifestResourceException` (bản dịch tiếng Việt nạp được).
- [ ] Ngày hiện trong bộ chọn là `dd/MM/yyyy`, phân tách ngày kiểu Việt Nam.

### 4.2. Bộ lọc đuôi

Chạy đúng 4 ca, đây là bước đã từng phát hiện lỗi:

- [ ] **Không tick đuôi nào** → danh sách hiện đầy đủ.
- [ ] Tick **`wav`** (chữ thường, không dấu chấm) → chỉ hiện tệp `.wav`.
- [ ] Tick **`.WAV`** (hoa, có dấu chấm) → kết quả **giống hệt** ca trên.
- [ ] Tick **`wav` + `dta`** cùng lúc → hiện cả hai loại.

Nếu danh sách vẫn rỗng, kiểm tra theo thứ tự:

1. `SELECT DISTINCT TOP 20 EXT FROM msg.FILES;` — xem CSDL thực sự lưu gì.
2. Ghi ra `EffectiveExtensions` trong `SessionFilesViewModel` — đã chuẩn hoá chưa.
3. Tham số SQL của `f.EXT IN (...)` — có dấu chấm chưa.
4. `row.Extension` trong `_allFiles` — có dấu chấm không.

### 4.3. Luồng khai thác

- [ ] Chọn khoảng ngày hợp lệ → bấm **Khai thác** không có chuỗi nào tick → hiện thống kê đuôi.
- [ ] Tick 1 chuỗi → **Khai thác** → bảng Phiên có dữ liệu, nút chuỗi chuyển màu "đã khai thác".
- [ ] Bấm **Khai thác** lần nữa khi đang chạy → lần trước bị hủy, không phải xếp hàng.
- [ ] Chọn khoảng ngày sai (bắt đầu > kết thúc) → báo lỗi, không khai thác.
- [ ] Chọn một phiên → khung tệp có danh sách; bấm một tệp → khung dưới xem trước.
- [ ] Xuất Word → tệp `.docx` mở được, bảng 11 cột đầy đủ.

### 4.4. Cửa sổ Cài đặt

Các ca cho phần này nằm trong `UserSettingsTests`, `UserFirstAppSettingsTests` và
`SettingsViewModelTests` (xem [CaiDat.md §9](CaiDat.md)). Những điểm cần kiểm tay:

- [ ] Đổi **thư mục gốc tệp** → bấm *Lưu* → mở một tệp, đường dẫn dựng theo thư mục mới
      ngay, **không** cần khởi động lại.
- [ ] Đổi **chuỗi kết nối** hoặc **cỡ trang** → lời nhắc phải nói cần khởi động lại.
- [ ] Gõ chuỗi sai → bấm *Kiểm tra kết nối* → báo sai cú pháp, **không** mở kết nối.
- [ ] Gõ chuỗi sai → bấm *Lưu thay đổi* → bị từ chối, tệp cấu hình không đổi.
- [ ] Sửa rồi đóng bằng **X** → hỏi *bỏ thay đổi chưa lưu*; bấm *Đóng* → hỏi **một lần
      duy nhất**, không hỏi lại lần hai.
- [ ] *Xoá toàn bộ cài đặt* → hỏi xác nhận → xoá tệp, các ô về mặc định, nhãn nguồn
      cập nhật theo.

## 5. Phạm vi kiểm thử hiện tại

| Đã kiểm thử tự động | Chỉ kiểm thử tay |
| --- | --- |
| 145 ca logic trong `WpfApp.Tests` (`dotnet test`) | Khởi động ứng dụng |
| 10 ca hiệu năng (logic + CSDL thật) | Luồng khai thác chuỗi |
| Xem trước 64 tệp mẫu (`tools/SampleFiles.Cli`) | Hiệu năng trên CSDL 23 triệu dòng |
| 3 ca lọc đuôi | Xuất Word |
| 5 câu SQL có dùng index | Thao tác kéo cửa sổ trong UI |
| Phân trang con trỏ khớp OFFSET | |
| Dò quy ước lưu đuôi EXT | |

## 6. Việc nên làm tiếp

| Đầu việc | Lý do |
| --- | --- |
| Thay normalizer riêng trong CLI bằng kiểm thử trực tiếp `Services.Files.FileExtensionCatalog` và `Services.Tables.FileExtensionCatalog` | Đang kiểm thử bản sao của logic, không phải logic thật — có thể lệch mà không ai biết |
| Đo lại trên CSDL 23 triệu dòng | Kiểm thử kế hoạch chạy trên LocalDB 3000 dòng; kế hoạch đúng nhưng thời gian thực tế phải đo trên dữ liệu thật |
| Thêm tệp 7z/rar hợp lệ | Đường giải nén chưa được kiểm chứng thực tế |
| Bổ sung `Sylvan.Data.Csv` làm dự phòng CSV | Thư viện đã tham chiếu nhưng chưa dùng; CsvHelper và bộ đọc thủ công đang hoạt động |
| Cân nhắc NAudio / LibVLC | Mở rộng định dạng phát được ngoài giới hạn của `MediaElement` |
| Chuyển phần kiểm thử khung xem trước sang `WpfApp.Tests` | Bộ kiểm thử 64 tệp mẫu đang nằm trong `Main` của công cụ dòng lệnh nên không tự động hoá được; đưa vào dự án test thì chạy được mỗi lần `dotnet test` |
| Cân nhắc `app.SelectCursorOpen/Fetch/Close` | Cơ chế server-side cursor của bản gốc DeltaBat; hiện ứng dụng dùng keyset trong SQL. Chuyển sang cursor sẽ tốn thêm vòng mạng mỗi lần chuyển trang — đánh đổi có thể không đáng |
