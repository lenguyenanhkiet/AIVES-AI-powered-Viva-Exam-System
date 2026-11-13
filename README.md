# AIVES – AI-powered Viva Exam System

Ứng dụng **ASP.NET Core Razor Pages (.NET 10)** + SignalR theo mô hình 3 tầng, chạy bằng Docker cùng SQL Server 2022.

Đăng nhập, cookie/JWT, cấu hình cá nhân và các lệnh test SQL/smoke test: xem [docs/AUTH.md](docs/AUTH.md).

Nhóm chức năng 1 (import CSV/XLSX, PDF/PPTX, AI/RAG có nguồn, RBAC/ownership): xem [hướng dẫn thiết lập](docs/GROUP_1_SETUP.md).
Cấu trúc folder: [ARCHITECTURE.md](docs/ARCHITECTURE.md). Phân công nhóm 2 cho 6 người tại [GROUP_2_TEAM_BACKLOG.md](docs/GROUP_2_TEAM_BACKLOG.md). Nhóm 2 trên `main`:

| Module | Route | Bàn giao |
|---|---|---|
| Foundation: schema, contracts, RBAC, transaction | `/ExamFoundation` | [Kiệt](docs/GROUP_2_KIET_HANDOFF.md) |
| Roster sinh viên | `/ExamStudents` | [Long](docs/GROUP_2_LONG_HANDOFF.md) |
| Xếp lịch, lịch cá nhân (sinh viên xem ca ở `/VivaSessions`) | `/Schedules`, `/VivaSessions` | [Dương](docs/GROUP_2_DUONG_HANDOFF.md) |
| Pool câu hỏi, kiểm tra eligibility, thêm/bỏ theo lô | `/ExamPools` | Sơn |
| Session, chọn câu, SignalR dashboard | `/VivaSessions`, `/ExamFoundation/Details/{id}` | [Dũng](docs/GROUP_2_DUNG_HANDOFF.md) |

Exam CRUD/lifecycle, phỏng vấn AI và chấm điểm đã có trong luồng demo.

---

## 1. Cấu trúc solution

```text
AIpoweredVivaExamSystem/
├── AIpoweredVivaExamSystem.Presentation/ # Razor Pages, ViewModels, cookie, SignalR
├── AIpoweredVivaExamSystem.Business/     # Services, validation, gọi AI, khởi tạo nghiệp vụ
├── AIpoweredVivaExamSystem.DataAccess/   # Entities, DTO, repository, DbContext, file, migrations
├── AIpoweredVivaExamSystem.Tests/          # xUnit
├── docker-compose.yml
├── .env.example
└── AIpoweredVivaExamSystem.slnx
```

Chiều tham chiếu:

```text
Presentation ──► Business ──► DataAccess
```

DataAccess không reference project khác. Presentation gọi service Business, không gọi repository hay DbContext.
Cách đăng ký DI và trách nhiệm từng tầng: [ARCHITECTURE.md](docs/ARCHITECTURE.md).

Docker Compose chạy 2 container:

```text
aives-web        Razor Pages       → http://localhost:8081
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
   AI_API_KEY=your_zai_key
   ELEVENLABS_API_KEY=your_optional_elevenlabs_key
   ```

2. Build và chạy:

   ```powershell
   docker compose up -d --build
   docker compose ps
   ```

3. Mở **http://localhost:8081**.

AI uses z.ai's `glm-5.3-flash` by default; `AI_MODEL` and `AI_BASE_URL` override the model and OpenAI-compatible endpoint. Blank overrides keep the defaults. Optional `ELEVENLABS_API_KEY` enables Vietnamese/English examiner audio and Scribe speech recognition; otherwise the browser reads questions aloud and uses built-in recognition when available. See [provider and voice settings](docs/GROUP_1_SETUP.md#interview-voice-and-answer-timing) for voice and Scribe model overrides and answer time limits. Outside Docker use `Ai__ApiKey` and `ElevenLabs__ApiKey`; the app does not load `.env` itself.

Lecturers review transcripts, authorised audio and AI score suggestions at `/Grading`, then save a draft or explicitly finalise their own scores. Students see only finalised totals and lecturer comments. Apply the new grading migration first; see [lecturer grading](docs/GROUP_1_SETUP.md#lecturer-grading-and-student-results).

For a repeatable local PRN222 demonstration with accounts, recordings and grading scenarios, see the [panel demo run sheet](docs/GROUP_1_SETUP.md#panel-demo).

Trong container, connection string được biến môi trường `ConnectionStrings__DefaultConnection`
(khai báo trong `docker-compose.yml`) ghi đè lên `appsettings.json`, dùng `Server=sqlserver,1433`.

Dừng: `docker compose down`. **Không** dùng `docker compose down -v` nếu muốn giữ dữ liệu.

---

## 4. EF Core Migration

Migration nằm trong `AIpoweredVivaExamSystem.DataAccess`; design-time factory tạo DbContext trực tiếp, không khởi động web host.
Ứng dụng **không** tự chạy migration khi khởi động, nên sau khi tạo DB mới cần cập nhật schema.
Cần `dotnet-ef` 10.x vì project dùng EF Core 10; bản 9.x không chạy được: `dotnet tool update -g dotnet-ef --version "10.*"`.

Khi chạy lệnh từ máy local tới SQL Server trong Docker, truyền connection string qua biến môi trường
(không ghi mật khẩu vào `appsettings.json`):

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=AIpoweredVivaExamSystemDb;User Id=sa;Password=<DB_PASSWORD>;TrustServerCertificate=True;"

dotnet ef database update --project AIpoweredVivaExamSystem.DataAccess
dotnet ef migrations add <TenMigration> --project AIpoweredVivaExamSystem.DataAccess --output-dir Migrations
dotnet ef migrations list --project AIpoweredVivaExamSystem.DataAccess
```

Khi đổi Entity: sửa Entity trong DataAccess → sửa Configuration nếu cần → tạo migration → commit cả migration.

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

## 8. Roster sinh viên kỳ thi — nhóm 2

Menu **Sinh viên kỳ thi** (`/ExamStudents`) dành cho lecturer/admin: chọn kỳ thi,
thêm STUDENT active bằng email có sẵn, xem tên/email/giờ ca Việt Nam và bỏ sinh viên.
Chỉ thay đổi roster khi Draft; không bỏ người đã có session. Module không tạo
tài khoản hoặc import CSV/XLSX. Xem [bàn giao roster của Long](docs/GROUP_2_LONG_HANDOFF.md)
để biết contracts, kiểm thử và các bước tích hợp lịch/publish/SignalR.
