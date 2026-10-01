# AIVES – AI-powered Viva Exam System

Ứng dụng **ASP.NET Core MVC (.NET 10)** theo mô hình 3 tầng, chạy bằng Docker cùng SQL Server 2022.

Login MVC, cookie/JWT, cấu hình cá nhân và các lệnh test SQL/smoke test: xem [docs/AUTH_MVC.md](docs/AUTH_MVC.md).

---

## 1. Cấu trúc solution

```text
AIpoweredVivaExamSystem/
├── AIpoweredVivaExamSystem.Web/            # Presentation: MVC Controllers, Views, ViewModels
├── AIpoweredVivaExamSystem.Application/    # Business: Services, DTO, Validators, interface Repository
├── AIpoweredVivaExamSystem.Domain/         # Business: Entities, Enums, luật nghiệp vụ
├── AIpoweredVivaExamSystem.Persistence/    # Data access: DbContext, Configurations, Repositories, Migrations
├── AIpoweredVivaExamSystem.Infrastructure/ # Dịch vụ ngoài (AI, email) – chưa dùng
├── AIpoweredVivaExamSystem.Tests/          # xUnit
├── docker-compose.yml
├── .env.example
└── AIpoweredVivaExamSystem.slnx
```

Chiều tham chiếu:

```text
Web ──► Application ──► Domain
 └────► Persistence ──► Application, Domain
```

Interface repository nằm ở `Application`, class cài đặt nằm ở `Persistence` (Dependency Inversion).
`Web` đăng ký các tầng qua DI trong `Program.cs` (`AddApplication()`, `AddPersistence(...)`).

Docker Compose chạy 2 container:

```text
aives-web        ASP.NET Core MVC   → http://localhost:8081
aives-sqlserver  SQL Server 2022    → localhost,1433
```

---

## 2. Yêu cầu

- Git, Docker Desktop (đang chạy)
- .NET 10 SDK và Visual Studio 2022+/Rider nếu muốn chạy ngoài Docker

---

## 3. Chạy bằng Docker

1. Tạo file `.env` ở thư mục gốc (không commit), theo mẫu `.env.example`:

   ```env
   DB_PASSWORD=mat_khau_manh_cua_ban
   JWT_KEY=key_ngau_nhien_it_nhat_32_byte
   ```

2. Build và chạy:

   ```powershell
   docker compose up -d --build
   docker compose ps
   ```

3. Mở **http://localhost:8081**.

Trong container, connection string được biến môi trường `ConnectionStrings__DefaultConnection`
(khai báo trong `docker-compose.yml`) ghi đè lên `appsettings.json`, dùng `Server=sqlserver,1433`.

Dừng: `docker compose down`. **Không** dùng `docker compose down -v` nếu muốn giữ dữ liệu.

---

## 4. EF Core Migration

Migration nằm trong `AIpoweredVivaExamSystem.Persistence`, startup project là `AIpoweredVivaExamSystem.Web`.
Ứng dụng **không** tự chạy migration khi khởi động, nên sau khi tạo DB mới cần cập nhật schema.

Khi chạy lệnh từ máy local tới SQL Server trong Docker, truyền connection string qua biến môi trường
(không ghi mật khẩu vào `appsettings.json`):

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=AIpoweredVivaExamSystemDb;User Id=sa;Password=<DB_PASSWORD>;TrustServerCertificate=True;"

dotnet ef database update --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Web
dotnet ef migrations add <TenMigration> --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Web --output-dir Migrations
dotnet ef migrations list --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Web
```

Khi đổi Entity: sửa Domain → sửa Configuration nếu cần → tạo migration → commit cả migration.

---

## 5. Chạy test

Test tích hợp tạo DB tạm `AivesRubricTests_<guid>` rồi xóa sau khi chạy:

```powershell
$env:AIVES_TEST_CONNECTION = "Server=localhost,1433;User Id=sa;Password=<DB_PASSWORD>;TrustServerCertificate=True;"
dotnet test
```

---

## 6. Lệnh thường dùng

| Việc | Lệnh |
|---|---|
| Kiểm tra cú pháp compose | `docker compose config` |
| Xem log web | `docker compose logs -f web` |
| Xem log SQL Server | `docker compose logs -f sqlserver` |
| Restart web | `docker compose restart web` |
| Build lại sau khi đổi code/`.csproj` | `docker compose up -d --build` |

SSMS kết nối bằng `localhost,1433`, user `sa`, mật khẩu là `DB_PASSWORD` trong `.env`.
Nếu port 1433 hoặc 8081 bị trùng, đổi port host trong một file `docker-compose.override.yml` riêng trên máy (không commit).

---

## 7. Quy tắc chung

1. Không commit `.env` và không ghi mật khẩu thật vào source code.
2. Không sửa migration đã merge và dùng chung.
3. Trong container dùng `Server=sqlserver,1433`; từ máy local (SSMS, `dotnet ef`) dùng `localhost,1433`.
