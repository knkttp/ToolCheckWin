# ToolCheckWin ⚡
> **Ứng Dụng Thẩm Định Phần Cứng Windows Chuyên Sâu (.NET 8 WPF)**

ToolCheckWin là công cụ thẩm định và chấm điểm phần cứng Laptop / PC tự động trên hệ điều hành Windows, hỗ trợ kỹ thuật viên và người dùng kiểm tra nhanh tình trạng máy, phát hiện khóa MDM / Autopilot doanh nghiệp, kiểm tra sức khỏe pin, ổ cứng SMART SSD, giải mã thông số tấm nền màn hình EDID và kiểm tra ngoại vi (bàn phím, âm thanh, touchpad).

---

## 🛠 Công Nghệ & Thư Viện (Tech Stack)
- **Framework:** .NET 8 WPF (`net8.0-windows10.0.19041.0`)
- **Kiến trúc:** Windows x64 (`win-x64`)
- **Thư viện NuGet:**
  - `System.Management (8.0.0)` — Truy vấn WMI / SMBIOS / CIMv2
  - `QuestPDF (2024.3.0)` — Kết xuất chứng chỉ kiểm định chuẩn A4
  - `NAudio (2.2.1)` — Phát tần số sóng Sine 440Hz kiểm tra 2 kênh loa Trái / Phải & ghi âm Mic
- **Win32 API & P/Invoke:** `SetWindowsHookEx` (Low-Level Keyboard Hook), `EnumDisplaySettings`

---

## 🚀 Tính Năng Chính
1. **Thông tin hệ thống & Màn hình EDID:**
   - Đọc tên máy, vi xử lý CPU, số thanh RAM, Bus RAM, danh sách GPU qua WMI.
   - Giải mã mảng byte EDID từ Registry (`HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY`) để trích xuất hãng sản xuất, tuần/năm xuất xưởng tấm nền, độ phân giải và tần số quét Hz.
2. **Sức khỏe Pin & Ổ cứng SMART:**
   - Trích xuất dung lượng thiết kế (`DesignedCapacity`), dung lượng sạc tối đa (`FullChargedCapacity`), số chu kỳ sạc (`CycleCount`) và tỷ lệ chai pin.
   - Ghép nối dữ liệu `MSFT_PhysicalDisk` & `MSFT_StorageReliabilityCounter` để đọc nhiệt độ ổ cứng SSD, giờ hoạt động và tổng số lỗi đọc/ghi.
3. **An ninh & Khóa quản trị MDM / Autopilot:**
   - Kiểm tra `dsregcmd /status` và quét sâu `HKLM\SOFTWARE\Microsoft\Enrollments` phát hiện khóa Intune / Autopilot.
   - Kiểm tra TPM 2.0 và trạng thái UEFI Secure Boot.
4. **Kiểm tra ngoại vi tương tác:**
   - Bộ ma trận 84 phím Laptop chuẩn với Low-Level Hook (`WH_KEYBOARD_LL`).
   - Cửa sổ kiểm tra điểm chết màn hình toàn màn hình qua 7 màu sắc.
   - Test âm thanh loa Stereo 2 kênh và thanh đo âm lượng Micro thời gian thực.
5. **Hệ thống chấm điểm 100 điểm & Phân hạng (A / B / C / F):**
   - Tự động đánh giá theo 5 tiêu chí (Pin, Ổ cứng, Màn hình, Ngoại vi, Bảo mật).
   - Tích hợp mã băm chống giả mạo **SHA-256 Anti-Tamper Hash**.
   - Xuất báo cáo chứng chỉ định dạng **PDF (A4)** và chụp ảnh **PNG**.

---

## 💻 Hướng Dẫn Biên Dịch & Chạy

### Yêu cầu:
- Windows 10/11 (64-bit)
- .NET 8.0 SDK

### Biên dịch dự án:
```powershell
dotnet build ToolCheckWin.csproj
```

### Đóng gói thành 1 file .exe độc lập (Single-file Portable):
```powershell
dotnet publish ToolCheckWin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./publish"
```
