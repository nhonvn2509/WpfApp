# Cài đặt

Cửa sổ **Cài đặt** cho phép người dùng đổi chuỗi kết nối, thư mục gốc chứa tệp và cỡ
trang mặc định — những thứ trước đây phải sửa tay trong `appsettings.json`.

Mở bằng nút bánh răng ở cuối thanh tiêu đề `MainWindow`.

> Xem thêm: [KienTruc.md](KienTruc.md) (cấu trúc lớp) · [VanHanh.md](VanHanh.md)
> (cấu hình lúc triển khai) · [KiemThu.md](KiemThu.md) (ca kiểm thử)

## 1. Vì sao tách cấu hình người dùng khỏi cấu hình kèm theo

Trước đây mọi cấu hình nằm trong `WpfApp/appsettings.json`. Cách đó có hai vấn đề khi
phát hành:

| Vấn đề | Hậu quả |
| --- | --- |
| Bản phát hành là **một tệp `.exe` duy nhất**, đặt trong thư mục có thể không có quyền ghi (`Program Files`) | Không thể lưu tuỳ chọn vào cạnh ứng dụng |
| Ghi đè `appsettings.json` để lưu tuỳ chọn | Mỗi lần nâng cấp là mất cấu hình người dùng vừa sửa |

Nay tách làm hai lớp, giá trị của người dùng **đứng trên** giá trị kèm theo:

| Lớp | Nguồn | Ghi được? | Ai sở hữu |
| --- | --- | --- | --- |
| Cấu hình người dùng | `%APPDATA%\DeltaBat\settings.json` | **Có** | Máy đang chạy, sống sót qua nhiều lần nâng cấp |
| Cấu hình kèm theo | `appsettings.json` + biến môi trường `DELTABAT_CONNECTION_STRING` | Không | Bộ cài đặt |

Thứ tự ưu tiên thực tế (cao → thấp):

1. Biến môi trường `DELTABAT_CONNECTION_STRING`
2. `%APPDATA%\DeltaBat\settings.json`
3. `ConnectionStrings:DeltaBat` trong `appsettings.json`

Cơ chế ghép hai lớp nằm ở `Services/Settings/UserFirstAppSettings.cs`, đăng ký thay thế
`IAppSettings` trong DI. Nhờ vậy **mọi nơi đang đọc `IAppSettings` không phải sửa một dòng
nào** mà vẫn tự động nhận giá trị người dùng đặt.

> `UserFirstAppSettings` phụ thuộc `JsonAppSettings` (kiểu cụ thể) chứ không phụ thuộc
> `IAppSettings` — nếu phụ thuộc interface thì nó tự trỏ về chính nó trong sổ đăng ký.

### 1.1. Cấu trúc JSON

Cấu trúc file người dùng **cố tình trùng** `appsettings.json` (`ConnectionStrings`,
`Files`, `Tables`) để lớp ghép chỉ cần đọc theo đường dẫn là ghi đè được, không phải
chép tay từng khoá:

```json
{
  "ConnectionStrings": { "DeltaBat": "Server=..." },
  "Files": { "RootPath": "D:\\DeltaBatData" },
  "Tables": { "PageSize": 200 }
}
```

### 1.2. Ghi tệp an toàn

Ghi bằng cách ra tệp tạm `settings.json.tmp` rồi mới `File.Move(overwrite: true)`. Nếu
ứng dụng bị tắt giữa lúc ghi thì tệp cũ vẫn nguyên vẹn thay vì để lại một tệp JSON
nửa vời làm hỏng cấu hình.

Khi đọc: tệp hỏng, tệp không đọc được, hoặc người dùng viết `"Files": null` — đều rơi
về mặc định thay vì làm hỏng ứng dụng. Người dùng sửa tay được nên tệp hỏng là chuyện
### 3.1. Thứ tự tính lại trạng thái khi lưu

Trong `Save()` thứ tự rất quan trọng:

1. Tính `restartHint` **trước** khi làm mới `_saved` — sau khi lưu không còn khác biệt
   nên `NeedsRestart` đã về `false` và lời nhắc sẽ luôn nói *"có hiệu lực ngay"*, sai mất
   ý nghĩa.
2. `_saved = _userSettings.Current.Clone()`.
3. `UpdateDirtyState()` — so với chính bản vừa ghi. Bỏ bước này thì `IsDirty` còn `true`
   và người dùng bấm *Đóng* lại bị hỏi *"bỏ thay đổi chưa lưu"* dù đã lưu xong.

## 4. Kiểm tra kết nối

Nút *Kiểm tra kết nối* không dùng `ISqlConnectionFactory.Create()` mà gọi
`ProbeAsync` — mở thử **chuỗi bất kỳ người dùng đang gõ**, chưa lưu:

```
SELECT @@SERVERNAME, DB_NAME(),
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(60)),
       CAST(SERVERPROPERTY('Edition') AS nvarchar(120))
```

Kết quả trả về `ConnectionProbe` (tên máy chủ, CSDL, phiên bản, edition, thời gian ms)
và hiển thị thành câu: *"Kết nối được `DeltaBat` trên `SERVER` (Developer Edition 16.x)
trong 12 ms."*

Ba điều kiện:

- Chuỗi sai **cú pháp** → báo ngay, **không** mở kết nối (`IsWellFormedConnectionString`
  dựng `SqlConnectionStringBuilder` và bắt `ArgumentException`).
- `ConnectTimeout` và `CommandTimeout` của lần mở thử đặt cứng **15 giây**, không theo
  chuỗi người gõ — bấm nút mà máy chủ chậm thì người dùng tưởng treo.
- Nút bị khoá (`CanExecute = false`) khi đang mở thử hoặc khi ô trống.

## 5. Chặn lưu giá trị sai

`TryValidate` chạy **trước khi ghi**, vì lưu xong sai thì lần khởi động sau ứng dụng
không lên được, mà lúc đó tệp cấu hình đã nằm trên đĩa:

| Trường hợp | Thông báo |
| --- | --- |
| Chuỗi kết nối sai cú pháp | *"Chuỗi kết nối chưa đúng cú pháp nên chưa thể lưu"* kèm ví dụ mẫu |
| Thư mục gốc không tồn tại | *"Thư mục không tồn tại: `<đường dẫn>`"* |

Sau khi lưu thành công cửa sổ tự đóng.

## 6. Đóng cửa sổ và chống hỏi hai lần

`SettingsWindow` chỉ giữ phần việc của cửa sổ: gắn ViewModel, gán `DataContext`, và
nghe sự kiện `Closing` để hỏi *"bỏ thay đổi chưa lưu"* khi người dùng bấm nút **X**.

Nút *Lưu* và *Đóng* đều gọi lệnh trong ViewModel, nên cửa sổ không cần can thiệp.

Cờ `_closeConfirmed` chặn việc hỏi hai lần cho cùng một việc:

```
Bấm nút Đóng
  → Close() → ConfirmClose() → người dùng đồng ý → _closeConfirmed = true
    → CloseRequested → cửa sổ.Close() → sự kiện Closing → ConfirmClose()
      → _closeConfirmed đã true → trả về true ngay, không hỏi lần nữa
```

Nếu không có cờ này, người dùng phải xác nhận hai lần cho cùng một việc.

Cửa sổ mở bằng `ShowDialog()` chứ không phải `Show()` — tránh có hai cửa sổ Cài đặt
cùng lúc, hai bản sẽ ghi đè lẫn nhau và người dùng không biết bản nào mới hơn.

## 7. Nhãn nguồn hiệu lực

Tab *Kết nối* hiện `IAppSettings.ConnectionStringSource`: *"Cấu hình người dùng (Cài
đặt)"*, *"Tệp appsettings.json"* hay *"Biến môi trường …"*.

Nhãn này có mặt vì một sự nhầm rất dễ gặp: người dùng sửa bản ghi đè trong Cài đặt mà
ứng dụng vẫn dùng bản của biến môi trường — và họ không thấy đúng thứ đang có hiệu
lực. Biến môi trường cố ý đứng trên cao nhất vì dành cho môi trường triển khai.

## 8. Cấu trúc mã nguồn

| Tệp | Vai trò |
| --- | --- |
| `Models/Settings/UserPreferences.cs` | Kiểu dữ liệu JSON + `Clone()` sâu |
| `Services/Settings/IUserSettings.cs` | Interface: `FilePath`, `Current`, `GetString`, `Save`, `Reset` |
| `Services/Settings/JsonUserSettings.cs` | Cài đặt: `%APPDATA%\DeltaBat\settings.json`, ghi qua tệp tạm |
| `Services/Settings/UserFirstAppSettings.cs` | Lớp trang trí ghép cấu hình người dùng lên cấu hình kèm theo |
| `Models/Database/ConnectionProbe.cs` | Record kết quả mở thử CSDL |
| `ViewModel/Settings/SettingsViewModel.cs` | Trạng thái 4 nhóm, validate, lệnh Save/Reset/Probe |
| `ViewModel/Settings/SettingsNavItem.cs` | Một mục trong cột điều hướng |
| `Views/SettingsWindow.xaml(.cs)` | Cửa sổ; chỉ gắn ViewModel và hỏi trước khi đóng |
| `Services/IDialogService.cs` | `ShowSettings()` — thêm vào interface |
| `Presentation/WpfDialogService.cs` | Dựng `SettingsWindow` từ `IServiceProvider` |

## 9. Kiểm thử

`tests/WpfApp.Tests` có ba tệp cho phần này:

| Tệp | Phạm vi |
| --- | --- |
| `UserSettingsTests` | Tệp không có → mặc định · lưu rồi đọc lại bằng instance khác · tạo thư mục còn thiếu · không sót tệp `.tmp` · trim giá trị · kẹp cỡ trang · tệp hỏng → mặc định · `"Files": null` → mục rỗng · `Reset` · `GetString` bỏ qua đường dẫn lạ |
| `UserFirstAppSettingsTests` | Không ghi đè → lấy của bộ cài đặt · có ghi đè → thắng · nhãn nguồn trỏ đúng chỗ · giá trị rỗng → lùi · đường dẫn không có ở người dùng → vẫn đọc của bộ cài đặt |
| `SettingsViewModelTests` | Cửa sổ mới hiện giá trị đã lưu và không bẩn · đổi chuỗi/thư mục/cỡ trang đánh dấu đúng trạng thái · đặt lại về giá trị cũ thì sạch · từ chối lưu chuỗi sai/thư mục không tồn tại · lưu xong sạch · lời nhắc khởi động lại đúng lúc · `ConfirmClose` hỏi đúng một lần · khôi phục mặc định · xoá cấu hình sau khi xác nhận · chọn thư mục |

Cả ba tệp dùng bộ thay thế trong `SettingsTestDoubles.cs`, không cần CSDL thật.

## 10. Khắc phục sự cố

| Triệu chứng | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| Lưu xong mà ứng dụng không đổi gì | Đang sửa chuỗi kết nối hoặc cỡ trang | Khởi động lại ứng dụng |
| Nhãn nguồn ghi *Biến môi trường* dù đã ghi đè trong Cài đặt | Biến môi trường đứng trên cao nhất | Bỏ `DELTABAT_CONNECTION_STRING` khỏi máy đó |
| Không xoá được tệp cấu hình | Tệp đang bị khoá | Đóng ứng dụng rồi xoá tay `%APPDATA%\DeltaBat\settings.json` |
| Tệp cấu hình hỏng sau khi sửa tay | JSON sai cú pháp | Ứng dụng vẫn chạy với mặc định; sửa lại tệp hoặc bấm *Xoá toàn bộ cài đặt* |
có thể xảy ra; **mất tuỳ chọn còn hơn là không khởi động được**.

Mọi giá trị đọc vào đều được:

- `Trim()` và coi chuỗi trắng là **không đặt** (`null`) → lùi về cấu hình kèm theo.
- `PageSize` kẹp về `[MinPageSize, MaxPageSize]` của `TablePage`.

## 2. Bốn nhóm tuỳ chọn

| Nhóm | Nội dung |
| --- | --- |
| **Kết nối** | Chuỗi kết nối ghi đè · nút *Xoá chuỗi ghi đè* · nút *Kiểm tra kết nối* · nhãn nguồn đang có hiệu lực |
| **Kho tệp tin** | Thư mục gốc chứa tệp · nút *Chọn thư mục* · dòng nhận xét thư mục có tồn tại không |
| **Bảng dữ liệu** | Cỡ trang mặc định, chọn trong danh sách `25 / 50 / 100 / 200 / 500` (kẹp 5–1000) |
| **Giao diện** | Chọn màu sắc ứng dụng giữa 5 theme, xem trước bằng ô màu |
| **Giới thiệu** | Phiên bản ứng dụng, đường dẫn file cấu hình, nút *Khôi phục mặc định*, *Xoá toàn bộ cài đặt*, *Mở thư mục cấu hình* |

Cửa sổ chỉ hiện tuỳ chọn **thật sự có tác dụng**. Một ô cài đặt mà bấm *Lưu* xong ứng
dụng vẫn không đổi gì thì tệ hơn là không có ô đó.

## 2.1. Giao diện

Năm theme được chia thành **2 theme sáng, 2 theme tối và 1 theme trung tính**. Mỗi
theme là một tệp `ResourceDictionary` riêng trong `Views/Themes/`:

| Nhóm | Theme | Nền | Màu nhấn | Dùng skin |
| --- | --- | --- | --- | --- |
| Tối | **Midnight** (mặc định) | Xanh than đậm | Xanh dương `#4C9AFF` | `SkinDark` |
| Sáng | **Ocean** | Xanh biển nhạt | Xanh ngọc `#087E9B` | `SkinDefault` |
| Trung tính | **Violet** | Tím xám trung độ | Tím `#B69AFF` | `SkinViolet` |
| Sáng | **Daylight** | Trắng | Xanh lam `#1F6FEB` | `SkinDefault` |
| Tối | **Carbon** | Than chì | Xanh VS Code `#3794FF` | `SkinDark` |

Màu nền, chữ, điểm nhấn, trạng thái chọn và icon được phối riêng theo từng bảng màu.
Các trạng thái active dùng cặp màu nền/chữ tương phản; cây tệp còn có màu icon active
riêng để nội dung được chọn vẫn rõ khi chuyển focus sang vùng khác.

**Carbon** lấy nguyên bảng màu VS Code Dark Modern (`#1F1F1F` nền, `#181818` thanh bên,
`#252526` vùng phụ, `#CCCCCC` chữ) cho người quen VS Code không phải điều chỉnh lại mắt.

Thanh tiêu đề cửa sổ (tên cửa sổ + nút Thu nhỏ / Phóng to / Đóng) cũng đổi màu theo
theme như phần còn lại của ứng dụng. Thanh này do ứng dụng tự vẽ
(`Views/Controls/WindowCaptionBar.xaml`) chứ không phải thanh tiêu đề mặc định của
Windows, vốn luôn theo giao diện hệ điều hành và không đổi theo theme ứng dụng.

Đổi theme **có hiệu lực ngay**, không cần khởi động lại — khác với chuỗi kết nối và
cỡ trang vốn phải khởi động lại.

Cơ chế đổi: `AppThemeService.Apply` thay thế hai `ResourceDictionary` trong
`Application.Current.Resources` — skin HandyControl và bảng màu của ứng dụng.
AvalonDock có `DockingManager.Theme` nên `DockWorkspace.RefreshTheme()` dựng lại theme
của khung neo. Chi tiết kỹ thuật: [KienTruc.md §10](KienTruc.md).

## 3. Tuỳ chọn nào áp dụng ngay, tuỳ chọn nào cần khởi động lại

| Tuỳ chọn | Khi nào có hiệu lực | Vì sao |
| --- | --- | --- |
| Thư mục gốc tệp | **Ngay** | `FilePathResolver` đọc `GetString("Files", "RootPath")` mỗi lần dựng đường dẫn, không nạp một lần lúc khởi động. JSON đã nạp sẵn trong bộ nhớ nên chi phí không đáng kể. |
| Giao diện | **Ngay** | Chỉ thay thế `ResourceDictionary` trong `Application.Resources`; WPF tự cập nhật mọi binding `DynamicResource`. |
| Chuỗi kết nối | Khởi động lại | `SqlConnectionFactory` đọc chuỗi khi tạo kết nối; connection pool đã mở theo chuỗi cũ. |
| Cỡ trang mặc định | Khởi động lại | `SessionsTableViewModel` / `ChainsTableViewModel` đọc cỡ trang lúc **dựng đối tượng** trong hàm khởi tạo DI. |

Chân chân `IsDirty` (có thay đổi chưa lưu) và `NeedsRestart` (đã sửa xong nhưng còn phải
khởi động lại mới có tác dụng) là hai trạng thái **khác nhau**:

- Đổi thư mục gốc → `IsDirty = true`, `NeedsRestart = false` → lời nhắc là *"Các thay
  đổi đã có hiệu lực ngay."*
- Đổi chuỗi kết nối → cả hai đều `true` → lời nhắc là *"Chuỗi kết nối và cỡ trang sẽ
  có hiệu lực sau khi khởi động lại ứng dụng."*

Nút *Lưu thay đổi* và nút *Đóng* chỉ bật khi `IsDirty`.