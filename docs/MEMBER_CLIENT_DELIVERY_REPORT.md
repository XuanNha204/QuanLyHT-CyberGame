# BÁO CÁO BÀN GIAO — MÁY KHÁCH HỘI VIÊN

**Ngày chốt:** 18/09/2026  
**Phạm vi:** UI giả lập máy con dùng dữ liệu thật của CyberGame Manager  
**Trạng thái:** Hoàn thành và đã nghiệm thu trên SQL Server thật

## 1. Kết quả theo yêu cầu

| Yêu cầu | Kết quả | Bằng chứng |
|---|:---:|---|
| Giữ đúng vận hành hiện tại | Đạt | Thu ngân vẫn phải mở ca và mở phiên; máy con không tự mở/chuyển máy |
| Project UI máy con | Đạt | `CyberGame.MemberClient`, WPF MVVM độc lập |
| Tính giờ thật | Đạt | Server tính theo UTC/charge segment; UI đồng bộ quote mỗi 2 giây và chỉ nội suy giữa hai lần đồng bộ |
| Gọi món thật | Đạt | Ghi Orders/OrderItems, trừ InventoryBalance, tạo InventoryTransaction, cộng ServiceAmount |
| Nạp tiền thật | Đạt | Member chỉ tạo Pending; Cashier duyệt mới cộng Wallet và tạo WalletTransaction/Payment/CashMovement |
| Kết thúc phiên thật | Đạt | Chốt segment, tạo Invoice, trừ ví, ghi audit, trả máy Available |
| Không có chức năng thừa | Đạt | Không remote desktop, shutdown, heartbeat agent, QR giả, SignalR, chọn/chuyển máy hoặc database riêng |

## 2. Thành phần bàn giao

| Nhóm | Thành phần chính |
|---|---|
| UI máy con | Login mã hội viên/PIN; dashboard phiên; giỏ/gọi món; yêu cầu nạp; lịch sử đơn/yêu cầu; xác nhận kết phiên |
| UI thu ngân | Thiết lập PIN; xem và duyệt/từ chối yêu cầu nạp |
| API member | Login, me, session/quote/complete, catalog/orders, top-up requests |
| Bảo mật | JWT tách `staff`/`member`; claim máy bất biến; PBKDF2; khóa 15 phút sau 5 PIN sai |
| Database | Mở rộng Customers; bảng MemberTopUpRequests; filtered unique index; rowversion; migration `AddMemberClientAndTopUpRequests` |
| Tài liệu | Kế hoạch, DB, API, UI/design system và báo cáo này |

## 3. Nghiệm thu tự động

| Phép kiểm | Kết quả |
|---|---|
| Build solution .NET 10 | Đạt — 0 lỗi, 0 cảnh báo |
| Test solution | Đạt — 85/85, 0 lỗi, 0 bỏ qua |
| Migration trên `CyberGameDb` | Đạt — đã áp dụng thành công |
| Health SQL Server | Đạt — HTTP 200 |
| Hồi quy lỗi mạng khi auto-complete | Đạt — cho phép thử lại, không gọi trùng đồng thời |
| Cleanup idempotency khi downstream lỗi | Đạt — không còn nguy cơ lưu nhầm mutation đang tracked |

## 4. Smoke test dữ liệu thật

| Bước | Kết quả quan sát |
|---|---|
| Thu ngân/ca | `cashier`, ca `Open` |
| Mở phiên | Hội viên `KH-001` trên `PC-01` |
| Cô lập token | Truyền thử `PC-02` vẫn bị khóa về `PC-01`; gọi API staff nhận 403 |
| Gọi món | `Nước Suối Aquafina 500ml`, đơn 8.000đ, đọc lại thấy trong phiên |
| Yêu cầu nạp | Pending → Approved; ví tăng đúng 10.000đ |
| Đối soát ca | `TotalCashTopUps = 10.000đ`; `ExpectedCash = 1.010.000đ` |
| Kết phiên | Tổng 8.005,56đ; sinh một invoice; máy trở về Available |
| Chống trừ lặp | Gọi lại cùng key và key mới đều trả cùng invoice; ví giữ nguyên 92.822,22đ |
| Hồi quy POS thu ngân | Auto-confirm tạo `Confirmed`, tồn 58 → 57; hủy đơn trả tồn về 58 |

Smoke test dùng database Development cục bộ theo yêu cầu “dữ liệu thật”. Ca cashier được giữ `Open` để có thể chạy demo ngay; không dùng dữ liệu này cho production.

## 5. Giám sát AGY

| Vòng | Thực hiện | Kết quả |
|---|---|---|
| 1 | AGY Gemini 3.8 Flash High triển khai theo kế hoạch đã khóa nghiệp vụ | Tạo MemberClient, API, migration, UI thu ngân và test |
| 2 | Codex audit độc lập | Phát hiện ràng buộc máy, enum JSON, converter XAML, số dư, token, route và cấu hình |
| 3 | AGY correction pass | Sửa toàn bộ danh sách và nâng bộ test |
| 4 | Codex kiểm chứng SQL Server thật | Phát hiện/sửa transaction gọi món và cleanup idempotency; chạy lại thành công |

## 6. Cấu hình chạy

- Server: chạy `CyberGame.Server` bằng profile `http` tại `http://localhost:5227`.
- Máy con: sửa `CyberGame.MemberClient/appsettings.json`, mỗi máy có một `MachineCode` riêng rồi chạy `CyberGame.MemberClient`.
- Tài khoản/PIN seed chỉ dành cho Development; production phải dùng secret riêng, tắt seed và đặt lại toàn bộ thông tin mẫu.
