# Báo cáo hoàn thiện mô hình máy quản trị và máy hội viên

Ngày nghiệm thu: 18/09/2026

## 1. Kết quả tổng quan

Hệ thống đã được hoàn thiện theo mô hình hai ứng dụng dùng chung `CyberGame.Server` và SQL Server:

| Thành phần | Vai trò sau điều chỉnh | Trạng thái |
|---|---|---|
| `CyberGame.Client` | Máy quản trị/thu ngân: máy trạm, phiên chơi, hội viên, ví, yêu cầu nạp, duyệt món, POS, sản phẩm, kho, ca, hóa đơn, doanh thu | Hoàn thành |
| `CyberGame.MemberClient` | Máy hội viên: đăng nhập, tính giờ thật, số dư thật, yêu cầu nạp, gọi món thật, trạng thái đơn, đổi PIN, kết thúc phiên | Hoàn thành |
| `CyberGame.Server` | API dùng chung, phân quyền staff/member, xử lý tiền–kho–phiên chơi nguyên tử | Hoàn thành |
| SQL Server `CyberGameDb` | Nguồn dữ liệu duy nhất; không có database giả hoặc dữ liệu mock trên UI | Hoàn thành |

## 2. Chức năng đã hoàn thiện

| Nhóm | Điều chỉnh | Kết quả |
|---|---|---|
| Hội viên | Đổi toàn bộ nhãn “Khách hàng” trong Client thành “Hội viên” | Hoàn thành |
| Tạo hội viên | Bắt buộc PIN 6 số khi tạo; PIN được hash cùng giao dịch tạo tài khoản | Hoàn thành |
| Đăng nhập máy khách | Hội viên vừa tạo đăng nhập MemberClient được ngay khi máy có phiên chơi | Hoàn thành |
| Ngừng hội viên | Thêm ngừng/kích hoạt lại; không hard-delete lịch sử tiền, phiên, hóa đơn | Hoàn thành |
| PIN | Quản trị có thể đặt lại PIN; hội viên tự đổi PIN sau khi xác minh PIN hiện tại | Hoàn thành |
| Nạp tiền máy khách | Chỉ tạo yêu cầu `Pending`; chưa cộng ví trước khi thu ngân duyệt | Hoàn thành |
| Duyệt nạp | Thu ngân duyệt/từ chối; duyệt cập nhật ví, ledger, payment và cash movement | Hoàn thành |
| Thực đơn | MemberClient tải tên món, nhóm, giá, đơn vị và tồn kho thật từ Server | Hoàn thành |
| Đặt món | Đơn MemberClient bắt đầu là `Draft`; chưa trừ kho/chưa tính phí | Hoàn thành |
| Duyệt món | POS có hàng đợi chờ duyệt, từ chối và đánh dấu đã phục vụ | Hoàn thành |
| Kiểm soát tiền/kho | Khi duyệt, Server kiểm tra lại phiên, ca, ví, tồn kho rồi mới cập nhật | Hoàn thành |
| Kết thúc phiên | Chặn kết thúc khi còn đơn `Draft`; chỉ `Confirmed`/`Fulfilled` được tính | Hoàn thành |
| Sản phẩm | Màn Kho thêm được món ăn/thức uống thật với nhóm, SKU, giá và tồn đầu | Hoàn thành |
| Theo dõi & báo cáo | Giữ nguyên máy trạm, ca, hóa đơn và báo cáo doanh thu hiện có | Hoàn thành |
| An toàn thao tác | Bổ sung idempotency cho tạo hội viên và sản phẩm; các giao dịch tiền/đơn vẫn dùng khóa chống gửi lặp | Hoàn thành |

## 3. Quy trình vận hành chuẩn

1. Chạy `CyberGame.Server` trên máy chủ và bảo đảm các máy truy cập được địa chỉ API.
2. Chạy `CyberGame.Client` trên máy quản trị/thu ngân, đăng nhập nhân viên và mở ca.
3. Tạo hội viên tại màn **Hội viên**, nhập PIN 6 số và nạp tiền ban đầu nếu cần.
4. Từ màn **Phòng máy**, mở phiên cho đúng hội viên trên đúng máy.
5. Trên máy khách, đặt `MemberClient:MachineCode` trong `CyberGame.MemberClient/appsettings.json` đúng mã máy, sau đó chạy `CyberGame.MemberClient`.
6. Hội viên đăng nhập bằng mã hội viên và PIN đã tạo.
7. Yêu cầu nạp xuất hiện tại màn Hội viên của máy quản trị; đơn món xuất hiện tại hàng đợi trong POS.
8. Thu ngân duyệt/từ chối yêu cầu nạp và đơn món. Đơn đã giao được đánh dấu **Đã phục vụ**.
9. Hội viên có thể đổi PIN và kết thúc phiên; nếu còn đơn chờ, hệ thống yêu cầu xử lý đơn trước.

Mỗi máy khách cần một mã máy khác nhau. Ví dụ máy `PC-02` phải cấu hình `MachineCode` là `PC-02`; không dùng chung một mã cho nhiều máy.

## 4. Kiểm thử nghiệm thu

| Kiểm tra | Kết quả |
|---|---|
| Build toàn solution | Thành công, 0 lỗi, 0 cảnh báo |
| Automated tests | 88/88 qua, 0 thất bại |
| Kiểm tra StaticResource XAML | Không còn resource thiếu trong Client/MemberClient |
| Tạo hội viên + đăng nhập ngay | Qua |
| Danh mục món thật | Qua; trả về tên `Cà Phê Sữa Đá Pha Phin` |
| Đơn chờ không giảm kho | Qua; tồn kho giữ 30 |
| Chặn kết thúc khi đơn chờ | Qua |
| Duyệt đơn | Qua; trạng thái `Confirmed`, tồn kho 30 → 29 |
| Đánh dấu phục vụ | Qua; trạng thái `Fulfilled` |
| Yêu cầu nạp và duyệt | Qua; trạng thái `Approved` |
| Đổi PIN | Qua; PIN cũ bị từ chối, PIN mới đăng nhập được |
| Kết thúc phiên | Qua; tạo tổng kết đúng phiên |
| Ngừng hội viên | Qua; trạng thái `Inactive`, đăng nhập bị từ chối |
| Thêm món thật từ quản trị | Qua; tạo mặt hàng với tồn đầu 3, sau test đã chuyển `IsActive=false` |
| Khởi động hai ứng dụng WPF | Qua; cả Client và MemberClient duy trì tiến trình, không thoát khi nạp giao diện ban đầu |

Smoke test dùng tài khoản `SMK162655`, sau nghiệm thu đã chuyển `Inactive` để giữ nguyên dấu vết đối soát tài chính.

## 5. Các file chính đã thay đổi

| Khu vực | File |
|---|---|
| DTO/API contract | `CyberGame.Application/DTOs/CustomerWalletDTOs.cs`, `MemberDTOs.cs` |
| Nghiệp vụ | `CustomerWalletService.cs`, `MemberService.cs`, `IMemberService.cs`, `OrderService.cs` |
| Server | `MemberController.cs`, `IdempotencyMiddleware.cs` |
| Client service | `IApiClient.cs`, `ApiClient.cs` |
| Client quản trị | `CustomerViewModel.cs`, `PosViewModel.cs`, `InventoryViewModel.cs` và các XAML tương ứng |
| MemberClient | `IMemberApiClient.cs`, `MemberApiClient.cs`, `ActiveSessionViewModel.cs`, `ActiveSessionView.xaml(.cs)`, converters |
| Tests | `MemberSessionAndOrderTests.cs`, `MemberAuthAndIsolationTests.cs`, `MemberClientViewModelTests.cs` |
| Tài liệu | `TWO_MACHINE_OPERATION_PLAN.md`, `TWO_MACHINE_OPERATION_REPORT.md` |

## 6. Ghi nhận AGY

AGY với model `gemini-3.8-flash-high` đã được gọi theo yêu cầu. Lượt đầu bị từ chối quyền ghi trong chế độ nền; hai lượt sau được cấp quyền và tăng thời gian chờ nhưng dịch vụ không trả nội dung, không tạo thay đổi file. Các tiến trình được dừng an toàn, không để lại mã dở dang. Toàn bộ phần triển khai, sửa lỗi, kiểm thử và smoke test trong báo cáo này do Codex trực tiếp thực hiện và nghiệm thu.
