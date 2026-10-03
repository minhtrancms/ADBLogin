# ADBLogin v2.0 - Standalone Antidetect Browser Manager

Hệ thống quản lý và vận hành hàng loạt Antidetect Browser Profile (Orbita-144) chuyên nghiệp, tối ưu hóa cho Windows 10/11 với hiệu năng cao, kiến trúc Standalone 100% Offline-First (hoạt động độc lập không cần server xác thực hay phụ thuộc bên ngoài).

---

## ✨ Tính Năng Nổi Bật

- **100% Offline-First**: Toàn bộ dữ liệu cấu hình, danh sách profile, session được quản lý trực tiếp trên máy cục bộ (`profiles.json`), loại bỏ hoàn toàn các popup xác thực, quảng cáo và kết nối server bên ngoài.
- **Giao diện Windows 11 Fluent Design**:
  - Giao diện tối giản, thanh công cụ chia nhóm trực quan.
  - Trạng thái profile hiển thị dạng huy hiệu (Badge Pill) hiện đại: `🟢 ĐANG MỞ`, `⚪ ĐÃ TẮT`, `🟢 Live`, `🔴 Dead`.
  - Hỗ trợ phím tắt tiện lợi: `Enter` (mở profile), `Delete` (xóa profile), `F5` (làm mới dữ liệu).
- **Chế độ Mobile Farm (Tỉ lệ 20:9)**:
  - Tích hợp công nghệ **System Scale Factor 70%** (`--force-device-scale-factor=0.7`) và cuộn ẩn `OverlayScrollbar`.
  - Tự động co dãn kích thước và căn chỉnh lưới theo số lượng máy chỉ định (ví dụ: dàn 1 hàng ngang 6 máy điện thoại song song không bị tràn xuống hàng).
- **Tạo Profile Tự Động Hàng Loạt (Auto Create)**:
  - Sinh danh tính ngẫu nhiên (Faker), tích hợp gán Proxy và xoay vòng User-Agent tự động.
- **Kiểm Tra Proxy Siêu Tốc (Multi-threaded Proxy Checker)**:
  - Kiểm tra hàng loạt Proxy HTTP/SOCKS qua đa luồng `Leaf.xNet`, hiển thị IP công khai và độ trễ Ping theo thời gian thực.
- **Quản Lý Phiên Làm Việc (Session Tracking)**:
  - Nhận diện chính xác tiến trình trình duyệt đang chạy trong thời gian thực, cho phép dừng từng máy hoặc đóng toàn bộ chỉ bằng 1 click.

---

## 🛠 Hướng Dẫn Biên Dịch & Cài Đặt

### Yêu cầu hệ thống:
- Hệ điều hành: Windows 10 / Windows 11 (64-bit)
- .NET Framework 4.8 hoặc mới hơn
- Trình duyệt Orbita-144 (hoặc Chromium tương đương)

### Lệnh biên dịch nhanh:
Chạy file script `build_app.bat` hoặc thực hiện lệnh:

```cmd
build_app.bat
```

Chương trình sẽ tự động biên dịch toàn bộ mã nguồn C# trong thư mục `src/` và tạo ra file thực thi `ADBLogin.exe` hoàn chỉnh.

---

## 📂 Cấu Trúc Thư Mục

```
ADBLogin_V111_Pass_999/
├── src/
│   ├── Core/
│   │   ├── Models/         # AppConfig, UserProfile, ProxySettings
│   │   └── Services/       # BrowserLauncherService, ProxyChecker, SessionManager, ...
│   └── UI/
│       ├── MainForm.cs     # Giao diện chính chuẩn Windows 11
│       ├── ProfileEditForm.cs
│       └── AutoCreateForm.cs
├── build_app.bat           # Script biên dịch tự động
├── profiles.json           # Dữ liệu profile cục bộ
└── ADBLogin.exe            # File thực thi chính
```
