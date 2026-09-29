# Rubric Domain và API

Rubric là bộ tiêu chí chấm điểm của một Question. Một Question có tối đa một
Rubric; một Rubric có ít nhất một tiêu chí.

## Phạm vi

- CRUD Rubric cùng danh sách tiêu chí, lọc theo Question, phân trang.
- Model và mapping Subject, Topic, Question theo ERD để có khóa ngoại thật.
- Chưa có API quản lý Subject/Topic/Question. Cần có Question trong database
  trước khi tạo Rubric (có thể dùng SQL mẫu bên dưới trong môi trường phát triển).
- Chưa tích hợp JWT/phân quyền vì repository chưa triển khai authentication.
- Chưa triển khai AI grading hay khóa rubric sau khi dùng để chấm điểm.

## Luồng xử lý và tệp chính

```text
RubricsController
  → IRubricService / RubricService (Application)
  → Rubric và RubricCriterion (Domain)
  → IRubricRepository / RubricRepository (Persistence)
  → ApplicationDbContext → SQL Server
```

- `Domain/Entities/Rubric.cs`: quy tắc tổng điểm và thay đổi danh sách tiêu chí.
- `Application/Rubrics/DTOs`: request/response; không trả EF entity trực tiếp.
- `Application/Rubrics/RubricValidators.cs`: FluentValidation cho đầu vào.
- `Persistence/Configurations`: tên bảng, kiểu dữ liệu, index, khóa ngoại.
- `Api/Middleware/ApiExceptionHandler.cs`: trả lỗi theo ProblemDetails.
- `AIpoweredVivaExamSystem.Tests`: domain và kiểm thử tích hợp API/SQL Server.

## Quy tắc phiên bản đầu

- `Name` không rỗng, tối đa 255 ký tự.
- `MaxScore` từ 0.01 đến 999.99, tối đa hai chữ số thập phân.
- `TotalScore` tự tính bằng tổng `MaxScore`, không nhận từ client và không vượt 999.99.
- `DisplayOrder` bắt đầu từ 1 và không trùng trong cùng rubric. Không bắt buộc liên tục.
- `QuestionId` không thay đổi qua PUT.
- `Id` tiêu chí do server tạo. POST không được gửi ID tiêu chí.
- PUT gửi **toàn bộ** danh sách: giữ `id` để sửa tiêu chí cũ, bỏ `id` để thêm mới,
  tiêu chí cũ không có trong danh sách sẽ bị xóa. ID không thuộc rubric trả 400.
- Mọi thay đổi rubric/tiêu chí được lưu trong một lần SaveChangesAsync và một transaction.
- DELETE hiện là **xóa cứng** rubric cùng tiêu chí vì chưa có module grading.
  Khi bổ sung AIGradingCriteria, phải dùng khóa ngoại Restrict tới RubricCriterion
  và bổ sung chính sách khóa/sửa rubric đã chấm điểm; không cascade dữ liệu grading.
- `DeletedAt` kế thừa từ lớp chung nhưng Rubric chưa dùng xóa mềm.
- `CreatedAt`/`UpdatedAt` dùng UTC, `datetimeoffset`, giống entity User hiện tại.
  Chưa tự gán `CreatedBy`/`UpdatedBy` khi authentication chưa có.

Database bổ sung `rowversion` trên Rubrics để phát hiện các thao tác ghi chồng nhau;
lỗi xung đột trả 409. Đây không phải ETag: chưa phát hiện mọi trường hợp người dùng
gửi lại dữ liệu cũ sau khi một request trước đó đã hoàn tất.

Enum phụ thuộc lưu bằng tên C# (`Active`, `Remember`, `Medium`, `Manual`, `Draft`),
theo cách UserStatus đang được ánh xạ. Subject/Topic dùng Active hoặc Inactive.
Question có thể không có Topic; nếu có thì Topic phải thuộc cùng Subject.

## Endpoint

| HTTP | URL | Thành công |
|---|---|---|
| POST | `/api/rubrics` | 201 + Location + rubric |
| GET | `/api/rubrics/{id}` | 200 + rubric |
| GET | `/api/rubrics?questionId={id}&page=1&pageSize=20` | 200 + trang kết quả |
| PUT | `/api/rubrics/{id}` | 200 + rubric cập nhật |
| DELETE | `/api/rubrics/{id}` | 204 |

`questionId` là bộ lọc tùy chọn. Page mặc định 1, PageSize mặc định 20, tối đa 100.
Page từ 1 đến 1.000.000. Danh sách sắp xếp theo CreatedAt giảm dần rồi Id;
tiêu chí sắp xếp theo DisplayOrder tăng dần.

Lỗi: 400 đầu vào sai, 404 không tìm thấy Question/Rubric, 409 trùng rubric
hoặc xung đột dữ liệu. GET danh sách không có kết quả trả 200 với `items: []`.

Ví dụ POST (thay questionId bằng ID thật):

```json
{
  "questionId": "11111111-1111-1111-1111-111111111111",
  "name": "Dependency Injection",
  "description": "Chấm kiến thức và ví dụ",
  "criteria": [
    {
      "name": "Định nghĩa",
      "description": "Giải thích đúng khái niệm",
      "expectedConcepts": "Dependency, constructor injection",
      "maxScore": 4,
      "displayOrder": 1
    },
    {
      "name": "Ví dụ",
      "description": null,
      "expectedConcepts": null,
      "maxScore": 6,
      "displayOrder": 2
    }
  ]
}
```

Response có `id`, `questionId`, `name`, `description`, `totalScore`, `createdAt`,
`updatedAt`, `criteria`. Mỗi tiêu chí có `id` để dùng lại khi PUT.

## Migration và chạy

Migration mới `AddRubricsAndQuestionDependencies` tạo Subjects, Topics, Questions,
Rubrics và RubricCriteria. Không sửa InitialCreate hoặc bảng Users.

Với .NET 10 SDK và dotnet-ef 10.0.12, sau khi cấu hình connection string:

```powershell
dotnet ef database update --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Api
docker compose up -d --build
```

Chạy từ Windows dùng `localhost,1433`; từ Docker dùng `sqlserver,1433`.
Không ghi mật khẩu vào source hoặc commit `.env`.
Swagger Docker: http://localhost:8080/swagger

## Tạo Question mẫu bằng SSMS (tùy chọn, chỉ môi trường phát triển)

Script sau tạo Subject, Topic, Question một lần, không tạo Rubric. Dùng QuestionId
trả về để gọi POST. Không có seed tự chạy khi API khởi động.

```sql
USE AIpoweredVivaExamSystemDb;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @SubjectId uniqueidentifier = NEWID();
DECLARE @TopicId uniqueidentifier = NEWID();
DECLARE @QuestionId uniqueidentifier = NEWID();
DECLARE @Now datetimeoffset = SYSDATETIMEOFFSET();

INSERT INTO Subjects (Id, Code, Name, Status, CreatedAt)
VALUES (@SubjectId, CONCAT('DEMO_', CONVERT(varchar(36), @SubjectId)), N'Môn học mẫu', 'Active', @Now);
INSERT INTO Topics (Id, SubjectId, Name, Status, CreatedAt)
VALUES (@TopicId, @SubjectId, N'Dependency Injection', 'Active', @Now);
INSERT INTO Questions (Id, SubjectId, TopicId, Content, BloomLevel, Difficulty, SourceType, Status, CreatedAt)
VALUES (@QuestionId, @SubjectId, @TopicId, N'Giải thích Dependency Injection và cho ví dụ.',
        'Understand', 'Medium', 'Manual', 'Draft', @Now);
COMMIT;
SELECT @QuestionId AS QuestionId;
```

## Kiểm thử

Domain test không cần database:

```powershell
dotnet test AIpoweredVivaExamSystem.Tests --filter FullyQualifiedName~RubricDomainTests
```

Kiểm thử tích hợp cần biến môi trường `AIVES_TEST_CONNECTION`, là connection string
SQL Server có quyền tạo/xóa database. Fixture luôn thay tên database thành
`AivesRubricTests_<guid>`, áp dụng migration, rồi xóa **database test đó** khi kết thúc.
Không dùng database ứng dụng để chứa dữ liệu test.

```powershell
dotnet test AIpoweredVivaExamSystem.Tests
```

Nếu máy chỉ có .NET 9, dùng Docker SDK như sau (PowerShell, tại root repo):

```powershell
docker build --target build -t aives-migration-runner -f AIpoweredVivaExamSystem.Api/Dockerfile .
$settings = docker compose config --format json | ConvertFrom-Json
$env:AIVES_TEST_CONNECTION = $settings.services.api.environment.ConnectionStrings__DefaultConnection
$testNetwork = $settings.networks.'aives-network'.name
try {
    docker run --rm --network $testNetwork --env AIVES_TEST_CONNECTION --mount "type=bind,source=$((Get-Location).Path),target=/workspace" --workdir /workspace aives-migration-runner dotnet test AIpoweredVivaExamSystem.Tests
} finally {
    Remove-Item Env:AIVES_TEST_CONNECTION
}
```

Test bao gồm CRUD thật qua HTTP, FK SQL Server, precision/validation, giữ ID tiêu chí,
đổi thứ tự, audit timestamps, rollback khi lưu thất bại và xung đột ghi đồng thời.
