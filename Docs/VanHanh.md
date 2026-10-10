# Vận hành

## 1. Yêu cầu môi trường

| Thành phần | Yêu cầu |
| --- | --- |
| Hệ điều hành | Windows 10/11, x64 |
| .NET SDK | 8.0 (chỉ cần khi phát triển) |
| SQL Server | 2019 trở lên; khi phát triển dùng `LocalDB` |
| Bộ giải mã media | Windows Media Foundation (có sẵn trên Windows) |

Bản phát hành **self-contained win-x64** nên máy đích không cần cài .NET.

## 2. Cài đặt lần đầu

```powershell
# 1. Tạo CSDL từ DDL
sqlcmd -S (localdb)\MSSQLLocalDB -d DeltaBat -i WpfApp\Scripts\DeltaBat.sql

# 2. Nạp dữ liệu mẫu (tùy chọn)
sqlcmd -S (localdb)\MSSQLLocalDB -d DeltaBat -i WpfApp\Scripts\SeedDeltaBat.sql
```

> Script SQL là **UTF-16LE**. Nếu chạy trong trình soạn SQL Server Management Studio thì
> phải mở bằng `File → Open → File` (SSMS tự nhận mã hoá) chứ không dùng `File → Open
> Project`. Với `sqlcmd -i` không cần chỉ định mã hoá.

## 3. Cấu hình

`WpfApp/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DeltaBat": "Server=(localdb)\\MSSQLLocalDB;Database=DeltaBat;Trusted_Connection=True;"
  }
}
```

Thứ tự ưu tiên:

1. Biến môi trường `DELTABAT_CONNECTION_STRING` (khuyến nghị cho môi trường thật —
   không lộ mật khẩu trong tệp).
2. `ConnectionStrings:DeltaBat` trong `appsettings.json`.
3. Nếu cả hai đều rỗng → báo lỗi rõ ràng, có nêu tên tệp đã tìm.

```powershell
$env:DELTABAT_CONNECTION_STRING = "Server=10.0.0.5;Database=DeltaBat;User Id=app;Password=***;TrustServerCertificate=True"
```

`appsettings.json` được đọc **một lần** rồi `Lazy` giữ lại; chỉnh sửa file cần khởi
động lại ứng dụng.

### 3.1. Cấu hình người dùng

Ngoài `appsettings.json`, ứng dụng còn đọc `%APPDATA%\DeltaBat\settings.json` — cấu hình
do người dùng sửa qua cửa sổ **Cài đặt**. Giá trị ở đây đứng trên `appsettings.json`,
nhưng dưới biến môi trường:

```
Biến môi trường  >  %APPDATA%\DeltaBat\settings.json  >  appsettings.json
```

| Việc | Cách làm |
| --- | --- |
| Xem nội dung | Mở cửa sổ Cài đặt → tab *Giới thiệu* → *Mở thư mục cấu hình* |
| Sửa tay | Sửa `settings.json`, khởi động lại ứng dụng |
| Xoá sạch | Xoá tệp `settings.json`, hoặc bấm *Xoá toàn bộ cài đặt* trong cửa sổ |

> Đọc sai kiểu tệp thì ứng dụng vẫn khởi động với giá trị mặc định — mất tuỳ chọn còn
> hơn là không lên được.

Chi tiết: [CaiDat.md](CaiDat.md).

## 4. Build và chạy

```powershell
# Build
dotnet build WpfApp\WpfApp.csproj -c Release

# Chạy ở chế độ phát triển
dotnet run --project WpfApp\WpfApp.csproj

# Đóng gói self-contained
dotnet publish WpfApp\WpfApp.csproj -c Release -r win-x64 --self-contained true
```

Kết quả publish nằm ở `WpfApp\bin\Release\net8.0-windows\win-x64\publish\`.

## 5. Quy trình dùng hằng ngày

1. Mở ứng dụng, kiểm tra dòng trạng thái hiện tên CSDL và số tham số đã nạp.
2. Chọn **khoảng ngày**.
3. Sang tab **Chuỗi**, tick các chuỗi cần xem.
4. Bấm **Khai thác**.
5. Duyệt phiên ở tab **Phiên**; bấm một tệp để xem trước.
6. Tick các tệp cần → **Xuất Word**.

Chi tiết nghiệp vụ: [NghiepVu.md](NghiepVu.md).

## 6. Hiệu năng

| Điểm | Giá trị / khuyến nghị |
| --- | --- |
| Thời gian khởi động | Phụ thuộc độ trễ mạng tới SQL Server |
| Kích thước trang mặc định | 100 dòng |
| Tối đa tệp hiển thị mỗi phiên | 1000 (`MaxFilesPerSession`) |
| Tối đa dòng xuất Word | 20 000 |
| `CommandTimeout` | 60 giây |
| Vòng đời kết nối | Mở/đóng theo từng truy vấn (không giữ connection pool tay) |

**Khoảng ngày là đòn bẩy hiệu quả nhất.** Chọn 1 ngày thay vì 1 tháng giảm thời gian
tải theo tỷ lệ.

Nếu thấy ứng dụng phản hồi chậm:

1. Kiểm tra đang chọn bao nhiêu chuỗi (tick càng nhiều càng nhiều phiên).
2. Giảm khoảng ngày.
3. Giảm kích thước trang.
4. Xem [CoSoDuLieu §8](CoSoDuLieu.md) để biết câu nào tốn thời gian và vì sao.

## 7. Xử lý sự cố

| Triệu chứng | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| `Không tìm thấy 'ConnectionStrings:DeltaBat'` | Thiếu cấu hình | Thêm vào `appsettings.json` hoặc đặt biến môi trường |
| `Không thể kết nối đến cơ sở dữ liệu` | SQL Server không chạy / sai tên instance | Kiểm tra dịch vụ, tên server, firewall |
| `MissingManifestResourceException` lúc khởi động | Bản dịch HandiControl bị tách thành satellite assembly | Xem cảnh báo ở [KienTruc §6.2](KienTruc.md) |
| Lọc đuôi không ra kết quả | Lẫn dấu chấm `.wav` vs `wav` | Xem [CoSoDuLieu §9](CoSoDuLieu.md) |
| Phiên có tệp nhưng cây tệp trống | `VAL_XML` rỗng hoặc lệch `DAY_FGR` | Xem câu SQL kiểm tra ở [CoSoDuLieu §9](CoSoDuLieu.md) |
| Video/âm thanh không phát | `MediaElement` không có codec | Mở bằng trình phát ngoài; xem [FilePreview.md](FilePreview.md) |
| "Không xem trước được" | Định dạng chưa hỗ trợ | Báo lại đuôi tệp để bổ sung chiến lược |
| Ứng dụng chạy nhưng không thấy dữ liệu | CSDL trống hoặc sai khoảng ngày | Kiểm tra bằng `SeedDeltaBat.sql` |

### 7.1. Sau khi sửa mã, cần khởi động lại

Mọi thay đổi cần **đóng hẳn ứng dụng rồi mở lại** — chạy song song bản cũ và bản mới
sẽ dùng nhầm tệp DLL. Nếu đang chạy từ Visual Studio, dừng (Shift+F5) rồi chạy lại.

## 8. Bảo trì định kỳ

| Việc | Tần suất |
| --- | --- |
| Cập nhật gói NuGet | Theo đợt phát hành |
| Chạy bộ kiểm thử tệp mẫu | Sau mỗi thay đổi khung xem trước |
| Kiểm tra hiệu năng truy vấn phiên | Khi dữ liệu vượt vài chục triệu dòng |
| Rà lại tệp mẫu 7z/rar | Khi bổ sung kho nén thật |

## 9. Quy trình thêm định dạng tệp mới

1. Thêm đuôi vào `Services/Files/Preview/FileExtensionCatalog.cs`.
2. Nếu thuộc nhóm có sẵn (`Image`, `Text`, `Table`...) thì chiến lược tương ứng đã xử lý
   — có thể chỉ cần thêm đuôi.
3. Nếu là nhóm mới: thêm giá trị `FilePreviewKind`, tạo `IXxxPreviewStrategy`, đăng ký
   trong DI, thêm `XxxPreviewPanel.xaml`, và thêm `DataTrigger` trong `FilePreviewPane`.
4. Thêm tệp mẫu vào `SampleFileBuilder` để có thứ kiểm thử.
5. Cập nhật [FilePreview.md](FilePreview.md).
