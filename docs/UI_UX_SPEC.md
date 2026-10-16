# Đặc tả UI/UX WPF — CyberGame Manager

## 1. Định hướng

Ứng dụng desktop vận hành liên tục, ưu tiên tốc độ thao tác và khả năng đọc dữ liệu hơn hiệu ứng. Phong cách Minimal/Swiss, mật độ cao vừa phải, light theme mặc định và dark theme sẵn cấu trúc token. Không dùng emoji làm icon; dùng vector geometry thống nhất.

Kết quả tra cứu UI/UX cho WPF không trả về guideline chuyên biệt sau hai lần tìm; vì vậy đặc tả này áp dụng nguyên tắc desktop/WPF chuẩn, kết hợp hệ màu và accessibility đã được xác minh từ design system.

## 2. Design tokens

### Màu light

| Token | Giá trị | Dùng cho |
|---|---:|---|
| Primary | `#2563EB` | Nút chính, selection, focus |
| PrimaryHover | `#1D4ED8` | Hover nút chính |
| Accent | `#C2410C` | Cảnh báo hành động/CTA cần chú ý |
| Background | `#F8FAFC` | Nền ứng dụng |
| Surface | `#FFFFFF` | Card, panel, dialog |
| Foreground | `#1E293B` | Text chính |
| MutedForeground | `#475569` | Text phụ, vẫn đạt độ tương phản |
| Border | `#CBD5E1` | Divider/control boundary |
| Success | `#15803D` | Thành công/Available |
| Warning | `#B45309` | Sắp hết/Paused |
| Danger | `#DC2626` | Lỗi/xóa/Offline |
| Info | `#0369A1` | Thông tin/InUse |

Dark theme dùng semantic token riêng; không đảo màu máy móc. Text thường đạt tối thiểu 4.5:1, focus/boundary 3:1.

### Typography và spacing

- Font UI: `Segoe UI Variable`/`Segoe UI`; dữ liệu mã/máy dùng `Cascadia Mono` nếu có.
- Title 24/32 SemiBold; page heading 20/28 SemiBold; section 16/24 SemiBold; body 14/20; caption 12/16.
- Spacing scale: 4, 8, 12, 16, 24, 32.
- Control cao 36 px; nút quan trọng 40 px; target icon-only tối thiểu 36×36 trên desktop.
- Corner radius 6–10 px; shadow rất nhẹ, ưu tiên border để phân tầng.

## 3. App shell

- Cửa sổ tối thiểu 1180×720; responsive tới 1024×640 bằng co sidebar và cuộn vùng nội dung.
- Sidebar trái 224 px: logo/tên chi nhánh, điều hướng, user/shift ở cuối.
- Top bar: tiêu đề trang, tìm nhanh theo ngữ cảnh, trạng thái API, thời gian, thông báo.
- Content có breadcrumb ngắn khi cần, không quá 3 cấp.
- Điều hướng theo quyền: Dashboard, Phòng máy, Phiên chơi, Khách hàng, Bán hàng, Kho, Hóa đơn, Ca làm việc, Bảo trì, Báo cáo, Nhân viên, Cài đặt.

## 4. Màn hình

### Đăng nhập

- Username, password, ghi nhớ đăng nhập, nút đăng nhập.
- Cho phép paste/password manager; Caps Lock warning; Enter để submit.
- Error summary phía trên và lỗi inline; không chỉ toast.
- Trạng thái đang xử lý vô hiệu hóa submit nhưng không làm mất dữ liệu.

### Dashboard

- Hàng KPI: doanh thu hôm nay, máy đang dùng/tổng máy, phiên đang chạy, tồn sắp hết.
- Sơ đồ trạng thái máy theo khu: card máy có mã, trạng thái, khách, thời lượng, tạm tính.
- Doanh thu 7 ngày dạng line/column chart với bảng số liệu tương đương.
- Danh sách hành động nhanh và cảnh báo: máy offline, tồn thấp, ticket quá hạn.

### Phòng máy

- Bộ lọc theo khu/trạng thái; Grid/List toggle.
- Card màu + icon + text trạng thái, không dựa vào màu đơn lẻ.
- Context action: mở phiên, tạm dừng/tiếp tục, chuyển máy, kết phiên, bảo trì.
- Panel chi tiết bên phải để giảm chuyển trang.

### Khách hàng

- DataGrid phân trang, tìm theo mã/tên/điện thoại.
- Detail gồm số dư, nhóm, lịch sử phiên, wallet ledger.
- Action rõ: tạo khách, nạp tiền, điều chỉnh (chỉ Manager), khóa/mở.

### Bán hàng/POS

- Trái: tìm sản phẩm và category; giữa: giỏ hàng; phải: khách/phiên, tổng tiền, thanh toán.
- Hỗ trợ bàn phím, tăng giảm số lượng, ghi chú món, trạng thái hết hàng.
- Không tự động trừ kho ở draft; hiển thị xác nhận khi confirm.

### Kho

- Bảng SKU, tên, tồn, mức cảnh báo, giá vốn/giá bán.
- Badge “Sắp hết” kèm text; bulk filter/export là sau MVP.
- Dialog nhập/điều chỉnh yêu cầu lý do và hiển thị tồn trước/sau.

### Hóa đơn và ca

- Hóa đơn lọc ngày/trạng thái/phương thức; detail có timeline payment.
- Ca hiện tại luôn hiện ở shell. Không cho giao dịch tiền mặt khi chưa mở ca.
- Close shift hiển thị công thức tiền kỳ vọng, các nhóm thu/chi và ô tiền thực đếm.

### Bảo trì

- Kanban đơn giản theo Open/In Progress/Resolved/Closed hoặc DataGrid tùy lựa chọn; luôn có nút thay trạng thái cho bàn phím, không drag-only.
- Ticket detail có máy, mức độ, người phụ trách, timeline, chi phí.

### Báo cáo

- Date range có preset Hôm nay/7 ngày/30 ngày/Tháng này.
- KPI + chart + bảng dữ liệu; chart không thay thế số liệu dạng text.
- Giới hạn khoảng ngày và nêu múi giờ chi nhánh.

## 5. Trạng thái UI bắt buộc

Mỗi màn hình dữ liệu có 4 trạng thái: loading, success, empty, error. Command bất đồng bộ chống double-click; lỗi giữ nguyên dữ liệu người dùng và nêu cách khắc phục. Toast chỉ dùng cho xác nhận ngắn; lỗi validation/transaction hiện trong trang hoặc dialog.

## 6. Accessibility và bàn phím

- Tab order theo thị giác, focus ring rõ, Escape đóng dialog, Enter kích hoạt primary action khi an toàn.
- Tất cả input có label; icon-only button có `AutomationProperties.Name` và tooltip.
- DataGrid hỗ trợ chọn bằng bàn phím; thao tác nguy hiểm không gắn vào double-click duy nhất.
- Không dùng placeholder thay label; không dùng màu làm chỉ báo duy nhất.
- Tôn trọng Windows high contrast/reduced animation trong phạm vi WPF hỗ trợ.
- Form nhiều lỗi focus vào error summary, đồng thời giữ lỗi inline cạnh trường.

## 7. Quy tắc an toàn thao tác

- Confirm dialog cho hủy hóa đơn, điều chỉnh ví/kho, đóng ca chênh lệch, kết phiên có số tiền lớn.
- Dialog nêu rõ đối tượng, tác động và hành động; nút nguy hiểm dùng màu Danger.
- Không đóng dialog khi server trả lỗi; hiển thị correlation/trace ID có thể copy.
- Dữ liệu có rowversion: khi conflict, báo “Dữ liệu vừa được thay đổi”, cho tải lại thay vì ghi đè im lặng.

## 8. Tiêu chí nghiệm thu UI

- Build sạch và mở được ở 100%/125%/150% DPI.
- Có thể hoàn thành luồng demo chỉ bằng bàn phím.
- Không có text tiếng Anh lẫn vào màn hình chính ngoài thuật ngữ kỹ thuật cần thiết.
- Không freeze UI khi gọi API; mọi tác vụ async có feedback.
- Dashboard và DataGrid không vỡ tại kích thước tối thiểu.
- Theme dùng ResourceDictionary/semantic token, không rải hex tùy tiện trong view.

## 9. Máy khách hội viên

UI máy con là project WPF độc lập `CyberGame.MemberClient`, dùng dark theme chức năng và cố định `MachineCode` bằng cấu hình cục bộ. Giao diện chỉ có đăng nhập hội viên, thời gian/cước, gọi món, yêu cầu nạp tại quầy và kết thúc phiên; không có chọn/chuyển máy, mở phiên, tự cộng ví hay chức năng nhân viên. Đặc tả chi tiết nằm tại `design-system/cybergame-manager/pages/member-client-simulator.md`.
