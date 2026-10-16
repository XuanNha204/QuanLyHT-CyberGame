# Kế hoạch hoàn thiện mô hình máy quản trị và máy hội viên

## 1. Mục tiêu và ranh giới

Hệ thống chỉ có hai ứng dụng vận hành, cùng kết nối một `CyberGame.Server` và một cơ sở dữ liệu thật:

- `CyberGame.Client`: máy quản trị/thu ngân. Quản lý máy, phiên chơi, hội viên, ví, yêu cầu nạp, đơn món, sản phẩm, kho, ca làm việc, hóa đơn và doanh thu.
- `CyberGame.MemberClient`: ứng dụng khóa tại từng máy khách. Hội viên đăng nhập, xem thời gian và số dư theo thời gian máy chủ, yêu cầu nạp tiền, gọi món, theo dõi trạng thái đơn, đổi PIN và kết thúc phiên.

Không tạo thêm chức năng ngoài nghiệp vụ phòng máy. Không dùng dữ liệu giả trong các luồng vận hành.

## 2. Thuật ngữ và quyền

- Toàn bộ chữ “Khách hàng” trên giao diện quản trị được đổi thành “Hội viên”. Tên lớp/DTO nội bộ có thể giữ nguyên để tránh thay đổi kỹ thuật không cần thiết.
- Nhân viên đăng nhập `CyberGame.Client` bằng tài khoản nhân sự và vai trò hiện có.
- Hội viên đăng nhập `CyberGame.MemberClient` bằng mã hội viên + PIN 6 số + mã máy được cấu hình tại máy khách.
- JWT nhân viên và JWT hội viên giữ tách biệt; API hội viên không được gọi API quản trị và ngược lại.

## 3. Luồng hội viên

### 3.1 Tạo, đăng nhập và ngừng hoạt động

1. Thu ngân tạo hội viên với mã, họ tên, điện thoại, tiền nạp ban đầu và PIN 6 số bắt buộc.
2. PIN chỉ lưu dạng hash; không trả PIN ra API và không ghi PIN vào log.
3. Sau khi tạo thành công, hội viên đăng nhập được ngay trên máy khách đang khả dụng.
4. Lệnh “Xóa/ngừng hoạt động” là soft-delete bằng trạng thái `Inactive`; không xóa dữ liệu ví, giao dịch, hóa đơn hoặc phiên chơi.
5. Hội viên `Inactive`/`Locked` không đăng nhập được. Quản trị có thể kích hoạt lại bằng chỉnh trạng thái.

### 3.2 Đổi PIN tại máy khách

- Hội viên nhập PIN hiện tại, PIN mới và xác nhận PIN mới.
- PIN mới gồm đúng 6 chữ số, khác PIN hiện tại.
- Server xác minh PIN cũ, hash PIN mới, đặt lại số lần đăng nhập sai và ghi audit không chứa giá trị PIN.

## 4. Luồng nạp tiền

### 4.1 Từ máy khách

1. Hội viên gửi số tiền đề nghị và ghi chú tùy chọn.
2. Server tạo `MemberTopUpRequest` trạng thái `Pending`; chưa thay đổi số dư.
3. Thu ngân thấy yêu cầu thật trong màn hình Hội viên, chọn duyệt hoặc từ chối.
4. Khi duyệt, server cập nhật ví, wallet transaction, payment và cash movement trong cùng giao dịch dữ liệu; yêu cầu chuyển `Approved`.
5. MemberClient tải lại số dư và lịch sử trạng thái từ server.

### 4.2 Nạp trực tiếp tại quầy

- Thu ngân vẫn có thể chọn hội viên và nạp tiền thủ công bằng luồng ví hiện có.
- Mọi nạp tiền mặt yêu cầu ca làm việc đang mở theo quy tắc hệ thống hiện tại.

## 5. Luồng gọi món và duyệt món

1. MemberClient tải danh mục thật từ `Products`, `ProductCategories` và tồn kho hiện hành; hiển thị tên món, nhóm món, giá, đơn vị và trạng thái còn hàng.
2. Khi hội viên đặt món, server kiểm tra phiên đang hoạt động, sản phẩm còn kinh doanh, số lượng hợp lệ và số dư khả dụng sơ bộ.
3. Đơn được tạo ở trạng thái `Draft` (chờ duyệt). Chưa trừ kho, chưa cộng phí dịch vụ.
4. Màn POS quản trị có hàng đợi đơn từ máy khách, hiển thị số đơn, máy, hội viên, thời gian, danh sách tên món và tổng tiền.
5. Khi quản trị duyệt:
   - Server khóa/đọc lại đơn, phiên, ví và tồn kho.
   - Kiểm tra phiên vẫn mở/tạm dừng, ca vẫn mở, tồn kho đủ và số dư còn đủ sau tiền giờ cùng các đơn đã duyệt.
   - Trừ tồn kho, ghi inventory transaction, chuyển đơn sang `Confirmed`, cập nhật phí dịch vụ phiên trong một lần lưu nguyên tử.
6. Quản trị có thể từ chối đơn `Draft` thành `Cancelled` mà không thay đổi kho/số dư.
7. Sau khi giao món, quản trị chuyển `Confirmed` thành `Fulfilled`.
8. MemberClient hiển thị đúng trạng thái: Chờ duyệt, Đã duyệt/đang chuẩn bị, Đã phục vụ, Đã từ chối.
9. Phiên không được kết thúc khi còn đơn `Draft`; người dùng nhận thông báo chờ quản trị duyệt hoặc từ chối. Chỉ đơn `Confirmed`/`Fulfilled` được tính tiền.

## 6. Quản lý món và kho

- Màn Kho có chức năng thêm mặt hàng thật: nhóm món, SKU, tên, đơn vị, giá bán, giá vốn, theo dõi tồn kho, mức cảnh báo và tồn ban đầu.
- Dùng các nhóm món hiện có; không tạo màn hình danh mục dư thừa.
- Mặt hàng mới xuất hiện ngay ở POS và MemberClient sau khi tải lại.
- Nhập kho và điều chỉnh kho tiếp tục dùng các giao dịch kho hiện có.

## 7. Giao diện

### CyberGame.Client

- Menu và tiêu đề dùng “Hội viên”.
- Màn Hội viên: danh sách, tìm kiếm, tạo kèm PIN, nạp tiền, cài lại PIN, ngừng/kích hoạt, lịch sử ví, duyệt/từ chối yêu cầu nạp.
- Màn POS: giữ bán tại quầy và thêm khu vực “Đơn từ máy khách” với làm mới, duyệt, từ chối, đã phục vụ.
- Màn Kho: thêm mặt hàng, nhập kho, điều chỉnh kho; biểu mẫu kiểm tra dữ liệu và thông báo rõ ràng.
- Các màn Máy trạm, Ca, Hóa đơn và Báo cáo doanh thu hiện có được giữ nguyên.

### CyberGame.MemberClient

- Tổng quan thời gian, tiền giờ, tiền dịch vụ và số dư dùng giờ server.
- Gọi món hiển thị tên món thật; không có dữ liệu hard-code/mock.
- Yêu cầu nạp và lịch sử trạng thái tải từ server.
- Thêm khu vực Đổi PIN với ô mật khẩu an toàn và thông báo kết quả.
- Không cho thao tác trùng lặp khi đang gửi; lỗi API phải hiển thị cho người dùng thay vì làm thoát ứng dụng.

## 8. API tối thiểu cần bổ sung/mở rộng

- Tạo hội viên nhận PIN ban đầu và xử lý nguyên tử.
- Cập nhật trạng thái hội viên cho thao tác ngừng/kích hoạt.
- Hội viên đổi PIN.
- Quản trị lấy danh sách đơn theo trạng thái, duyệt, từ chối, hoàn tất phục vụ.
- Quản trị tạo sản phẩm.
- Các endpoint POST quan trọng giữ idempotency để thao tác gửi lại không nhân đôi tiền hoặc đơn.

## 9. Cơ sở dữ liệu

- Tận dụng các bảng và trạng thái hiện có: `Customers`, `Wallets`, `WalletTransactions`, `MemberTopUpRequests`, `Orders`, `OrderItems`, `Products`, `ProductCategories`, `InventoryBalances`, `InventoryTransactions`, `PlaySessions`.
- Không cần thêm bảng mới nếu schema hiện tại đã có PIN hash và yêu cầu nạp.
- Không hard-delete hội viên hoặc giao dịch tài chính.
- Mọi biến động tiền/kho phải có bản ghi đối soát và diễn ra nguyên tử.

## 10. Kiểm thử và tiêu chí hoàn thành

- Build toàn solution không lỗi/cảnh báo.
- Toàn bộ test hiện có tiếp tục qua; bổ sung test cho tạo hội viên kèm PIN, đổi PIN, vô hiệu hóa đăng nhập, đơn `Draft`, duyệt/từ chối đơn và chặn kết thúc phiên khi còn đơn chờ.
- Kiểm tra tài nguyên XAML để không còn lỗi làm ứng dụng thoát.
- Smoke test với SQL Server và API thật theo chuỗi:
  1. Quản trị tạo hội viên kèm PIN.
  2. Hội viên đăng nhập tại máy khách và có phiên chơi.
  3. Gửi yêu cầu nạp; quản trị duyệt; số dư tăng đúng một lần.
  4. Danh mục trả tên món thật; hội viên đặt món `Draft`; kho chưa giảm.
  5. Quản trị duyệt; kho giảm đúng; phí dịch vụ tăng đúng; sau đó đánh dấu đã phục vụ.
  6. Hội viên đổi PIN; PIN cũ thất bại, PIN mới đăng nhập thành công.
  7. Kết thúc phiên và đối soát ví/hóa đơn.
  8. Ngừng hoạt động hội viên; đăng nhập bị từ chối nhưng lịch sử vẫn còn.

## 11. Ngoài phạm vi

- Không xây thêm launcher game, điều khiển tiến trình Windows, kiosk shell, chat, khuyến mãi, tích điểm hoặc thanh toán online.
- Không đổi kiến trúc công nghệ, không tạo database song song và không nhân bản dữ liệu giữa hai ứng dụng.
