# Hướng dẫn sử dụng Docker cho project AIVES

Tài liệu này dành cho các thành viên trong nhóm để có thể clone project, cấu hình môi trường và chạy backend AIVES bằng Docker mà không cần cài SQL Server trực tiếp trên máy.

---

## 1. Kiến trúc Docker hiện tại

Backend AIVES gồm các project:

```text
AIpoweredVivaExamSystem/
├── AIpoweredVivaExamSystem.Api/
├── AIpoweredVivaExamSystem.Application/
├── AIpoweredVivaExamSystem.Domain/
├── AIpoweredVivaExamSystem.Infrastructure/
├── AIpoweredVivaExamSystem.Persistence/
├── docker-compose.yml
├── .dockerignore
├── .env
├── .env.example
├── .gitignore
└── AIpoweredVivaExamSystem.slnx
```

Docker Compose hiện chạy 2 container:

```text
Docker Compose
├── aives-api
│   └── ASP.NET Core Web API (.NET 10)
│
└── aives-sqlserver
    └── SQL Server 2022
```

Các project `Application`, `Domain`, `Infrastructure` và `Persistence` là Class Library nên **không tạo container riêng**. Chúng được build cùng với API.

---

## 2. Yêu cầu trước khi chạy

Mỗi thành viên cần cài:

- Git
- Docker Desktop
- Visual Studio 2022/phiên bản hỗ trợ .NET 10 nếu muốn code bằng Visual Studio
- .NET 10 SDK nếu muốn chạy/build trực tiếp ngoài Docker

Không bắt buộc cài SQL Server local nếu chỉ sử dụng SQL Server container.

Kiểm tra Docker:

```powershell
docker --version
docker compose version
```

Docker Desktop phải đang chạy trước khi dùng các lệnh Docker.

---

## 3. Clone project

Ví dụ:

```powershell
git clone https://github.com/lenguyenanhkiet/AIVES-AI-powered-Viva-Exam-System.git
cd AIpoweredVivaExamSystem
```

Sau khi clone, kiểm tra thư mục có:

```text
docker-compose.yml
AIpoweredVivaExamSystem.Api/
AIpoweredVivaExamSystem.Application/
AIpoweredVivaExamSystem.Domain/
AIpoweredVivaExamSystem.Infrastructure/
AIpoweredVivaExamSystem.Persistence/
```

---

## 4. Tạo file `.env`

File `.env` chứa thông tin bí mật và **không được commit lên Git**.

Tạo file:

```text
AIpoweredVivaExamSystem/.env
```

Ví dụ:

```env
DB_PASSWORD=Aives@2026Strong!
JWT_KEY=replace_with_a_long_random_secret_key_at_least_32_chars
AI_API_KEY=replace_with_real_ai_api_key_when_needed
```

### Lưu ý

- Không push `.env` lên GitHub.
- Có thể tạo `.env.example` để hướng dẫn thành viên khác.
- Khi thêm thông tin key vào file `.env` thì hãy ghi vào `.env.example` rồi push lên github để mọi người có thể lấy.

Ví dụ `.env.example`:

```env
DB_PASSWORD=your_database_password
JWT_KEY=your_jwt_secret
AI_API_KEY=your_ai_api_key
```

---

## 5. Nội dung `docker-compose.yml`

Project sử dụng cấu hình dạng:

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: aives-sqlserver

    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${DB_PASSWORD}"

    ports:
      - "1433:1433"

    volumes:
      - sqlserver_data:/var/opt/mssql

    networks:
      - aives-network

    restart: unless-stopped


  api:
    build:
      context: .
      dockerfile: AIpoweredVivaExamSystem.Api/Dockerfile

    container_name: aives-api

    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ASPNETCORE_URLS: "http://+:8080"

      ConnectionStrings__DefaultConnection: "Server=sqlserver,1433;Database=AIpoweredVivaExamSystemDb;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=True;"

      Jwt__Key: "${JWT_KEY}"
      Jwt__Issuer: "AIpoweredVivaExamSystem"
      Jwt__Audience: "AIpoweredVivaExamSystem"

    `Nếu thêm key mới thì thêm ngay vào đây, value của key nào mà secret thì khai báo ở .env. Ví dụ như ở dòng dưới`
      AI__ApiKey: "${AI_API_KEY}"
    ports:
      - "8080:8080"

    depends_on:
      - sqlserver

    networks:
      - aives-network

    restart: unless-stopped


volumes:
  sqlserver_data:


networks:
  aives-network:
    driver: bridge
```


---

## 6. Kiểm tra cú pháp Docker Compose

Trước khi chạy lần đầu:

```powershell
docker compose config
```

Nếu không báo lỗi YAML thì cấu hình hợp lệ.

Nếu gặp lỗi như:

```text
services.container_name must be a mapping
```

thì nguyên nhân thường là sai thụt lề trong `docker-compose.yml`.

YAML nên dùng khoảng trắng, không dùng tab.

---

## 7. Build và chạy project

Đứng tại thư mục chứa `docker-compose.yml`:

```powershell
docker compose up -d --build
```

Ý nghĩa:

- `up`: tạo/chạy container
- `-d`: chạy background
- `--build`: build lại image API

Lần đầu có thể mất vài phút vì Docker phải tải image .NET và SQL Server.

---

## 8. Kiểm tra trạng thái container

```powershell
docker compose ps
```

Kết quả mong đợi:

```text
NAME               STATUS
aives-api          Up
aives-sqlserver    Up
```

Có thể kiểm tra trực tiếp:

```powershell
docker ps
```

---

## 9. Xem log

### Log API

```powershell
docker compose logs api
```

Theo dõi realtime:

```powershell
docker compose logs -f api
```

### Log SQL Server

```powershell
docker compose logs sqlserver
```

Theo dõi realtime:

```powershell
docker compose logs -f sqlserver
```

Thoát chế độ xem log realtime bằng:

```text
Ctrl + C
```

Việc này không dừng container.

---

## 10. Truy cập API

API được map:

```text
http://localhost:8080
```

Nếu project sử dụng built-in OpenAPI:

```text
http://localhost:8080/openapi/v1.json
```

Nếu project sử dụng Swagger UI:

```text
http://localhost:8080/swagger
```

---

## 11. Kết nối SQL Server bằng SSMS

SQL Server container expose port:

```text
1433
```

Khi kết nối từ **SSMS trên máy Windows**, dùng:

```text
Server Name: localhost,1433
Authentication: SQL Server Authentication
User Name: sa
Password: giá trị DB_PASSWORD trong .env
Trust Server Certificate: bật
```

Ví dụ:

```text
Server: localhost,1433
User: sa
Password: Aives@2026Strong!
```

---

## 12. Kiểm tra port SQL Server

Nếu SSMS không kết nối được:

```powershell
Test-NetConnection localhost -Port 1433
```

Nếu thành công:

```text
TcpTestSucceeded : True
```

Nếu là:

```text
TcpTestSucceeded : False
```

hãy kiểm tra:

```powershell
docker compose ps
docker compose logs sqlserver
```

---

## 13. Dừng project

```powershell
docker compose down
```

Lệnh này:

- dừng container
- xóa container/network được Compose tạo
- **không xóa SQL Server volume**

Dữ liệu database vẫn còn.

---

## 14. Khởi động lại

Sau khi đã build một lần:

```powershell
docker compose up -d
```

Nếu có thay đổi code backend và muốn build lại:

```powershell
docker compose up -d --build
```

---

## 15. Restart một service

Restart API:

```powershell
docker compose restart api
```

Restart SQL Server:

```powershell
docker compose restart sqlserver
```

---

## 16. Xóa toàn bộ database Docker

Chỉ sử dụng khi thực sự muốn reset database:

```powershell
docker compose down -v
```

`-v` sẽ xóa `sqlserver_data`.

**Cảnh báo:** toàn bộ dữ liệu SQL Server trong Docker volume sẽ bị mất.

Sau đó chạy lại:

```powershell
docker compose up -d --build
```

---

## 17. Workflow khi code hằng ngày

Thông thường:

### Bước 1: Pull code

```powershell
git pull
```

### Bước 2: Build và chạy

Nếu có thay đổi backend/Dockerfile/package:

```powershell
docker compose up -d --build
```

Nếu không có thay đổi cần rebuild image:

```powershell
docker compose up -d
```

### Bước 3: Kiểm tra

```powershell
docker compose ps
```

### Bước 4: Code và test

Kiểm tra API:

```text
http://localhost:8080
```

Kiểm tra database qua SSMS:

```text
localhost,1433
```

### Bước 5: Khi làm xong

```powershell
docker compose down
```

---

## 18. Khi thêm NuGet package

Ví dụ thêm package vào `Persistence`:

```powershell
dotnet add AIpoweredVivaExamSystem.Persistence package Microsoft.EntityFrameworkCore.SqlServer
```

Sau đó nên rebuild image:

```powershell
docker compose up -d --build
```

Vì file `.csproj` đã thay đổi.

---

## 19. EF Core Migration

Migration nằm trong:

```text
AIpoweredVivaExamSystem.Persistence
```

Startup project:

```text
AIpoweredVivaExamSystem.Api
```

### Tạo migration

```powershell
dotnet ef migrations add InitialCreate --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Api --output-dir Migrations
```

### Update database

Nếu chạy EF từ máy local và muốn kết nối SQL Docker, connection string local phải dùng:

```text
Server=localhost,1433
```

Nếu migration được thực thi từ API container thì connection string sử dụng:

```text
Server=sqlserver,1433
```

### Xem migration

```powershell
dotnet ef migrations list --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Api
```

### Xóa migration cuối chưa apply

```powershell
dotnet ef migrations remove --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Api
```

---

## 20. Khi thay đổi Entity/Database

Ví dụ thêm entity hoặc chỉnh field:

1. Pull code mới nhất.
2. Chỉnh Domain Entity.
3. Chỉnh EF Configuration nếu cần.
4. Tạo migration mới.
5. Commit cả migration.
6. Các thành viên khác pull code.
7. Rebuild/run Docker.
8. Apply migration vào database.

Không nên để mỗi người tự tạo migration cho cùng một thay đổi vì dễ conflict.

Nên thống nhất người chịu trách nhiệm migration/database trong team.

---

## 21. Kiểm tra Dockerfile

Dockerfile nằm tại:

```text
AIpoweredVivaExamSystem.Api/Dockerfile
```

Vì API reference nhiều Class Library, build context phải là root:

```yaml
build:
  context: .
  dockerfile: AIpoweredVivaExamSystem.Api/Dockerfile
```

Không đổi thành:

```yaml
context: ./AIpoweredVivaExamSystem.Api
```

nếu Dockerfile cần copy `Application`, `Domain`, `Infrastructure`, `Persistence`.

---

## 22. `.dockerignore`

Nên có:

```text
**/bin
**/obj
.vs
.git
.env
```

Mục đích:

- giảm kích thước build context
- build nhanh hơn
- không copy secret `.env` vào image

---

## 23. `.gitignore`

Tối thiểu:

```gitignore
.vs/
**/bin/
**/obj/
.env
*.user
*.suo
*.log
TestResults/
.DS_Store
Thumbs.db
```

Không commit:

```text
.env
.vs/
bin/
obj/
```

---

## 24. Một số lỗi thường gặp

### Lỗi YAML

```text
services.container_name must be a mapping
```

Nguyên nhân:

- sai indentation
- property không nằm dưới đúng service
- dùng tab

Kiểm tra:

```powershell
docker compose config
```

---

### API không kết nối được SQL Server

Sai:

```text
Server=localhost,1433
```

trong container API.

Đúng:

```text
Server=sqlserver,1433
```

---

### SSMS không kết nối được

Trong SSMS phải dùng:

```text
localhost,1433
```

không phải:

```text
sqlserver,1433
```

Kiểm tra:

```powershell
docker compose ps
Test-NetConnection localhost -Port 1433
docker compose logs sqlserver
```

---

### Port 1433 đã được sử dụng

Kiểm tra:

```powershell
netstat -ano | findstr :1433
```

Có thể do SQL Server local đang chạy.

Một phương án là đổi port host:

```yaml
ports:
  - "14330:1433"
```

Khi đó SSMS dùng:

```text
localhost,14330
```

API container vẫn dùng:

```text
sqlserver,1433
```

---

### Port 8080 bị trùng

Có thể đổi:

```yaml
ports:
  - "8081:8080"
```

Khi đó truy cập API:

```text
http://localhost:8081
```

---

### API container bị restart liên tục

Kiểm tra:

```powershell
docker compose logs -f api
```

Thường do:

- app crash khi startup
- connection string sai
- migration lỗi
- thiếu environment variable
- compile/runtime error

---

## 25. Các lệnh Docker thường dùng

| Mục đích | Lệnh |
|---|---|
| Build và chạy | `docker compose up -d --build` |
| Chạy không rebuild | `docker compose up -d` |
| Xem container | `docker compose ps` |
| Xem log API | `docker compose logs -f api` |
| Xem log DB | `docker compose logs -f sqlserver` |
| Dừng | `docker compose down` |
| Restart API | `docker compose restart api` |
| Build riêng API | `docker compose build api` |
| Xóa DB volume | `docker compose down -v` |
| Kiểm tra Compose | `docker compose config` |

---

## 26. Quy tắc làm việc chung cho team

1. Không commit file `.env`.
2. Không hard-code password/database secret trong source code.
3. Không sửa migration đã được merge và sử dụng chung nếu không thật sự cần.
4. Khi `.csproj` hoặc Dockerfile thay đổi, chạy lại `docker compose up -d --build`.
5. Trước khi báo lỗi Docker, luôn kiểm tra:
   ```powershell
   docker compose ps
   docker compose logs api
   docker compose logs sqlserver
   ```
6. API container kết nối DB bằng `sqlserver,1433`.
7. SSMS trên máy kết nối DB bằng `localhost,1433`.
8. Không dùng `docker compose down -v` nếu cần giữ dữ liệu.
9. Commit `docker-compose.yml`, `.dockerignore`, `.env.example`.
10. Không commit `.env`.

---

## 27. Quick Start cho thành viên mới

Sau khi clone:

```powershell
cd AIpoweredVivaExamSystem
```

Tạo `.env`:

```env
DB_PASSWORD=Aives@2026Strong!
JWT_KEY=replace_with_a_long_secret_key
AI_API_KEY=replace_when_needed
```

Kiểm tra:

```powershell
docker compose config
```

Build và chạy:

```powershell
docker compose up -d --build
```

Kiểm tra:

```powershell
docker compose ps
```

API:

```text
http://localhost:8080
```

SQL Server trong SSMS:

```text
Server: localhost,1433
Username: sa
Password: DB_PASSWORD trong .env
```

Khi làm xong:

```powershell
docker compose down
```

---

## 28. Tóm tắt kiến trúc kết nối

```text
                         Internet
                            |
                   AI / Speech APIs
                            |
                            v
+---------------------------------------------------+
|                 Docker Compose                    |
|                                                   |
|   +---------------------+                         |
|   |      aives-api      |                         |
|   | ASP.NET Core .NET10 |                         |
|   |                     |                         |
|   | Server=sqlserver    |                         |
|   +----------+----------+                         |
|              |                                    |
|              v                                    |
|   +---------------------+                         |
|   |  aives-sqlserver    |                         |
|   |   SQL Server 2022   |                         |
|   +----------+----------+                         |
|              | 1433                               |
+--------------+------------------------------------+
               |
               v
         localhost:1433
               |
              SSMS
```

---

**Mục tiêu:** một thành viên mới chỉ cần clone repository, tạo `.env` và chạy:

```powershell
docker compose up -d --build
```

là có thể chạy backend và SQL Server của project AIVES.
