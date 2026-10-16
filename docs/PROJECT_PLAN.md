# Kế hoạch tổng thể — CyberGame Manager

> Phần mở rộng máy khách hội viên được đặc tả tại `docs/MEMBER_CLIENT_SIMULATOR_PLAN.md` và phải tuân thủ nguyên tắc không tự mở phiên, không tự cộng tiền, dùng chung dữ liệu/API thật.

## 1. Mục tiêu sản phẩm

CyberGame Manager là hệ thống vận hành quán game theo mô hình client–server. Ứng dụng WPF dành cho chủ quán, quản lý, thu ngân và kỹ thuật viên; ASP.NET Core Web API là nguồn dữ liệu trung tâm. Phiên bản đầu phải vận hành được một chi nhánh, nhưng mô hình dữ liệu hỗ trợ nhiều chi nhánh ngay từ đầu.

Mục tiêu nghiệp vụ:

- Theo dõi trạng thái máy theo thời gian gần thực: trống, đang dùng, tạm khóa, bảo trì, ngoại tuyến.
- Mở, tạm dừng, tiếp tục và kết thúc phiên chơi; tính tiền theo bảng giá có hiệu lực và lưu ảnh chụp đơn giá tại thời điểm sử dụng.
- Quản lý hội viên, số dư, nạp/điều chỉnh tiền và lịch sử giao dịch bất biến.
- Bán đồ ăn, nước uống, dịch vụ; quản lý tồn kho và hóa đơn hợp nhất tiền máy với tiền dịch vụ.
- Quản lý ca thu ngân, dòng tiền, chi phí và đối soát cuối ca.
- Quản lý thiết bị, lịch sử bảo trì, tài khoản nhân viên và nhật ký kiểm toán.
- Cung cấp dashboard và báo cáo doanh thu, công suất máy, tồn kho, khách hàng.

## 2. Phạm vi phát hành

### MVP bắt buộc

1. Xác thực JWT, refresh token, khóa tài khoản, phân quyền theo vai trò.
2. Chi nhánh, khu máy, máy trạm, trạng thái máy và giá giờ chơi.
3. Khách hàng/hội viên, ví, nạp tiền, điều chỉnh có lý do.
4. Phiên chơi trả trước bằng ví hoặc trả sau; tính phí theo giây và làm tròn khi xuất hóa đơn.
5. Danh mục hàng, sản phẩm, tồn kho, nhập/điều chỉnh tồn.
6. Đơn dịch vụ gắn với phiên chơi hoặc bán lẻ.
7. Hóa đơn, chi tiết hóa đơn, thanh toán tiền mặt/chuyển khoản/ví.
8. Ca làm việc và đối soát tiền mặt.
9. Phiếu bảo trì máy.
10. Dashboard và các báo cáo chính.
11. Audit log cho thao tác nhạy cảm.
12. WPF theo MVVM, giao diện tiếng Việt, hỗ trợ bàn phím và trạng thái tải/lỗi/rỗng.

### Sau MVP

- Agent cài trên từng máy trạm, điều khiển khóa/mở máy từ xa, heartbeat thực.
- Đặt chỗ, khuyến mãi/coupon, tích điểm, gói giờ chơi.
- In hóa đơn, tích hợp QR thanh toán, SMS/email.
- Đồng bộ nhiều cơ sở, báo cáo hợp nhất, xuất Excel/PDF.
- Chế độ offline và hàng đợi đồng bộ.

## 3. Vai trò và quyền

| Vai trò | Quyền chính |
|---|---|
| Owner | Toàn quyền, cấu hình hệ thống, người dùng, báo cáo tài chính |
| Manager | Vận hành, giá, kho, nhân viên, báo cáo; không thay đổi quyền Owner |
| Cashier | Mở/kết phiên, hội viên, nạp tiền, bán hàng, hóa đơn, ca làm việc |
| Technician | Xem máy, đổi trạng thái kỹ thuật, xử lý phiếu bảo trì |

API luôn kiểm tra quyền; việc ẩn nút trên UI chỉ là lớp hỗ trợ trải nghiệm, không thay thế phân quyền phía server.

## 4. Luồng nghiệp vụ cốt lõi

### 4.1 Mở và kết thúc phiên chơi

1. Thu ngân chọn máy đang `Available`.
2. Chọn khách hội viên hoặc khách vãng lai, hình thức `PrepaidWallet` hoặc `Postpaid`.
3. Server kiểm tra máy, ca làm việc, số dư và xung đột phiên đang mở trong một transaction.
4. Server tạo `PlaySession`, `SessionChargeSegment`, chuyển máy sang `InUse` và ghi `SessionEvent`.
5. Khi thay đổi giá, phiên hiện tại giữ nguyên segment cũ; thay giá trong phiên phải tạo segment mới.
6. Tạm dừng/tiếp tục tạo sự kiện và cập nhật tổng thời gian tính phí; một máy chỉ có tối đa một phiên mở.
7. Kết thúc phiên: server khóa thời điểm kết thúc, tính tiền từ các segment, cộng các đơn dịch vụ chưa thanh toán, tạo invoice.
8. Thu tiền bằng một hoặc nhiều payment. Invoice chỉ thành `Paid` khi tổng thanh toán hợp lệ bằng số tiền phải trả.
9. Với ví trả trước, khoản giữ/khấu trừ được ghi bằng wallet transaction; không sửa trực tiếp số dư mà không có ledger.

### 4.2 Nạp tiền hội viên

1. Thu ngân phải có ca đang mở.
2. Nhập số tiền, phương thức thanh toán và ghi chú.
3. Server tạo payment/receipt logic, `WalletTransaction` loại `TopUp` và cập nhật số dư trong cùng transaction.
4. Giao dịch đã ghi sổ không xóa; hoàn/điều chỉnh phải tạo giao dịch đối ứng và audit log.

### 4.3 Bán hàng và tồn kho

1. Tạo order bán lẻ hoặc gắn với một phiên đang mở.
2. Đơn giá và tên sản phẩm được snapshot vào order item.
3. Khi xác nhận order, sản phẩm quản lý tồn sinh `InventoryTransaction` loại `Sale`.
4. Hủy order đã xác nhận sinh giao dịch `Return`, không xóa lịch sử.
5. Không cho tồn âm trừ khi cấu hình chi nhánh cho phép.

### 4.4 Ca thu ngân

1. Mỗi thu ngân chỉ có một ca `Open` tại một chi nhánh.
2. Ghi số dư đầu ca; mọi thu/chi tiền mặt liên kết ca.
3. Cuối ca server tính tiền kỳ vọng = đầu ca + thu tiền mặt + thu khác vào quỹ - hoàn tiền - chi tiền mặt.
4. Thu ngân nhập tiền thực đếm; hệ thống ghi chênh lệch và yêu cầu ghi chú nếu vượt ngưỡng cấu hình.

### 4.5 Bảo trì

1. Máy có thể được đưa sang `Maintenance` nếu không có phiên đang chạy.
2. Phiếu có mức độ, người phụ trách, mô tả, nhật ký xử lý và chi phí.
3. Khi đóng phiếu, máy trở về `Available` hoặc `Offline` theo kết quả kiểm tra.

## 5. Kiến trúc

### Server

- ASP.NET Core Web API .NET 10.
- Kiến trúc theo feature/module nhưng giữ số project vừa phải: `Domain`, `Application`, `Infrastructure`, `Server`.
- EF Core + SQL Server cho production; SQLite/in-memory chỉ dùng cho test và local test khi cần.
- FluentValidation hoặc validation tương đương ở biên API.
- JWT access token ngắn hạn, refresh token băm trong database.
- RFC 7807 Problem Details cho lỗi.
- OpenAPI cho toàn bộ endpoint.
- UTC trong database/API; chuyển sang múi giờ chi nhánh ở UI.
- Optimistic concurrency bằng `rowversion` cho aggregate dễ tranh chấp.

### Client

- WPF .NET 10, MVVM, dependency injection, typed `HttpClient`.
- Các lớp `Views`, `ViewModels`, `Services`, `Models`, `Converters`, `Resources`.
- Điều hướng bằng shell + content region; không mở nhiều cửa sổ tùy tiện.
- Token chỉ giữ trong memory trong MVP; refresh token được lưu bằng Windows DPAPI nếu bật “ghi nhớ đăng nhập”.
- Không chứa connection string database; mọi dữ liệu qua API.

### Ranh giới module

| Module | Trách nhiệm |
|---|---|
| Identity | Người dùng, vai trò, token, khóa tài khoản |
| Organization | Chi nhánh, cấu hình chi nhánh |
| Stations | Khu máy, máy trạm, trạng thái |
| Pricing | Bảng giá và quy tắc theo thời gian |
| Customers | Hồ sơ hội viên, nhóm khách |
| Wallet | Số dư và sổ giao dịch |
| Sessions | Phiên chơi, segment tính phí, sự kiện |
| Catalog | Danh mục, sản phẩm |
| Inventory | Tồn kho và ledger kho |
| Sales | Order và order item |
| Billing | Invoice, invoice line, payment |
| Cashier | Ca làm việc và cash movement |
| Maintenance | Phiếu và nhật ký bảo trì |
| Reporting | Dashboard và báo cáo đọc tối ưu |
| Audit | Nhật ký hành động nhạy cảm |

## 6. Yêu cầu phi chức năng

- Mọi thay đổi tiền, tồn kho, phiên và trạng thái máy chạy trong transaction database.
- Endpoint tạo giao dịch hỗ trợ `Idempotency-Key` để chống bấm lặp.
- Không trả password hash, refresh token hash hoặc dữ liệu bí mật qua API.
- Log không chứa mật khẩu/token; có correlation ID.
- Pagination mặc định 20, tối đa 200; tìm kiếm chuẩn hóa chuỗi.
- Query báo cáo dùng `AsNoTracking`, giới hạn khoảng ngày và projection.
- Thao tác thông thường phản hồi dưới 500 ms trên LAN với dữ liệu mục tiêu 100 máy, 50.000 khách và 1 triệu ledger rows.
- Backup/restore là trách nhiệm triển khai SQL Server; ứng dụng có health endpoint để giám sát.
- Các trường tiền dùng `decimal(18,2)`; tỷ lệ dùng `decimal(9,4)`; không dùng `double` cho tiền.
- Thời gian lưu `datetime2(7)` UTC; ngày nghiệp vụ lưu `date` khi không cần giờ.
- Accessibility: focus rõ, thứ tự Tab hợp lý, label cho control, không dùng màu làm tín hiệu duy nhất.

## 7. Chiến lược kiểm thử

| Tầng | Phạm vi |
|---|---|
| Unit | Tính tiền theo segment, làm tròn, chuyển trạng thái, cân bằng ví/tồn/ca |
| Integration | API + EF Core: login, mở/kết phiên, nạp tiền, order, invoice, close shift |
| Authorization | Ma trận quyền cho endpoint nhạy cảm |
| Concurrency | Hai yêu cầu mở cùng máy; hai yêu cầu trừ cùng tồn/số dư |
| Contract | Problem Details, pagination, validation, OpenAPI |
| Client | ViewModel command, validation, loading/error state, navigation |
| Smoke | Server khởi động, database migrate/seed, client build và đăng nhập demo |

## 8. Dữ liệu mẫu

- Chi nhánh: `CyberGame Trung Tâm`.
- Khu máy: `Standard`, `VIP`.
- 12 máy mẫu: `PC-01` … `PC-12`.
- Giá: Standard 10.000đ/giờ, VIP 15.000đ/giờ.
- Tài khoản phát triển: Owner và Cashier, mật khẩu lấy từ development configuration/user-secrets; tuyệt đối không hard-code production secret.
- Một số khách hàng, sản phẩm và tồn kho mẫu để dashboard có dữ liệu.

## 9. Lộ trình triển khai

1. Chuẩn hóa solution, thêm project kiến trúc, package và cấu hình.
2. Domain model, DbContext, Fluent mappings, migrations, seed.
3. Identity/JWT/RBAC và hạ tầng lỗi/audit/idempotency.
4. Stations + Pricing + Customers + Wallet.
5. Sessions + Billing, bao gồm engine tính tiền và concurrency.
6. Catalog + Inventory + Sales.
7. Shifts + Expenses + Maintenance + Reporting.
8. WPF shell, theme, login, dashboard và các màn hình nghiệp vụ.
9. Automated tests, build, smoke test, tài liệu chạy và dữ liệu demo.
10. Review blocker, sửa lỗi, nghiệm thu theo Definition of Done.

## 10. Definition of Done

- `dotnet build` toàn solution không lỗi.
- Test nghiệp vụ quan trọng chạy đạt.
- Có migration đầu tiên và hướng dẫn tạo database.
- Có thể đăng nhập, mở ca, nạp tiền, mở/kết phiên, bán hàng và thanh toán theo luồng demo.
- UI không còn màn hình mẫu WeatherForecast/MainWindow trống.
- OpenAPI mô tả endpoint; lỗi trả theo Problem Details.
- Không có secret thật trong repository.
- Có tài liệu kiến trúc, database, API, UI/UX và hướng dẫn chạy.
- AGY review cuối không còn lỗi mức High/Medium; mọi kết luận được kiểm tra lại bằng build/test tại máy local.
