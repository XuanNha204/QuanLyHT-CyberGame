# CyberGame Manager — Hệ Thống Quản Lý Phòng Máy & Vận Hành Cyber Game

Phần mềm quản lý phòng máy tập trung dành cho các chuỗi Cyber Game hiện đại, được xây dựng trên nền tảng **.NET 10** theo kiến trúc **Clean Architecture / Vertical Slice**, kết hợp **ASP.NET Core Web API** và ứng dụng máy trạm **WPF MVVM** chuẩn tiếng Việt.

---

## 1. Cấu Trúc Giải Pháp (Solution Architecture)

```
CyberGame/
├── CyberGame.Domain/            # Lớp nghiệp vụ cốt lõi (Entities, Enums, Domain Services, Rules)
│   ├── Entities/                # Base, Identity, Station, Customer, Session, Catalog, Billing, Shift, Maintenance
│   ├── Enums/                   # Trạng thái máy, phiên chơi, hóa đơn, vai trò, hình thức tính phí...
│   ├── Services/                # SessionPricingCalculator (tính tiền giờ), ShiftCashCalculator (đối soát ca)
│   └── Exceptions/              # DomainException, NotFoundException, ConflictException, BusinessRuleException
│
├── CyberGame.Application/       # Lớp ứng dụng (Use cases, DTOs, Service Contracts & Implementations)
│   ├── Common/                  # ICyberGameDbContext, ICurrentUserService, IPasswordHasher, IJwtTokenService
│   ├── DTOs/                    # Auth, Station, CustomerWallet, Session, Catalog, Order, Billing, Shift, Report
│   └── Services/                # AuthService, BranchMachineService, PricingService, CustomerWalletService,
│                                # SessionService, CatalogInventoryService, OrderService, BillingService,
│                                # ShiftService, MaintenanceService, DashboardReportService
│
├── CyberGame.Infrastructure/    # Lớp hạ tầng (Database & Security)
│   ├── Data/                    # CyberGameDbContext (Fluent API), Migrations, DbInitializer (Seed Data)
│   └── Security/                # PasswordHasher (PBKDF2-SHA256 salt), JwtTokenService (HMAC-SHA256)
│
├── CyberGame.Server/            # Máy chủ Web API (RESTful, JWT Auth, RBAC, RFC 7807 ProblemDetails)
│   ├── Controllers/             # Auth, Users, Branches, Machines, Pricing, Customers, Sessions, POS, Shifts...
│   ├── Middleware/              # ExceptionHandlingMiddleware (RFC 7807 ProblemDetails)
│   └── Program.cs               # DI container, JWT Bearer configuration, CORS, OpenAPI, DB Auto-migration
│
├── CyberGame.Client/            # Ứng dụng Quản lý Máy trạm Desktop WPF MVVM chuẩn tiếng Việt
│   ├── Common/                  # ViewModelBase, RelayCommand, Value Converters (Tiền tệ, Thời gian, Trạng thái)
│   ├── Resources/               # Theme.xaml (Design tokens MASTER.md, Colors, Typography, Card, DataGrid styles)
│   ├── Services/                # ApiClient (HTTP/REST client), StateService (Quản lý phiên đăng nhập & ca)
│   ├── ViewModels/              # Login, Shell, Dashboard, Station, Customer, POS, Inventory, Shift, Invoice, Report
│   └── Views/                   # LoginView, ShellView, DashboardView, StationView, CustomerView, PosView...
│
├── CyberGame.MemberClient/      # Ứng dụng Máy Khách Hội Viên WPF MVVM (Member Client Simulator)
│   ├── Common/                  # ViewModelBase, RelayCommand, Converters (Currency, Duration, Status)
│   ├── Models/                  # MemberClientConfig (ApiBaseUrl, MachineCode)
│   ├── Resources/               # Theme.xaml (Design tokens Dark Cyber Theme, Badge, Button, Input)
│   ├── Services/                # MemberApiClient (Token Bearer, Idempotency-Key UUID, Error handling)
│   ├── ViewModels/              # LoginViewModel, ActiveSessionViewModel (Polling 1s/2s, Cart, Top-up, End session), MainViewModel
│   └── Views/                   # LoginView, ActiveSessionView, MainWindow
│
└── CyberGame.Tests/             # Bộ kiểm thử tự động (Unit Tests & End-to-End Vertical Slice)
    ├── PricingCalculationTests.cs       # Kiểm tra tính cước giờ chơi, làm tròn hóa đơn, trừ thời gian pause
    ├── ShiftCalculationTests.cs         # Kiểm tra tính tiền kỳ vọng trong két và chênh lệch ca thu ngân
    ├── VerticalSliceTests.cs            # Luồng nghiệm thu end-to-end từ mở ca, nạp ví, mở máy, gọi món, kết phiên, đóng ca
    ├── MemberAuthAndIsolationTests.cs   # Kiểm tra xác thực PIN, lockout 5 lần sai, cô lập actor_type staff/member
    ├── MemberTopUpRequestTests.cs       # Kiểm tra yêu cầu nạp tiền, duyệt/từ chối tại quầy, cập nhật nguyên tử
    ├── MemberSessionAndOrderTests.cs    # Kiểm tra báo giá cước, gọi món trừ kho, kết phiên trừ ví & idempotent replay
    └── MemberClientViewModelTests.cs    # Kiểm tra luồng UI ViewModel máy khách, giỏ hàng, token và idempotency header
```

---

## 2. Tài Khoản Thử Nghiệm Phát Triển (Seed Credentials)

Hệ thống tự động khởi tạo các tài khoản phát triển với mật khẩu được băm an toàn bằng PBKDF2-SHA256:

| Tên đăng nhập | Mật khẩu | Vai trò (Role) | Mô tả quyền hạn |
|---|---|---|---|
| `owner` | `Admin@123` | **Chủ phòng máy (Owner)** | Toàn quyền cấu hình chi nhánh, xem doanh thu, quản lý nhân viên, bảng giá, kho bãi. |
| `cashier` | `Cashier@123` | **Thu ngân (Cashier)** | Mở/đóng ca làm việc, nạp tiền ví hội viên, mở máy trạm, POS bán đồ ăn/nước uống, thanh toán hóa đơn. |
| `tech` | `Tech@123` | **Kỹ thuật viên (Technician)** | Theo dõi tình trạng máy trạm, tiếp nhận và xử lý sự cố thiết bị, cập nhật nhật ký bảo trì. |

### Tài khoản Hội viên thử nghiệm trên Máy Khách (MemberClient)
Được cấu hình mã PIN mẫu qua `SeedCredentials:Members:DefaultPin` (`123456`) trong `appsettings.Development.json`:

| Mã hội viên (Card/Code) | Mã PIN mặc định | Họ và tên | Số dư ví ban đầu | Chi nhánh |
|---|---|---|---|---|
| `KH-001` | `123456` | Nguyễn Văn An | 100.000 đ | CyberGame Trung Tâm (`CG-TT01`) |
| `KH-002` | `123456` | Trần Thị Bình | 50.000 đ | CyberGame Trung Tâm (`CG-TT01`) |

> [!NOTE]
> Hội viên chỉ có thể đăng nhập trên máy trạm khi máy trạm đó **đã được thu ngân mở phiên chơi trước đó** bằng tài khoản của chính hội viên. Khi sai mã PIN 5 lần liên tiếp, tài khoản hội viên sẽ bị tạm khóa 15 phút.

> [!IMPORTANT]
> Mật khẩu, mã PIN và JWT Secret Key trong môi trường sản xuất phải được cấu hình thông qua biến môi trường hoặc Azure Key Vault / AWS Secrets Manager, không lưu trực tiếp trong mã nguồn.

---

## 3. Hướng Dẫn Cài Đặt & Khởi Chạy

### Yêu cầu hệ thống
- **.NET 10 SDK** (hoặc mới hơn).
- **SQL Server 2019 / 2022** (hoặc LocalDB).

### Bước 1: Cấu hình chuỗi kết nối cơ sở dữ liệu & bảo mật
Tệp `CyberGame.Server/appsettings.json` chỉ chứa cấu hình dùng chung, không chứa bí mật:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CyberGameDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Issuer": "CyberGameServer",
    "Audience": "CyberGameClient",
    "ExpiryMinutes": 120,
    "MemberExpiryMinutes": 720
  },
  "SeedData": {
    "Enabled": false
  }
}
```

Khi chạy ở môi trường `Development`, tệp `CyberGame.Server/appsettings.Development.json` cung cấp khóa JWT và bật dữ liệu mẫu. Với môi trường production, thiết lập `Jwt__Secret` (tối thiểu 32 ký tự) và chuỗi kết nối bằng biến môi trường hoặc kho bí mật; chỉ bật `SeedData__Enabled=true` khi chủ động cần dữ liệu mẫu. Thời hạn JWT cho nhân viên (`ExpiryMinutes`, mặc định 120 phút) và hội viên máy trạm (`MemberExpiryMinutes`, mặc định 720 phút / 12 giờ) được cấu hình độc lập.

> [!NOTE]
> Hệ thống áp dụng kiểm tra Fail-Fast: `Jwt:Secret` là bắt buộc và phải có độ dài tối thiểu 32 ký tự, nếu không máy chủ sẽ dừng khởi động để bảo đảm an toàn. Không sao chép khóa Development sang production.

### Bước 2: Khởi chạy Máy chủ API (CyberGame.Server)
Khi máy chủ khởi động, hệ thống sẽ tự động thực thi EF Core Migration (`Database.MigrateAsync`) và khởi tạo dữ liệu mẫu nếu được bật trong cấu hình:
```powershell
dotnet run --project CyberGame.Server/CyberGame.Server.csproj
```
API mặc định lắng nghe tại:
- HTTP API: `http://localhost:5227`
- Health Check: `http://localhost:5227/health`
- OpenAPI v1 Spec (JSON): `http://localhost:5227/openapi/v1.json`

*(Lưu ý: Hệ thống sử dụng trực tiếp Microsoft.AspNetCore.OpenApi (`MapOpenApi`), không kích hoạt Swagger UI nhằm tối ưu hiệu năng và thu gọn footprint).*

### Bước 3: Khởi chạy Ứng dụng Quản lý (CyberGame.Client)
Trong một cửa sổ terminal khác:
```powershell
dotnet run --project CyberGame.Client/CyberGame.Client.csproj
```
- Giao diện desktop WPF tự động kết nối trực tiếp đến API tại `http://localhost:5227/api/v1/`.
- Đăng nhập bằng tài khoản thu ngân (`cashier` / `Cashier@123`).
- **Thực hiện mở ca làm việc** (ví dụ Ca 1 với tiền két ban đầu 1.000.000đ).
- **Mở máy trạm** cho hội viên: Chọn máy (ví dụ `PC-01`), bấm **Mở máy**, chọn hội viên `KH-001` (Nguyễn Văn An) với hình thức **Ví trả trước (Prepaid Wallet)**.

### Bước 4: Khởi chạy Máy Khách Hội Viên (CyberGame.MemberClient)
Trong một cửa sổ terminal riêng biệt thứ ba (đại diện cho máy trạm của khách):
```powershell
dotnet run --project CyberGame.MemberClient/CyberGame.MemberClient.csproj
```
- Tệp cấu hình `CyberGame.MemberClient/appsettings.json`:
```json
{
  "MemberClient": {
    "ApiBaseUrl": "http://localhost:5227/api/v1/",
    "MachineCode": "PC-01"
  }
}
```
- Khi chạy trên máy trạm nào, chỉ định `MachineCode` tương ứng trong `appsettings.json` (ví dụ `PC-01`, `PC-02`...).
- **Đăng nhập hội viên**: Nhập Mã hội viên `KH-001` và Mã PIN `123456`.
- **Trải nghiệm phiên chơi thực tế**:
  - Đồng hồ đếm thời gian sử dụng realtime (cập nhật từng giây).
  - Tự động lấy báo giá cước giờ (`quote`) mỗi 2 giây, tính chính xác số dư khả dụng (`AvailableBalance = Balance - BillableUsage`).
  - Danh mục F&B trực tiếp từ kho chi nhánh: Chọn đồ ăn/nước uống, thêm vào giỏ hàng, ghi chú món (ví dụ: *ít đá*), bấm **Đặt món ngay** -> hệ thống tự động trừ tồn kho thực tế và gắn đơn hàng vào ca của thu ngân.
  - Yêu cầu nạp tiền tại quầy: Nhập số tiền muốn nạp (ví dụ 50.000đ), gửi yêu cầu -> Thu ngân tại `CyberGame.Client` (Tab *Hội viên* -> *Yêu cầu nạp tiền chờ duyệt*) bấm **Duyệt nạp** -> Ví hội viên tự động tăng, tiền mặt ca thu ngân tự động cộng, máy khách tự động đồng bộ số dư mới nhất.
  - Bấm **Kết thúc phiên**: Hệ thống chốt cước giờ, khấu trừ tiền ví trả trước, trả máy về trạng thái `Available` và hiển thị hóa đơn tổng kết rõ ràng.

> [!IMPORTANT]
> **Nguyên tắc vận hành bất biến của CyberGame:**
> - Máy khách (`MemberClient`) **tuyệt đối không có quyền tự mở phiên mới**, **không được tự cộng tiền vào ví**, **không được chuyển máy** và **không có quyền truy cập bất kỳ API nào của nhân viên**.
> - Thu ngân là người mở ca và mở phiên cho máy. Hội viên chỉ đăng nhập vào máy đã được nhân viên kích hoạt mở phiên.

---

## 4. Kiểm Thử Nghiệp Vụ Tự Động (Automated Testing)

Chạy bộ kiểm thử tự động của toàn bộ giải pháp (CyberGame.slnx):
```powershell
dotnet test CyberGame.slnx
```

### Chi tiết các ca kiểm thử:
1. **PricingCalculationTests:**
   - Tính toán tiền giờ theo đơn giá theo giờ và số giây sử dụng thực tế.
   - Trừ chính xác thời lượng tạm dừng (`TotalPausedSeconds`).
   - Xử lý các mốc thời gian đặc biệt (0 giây, đơn giá 0đ, thời lượng ngắn).
   - Quy tắc làm tròn hóa đơn về bội số gần nhất (500đ / 1.000đ).
2. **ShiftCalculationTests & ShiftReconciliationTests:**
   - Tính tổng tiền mặt kỳ vọng trong két: `Tiền đầu ca + Thu nạp ví mặt + Thu bán hàng mặt + Thu phát sinh (CashIn) - Chi phát sinh (Expense/CashOut)`.
   - Tính chênh lệch tiền mặt khi chốt ca (`Tiền thực tế - Tiền kỳ vọng`).
   - Đối soát chi phí phát sinh dạng `CashMovementType.Expense` trong tổng kết ca và chốt ca.
3. **SessionCancellationTests:**
   - Kiểm tra lý do hủy phiên là bắt buộc (`reason_required`).
   - Từ chối hủy phiên khi còn đơn hàng chưa hủy (`session_has_uncancelled_orders`).
   - Từ chối hủy phiên khi đã phát sinh cước giờ chơi (`session_has_accrued_charges`).
   - Cho phép hủy sạch khi không có đơn hàng và 0đ cước, tự động trả máy về trạng thái `Available`.
4. **BranchIsolationTests:**
   - Đảm bảo tính cô lập tuyệt đối dữ liệu giữa các chi nhánh (`BranchId`).
   - Quản lý (`Manager`) không thể xem, tạo, cập nhật hoặc đặt lại mật khẩu cho tài khoản thuộc chi nhánh khác (`cross_branch_access`).
   - Mọi truy vấn chi nhánh chéo trên máy trạm, khách hàng, hóa đơn đều trả về `NotFoundException`.
5. **IdempotencyTests:**
   - Kiểm tra `IdempotencyMiddleware` yêu cầu header `Idempotency-Key` dạng UUID (<= 100 ký tự) trên các API giao dịch tạo mới (POST).
   - Đảm bảo scope cô lập theo `UserId` + `BranchId` + `HttpMethod` + `Path`, tuyệt đối không replay kết quả giữa các tài khoản hoặc chi nhánh khác nhau.
   - Cơ chế khóa placeholder `ResponseStatusCode = 0` trước khi chạy downstream; chặn đứng yêu cầu đồng thời và trả 409 Conflict (`idempotency_in_progress`).
   - Replay kết quả đã lưu trữ khi nhận cùng key và body, không thực thi lại downstream.
   - Trả về mã lỗi 409 Conflict (`idempotency_conflict`) khi key trùng nhưng nội dung yêu cầu khác.
   - Tự động xóa placeholder khi downstream thất bại/non-2xx và không replay các bản ghi đã hết hạn.
6. **ApiClientContractTests:**
   - Kiểm tra contract của `ApiClient` với `HttpMessageHandler` giả lập:
   - `StartSessionAsync` gọi đúng route `POST api/v1/sessions` (không gọi `sessions/start`).
   - `GetCustomersAsync` sử dụng đúng query parameter `search` (không dùng `query`).
   - `GetProductsAsync` deserialize chính xác cấu trúc `PagedResult<ProductDto>` và trả về `List<ProductDto>`.
   - `ReceiveInventoryAsync` và `AdjustInventoryAsync` nhận và trả về đúng kiểu `InventoryItemDto`.
   - `GetInvoicesAsync` truyền chính xác khoảng ngày `fromDate` và `toDate` cho máy chủ lọc dữ liệu.
7. **ConverterTests:**
   - Đảm bảo tất cả bộ chuyển đổi hiển thị (Display-only converters) trả về `Binding.DoNothing` trong `ConvertBack` thay vì ném ngoại lệ `NotImplementedException`.
   - Kiểm tra tính hợp lệ của enum `CashMovementType` (bao gồm `Expense` và `CashIn`) cho giao diện thu/chi ca trực.
8. **VerticalSliceTests (End-to-end integration test):**
   - Luồng nghiệp vụ hoàn chỉnh: Mở ca -> Nạp ví -> Bắt đầu phiên -> Đặt món ăn -> Hoàn tất phiên -> Chốt ca đối soát với chênh lệch 0đ.
9. **MemberAuthAndIsolationTests:**
   - Xác thực đăng nhập hội viên bằng Mã thẻ + PIN hash (PBKDF2).
   - Tăng số lần đếm sai `MemberFailedLoginCount`; khóa tài khoản 15 phút sau 5 lần nhập sai liên tiếp.
   - Chặn đăng nhập máy trạm chéo chi nhánh (`branch_mismatch`).
   - Phân tách và cô lập tuyệt đối JWT Token: `actor_type=staff` vs `actor_type=member`. Member không thể truy cập API nhân viên và ngược lại.
10. **MemberTopUpRequestTests:**
    - Tạo yêu cầu nạp tiền mặt tại quầy ở trạng thái `Pending`.
    - Chặn hội viên tạo yêu cầu nạp tiền mới khi đang có một yêu cầu chờ duyệt (409 Conflict).
    - Thu ngân duyệt nạp tiền: Cập nhật nguyên tử số dư ví + tạo bản ghi thanh toán ví (`Payment`) + tạo biến động tiền mặt ca trực (`CashMovement`).
    - Chặn duyệt hai lần trên cùng một yêu cầu; từ chối yêu cầu không làm biến động số dư ví.
11. **MemberSessionAndOrderTests:**
    - Báo giá cước giờ chơi realtime theo block và đơn giá bậc thang, tính chính xác số dư khả dụng (`AvailableBalance`).
    - Đóng băng thời gian cước khi phiên ở trạng thái tạm dừng (`Paused`).
    - Gọi món F&B từ máy khách: trừ tồn kho thực tế (`InventoryBalance`), ghi nhận lịch sử xuất kho gán vào nhân viên ca trực (`Shift.UserId`).
    - Chặn đặt món khi tồn kho không đủ hoặc khi số dư ví khả dụng không đủ thanh toán.
    - Kết thúc phiên từ máy khách: chốt cước giờ, khấu trừ ví trả trước, trả máy về `Available`, sinh hóa đơn và bảo đảm tính idempotent khi gọi lại.
12. **MemberClientViewModelTests:**
    - Kiểm thử giao diện WPF MVVM của máy khách: xác thực trường nhập liệu đăng nhập, cảnh báo khi máy chưa mở phiên.
    - Quản lý giỏ hàng: thêm/bớt/xóa món, tính tổng tiền giỏ hàng realtime, chặn vượt tồn kho.
    - Kiểm tra số tiền nạp tối thiểu và chặn tạo yêu cầu mới khi đang có yêu cầu chờ duyệt.
    - Đảm bảo `MemberApiClient` gửi đúng Bearer token và header `Idempotency-Key` dạng UUID trên các request POST.

---

## 5. Danh Sách Nghiệm Thu Tính Năng (Definition of Done Verification)

| Hạng mục | Trạng thái | Ghi chú |
|---|:---:|---|
| **Clean Architecture** | Hoàn thành | Phân tách tách bạch Domain, Application, Infrastructure, Server, Client, MemberClient, Tests |
| **SQL Server + EF Core** | Hoàn thành | 4 migration: khởi tạo schema, ràng buộc unique có điều kiện, mở rộng scope idempotency, và mở rộng hội viên/yêu cầu nạp tiền (`AddMemberClientAndTopUpRequests`); Seed đầy đủ tài khoản, máy trạm, menu, hội viên có mã PIN |
| **Bảo mật & Phân quyền** | Hoàn thành | JWT token cô lập `actor_type` (`staff` vs `member`), PBKDF2 PIN hashing, lockout 5 lần sai, DefaultPolicy chặn member gọi staff endpoints |
| **Quản lý phòng máy** | Hoàn thành | Sơ đồ 12 máy trạm, card trực quan màu sắc, mở máy, tạm dừng, tiếp tục, chuyển máy, kết phiên |
| **Quản lý hội viên & ví** | Hoàn thành | Tìm kiếm, tạo hội viên, cài đặt/đổi mã PIN, duyệt/từ chối nạp tiền mặt tại quầy, sổ cái lịch sử biến động số dư |
| **POS & Quản lý kho** | Hoàn thành | Danh mục đồ ăn thức uống, giỏ hàng, gắn vào máy trạm, tự động trừ tồn kho, nhập kho, kiểm kê |
| **Máy Khách Hội Viên (MemberClient)** | Hoàn thành | WPF MVVM độc lập, cố định máy theo `MachineCode`, đếm giờ realtime, polling quote 2s, gọi món F&B trừ kho thật, yêu cầu nạp tiền tại quầy, kết phiên tự động trừ ví |
| **Quản lý ca & Tiền mặt** | Hoàn thành | Mở ca với tiền két ban đầu, ghi nhận thu/chi phát sinh, tích hợp tiền nạp ví tại quầy vào ca trực, đối soát tiền kỳ vọng, chốt ca |
| **Bảo trì & Sự cố** | Hoàn thành | Lập phiếu sự cố máy trạm, cập nhật tiến độ xử lý, chi phí sửa chữa, nhật ký kỹ thuật |
| **Dashboard & Báo cáo** | Hoàn thành | KPI doanh thu, công suất sử dụng phòng máy, biểu đồ doanh thu theo ngày, top món bán chạy |
| **WPF MVVM Tiếng Việt** | Hoàn thành | Tuân thủ Design Tokens `MASTER.md` & `member-client-simulator.md`, 4 trạng thái dữ liệu (Loading/Empty/Error/Success) |
| **Kiểm thử tự động** | Hoàn thành | **100% test cases passed (85/85 tests)**, bao gồm vertical slice end-to-end, hợp đồng API, idempotency và bộ test máy khách hội viên |
