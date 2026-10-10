# DeltaBat — Không gian phân tích dữ liệu (WPF)

Ứng dụng WPF .NET 8 đọc và phân tích cơ sở dữ liệu **DeltaBat**: khai thác danh sách
chuỗi truyền thông vệ tinh, duyệt các phiên thu được theo chuỗi, xem tệp thu được và
xuất báo cáo Word.

> **Tài liệu đầy đủ:** [`Docs/`](Docs/README.md) — kiến trúc, nghiệp vụ, cơ sở dữ liệu,
> tham chiếu API, khung xem trước tệp, vận hành và bảo trì.

## Chạy nhanh

```powershell
# 1. Tạo CSDL từ script (SQL Server 2019+, file DeltaBat.sql là UTF-16LE)
sqlcmd -S (localdb)\MSSQLLocalDB -d DeltaBat -i WpfApp\Scripts\DeltaBat.sql

# 2. Nạp dữ liệu mẫu
sqlcmd -S (localdb)\MSSQLLocalDB -d DeltaBat -b -i WpfApp\Scripts\SeedDeltaBat.sql

Seed stores file paths under `D:\Code\Dev\WpfApp\SampleFiles`; keep the sample files in that folder to test preview.
Muốn để tệp ở chỗ khác (ví dụ ổ khác) thì đặt biến môi trường `SampleFilesPath` **trước khi**
chạy `sqlcmd` — script sẽ ghi đúng đường dẫn đó vào `msg.FILES.PATH_NAME` và vào `VAL_XML`:

```powershell
# Sinh 64 tệp mẫu thật vào thư mục đích, rồi chạy từng tệp qua đúng pipeline xem trước
dotnet run --project tools\SampleFiles.Cli -- E:\DeltaBatTest\SampleFiles

# Xoá dữ liệu cũ và nạp lại, trỏ vào thư mục trên
$env:SampleFilesPath = 'E:\DeltaBatTest\SampleFiles'
sqlcmd -S (localdb)\MSSQLLocalDB -d DeltaBat -b -i WpfApp\Scripts\SeedDeltaBat.sql
```

# 3. Chạy ứng dụng
dotnet run --project WpfApp\WpfApp.csproj
```

Chuỗi kết nối đọc từ `WpfApp/appsettings.json` (`ConnectionStrings:DeltaBat`), có thể
ghi đè bằng biến môi trường `DELTABAT_CONNECTION_STRING`.

## Cấu trúc thư mục

```
WpfApp/
├── App.xaml[.cs]                 Khởi động, nối DI, chuẩn hoá văn hoá vi-VN
├── appsettings.json              Cấu hình chuỗi kết nối
├── Scripts/                      DeltaBat.sql (schema) + SeedDeltaBat.sql (dữ liệu mẫu)
├── Enums/                        FilePreviewKind, FilePreviewLayout, ExportType...
├── Models/                       Row, ViewModel dữ liệu dùng chung
├── Services/
│   ├── Data/                     Kết nối CSDL, đếm dòng, thống kê DMV
│   ├── Tables/                   Truy vấn SQL cho Chuỗi / Phiên / Tệp / Liên hệ
│   └── Files/                    Xem trước tệp, xuất Word, báo cáo chi tiết
├── ViewModel/                    MVVM (CommunityToolkit.Mvvm)
├── Views/                        XAML: cửa sổ, bảng, dock panel, panel xem trước
└── Presentation/                 IconCatalog, ResourceKeyToBrush, hộp thoại

tools/SampleFiles.Cli/            Sinh 64 tệp mẫu thật + chạy từng tệp qua pipeline xem trước
tests/WpfApp.Tests/              Kiểm thử logic và hiệu năng bằng xUnit (dotnet test)
Docs/                             Tài liệu dự án
```

## Chạy kiểm thử

```powershell
dotnet test tests\WpfApp.Tests\WpfApp.Tests.csproj
```

Bộ kiểm thử này gọi trực tiếp lớp thật của ứng dụng (không sao chép logic), không cần
CSDL và không mở cửa sổ WPF. Chi tiết ở [Docs/KiemThu.md](Docs/KiemThu.md).

## Công nghệ

| Thành phần | Phiên bản | Vai trò |
| --- | --- | --- |
| .NET | 8.0-windows | Nền tảng, self-contained win-x64 |
| WPF | — | Giao diện desktop, binding + MVVM |
| CommunityToolkit.Mvvm | 8.4.2 | `[ObservableProperty]`, `[RelayCommand]` |
| Dirkster.AvalonDock | 4.74.1 | Khung neo panel (kéo thả, neo, tách cửa sổ, nhóm tab) |
| Microsoft.Data.SqlClient | 5.1.9 | Truy cập SQL Server |
| HandyControl | 3.5.1 | DatePicker, Pagination (kèm bản dịch vi) |
| Extended.Wpf.Toolkit | 5.1.2 | Bộ chọn ngày giờ |
| MahApps.Metro.IconPacks.Lucide | 6.2.1 | Bộ icon |
| DocumentFormat.OpenXml | 3.3.0 | Sinh tài liệu Word |
| NetVips | 3.2.0 (+ native libvips 8.18 win-x64) | Giải mã ảnh nhiều định dạng |
| SharpCompress | 1.0.0 | Đọc gói nén 7z/rar/tar/gz... |
| PdfPig | 0.1.16 | Trích văn bản PDF |
| CsvHelper | 33.1.0 | Đọc CSV/TSV chuẩn RFC 4180 |
| System.Data.SQLite.Core | 1.0.119 | Xem tệp SQLite |

## Các tính năng chính

- **Bảng Chuỗi** — danh sách chuỗi truyền thông (vệ tinh, tần số, modem), lọc theo
  ngày, tick chuỗi để khai thác.
- **Bảng Phiên** — các phiên thu được trong khoảng ngày đã chọn, phân loại theo trạng
  thái, loại dữ liệu, mức ưu tiên, chất lượng.
- **Cây tệp** — danh sách tệp của phiên, đọc từ cột `VAL_XML` của phiên.
- **Khung chi tiết phiên** — tách theo bản gốc DeltaBat: thẻ **Dossier** hiện thuộc
  tính của chính phiên, mỗi phần còn lại của `VAL_XML` (nguồn phát, liên hệ, vị trí, tệp)
  là một thẻ riêng có lưới riêng — thay vì xếp nhiều bảng nhỏ trong một khung cuộn.
- **Xem trước tệp** — khung preview cố định ở cuối cửa sổ chính tự hiển thị tệp đầu tiên
  khi chọn phiên và cập nhật khi nhấp tệp khác trong cây tệp hoặc bảng metadata. Hỗ trợ
  ảnh, âm thanh, video, văn bản; định dạng khác có thể mở bằng ứng dụng mặc định.
  Xem [`Docs/FilePreview.md`](Docs/FilePreview.md).
- **Xuất Word** — xuất danh sách tệp theo dòng đã tick hoặc theo điều kiện lọc.
- **Thống kê CSDL** — đếm dòng nhanh qua DMV, dung lượng bảng, câu lệnh chậm.
