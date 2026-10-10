# Tài liệu dự án DeltaBat (WPF)

Bộ tài liệu mô tả toàn bộ dự án: nghiệp vụ, cơ sở dữ liệu, kiến trúc, mã nguồn,
vận hành và bảo trì.

| Tài liệu | Nội dung |
| --- | --- |
| [README.md](README.md) | Mục lục và hướng dẫn đọc |
| [KienTruc.md](KienTruc.md) | Kiến trúc phần mềm, lớp, luồng dữ liệu, quyết định thiết kế (gồm §10 về hệ thống giao diện) |
| [NghiepVu.md](NghiepVu.md) | Nghiệp vụ DeltaBat: chuỗi, phiên, tệp, khai thác, xuất báo cáo |
| [CoSoDuLieu.md](CoSoDuLieu.md) | Schema SQL Server: 9 schema, 43 bảng, chỉ mục, câu hỏi thường gặp |
| [MaNguon.md](MaNguon.md) | Tham chiếu từng lớp theo vai trò, quy ước đặt tên |
| [FilePreview.md](FilePreview.md) | Khung xem trước tệp: panel theo định dạng, thư viện dùng |
| [CaiDat.md](CaiDat.md) | Cửa sổ Cài đặt: cấu hình người dùng, kiểm tra kết nối, tuỳ chọn nào cần khởi động lại |
| [VanHanh.md](VanHanh.md) | Cài đặt, cấu hình, hiệu năng, xử lý sự cố |
| [KiemThu.md](KiemThu.md) | Bộ kiểm thử tự động, cách chạy, phạm vi kiểm tra |

## Mô tả ngắn

DeltaBat là **không gian phân tích dữ liệu** cho hệ thống thu thập tín hiệu qua vệ tinh.
Dữ liệu thô do các thiết bị thu gửi về được lưu trong SQL Server theo mô hình:

```
Chuỗi truyền thông  →  Phiên thu được  →  Tệp thu được
   (main.CHAINS)        (msg.SESSIONS)      (msg.FILES)
                              │
                              └── VAL_XML: metadata mở rộng (XML)
```

Ứng dụng cho phép người phân tích:

1. **Xem danh sách chuỗi** (vệ tinh, tần số, modem) và tick những chuỗi cần khai thác.
2. **Khai thác**: nạp toàn bộ phiên thu được trong khoảng ngày đã chọn, gom theo chuỗi.
3. **Duyệt phiên**: xem metadata mở rộng, duyệt cây tệp, xem trước nội dung tệp.
4. **Xuất báo cáo** ra Word để gửi báo cáo.

## Công nghệ và ràng buộc

- Nền tảng: **.NET 8**, WPF, đóng gói **self-contained win-x64** (chạy được máy
  không cài .NET).
- Cơ sở dữ liệu: **SQL Server 2019+** (LocalDB khi phát triển).
- Bảng `msg.FILES` có ~23 triệu dòng; các index của nó đều dựng trên `DAY_FGR`, nên mọi
  truy vấn phải khoá vùng ngày để tránh quét toàn bảng (xem [CoSoDuLieu.md](CoSoDuLieu.md)).
- Ngôn ngữ và giao diện: **tiếng Việt**, culture `vi-VN` cố định.
