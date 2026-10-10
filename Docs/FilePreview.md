# Xem trước tệp

Khung xem trước được ghim cố định ở dưới cửa sổ chính. Chọn một phiên để tự động xem
trước tệp đầu tiên trong danh sách tệp của phiên đó. Nhấp một tệp khác trong cây tệp
hoặc bảng metadata để chuyển preview sang tệp đó. Khung vẫn giữ nguyên vị trí khi
chuyển giữa các phiên; nếu chưa chọn tệp, khung hiển thị hướng dẫn thay vì nội dung cũ.

## Định dạng

| Nhóm | Định dạng nhận diện | Cách hiển thị |
| --- | --- | --- |
| Ảnh | BMP, GIF, ICO, JPEG, PNG, TIFF, WDP | Ảnh được co vừa khung xem trước. |
| Âm thanh | AAC, AIFF, FLAC, M4A, MP3, OGG, OPUS, WAV, WMA và một số biến thể | Phát tự động, có điều khiển phát/tạm dừng/dừng và thanh thời gian. |
| Video | 3GP, AVI, M2TS, M4V, MKV, MOV, MP4, MPEG, MTS, WMV | Phát tự động trong khung, có điều khiển phát/tạm dừng/dừng và thanh thời gian. |
| Văn bản | TXT, CSV, JSON, XML, HTML, Markdown, mã nguồn, cấu hình và log phổ biến | Hiện nội dung dạng văn bản, tối đa 1.000.000 ký tự đầu tiên. |
| Định dạng khác | PDF và các định dạng chưa nhận diện | Hiện loại, kích thước, thời điểm cập nhật; có nút mở bằng ứng dụng mặc định. |

Ảnh, văn bản và phát media sử dụng các API có sẵn trong WPF/.NET, không cần thư viện
trả phí hoặc cài thêm gói bên thứ ba. Âm thanh/video dựa trên codec có trên Windows;
nếu codec không hỗ trợ định dạng hoặc codec bên trong tệp, hãy dùng nút **Mở bằng ứng
dụng mặc định**. PDF hiện chưa có bộ đọc tích hợp.

Đường dẫn được ghép từ thông tin tệp và thư mục gốc cấu hình trong Cài đặt. Nếu tệp
không tồn tại hoặc đường dẫn chưa được cấu hình, cửa sổ sẽ báo trạng thái thay vì cố
đọc tệp.
