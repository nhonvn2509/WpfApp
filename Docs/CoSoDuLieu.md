# Cơ sở dữ liệu DeltaBat

## 1. Tổng quan

| Thuộc tính | Giá trị |
| --- | --- |
| Hệ quản trị | SQL Server 2019 trở lên (hỗ trợ `spatial`, index lọc) |
| Mức tương thích | `130` |
| Mã hoá | UTF-16LE (cả `DeltaBat.sql` và `SeedDeltaBat.sql`) |
| Quy ước | Bảng `UPPER_CASE`, cột `UPPER_CASE`, khóa `PK_<SCHEMA>_<TABLE>` |

Script nằm ở `WpfApp/Scripts/`:

| Tệp | Nội dung |
| --- | --- |
| `DeltaBat.sql` | Toàn bộ DDL: 9 schema, 43 bảng, view, index, thủ tục |
| `SeedDeltaBat.sql` | Dữ liệu mẫu: 25 chuỗi, 500 phiên, 3000 tệp, 1500 liên hệ |

Nạp cả hai:

```powershell
sqlcmd -S (localdb)\MSSQLLocalDB -d DeltaBat -i WpfApp\Scripts\DeltaBat.sql
sqlcmd -S (localdb)\MSSQLLocalDB -d DeltaBat -b -i WpfApp\Scripts\SeedDeltaBat.sql
```

## 2. Mô hình dữ liệu

```
                    ┌──────────────────┐
                    │  main.CHAINS     │  Chuỗi truyền thông
                    │  (vệ tinh, tần   │  1 ────┐
                    │   số, modem)     │        │
                    └──────────────────┘        │ 1:N (ID_TRANSP)
                                              ▼
        ┌──────────────────────────────────────────────────────┐
        │                  msg.SESSIONS                         │  Phiên thu được
        │  ID_S · NAME · DT_START/FINAL · DURATION · QUALITY   │
        │  ID_TRANSP · ID_STATUS · ID_TYPE · ID_CRYP · VAL_XML  │
        └───────┬──────────────────────────┬───────────────────┘
                │ 1:N (ID_S)                │ 1:N (ID_S)
                ▼                           ▼
   ┌────────────────────────┐   ┌──────────────────────────────┐
   │    msg.FILES           │   │     msg.CONTACTS             │
   │  Tệp thu được          │   │  Đầu mối liên lạc            │
   │  SHORT_NAME · EXT ·    │   │  NAME · DIR · VAL · ID_TYPE  │
   │  LEN · MD5 · VAL_XML   │   └──────────────────────────────┘
   └───────┬────────────────┘
           │ 1:N (ID_F)
           ▼
   ┌────────────────────────┐
   │   msg.MARKERS          │  Điểm đánh dấu trong tệp
   │  MARK_OFFSET · CHANNEL │  (mốc thời gian, tần số)
   └────────────────────────┘
```

Quan hệ không khai báo bằng ràng buộc `FOREIGN KEY` — DeltaBat thiết kế theo kiểu
"bảng phẳng hiệu năng cao", các cột `ID_*` chỉ là số tham chiếu thuần. Ứng dụng vì
vậy phải tự `JOIN` và tự chịu trách nhiệm về tính nhất quán.

## 3. Schema `main` — danh mục nghiệp vụ

| Bảng | Ý nghĩa |
| --- | --- |
| `main.CHAINS` | Chuỗi truyền thông |
| `main.REMARKS` | Ghi chú gắn với đối tượng khác |
| `main.SUMMARY_INFO` | Thông tin tóm tắt phiên |

### 3.1. `main.CHAINS` — bảng trung tâm

Mỗi dòng là một chuỗi thu thập: vệ tinh, tần số, modem, thiết bị.

| Cột | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `ID` | `int` IDENTITY | Khoá, tăng dần |
| `PROFILE` | `varchar(64)` | Tên hồ sơ chuỗi |
| `FRQ_GUID` | `uniqueidentifier` | Định danh tần số |
| `SAT_NAME` | `varchar(64)` | Tên vệ tinh |
| `SAT_BAND` | `nvarchar(16)` | Băng tần (Ku, Ka, C...) |
| `SAT_POL` | `nvarchar(16)` | Phân cực (V, H, RHCP, LHCP) |
| `FREQUNCY_MHZ` | `nvarchar(32)` | Tần số MHz — kiểu **string** vì có thể chứa cả lưới tần số |
| `MODULATION` | `varchar(16)` | Điều chế (QPSK, 8PSK...) |
| `EQUIPMENT` | `varchar(128)` | Thiết bị thu |
| `DIR_FROM` / `DIR_TO` | `varchar(16)` | Hướng truyền từ / đến (xem `info.TYPES_DIR`) |
| `DT_INS` / `DT_UPD` | `datetime` | Mốc thêm / cập nhật |
| `CNT_SS` | `int` | Số phiên của chuỗi |
| `DESCRIPTION` | `varchar(256)` | Mô tả |
| `SRC_NAME` | `nvarchar(64)` | Nguồn thu |
| `MODEM` | `nvarchar(64)` | Modem |

> `FREQUNCY_MHZ` sai chính tả so với `FREQUENCY_MHZ` nhưng đây là tên cột gốc của
> DeltaBat — phải giữ nguyên, không đổi.


## 4. Schema `msg` — dữ liệu thu được

### 4.1. `msg.SESSIONS` — phiên thu được

Một phiên = một lần thu trong một khoảng thời gian trên một chuỗi.

| Cột | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `ID_S` | `bigint` IDENTITY | Khoá |
| `GUID` | `uniqueidentifier` | Định danh phiên |
| `NAME` | `varchar(32)` | Tên phiên |
| `DT_START` / `DT_FINAL` | `datetime` | Bắt đầu / kết thúc |
| `DURATION` | `varchar(12)` | Thời lượng — **string**, không phải số |
| `PRIOR` | `int` | Mức ưu tiên |
| `ID_DIR` | `int` | → `info.TYPES_DIR` |
| `ID_CHANNEL` | `int` | Kênh |
| `ID_TRANSP` | `int` | → `main.CHAINS.ID` |
| `ID_STATUS` | `int` | → `info.TYPES_STATUS` |
| `ID_TYPE` | `int` | → `info.TYPES_DATA` |
| `ID_SAVE` | `int` | Cờ lưu trữ |
| `ID_LANG` | `int` | Ngôn ngữ |
| `ID_CRYP` | `int` | → `info.TYPES_CRYPT` |
| `QUALITY` | `int` | Chất lượng 1–5 → dãy sao |
| `N_FILES` | `int` | Số tệp của phiên |
| `ID_STOR` | `smallint` | Nơi lưu trữ |
| `ID_PLACE` | `tinyint` | Vị trí thu |
| `MEMO` | `nvarchar(256)` | Ghi chú |
| `VAL_XML` | `xml` | **Metadata mở rộng** (xem mục 6) |
| `EOP` | `bit` | Cờ End-Of-Protocol |
| `DAY_FGR` | `tinyint` | Ngày dữ liệu — một phần của khoá |
| `ROW_VER` | `timestamp` | Đồng bộ lạc quan |

Khoá chính: `(ID_S DESC, DAY_FGR DESC)` — chiều giảm dần để câu `ORDER BY ID_S DESC
FETCH NEXT` lấy được bản ghi mới nhất mà không phải sắp xếp.

> **`DAY_FGR` là số thứ tự ngày, không phải kiểu ngày.** Trong dữ liệu mẫu nó bằng
> `DAY(DT_START)`. Giá trị này tham gia khoá để phân vùng dữ liệu theo ngày.

### 4.2. `msg.FILES` — tệp thu được

| Cột | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `ID_F` | `bigint` IDENTITY | Khoá |
| `ID_S` | `bigint` | → `msg.SESSIONS.ID_S` |
| `DT_START` | `datetime` | Bắt đầu ghi |
| `FN` / `FP` | `int` | Số thứ tự tệp / vị trí trong khung |
| `PATH_NAME` | `varchar(256)` | Đường dẫn đầy đủ |
| `SHORT_NAME` | `varchar(256)` | Tên tệp không đuôi |
| `EXT` | `varchar(16)` | **Đuôi tệp, KÈM dấu chấm** |
| `LEN` | `bigint` | Kích thước byte |
| `PRIOR` | `int` | Ưu tiên |
| `USER_SEL` | `tinyint` | Người dùng đánh dấu |
| `ID_STATUS` / `ID_TYPE` / `ID_SAVE` / `ID_LANG` | `int` | → bảng `info.TYPES_*` |
| `QUALITY` | `int` | Chất lượng |
| `ID_STOR` | `smallint` | Nơi lưu trữ |
| `IS_EXISTS` | `bit` | Tệp còn trên đĩa |
| `MD5` | `uniqueidentifier` | MD5 dạng GUID |
| `VAL_XML` | `xml` | Metadata của tệp |
| `DAY_FGR` | `tinyint` | Ngày dữ liệu |

> **Quy tắc quan trọng — `EXT` có dấu chấm.** Giá trị trong CSDL là `.wav`, `.dta`,
> không phải `wav`. Mọi điều kiện SQL phải dùng dạng có dấu chấm, còn lọc tại chỗ
> dùng dạng trần. Xem [KienTruc.md §7.5](KienTruc.md).

Khoá chính: `(ID_F DESC, DAY_FGR DESC)`. Bảng còn có các index phụ trợ:

| Index | Cột | Dùng để |
| --- | --- | --- |
| `IX_FS_ID_S` | `DAY_FGR, ID_S` | Tìm tệp theo phiên trong một vùng ngày |
| `IX_FS_EXT` | `ID_S, DAY_FGR, EXT` | Tìm tệp theo phiên kèm lọc đuôi |
| `IX_FS_EXT_TYPE` | `ID_S, DAY_FGR, EXT, ID_TYPE` | Tìm theo phiên, đuôi và loại tệp |
| `IX_FS_DT_START` | `DAY_FGR, ID_F, DT_START` | Lọc theo khoảng thời gian của tệp |
| `IX_FS_FULLNAME` | theo tên đầy đủ | Tìm theo tên tệp |
| `IX_FS_STOR_ID` | `DAY_FGR, ID_STOR` | Theo nơi lưu trữ |

> **Điểm mấu chốt khi viết truy vấn:** các index này đều đặt `DAY_FGR` ở **cột đầu**
> (trừ `IX_FS_EXT` và `IX_FS_EXT_TYPE` đặt `ID_S` trước, `DAY_FGR` ở giữa). Chỉ lọc
> `ID_S` mà không khoá `DAY_FGR` sẽ khiến SQL Server không dùng được index phần lớn, rơi
> về quét cả bảng. Xem [KienTruc.md §7](KienTruc.md) và `DayFgrScope`.

### 4.3. `msg.CONTACTS` — đầu mối liên lạc

| Cột | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `ID_C` | `bigint` IDENTITY | Khoá |
| `ID_S` | `bigint` | → `msg.SESSIONS.ID_S` |
| `ID_F` | `bigint` | → `msg.FILES.ID_F` (NULL nếu ở mức phiên) |
| `DT_START` | `datetime` | Thời điểm |
| `ID_TYPE` | `int` | → `info.TYPES_CONTACT` |
| `NAME` | `nvarchar(32)` | Tên hiển thị |
| `DIR` | `char(4)` | Hướng: `FROM` / `TO` |
| `PRIOR` | `int` | Ưu tiên |
| `VAL` | `nvarchar(128)` | **Giá trị liên lạc** (số điện thoại, email, IMSI) |
| `REM` | `nvarchar(512)` | Ghi chú |
| `VAL_XML` | `xml` | Metadata |

### 4.4. `msg.MARKERS` — điểm đánh dấu

Đánh dấu một vị trí trong tệp — dùng để ghim mốc thời gian hoặc tần số.

| Cột | Ý nghĩa |
| --- | --- |
| `ID_S` / `ID_F` / `ID_C` | Tham chiếu tới phiên, tệp, liên hệ |
| `MARK_TYPE` | `tinyint` — loại đánh dấu |
| `MARK_OFFSET` | `bigint` — vị trí byte trong tệp |
| `MARK_DT` | `datetime` — mốc thời gian |
| `CHANNEL` | `int` — kênh |
| `FRQ_GUID` | Định danh tần số |

### 4.5. Các bảng phụ trợ của `msg`

| Bảng | Nội dung |
| --- | --- |
| `msg.EXPORT` | Lịch sử các lần xuất dữ liệu |
| `msg.FILTERS_STR` | Bộ lọc dạng chuỗi đã áp dụng |
| `msg.FILTERS_INT` | Bộ lọc dạng số |
| `msg.FILTERS_GEO` | Bộ lọc theo vị trí địa lý |
| `msg.TASKS` | Nhiệm vụ nền |


## 5. Các schema còn lại

### 5.1. `info` — từ điển

| Bảng | Nội dung |
| --- | --- |
| `info.TYPES_DATA` | Loại dữ liệu phiên |
| `info.TYPES_STATUS` | Trạng thái phiên |
| `info.TYPES_DIR` | Hướng kênh |
| `info.TYPES_CRYPT` | Phương pháp mã hoá |
| `info.TYPES_CONTACT` | Kiểu đầu mối liên lạc |
| `info.TYPES_FILE` | Nhóm tệp và danh mục đuôi đăng ký |
| `info.TYPES_FILTER` | Loại bộ lọc |
| `info.FILTERS` | Danh mục bộ lọc |
| `info.COUNTRIES` | Danh mục quốc gia |
| `info.DOCUMENTS` | Danh mục tài liệu |
| `info.SUMMARY` | Bảng tóm tắt |

Các bảng `TYPES_*` có `ID` bắt đầu từ **0** (khác `TYPES_FILE` bắt đầu từ 1) — vì
vậy `ID = 0` là một giá trị hợp lệ, không phải "chưa gán". Cần phân biệt `NULL` với `0`.

**`info.TYPES_DATA`** (loại dữ liệu):

| ID | NAME | Mô tả |
| --- | --- | --- |
| 0 | `VOICE` | Lưu lượng thoại / điện thoại |
| 1 | `DATA` | Lưu lượng dữ liệu |
| 2 | `FAX` | Lưu lượng fax |
| 3 | `SMS` | Tin nhắn ngắn |
| 4 | `GPS` | Dữ liệu vệ tinh định vị |
| 5 | `MAIL` | Thư điện tử |

**`info.TYPES_STATUS`** (trạng thái):

| ID | NAME | Mô tả |
| --- | --- | --- |
| 0 | `NEW` | Mới nhận |
| 1 | `PARSED` | Đã phân tích và lập chỉ mục |
| 2 | `PROCESSED` | Đã xử lý |
| 3 | `READY` | Sẵn sàng cho người dùng |
| 4 | `READ` | Người dùng đã đọc |
| 5 | `DELETE` | Đánh dấu xoá |

**`info.TYPES_FILE`** — cây nhóm tệp, 8 nhóm gốc (`PARENTID = -1`):

| ID | Nhóm | Đuôi đăng ký |
| --- | --- | --- |
| 1 | `DOCUMENT` | pdf, docx, doc, xlsx, xls, pptx, odt |
| 2 | `IMAGE` | jpg, png, gif, bmp, tif |
| 3 | `AUDIO` | wav, mp3, ogg, amr |
| 4 | `VIDEO` | mp4, avi, mkv |
| 5 | `ARCHIVE` | zip, rar, 7z, tar, gz |
| 6 | `TEXT` | txt, log, csv, xml, json |
| 7 | `DATA` | dat, bin, hex |
| 8 | `OTHER` | (không khai báo đuôi) |

Đuôi con được `INSERT` với `PARENTID` trỏ về nhóm, `IDENTITY` tự tăng.

### 5.2. `stor` — lưu trữ

| Bảng | Nội dung |
| --- | --- |
| `stor.LIST_FS` | Danh sách hệ thống tệp / ổ đĩa lưu trữ |
| `stor.LIST_DAYS` | Danh sách ngày có dữ liệu |
| `stor.LOG` | Nhật ký thao tác lưu trữ |
| `stor.CLEANER_SS` | Bộ dọn dẹp phiên |
| `stor.CLEANER_BD` | Dọn dữ liệu lõi |
| `stor.CLEANER_DB` | Dọn CSDL |
| `stor.CLEANER_UP` | Dọn tệp tải lên |
| `stor.CLEANERS` | Danh mục bộ dọn |

`stor.LIST_FS` cấu hình thư mục gốc (`WCF_CONT`, `NDF`, `FS_LOG`, `LOG`) — đây là nơi
ứng dụng dựng đường dẫn tệp thật từ `SHORT_NAME` + `EXT`.

### 5.3. `admin` — quản trị

| Bảng | Nội dung |
| --- | --- |
| `admin.CONFIG` | Tham số cấu hình (4 kiểu giá trị) |
| `admin.ERRORS` | Nhật ký lỗi |
| `admin.INDEXES` | Chỉ mục đã đăng ký |
| `admin.LOG` | Nhật ký hệ thống |
| `admin.PROC_THREAD` | Tiến trình chạy nền |

`admin.CONFIG` có cột đa kiểu: `VAL_STR`, `VAL_INT`, `VAL_XML`, `VAL_DT`, kèm `CAPTION`
(mô tả tiếng Việt) và `NAME` (khoá). Số tham số được đếm khi khởi động và hiện trên
dòng trạng thái.

### 5.4. `app` — ứng dụng

| Bảng | Nội dung |
| --- | --- |
| `app.USERS` | Người dùng (`USER_NAME`, `HOST_NAME`, `CFG_XML`) |
| `app.FAVORITE` | Mục yêu thích |
| `app.LOG` | Nhật ký người dùng |
| `app.QUERY` | Truy vấn đã lưu |
| `app.SELECTION` | Lựa chọn đang lưu |

### 5.5. `stat` — thống kê

| Bảng | Nội dung |
| --- | --- |
| `stat.CAPACITY_ARH` | Dung lượng theo ngày (chi tiết) |
| `stat.CAPACITY_DAY` | Dung lượng gộp theo ngày |

`stat.CAPACITY_ARH` là bảng thống kê giàu thông tin — theo từng ngày lưu số phiên, số
tệp, phân bố ưu tiên và **dung lượng theo từng loại dữ liệu**:

| Nhóm cột | Ý nghĩa |
| --- | --- |
| `CNT_SS`, `CNT_FF` | Số phiên, số tệp |
| `PRIOR_M1`, `PRIOR_LO`, `PRIOR_HI` | Phân bố ưu tiên |
| `CNT_VOICE` … `CNT_GPS` | Số lượng theo loại dữ liệu |
| `SIZE_MB_TOTAL`, `SIZE_MB_VOICE` … `SIZE_MB_GPS` | Dung lượng (MB) theo loại |
| `LAST_ID_S`, `LAST_ID_F` | ID lớn nhất đã ghi — dùng để theo dõi tiến độ thu |

### 5.6. `jobs` — tác vụ

| Bảng | Nội dung |
| --- | --- |
| `jobs.STATUS` | Trạng thái tác vụ |
| `jobs.LOG` | Nhật ký tác vụ |

## 6. Cột `VAL_XML` — metadata mở rộng

Hai bảng `msg.SESSIONS` và `msg.FILES` có cột kiểu `xml` chứa metadata mà không có cột
tương ứng trong bảng. Cấu trúc do DeltaBat ghi ra:

```xml
<delta_hdr>
  <session>
    <sources>...</sources>
    <contacts>...</contacts>
    <geolocation>...</geolocation>
    <filters>...</filters>
    <files>...</files>
  </session>
  <parser>...</parser>
  <inp_hdr>...</inp_hdr>
</delta_hdr>
```

`SessionMetadataParser` cắt XML thành nhiều bảng nhỏ. `sources`, `contacts` và `geolocation`
được giữ làm nhóm riêng; các thuộc tính và phần tử con được trải thành cột/dòng để
không mất tên trường khi XML lồng nhau. Trong khung chi tiết phiên, tab **Liên hệ**
hiển thị `contacts`; tab **Vị trí** còn hiển thị bản đồ Mapsui với
các điểm có cặp vĩ độ/kinh độ hợp lệ. Bản đồ nền được đọc hoàn toàn từ tệp MBTiles cục bộ,
không gửi yêu cầu mạng. Bản đồ mở rộng được lưu trong
`WpfApp/Assets/Maps/vietnam-south-china-sea-shortbread-1.0.mbtiles`; ứng dụng chỉ tìm bản đồ
này trong source tree và không đọc bản đồ từ thư mục dữ liệu người dùng hay đường dẫn cấu hình.
Tệp MBTiles dung lượng lớn này chỉ được lưu cục bộ, không đưa lên GitHub; cần giữ tệp tại
đường dẫn trên để sử dụng bản đồ offline.
Tệp mở rộng được ghép từ bản đồ Việt Nam của Geofabrik
và các gói MBTiles Shortbread vùng biển do BBBike tạo. Các ô bổ sung bao phủ thêm
vùng biển phía nam Việt Nam và phía tây Biển Đông, gồm một phần khu vực Trường Sa;
việc mở rộng được chia thành nhiều ô theo giới hạn dung lượng mỗi gói, nên chưa bao
phủ toàn bộ vùng biển Đông. Bounds tổng hợp là 99°Đ–122°Đ, 0°–23.4021°B; đây là giới hạn
ngoài cùng, không có nghĩa mọi vị trí bên trong đều có tile. Khi trùng tọa độ tile,
bản Geofabrik Việt Nam được giữ ưu tiên. Dữ liệu OpenStreetMap theo ODbL; cần giữ dòng
ghi công trên bản đồ. Tab Vị trí chỉ đặt marker khi ô vector tile ở mức zoom cao nhất
có trong MBTiles; vị trí ngoài vùng dữ liệu tile được thông báo và không vẽ marker.
Nhãn bản đồ dùng trường `name_en` với font Arial để tránh glyph không hỗ trợ của một số
ngôn ngữ địa phương bị hiện thành ô vuông; đối tượng không có tên tiếng Anh có thể không có nhãn.
Khi tệp không có trong source tree hoặc không hợp lệ, khung bản đồ hiện thông báo.

| Nhóm | Trạng thái |
| --- | --- |
| `sources` | Hiển thị |
| `contacts` | Hiển thị |
| `geolocation` | Hiển thị |
| `session` | Ẩn — bảng Phiên bên trái đã hiện |
| `filters` | Ẩn — dải bộ lọc bên trái đã hiện |
| `parser` | Ẩn — thông tin kỹ thuật của ứng dụng, không phải dữ liệu thu |
| `inp_hdr` | Ẩn — lý do tương tự |

Cột bị ẩn: `path` (trùng với tên tệp đã hiện ở cây tệp) và `q` (chất lượng đã hiện
dạng sao). Riêng bảng "Tệp" ẩn thêm `type` vì loại tệp đã thấy qua đuôi và icon.

Trần 1000 dòng mỗi phần để XML bất thường không làm treo lưới.

`SessionFileTreeParser` lấy danh sách tệp từ đây: chỉ nhận **phần tử lá** có tên
`file`/`files`/`item`/`record`/`entry`. Bỏ qua phần tử lá là vì thẻ chứa danh sách
`<files>` trùng tên nên trước đây bị tính nhầm là một tệp, sinh dòng "(không tên)"
ở đầu mỗi phiên.


## 7. Dữ liệu mẫu (`SeedDeltaBat.sql`)

Script tạo dữ liệu mẫu **lặp lại được**: mỗi lần chạy xoá dữ liệu do chính nó sinh
rồi tạo lại với cùng bộ định danh, nên kết quả luôn giống nhau.

| Thành phần | Số lượng | Ghi chú |
| --- | --- | --- |
| Chuỗi `main.CHAINS` | 25 | 01–10/10/2026, mỗi ngày 1–3 chuỗi |
| Phiên `msg.SESSIONS` | 500 | 20 phiên mỗi chuỗi, `DAY_FGR = DAY(DT_START)` |
| Tệp `msg.FILES` | 3000 | 6 tệp mỗi phiên, 32 đuôi |
| Liên hệ `msg.CONTACTS` | 1500 | 3 mỗi phiên: `TERMINAL FROM` / `TERMINAL TO` / `MAIL` |
| Bộ lọc `msg.FILTERS_*` | 500 mỗi bảng | `_STR`, `_INT`, `_GEO` |
| `VAL_XML` | Mọi phiên và mọi tệp | `delta_hdr / session / sources, contacts, geolocation, filters, files` + `parser` + `inp_hdr` |
| `stor.LIST_DAYS`, `stat.CAPACITY_DAY` | Dựng lại | Từ dữ liệu vừa sinh |

Các bảng từ điển (`info.TYPES_*`, `info.COUNTRIES`, `info.FILTERS`, `admin.CONFIG`,
`jobs.STATUS`, `stor.LIST_FS`, `app.USERS`) được nạp đúng giá trị của CSDL tham chiếu
`database95`.

> `DELETE` không đặt lại bộ đếm `IDENTITY`, nên script dùng `DBCC CHECKIDENT` để
> đảm bảo chuỗi 1..25, phiên 1..500, tệp 1..3000, liên hệ 1..1500.

> Yêu cầu `QUOTED_IDENTIFIER ON` — nhiều bảng có index lọc và index không gian, và có
> cột XML nên `INSERT`/`DELETE` cùng phương thức XML đều cần thiết lập này.

## 8. Hiệu năng

### 8.1. Bức tranh tổng thể

`msg.FILES` là bảng lớn nhất: **~23 triệu dòng**. Bảng có clustered PK
`(ID_F, DAY_FGR)` cùng 6 index phụ trợ, nhưng **phần lớn đặt `DAY_FGR` ở cột đầu**,
nên câu truy vấn chỉ lọc `ID_S` sẽ không dùng được index và rơi về quét cả bảng.

Điều này quyết định kiến trúc truy vấn: **luôn khoá vùng ngày** bằng
`DAY_FGR IN (...)` (`DayFgrScope` trong mã nguồn), rồi mới lọc theo `ID_S` hay đuôi.

Đã đo và xác nhận bằng kế hoạch thực thi thật (`SET STATISTICS XML ON`):

| Câu truy vấn | Index được dùng |
| --- | --- |
| Tệp của phiên + khoá ngày | `IX_FS_EXT`, `PK_MSG_FILES_TBL` |
| Tệp theo chuỗi (EXISTS) | `IX_FS_EXT_TYPE`, `IX_FS_DT_START`, `IX_SS_IDTRANSP` |
| Đếm tệp của phiên | `IX_FS_EXT`, `IX_FS_DT_START` |

### 8.2. Những câu **không được** chạy

| Câu | Thời gian đo | Vì sao không dùng |
| --- | --- | --- |
| `SELECT EXT, COUNT_BIG(*) FROM msg.FILES GROUP BY EXT` | 70–86 giây | Quét toàn bảng |
| `SELECT COUNT(*) FROM msg.FILES` | hàng chục giây | Quét toàn bảng |

Thay vào đó:

- Số đếm dòng lấy từ `sys.dm_db_partition_stats` (tức thì).
- Danh mục đuôi đọc từ danh mục tĩnh trong bộ nhớ.

### 8.3. Thói quen nên giữ khi viết truy vấn

- Luôn có điều kiện khoá dẫn tới PK hoặc ít nhất thu hẹp bằng `ID_S`.
- Không dùng hàm lên cột trong `WHERE` (vd `DATE(f.DT_START) = ...`) — làm mất khả năng
  dùng index; luôn so sánh với khoảng thời gian.
- Giữ `WITH (NOLOCK)` để không chặn tiến trình ghi.
- Dùng `OPTION (RECOMPILE)` khi câu thay đổi theo bộ lọc — tránh kế hoạch cache
  sai.
- Có `MAX` trên số dòng khi hiển thị (`MaxFilesPerSession = 1000`).

## 9. Câu hỏi thường gặp

**Sao lọc đuôi file không ra kết quả?**
Gần như luôn do lẫn dấu chấm. Cột `f.EXT` lưu `.wav`; nếu truyền `wav` vào SQL sẽ
sinh `IN ('wav')` và không khớp gì. Kiểm tra giá trị thật:

```sql
SELECT TOP 10 ID_F, EXT FROM msg.FILES ORDER BY ID_F DESC;
```

**Sao phiên có tệp nhưng khung cây tệp trống?**
Cột `VAL_XML` của phiên bị `NULL` hoặc rỗng, hoặc tệp nằm ở `DAY_FGR` khác. Vì
`FilesSqlQueryBuilder` cố ý **không** lọc `DAY_FGR`, hãy kiểm tra:

```sql
SELECT s.ID_S, s.DAY_FGR AS SessionDay, f.DAY_FGR AS FileDay, COUNT(*) AS Files
FROM msg.SESSIONS AS s
JOIN msg.FILES AS f ON f.ID_S = s.ID_S
WHERE s.ID_S = <ID_S>
GROUP BY s.DAY_FGR, f.DAY_FGR;
```

**`ID = 0` trong `info.TYPES_*` nghĩa là gì?**
Là giá trị hợp lệ (`VOICE`, `NEW`, `NONE`...), vì các bảng từ điển `IDENTITY(0,1)`.
Chỉ `info.TYPES_FILE` bắt đầu từ 1. Phải phân biệt `NULL` với `0`.

**Vì sao `FREQUNCY_MHZ` là chuỗi?**
Vì một chuỗi có thể biểu diễn cả lưới tần số, không chỉ một giá trị đơn. Đây là quy
định của bản gốc DeltaBat, giữ nguyên.

**Có thể thêm index cho `msg.FILES` không?**
Về mặt kỹ thuật được, nhưng bảng này do hệ thống thu thập ghi liên tục; thêm index
làm chậm ghi và tăng rủi ro. Ưu tiên lọc qua `ID_S` trước.

## 10. Xem nhanh cấu trúc

```sql
-- Danh sách bảng theo schema
SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
ORDER BY TABLE_SCHEMA, TABLE_NAME;

-- Cột của một bảng
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'msg' AND TABLE_NAME = 'FILES'
ORDER BY ORDINAL_POSITION;

-- Index của một bảng
SELECT i.name, i.type_desc, c.name AS ColumnName, ic.key_ordinal
FROM sys.indexes AS i
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = i.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID('msg.FILES')
ORDER BY i.index_id, ic.key_ordinal;
```
