# Nghiệp vụ DeltaBat

## 1. DeltaBat làm gì

DeltaBat là hệ thống **thu thập tín hiệu qua vệ tinh**. Các thiết bị thu ở đầu bên kia
vệ tinh bắt tín hiệu (thoại, dữ liệu, fax, tin nhắn, thư điện tử, dữ liệu GPS) rồi
truyền xuống trạm mặt đất. Ứng dụng này là **không gian phân tích** cho dữ liệu đó:
người phân tích tìm kiếm, xem xét và xuất báo cáo.

Bốn khái niệm trung tâm:

```
Chuỗi (Chain)   = một kênh truyền thông cố định: vệ tinh + tần số + modem + thiết bị
Phiên (Session) = một lần thu trong một khoảng thời gian trên chuỗi đó
Tệp   (File)    = một phần dữ liệu thu được trong phiên (một lần gọi, một tệp hình...)
Liên hệ         = đầu mối liên lạc tìm thấy trong phiên (số điện thoại, email, IMSI)
```

Một chuỗi có nhiều phiên. Một phiên có nhiều tệp và nhiều liên hệ.

## 2. Thuật ngữ chuyên ngành

| Thuật ngữ | Ý nghĩa |
| --- | --- |
| **Vệ tinh (SAT)** | Vệ tinh truyền thông, ví dụ `Thuraya`, `Iridium` |
| **Băng tần (Band)** | `Ku`, `Ka`, `C`, `L`, `S` — dải tần vệ tinh dùng |
| **Phân cực (Pol)** | `V` (dọc), `H` (ngang), `RHCP`/`LHCP` (xoay tròn) |
| **Tần số (MHz)** | Tần số làm việc |
| **Điều chế** | `QPSK`, `8PSK`, `QAM`... |
| **Modem** | Thiết bị đầu cuối vệ tinh |
| **EOP** | End-Of-Protocol — dấu hiệu kết thúc một lần thu |
| **DAY_FGR** | Số thứ tự ngày, phân vùng dữ liệu |
| **VAL_XML** | Cột XML chứa metadata mở rộng |
| **Marker** | Điểm đánh dấu (mốc thời gian) trong tệp |

## 3. Vòng đời dữ liệu

```
   [Hệ thống thu]                                        ┌──────────────┐
         │                                                │              │
         ▼                                                │  DeltaBat    │
   Trạm mặt đất nhận tín hiệu                            │  (ghi CSDL)  │
         │                                                │              │
         ▼                                                └──────┬───────┘
   ┌───────────────┐                                            │
   │  Phân tích    │  giải mã, tách kênh                        │
   │  tín hiệu     │                                            ▼
   └───────┬───────┘                              ┌─────────────────────────┐
           │                                      │  admin.CONFIG           │
           ▼                                      │  msg.SESSIONS + FILES   │
   ┌────────────────────┐                         │  info.TYPES_* (từ điển)  │
   │ Ghi vào CSDL:      │────────────────────────▶│  stat.CAPACITY_* (số)   │
   │  phiên, tệp,       │                         │                         │
   │  liên hệ, metadata │                         └─────────────────────────┘
   └────────────────────┘                                          │
                                                                   ▼
   ┌──────────────────────────────────────────────────────────────────────┐
   │                    Ứng dụng phân tích (dự án này)                    │
   │  Chuỗi → Phiên → Tệp → Xem trước / Xuất báo cáo                       │
   └──────────────────────────────────────────────────────────────────────┘
```

## 4. Quy trình phân tích

### 4.1. Bước 1 — Chọn khoảng ngày

Đầu tiên chọn khoảng ngày muốn xem. Đây là điều kiện giới hạn quan trọng nhất: dữ
liệu có thể rất lớn, nên mọi truy vấn đều bắt đầu từ khoảng ngày. Ngày kết thúc
được cộng thêm một ngày thành mốc loại trừ, để bao trọn cả ngày cuối.

Trong tab **Nguồn**, nhập từ khóa sẽ tự tìm sau 1,5 giây ngừng gõ; thay đổi thời điểm
bắt đầu hoặc kết thúc cũng tự lọc bảng sau 1,5 giây. Nút **Đặt lại** khôi phục từ khóa
rỗng và khoảng thời gian mặc định trong ngày hiện tại. Nút **Khai thác** nằm ngay tại
tab này.

Nếu thời điểm bắt đầu lớn hơn thời điểm kết thúc, ứng dụng hiển thị lỗi và không lọc
bảng hoặc khai thác.

### 4.2. Bước 2 — Tick chuỗi

Chuyển sang tab **Nguồn**, xem danh sách nguồn có trong khoảng thời gian. Tick những nguồn
cần phân tích. Có thể tick nhiều nguồn cùng lúc.

> Nếu **không** tick chuỗi nào, bấm "Khai thác" vẫn chạy nhưng chỉ tính thống kê đuôi
> tệp toàn hệ mà không nạp phiên — chế độ này hữu ích khi chỉ muốn xem "trong khoảng
> này có những loại tệp nào".

### 4.3. Bước 3 — Khai thác

Bấm nút **Khai thác**. Ứng dụng:

1. Nạp danh sách phiên của các chuỗi đã tick, trong khoảng ngày.
2. Sự kiện `SessionsReady` báo sang khung tệp để nạp cây tệp.
3. Tính danh mục đuôi tệp cho dải lọc bên trái.
4. Đánh dấu các chuỗi đã khai thác (nút "Khai thác" nổi lên trên thanh trạng thái).

Bấm lại lần nữa trong khi đang chạy sẽ **hủy** lần trước và chạy lại — không phải xếp
hàng chờ. Luôn chỉ có một lần khai thác chạy đồng thời (bảo đảm bằng `SemaphoreSlim`).

### 4.4. Bước 4 — Duyệt phiên và tệp

Chọn một phiên ở tab **Phiên**:

- **Khung phải** hiện danh sách tệp của phiên (đọc từ `VAL_XML`).
- **Khung dưới** hiện khung xem trước khi bấm một tệp.
- **Dải bên trái** hiện danh mục đuôi tệp để lọc.

Xem chi tiết trong [FilePreview.md](FilePreview.md).

### 4.5. Bước 5 — Xuất báo cáo

Chọn các tệp cần xuất rồi bấm **Xuất Word**, chọn thư mục đích. Tạo tài liệu `.docx`
gồm bảng các tệp với 11 cột: STT, Mã, Phiên, Tên file, Đuôi, Dung lượng, Trạng thái,
Chất lượng, Thời gian, Đường dẫn, Tồn tại. Trần 20 000 dòng mỗi lần xuất.


## 5. Bộ lọc đuôi tệp

Dải bên trái liệt kê các đuôi tệp đã biết. Tick một hoặc nhiều đuôi để chỉ xem các
định dạng mình cần — tương đương tính năng lọc "Đuôi file" của Windows Explorer.

Đuôi được chuẩn hoá ở cả hai chiều trước khi so khớp:

| Nhập vào | Chuẩn hoá | Dạng gửi cho SQL |
| --- | --- | --- |
| `wav` | `wav` | `.wav` |
| `.wav` | `wav` | `.wav` |
| `.WAV` | `wav` | `.wav` |
| `D:\data\file.WAV` | `wav` | `.wav` |
| `  wav  ` | `wav` | `.wav` |

Nguyên nhân lỗi lịch sử và cách sửa: xem
[KienTruc.md §7.5](KienTruc.md).

> **Vì sao số lượng tệp hiện là 0?** Đếm từng đuôi phải `GROUP BY` trên
> `msg.FILES` (~23 triệu dòng), mất 70–86 giây. Ứng dụng đọc danh mục đuôi từ cấu hình
> trong bộ nhớ đúng như bản gốc DeltaBat, nên không có số đếm. Thông báo trạng thái
> nói rõ điều này.

## 6. Trạng thái và chất lượng

**Trạng thái phiên** (`info.TYPES_STATUS`):

| Tên | Mô tả |
| --- | --- |
| `NEW` | Mới nhận |
| `PARSED` | Đã phân tích và lập chỉ mục |
| `PROCESSED` | Đã xử lý |
| `READY` | Sẵn sàng cho người dùng |
| `READ` | Người dùng đã đọc |
| `DELETE` | Đánh dấu xoá |

**Chất lượng** (`QUALITY`): số nguyên, hiển thị thành dãy **5 sao**, sao đầu đầy theo
giá trị. ToolTip ghi rõ "Chất lượng 3/5" hoặc "Không ghi chất lượng" khi `NULL`.

**Mức ưu tiên** (`PRIOR`): hiển thị thành badge màu, số càng cao càng ưu tiên.

## 7. Loại dữ liệu phiên

`info.TYPES_DATA` quyết định icon hiển thị: `VOICE` (điện thoại), `DATA` (dữ liệu),
`FAX` (máy fax), `SMS` (tin nhắn), `GPS` (định vị), `MAIL` (thư điện tử).

## 8. Khoảng trống tương lai

| Nhu cầu | Trạng thái |
| --- | --- |
| Trình phát âm thanh/video nhiều định dạng | Đang dùng `MediaElement` của WPF; codec lạ (vd FLV) phải mở bằng trình phát ngoài |
| Đọc tệp 7z/rar thật | Đã dùng SharpCompress, nhưng tệp mẫu hiện chỉ có chữ ký, chưa phải kho nén hợp lệ |
| Số lượng tệp theo từng đuôi | Không có, vì chi phí quét bảng quá lớn |
| Ghi trạng thái `READ` khi người dùng mở phiên | Chưa triển khai — ứng dụng chỉ đọc |
| Bảng Liên hệ | Đã có service và ViewModel, chưa gắn vào bố cục chính |
