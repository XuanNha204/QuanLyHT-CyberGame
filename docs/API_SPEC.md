# Đặc tả API — CyberGame Manager

Prefix: `/api/v1`. JSON camelCase. Thời gian ISO-8601 UTC. Danh sách trả `{ items, page, pageSize, totalCount, totalPages }`. Lỗi theo RFC 7807 với `traceId`, `code`, `errors` khi validation.

## Identity

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| POST | `/auth/login` | Anonymous | Đăng nhập, trả access/refresh token và hồ sơ |
| POST | `/auth/refresh` | Anonymous | Rotate refresh token |
| POST | `/auth/logout` | Authenticated | Thu hồi refresh token hiện tại |
| GET | `/auth/me` | Authenticated | User, branch và permission |
| GET/POST/PUT | `/users` | Owner/Manager | Danh sách, tạo, cập nhật nhân viên |
| POST | `/users/{id}/reset-password` | Owner/Manager | Đặt lại mật khẩu, revoke token |

## Branch, machines và pricing

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| GET/PUT | `/branches/current` | Auth / Manager | Xem/cập nhật chi nhánh |
| GET/POST/PUT | `/machine-groups` | Auth / Manager | CRUD khu máy |
| GET/POST/PUT | `/machines` | Auth / Manager | Danh sách, tạo, cập nhật máy |
| PATCH | `/machines/{id}/status` | Manager/Technician | Đổi trạng thái có kiểm tra |
| GET/POST/PUT | `/pricing-plans` | Auth / Manager | Bảng giá và quy tắc |

## Customers và wallet

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| GET/POST/PUT | `/customers` | Cashier+ | Tìm, tạo, cập nhật khách |
| GET | `/customers/{id}` | Cashier+ | Hồ sơ, số dư, lịch sử gần đây |
| GET | `/customers/{id}/wallet-transactions` | Cashier+ | Ledger có phân trang |
| POST | `/customers/{id}/wallet/top-up` | Cashier+ | Nạp tiền, yêu cầu ca mở + idempotency |
| POST | `/customers/{id}/wallet/adjust` | Manager+ | Điều chỉnh có lý do + audit |
| POST | `/customers/{id}/member-pin` | Cashier+ | Thiết lập PIN đúng 6 chữ số cho máy khách |
| GET | `/member-top-up-requests?status=Pending` | Cashier+ | Danh sách yêu cầu nạp từ máy khách |
| POST | `/member-top-up-requests/{id}/approve` | Cashier+ | Duyệt tiền mặt; cập nhật ví, payment và cash movement nguyên tử |
| POST | `/member-top-up-requests/{id}/reject` | Cashier+ | Từ chối, không thay đổi ví |

## Máy khách hội viên

JWT hội viên có `actor_type=member`, `customer_id`, `branch_id`, `machine_code`; không thể dùng để gọi API nhân viên.

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| POST | `/member/auth/login` | Anonymous | Đăng nhập mã hội viên + PIN trên máy cố định |
| GET | `/member/me` | Member | Hồ sơ, ví và máy lấy từ token |
| GET | `/member/session` | Member | Phiên Open/Paused đúng hội viên–máy; 204 nếu chưa được thu ngân mở |
| GET | `/member/session/quote` | Member | Thời gian và cước authoritative từ server |
| POST | `/member/session/complete` | Member | Chốt phiên, hóa đơn, ví và trả máy Available |
| GET | `/member/catalog` | Member | Sản phẩm thật đang bán/còn tồn của chi nhánh |
| GET/POST | `/member/orders` | Member | Xem/gọi món vào phiên hiện tại |
| GET/POST | `/member/top-up-requests` | Member | Xem/gửi yêu cầu nạp tiền mặt tại quầy |

## Sessions

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| GET | `/sessions/active` | Auth | Phiên đang hoạt động |
| GET | `/sessions` | Cashier+ | Lịch sử, lọc theo máy/khách/ngày |
| POST | `/sessions` | Cashier+ | Mở phiên |
| POST | `/sessions/{id}/pause` | Cashier+ | Tạm dừng |
| POST | `/sessions/{id}/resume` | Cashier+ | Tiếp tục |
| POST | `/sessions/{id}/transfer` | Manager+ | Chuyển máy an toàn |
| POST | `/sessions/{id}/complete` | Cashier+ | Chốt tiền, tạo invoice |
| POST | `/sessions/{id}/cancel` | Manager+ | Hủy có lý do/audit |
| GET | `/sessions/{id}/quote` | Cashier+ | Tạm tính hiện tại |

## Catalog, inventory và orders

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| GET/POST/PUT | `/categories` | Auth / Manager | Danh mục sản phẩm |
| GET/POST/PUT | `/products` | Auth / Manager | Sản phẩm và giá |
| GET | `/inventory` | Cashier+ | Tồn hiện tại, cảnh báo sắp hết |
| POST | `/inventory/receive` | Manager+ | Nhập kho |
| POST | `/inventory/adjust` | Manager+ | Điều chỉnh có lý do |
| GET/POST | `/orders` | Cashier+ | Danh sách/tạo draft |
| PUT | `/orders/{id}` | Cashier+ | Sửa draft |
| POST | `/orders/{id}/confirm` | Cashier+ | Xác nhận và trừ kho |
| POST | `/orders/{id}/fulfill` | Cashier+ | Hoàn tất phục vụ |
| POST | `/orders/{id}/cancel` | Cashier+ | Hủy/hoàn kho |

## Billing và shifts

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| GET | `/invoices` | Cashier+ | Danh sách hóa đơn |
| GET | `/invoices/{id}` | Cashier+ | Chi tiết hóa đơn |
| POST | `/invoices/{id}/payments` | Cashier+ | Thanh toán, hỗ trợ split payment |
| POST | `/invoices/{id}/void` | Manager+ | Hủy và đối ứng ledger |
| GET | `/shifts/current` | Cashier+ | Ca hiện tại của user |
| POST | `/shifts/open` | Cashier+ | Mở ca |
| POST | `/shifts/{id}/cash-movements` | Cashier+ | Thu/chi quỹ thủ công |
| POST | `/shifts/{id}/close` | Cashier+ | Đóng và đối soát ca |
| GET | `/shifts/{id}/summary` | Cashier+ | Báo cáo ca |

## Expenses, maintenance và reports

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| GET/POST/PUT | `/expense-categories` | Manager+ | Danh mục chi phí |
| GET/POST/PUT | `/expenses` | Manager+ | Ghi nhận/phê duyệt chi phí |
| GET/POST/PUT | `/maintenance-tickets` | Technician+ | Danh sách và phiếu bảo trì |
| POST | `/maintenance-tickets/{id}/logs` | Technician+ | Thêm diễn biến/chuyển trạng thái |
| GET | `/dashboard/summary` | Auth | KPI và trạng thái máy |
| GET | `/reports/revenue` | Manager+ | Doanh thu theo ngày/kênh |
| GET | `/reports/machine-utilization` | Manager+ | Công suất khu/máy |
| GET | `/reports/top-products` | Manager+ | Sản phẩm bán chạy |
| GET | `/reports/wallet-liability` | Owner | Tổng nghĩa vụ số dư ví |
| GET | `/health` | Anonymous | Health check không lộ chi tiết nhạy cảm |

## Quy ước request quan trọng

- Các POST tài chính/giao dịch bắt buộc header `Idempotency-Key` UUID.
- Request cập nhật aggregate gửi `rowVersion` để phát hiện dữ liệu cũ.
- `complete session`, `confirm order`, `payment`, `top-up`, `inventory adjust`, `close shift` trả aggregate mới nhất và ledger liên quan.
- Validation code ổn định, ví dụ `machine_not_available`, `shift_required`, `insufficient_wallet_balance`, `insufficient_inventory`, `concurrency_conflict`.

## Signal gần thời gian thực

MVP cho client poll dashboard/active sessions mỗi 10–15 giây và refresh ngay sau command. SignalR là bước kế tiếp; thiết kế service không phụ thuộc polling để có thể bổ sung hub mà không đổi domain.
