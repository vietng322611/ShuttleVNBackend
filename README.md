# ShuttleVN Backend API

Hệ thống RESTful Web API cho nền tảng quản lý và đặt sân cầu lông trực tuyến **ShuttleVN**.

---

## Mục lục

* [Công nghệ sử dụng (Tech Stack)](#công-nghệ-sử-dụng-tech-stack)
* [Cấu trúc Solution](#cấu-trúc-solution)
* [Cài đặt & Khởi chạy](#cài-đặt--khởi-chạy)
  * [1. Yêu cầu môi trường](#1-yêu-cầu-môi-trường)
  * [2. Cấu hình biến môi trường](#2-cấu-hình-biến-môi-trường)
  * [3. Cập nhật Database (Migrations)](#3-cập-nhật-database-migrations)
  * [4. Khởi chạy ứng dụng](#4-khởi-chạy-ứng-dụng)
* [Chạy với Docker](#chạy-với-docker)
* [Triển khai (Deployment)](infrastructure/README.md)
* [Các tính năng chính](#các-tính-năng-chính)

---

## Công nghệ sử dụng (Tech Stack)

* **Runtime & Framework**: [.NET 10](https://dotnet.microsoft.com/) (C# 13 / `net10.0`), ASP.NET Core Web API
* **Kiến trúc**: Clean Architecture (Onion / Layered Architecture)
* **Cơ sở dữ liệu**: [PostgreSQL 15+](https://www.postgresql.org/) kết hợp [Entity Framework Core 10](https://learn.microsoft.com/ef/core/) (Npgsql)
* **Xác thực & Bảo mật**:
  * ASP.NET Core Cookie Authentication
  * CSRF Protection (`X-CSRF-TOKEN`)
  * Cơ chế Idempotency chống gửi đơn trùng lặp
* **Xử lý đồng thời (Concurrency Control)**:
  * Khóa bi quan cấp dòng (`SELECT ... FOR UPDATE`)
  * Ràng buộc loại trừ tại Database (PostgreSQL GiST `tsrange` Exclusion Constraint) triệt tiêu hoàn toàn nguy cơ trùng giờ đặt sân
* **Email & Thông báo**: [Resend API](https://resend.com/) & SMTP (MailKit) với HTML Template Renderer và Background Notification Worker
* **Ghi log & Tài liệu**: Serilog (Structured Logging), OpenAPI v3 JSON spec (/openapi/v1.json trong Development)
* **Container hóa**: Docker, Docker Compose

---

## Cấu trúc Solution

Dự án tuân thủ nghiêm ngặt nguyên tắc phụ thuộc một chiều của Clean Architecture:

```text
ShuttleVNBackend.Api ─────────┐
       │                      ▼
       │            ShuttleVNBackend.Infrastructure
       ▼                      │
ShuttleVNBackend.Application ─┤
       │                      │
       ▼                      ▼
ShuttleVNBackend.Core ◄───────┘
```

| Project | Trách nhiệm chính |
| :--- | :--- |
| **`ShuttleVNBackend.Core`** | Thực thể Domain (Entities: `Booking`, `BadmintonCourt`, `Customer`, `Employee`...), Enums, Domain Invariants. Không phụ thuộc thư viện ngoài. |
| **`ShuttleVNBackend.Application`** | Các Use Case/Services nghiệp vụ (`BookingService`, `StatisticsService`...), DTOs, Quy tắc nghiệp vụ, Hợp đồng Repository (`Interfaces`). |
| **`ShuttleVNBackend.Infrastructure`** | Triển khai EF Core DbContext (`ShuttleVnDbContext`), Migrations, Repositories, hạ tầng Email, Idempotency và phân quyền người dùng. |
| **`ShuttleVNBackend.Api`** | API Controllers, Middlewares (xử lý ngoại lệ toàn cục, logging), Dependency Injection, cấu hình Cookie Auth và OpenAPI. |

---

## Cài đặt & Khởi chạy

### 1. Yêu cầu môi trường
* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* [PostgreSQL](https://www.postgresql.org/) (hoặc chạy qua Docker)
* EF Core CLI Tool (cài đặt: `dotnet tool install --global dotnet-ef`)

### 2. Cấu hình biến môi trường
Sao chép tệp cấu hình mẫu và điền thông tin kết nối:

```bash
cp .env.example .env
```

Cấu hình các tham số trong `.env`:
```env
# Chuỗi kết nối Database PostgreSQL
ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=shuttlevn;Username=postgres;Password=YOUR_PASSWORD'

# Mật khẩu khởi tạo cho tài khoản Admin mặc định
DefaultAdmin__Password='AdminPassword123!'

# Cấu hình Email Service (Provider: Resend hoặc Smtp, phân biệt hoa/thường)
EmailService__Provider='Resend'
EmailService__ApiKey='re_your_resend_api_key'
EmailService__Smtp__Host=''
EmailService__Smtp__Username=''
EmailService__Smtp__Password=''

# Cấu hình Trợ lý AI (OpenAI-compatible)
Assistant__BaseUrl='https://api.openai.com/v1'
Assistant__Model='gpt-4o-mini'
Assistant__ApiKey='your_api_key_here'
Assistant__ExtraBody='{}'
```

### 3. Cập nhật Database (Migrations)
Áp dụng các migration lên PostgreSQL:

* **Windows**: Chạy script tiện ích:
  ```cmd
  database-update.bat
  ```
* **Hoặc bằng lệnh CLI**:
  ```bash
  dotnet ef database update --project ShuttleVNBackend.Infrastructure --startup-project ShuttleVNBackend.Api
  ```

### 4. Khởi chạy ứng dụng
Chạy trực tiếp qua .NET CLI:
```bash
dotnet run --project ShuttleVNBackend.Api
```

API mặc định sẽ lắng nghe tại:
* HTTP: `http://localhost:5062`
* Tài liệu OpenAPI: `http://localhost:5062/openapi/v1.json` (chỉ ở môi trường Development)

---

## Chạy với Docker

Khởi chạy container nhanh bằng Docker Compose:
```bash
docker compose up -d --build
```
Container API sẽ được map ra cổng `8080:8080` (hoặc cổng cấu hình trong `docker-compose.yml`).

---

## Các tính năng chính

1. **Quản lý Đặt sân & Lịch thi đấu**:
   * Đặt sân theo khoảng thời gian thực tế, hỗ trợ khách vãng lai và thành viên.
   * Chống trùng lịch song song tuyệt đối (PostgreSQL GiST Exclusion Constraint).
   * Vòng đời đơn đặt sân: Chờ duyệt (`Pending`) ➔ Đã duyệt (`Confirmed`) ➔ Hoàn thành (`Completed`) / Đã hủy (`Cancelled`).
   * Tự động giải phóng đơn hết hạn (Pending Booking Expiry Background Service).
2. **Kích hoạt tài khoản & Mật khẩu**:
   * Cơ chế gửi link kích hoạt 24h và đặt mật khẩu qua Email cho khách hàng mới (UC-42).
3. **Báo cáo & Thống kê (Statistics & Dashboard)**:
   * Thống kê doanh thu theo ngày / tháng.
   * Báo cáo tần suất sử dụng sân (Court Usage) và tỷ lệ lấp đầy.
   * API tổng hợp số liệu cho Dashboard quản trị viên.
4. **Tra cứu & Tìm kiếm**:
   * Quick Lookup cho nhân viên: tra cứu nhanh theo mã đơn, số điện thoại, tên khách hàng.
   * Tìm kiếm đơn đặt sân phía khách hàng.
5. **Trợ lý ảo AI (AI Assistant)**:
   * Endpoint tra cứu thông tin `/api/assistant/chat` và luồng thời gian thực `/api/assistant/chat/stream` (Server-Sent Events).
   * Hỗ trợ gọi tool kiểm tra lịch trống và giá sân (`list_courts`, `get_court_availability`).
   * Giới hạn an toàn: tối đa 20 tin nhắn, 2000 ký tự/tin nhắn, rate limit 20 request/phút/IP.