# KẾ HOẠCH TRIỂN KHAI UI GIẢ LẬP MÁY KHÁCH HỘI VIÊN

## 1. Mục tiêu và nguyên tắc khóa phạm vi

Tạo project WPF `CyberGame.MemberClient` mô phỏng đúng một máy chơi. Ứng dụng dùng API và SQL Server hiện tại, không dùng dữ liệu giả. Mỗi bản chạy được gắn cố định với một `MachineCode` trong cấu hình cục bộ.

Phần mềm quản lý hiện tại vẫn là nơi nhân viên mở ca, tạo hội viên, nạp tiền thực tế và mở phiên chơi. Máy khách hội viên chỉ thực hiện các thao tác hợp lệ trên chính hội viên, máy và phiên đã được nhân viên tạo.

### Trong phạm vi

- Hội viên đăng nhập bằng mã hội viên và PIN.
- Nhận diện máy từ cấu hình, không cho người chơi chọn máy trên giao diện.
- Đọc phiên đang mở của đúng hội viên trên đúng máy.
- Hiển thị thời gian chơi, đơn giá, tiền giờ, tiền dịch vụ, số dư ví và số dư khả dụng theo dữ liệu server.
- Gọi món vào phiên đang chơi; đơn hàng, tồn kho và tổng tiền là dữ liệu thật.
- Gửi yêu cầu nạp tiền mặt tại quầy; thu ngân duyệt trên ứng dụng quản lý rồi ví, payment, cash movement và wallet ledger mới thay đổi.
- Hội viên kết thúc phiên/đăng xuất; server tạo hóa đơn và trừ ví theo nghiệp vụ hiện tại.

### Ngoài phạm vi

- Không điều khiển khóa màn hình Windows, shutdown/restart, heartbeat, cập nhật agent hoặc theo dõi tiến trình game.
- Không cho hội viên tự mở ca, tự mở/chuyển máy, tự cộng tiền hoặc gọi API nhân viên.
- Không giả lập ngân hàng/QR, không tự xác nhận thanh toán.
- Không dùng SignalR khi polling định kỳ đã đủ cho một UI giả lập.
- Không tạo dữ liệu riêng hoặc database riêng cho simulator.

## 2. Luồng vận hành chuẩn

### Chuẩn bị bởi nhân viên

1. Thu ngân đăng nhập ứng dụng `CyberGame.Client` và mở ca.
2. Thu ngân tạo hoặc chọn hội viên, thiết lập/reset PIN nếu cần.
3. Thu ngân nạp số dư ban đầu nếu nhận tiền, sau đó mở phiên `PrepaidWallet` trên máy đã chọn.
4. `CyberGame.MemberClient` của máy đó đã được cấu hình đúng `MachineCode`.

### Hội viên sử dụng máy

1. Hội viên nhập mã và PIN tại máy khách.
2. Server xác thực tài khoản, chi nhánh và trạng thái hội viên.
3. Ứng dụng tìm phiên `Open`/`Paused` có cùng `CustomerId` và `MachineCode`.
4. Nếu chưa có phiên, giao diện báo “Chờ thu ngân mở phiên”; không tự tạo phiên.
5. Khi có phiên, ứng dụng tải quote từ server, hiển thị giờ/cước và làm mới định kỳ.
6. Hội viên có thể gọi món. Đơn được gắn vào `PlaySessionId`, `CustomerId` và `ShiftId` của phiên; xác nhận đơn sẽ trừ tồn kho thật.
7. Hội viên có thể gửi yêu cầu nạp tiền mặt. Số dư chỉ tăng sau khi thu ngân duyệt yêu cầu trong ca đang mở.
8. Khi hội viên chọn kết thúc, server hoàn tất phiên, tạo invoice, trừ ví, trả máy về `Available`; ứng dụng hiển thị hóa đơn tóm tắt rồi quay về đăng nhập.

## 3. Kiến trúc thay đổi

```text
CyberGame.MemberClient (WPF MVVM)
        |
        | HTTPS/JSON + JWT Member + Idempotency-Key
        v
CyberGame.Server /api/v1/member/*
        |
        v
Application services -> EF Core -> CyberGameDb (database hiện tại)

CyberGame.Client (thu ngân)
        |
        +-- duyệt/từ chối yêu cầu nạp tiền
```

Không cho `CyberGame.MemberClient` tham chiếu Infrastructure hoặc truy cập SQL Server trực tiếp. Project chỉ tham chiếu DTO/domain enum cần thiết và gọi API.

## 4. Thiết kế database tối thiểu

### Mở rộng `Customers`

| Cột | Kiểu | Mục đích |
|---|---|---|
| `MemberPinHash` | nvarchar(500), nullable | PIN đã băm PBKDF2; nullable nghĩa là chưa được phép đăng nhập máy khách |
| `MemberFailedLoginCount` | int, not null, default 0 | Đếm đăng nhập sai |
| `MemberLockoutEndUtc` | datetime2, nullable | Khóa tạm sau 5 lần sai |
| `MemberLastLoginAtUtc` | datetime2, nullable | Theo dõi lần dùng máy khách gần nhất |

Không lưu PIN thuần. Staff tạo/reset PIN thông qua API phân quyền Owner/Manager/Cashier.

### Bảng `MemberTopUpRequests`

| Cột | Quy tắc |
|---|---|
| `Id` | PK Guid |
| `BranchId` | FK Branches, bắt buộc |
| `CustomerId` | FK Customers, bắt buộc |
| `Amount` | decimal(18,2), > 0 |
| `Status` | Pending, Approved, Rejected |
| `Note` | nvarchar(500), nullable |
| `CreatedAtUtc` | thời điểm hội viên gửi |
| `ProcessedAtUtc` | nullable |
| `ProcessedByUserId` | FK Users, nullable |
| `WalletTransactionId` | FK WalletTransactions, nullable, unique khi được duyệt |
| `RowVersion` | rowversion |

Chỉ cho phép một yêu cầu `Pending` cho mỗi hội viên tại một thời điểm bằng filtered unique index. Việc duyệt yêu cầu phải chạy trong một transaction và chống duyệt trùng.

## 5. Xác thực và phân quyền

- JWT nhân viên bổ sung claim `actor_type=staff`.
- JWT hội viên có `actor_type=member`, `customer_id`, `branch_id`, role `Member`; token chỉ giữ trong bộ nhớ của WPF.
- Default authorization policy yêu cầu `actor_type=staff`, vì vậy các controller hiện có với `[Authorize]` không thể bị token hội viên truy cập.
- Policy `MemberOnly` yêu cầu `actor_type=member` và chỉ dùng cho `/api/v1/member/*`.
- PIN gồm đúng 6 chữ số, hash bằng `IPasswordHasher`, khóa 15 phút sau 5 lần sai.
- Token hội viên có thời hạn đủ cho một ca chơi nhưng không được ghi ra file cấu hình/log.
- Mọi truy vấn member kiểm tra đồng thời `CustomerId`, `BranchId`, `MachineCode` và quyền sở hữu session.

## 6. API cần bổ sung

### API hội viên

| Method | Route | Mục đích |
|---|---|---|
| POST | `/api/v1/member/auth/login` | Đăng nhập bằng `memberCode`, `pin`, `machineCode` |
| GET | `/api/v1/member/me` | Hồ sơ, số dư ví và máy đang cấu hình |
| GET | `/api/v1/member/session` | Phiên đang mở của đúng hội viên/máy; 204 nếu chưa có |
| GET | `/api/v1/member/session/quote` | Quote thời gian/cước thật tại `DateTime.UtcNow` |
| POST | `/api/v1/member/session/complete` | Kết thúc đúng phiên của hội viên |
| GET | `/api/v1/member/catalog` | Sản phẩm đang bán và còn tồn tại chi nhánh |
| GET | `/api/v1/member/orders` | Đơn của phiên hiện tại |
| POST | `/api/v1/member/orders` | Gọi món cho phiên hiện tại |
| GET | `/api/v1/member/top-up-requests` | Trạng thái các yêu cầu nạp gần nhất |
| POST | `/api/v1/member/top-up-requests` | Tạo yêu cầu nạp tiền mặt tại quầy |

### API nhân viên bổ sung

| Method | Route | Quyền |
|---|---|---|
| POST | `/api/v1/customers/{id}/member-pin` | Owner, Manager, Cashier |
| GET | `/api/v1/member-top-up-requests?status=Pending` | Owner, Manager, Cashier |
| POST | `/api/v1/member-top-up-requests/{id}/approve` | Owner, Manager, Cashier; bắt buộc ca mở |
| POST | `/api/v1/member-top-up-requests/{id}/reject` | Owner, Manager, Cashier |

Các POST giao dịch dùng `Idempotency-Key`; middleware phải nhận diện các route mới. Login được loại trừ.

## 7. Quy tắc nghiệp vụ bắt buộc

### Phiên và thời gian

- MemberClient không tạo session.
- Server là nguồn thời gian chuẩn. WPF dùng timer một giây chỉ để nội suy giữa hai lần lấy quote, không tự ghi cước.
- Refresh quote 2–3 giây/lần; refresh hồ sơ, ví, đơn và yêu cầu nạp 5 giây/lần hoặc ngay sau thao tác.
- `Paused` giữ nguyên cước; `Completed/Cancelled` dừng timer và quay về màn hình kết quả/đăng nhập.
- Nếu mất kết nối, timer hiển thị trạng thái ước tính và khóa giao dịch; sau khi kết nối lại phải đồng bộ lại từ server.

### Gọi món

- Chỉ cho gọi món khi session thuộc member và đang `Open` hoặc `Paused`.
- Chỉ sản phẩm `IsActive`, đúng branch, còn tồn nếu `TrackInventory=true`.
- Đơn dùng `ShiftId` và user phụ trách của phiên hiện tại; không yêu cầu hoặc giả mạo tài khoản nhân viên từ WPF.
- Xác nhận đơn và trừ tồn chạy cùng transaction.
- Tổng giỏ không được vượt `WalletBalance - CurrentSessionTotal`; nếu không đủ phải từ chối trước khi trừ kho.
- Thành công phải cập nhật ngay service amount và available balance.

### Nạp tiền

- Member chỉ gửi yêu cầu; không tự thay đổi `Wallet.Balance`.
- Thu ngân duyệt trong ca mở. Một transaction duy nhất cập nhật request, wallet, wallet transaction, payment và cash movement.
- Duyệt lại request đã xử lý trả về conflict và không cộng tiền lần hai.
- Sau duyệt, MemberClient nhận số dư thật ở lần refresh kế tiếp.

### Kết thúc

- Chỉ hoàn tất session đang gắn với token member và machine code đã đăng nhập.
- Dùng đúng logic invoice/wallet hiện có; actor nghiệp vụ là nhân viên sở hữu `ShiftId` của session, audit ghi nguồn `MemberClient`.
- Request lặp lại không tạo invoice hoặc khấu trừ ví lần hai.

## 8. Thiết kế UI WPF

Chi tiết token nằm ở `design-system/cybergame-manager/pages/member-client-simulator.md`.

### Màn hình đăng nhập

- Tên máy đọc-only ở đầu màn hình.
- Mã hội viên, PIN, nút đăng nhập; hỗ trợ Enter.
- Thông báo rõ tài khoản bị khóa, sai PIN, máy không đúng chi nhánh hoặc server mất kết nối.

### Màn hình phiên chơi

- Header: họ tên/mã hội viên, máy, trạng thái kết nối, kết thúc phiên.
- 5 chỉ số: số dư ví, thời gian đã chơi, tiền giờ, tiền dịch vụ, số dư khả dụng.
- Tab Tổng quan, Gọi món, Nạp tiền.
- Cảnh báo số dư thấp bằng màu + biểu tượng + văn bản.

### Gọi món

- Danh mục/sản phẩm có giá và tồn; giỏ hàng; ghi chú từng món; xác nhận một lần.
- Disable nút trong lúc gửi để chống click lặp; hiển thị trạng thái đơn Confirmed/Fulfilled/Cancelled.

### Nạp tiền

- Chỉ “Yêu cầu nạp tiền mặt tại quầy”, nhập số tiền và ghi chú.
- Hiển thị rõ “Ví chỉ tăng sau khi thu ngân xác nhận”.
- Danh sách trạng thái Pending/Approved/Rejected.

### Thay đổi ứng dụng quản lý

- `CustomerView` bổ sung thiết lập/reset PIN.
- Có danh sách yêu cầu nạp đang chờ, nút Duyệt/Từ chối và trạng thái xử lý.
- Không thay đổi luồng nạp trực tiếp hiện có của thu ngân.

## 9. Các bước triển khai

1. Bổ sung entity, enum, DbSet, mapping, migration và Development seed PIN.
2. Bổ sung actor type JWT, Staff default policy và MemberOnly policy; kiểm thử token member không vào API staff.
3. Viết DTO/service/controller member và service xử lý yêu cầu nạp.
4. Refactor tối thiểu logic order/session để tái sử dụng, không sao chép công thức tính tiền hoặc trừ kho.
5. Mở rộng `CyberGame.Client` cho PIN và duyệt yêu cầu nạp.
6. Tạo `CyberGame.MemberClient` WPF MVVM và thêm vào solution.
7. Bổ sung unit, authorization, integration, API contract và ViewModel tests.
8. Build, migrate database Development, chạy toàn bộ test và smoke test luồng thật với SQL Server.

## 10. Kiểm thử và tiêu chí nghiệm thu

| Nhóm | Ca bắt buộc |
|---|---|
| Member auth | PIN đúng/sai, khóa 5 lần, inactive, sai branch/machine, staff API trả 403 |
| Session | Không tự tạo phiên; chỉ lấy đúng member+machine; quote tăng theo thời gian server; pause không tăng |
| Order | Gắn đúng session/customer/shift; trừ kho một lần; hết tồn; thiếu số dư; idempotency |
| Top-up | Tạo pending; một pending/member; approve cần ca; ledger/payment/cash movement khớp; approve lặp không cộng lại |
| Completion | Chỉ chủ session; invoice một lần; ví trừ đúng; máy Available; request lặp an toàn |
| WPF contract | Đúng route/query/DTO; timer dừng khi logout; reconnect; command không submit hai lần |
| Regression | Toàn bộ 50 test cũ vẫn đạt |

### Luồng smoke test bắt buộc

1. Migrate/seed Development database.
2. Cashier đăng nhập và mở ca.
3. Cashier mở session prepaid cho hội viên seed trên máy seed.
4. MemberClient đăng nhập đúng hội viên/máy; quan sát quote tăng thật.
5. Gọi một món; xác minh order và tồn kho thật.
6. Gửi yêu cầu nạp; cashier duyệt; xác minh balance, ledger, payment và cash movement.
7. Member kết thúc; xác minh invoice, wallet và machine status.

## 11. Definition of Done

- Project mới nằm trong solution, build 0 error và không làm hỏng `CyberGame.Client`.
- Có migration chạy được cả SQL Server và test database.
- Không tồn tại endpoint member có thể thay số dư trực tiếp.
- Không có dữ liệu mock trong đường chạy chính.
- Các giá trị thời gian/cước/đơn hàng/số dư đều đọc từ API thật.
- Test cũ và test mới đều đạt; smoke test SQL Server đạt.
- README có hướng dẫn cấu hình `MachineCode`, PIN seed và thứ tự chạy Server/Client/MemberClient.
