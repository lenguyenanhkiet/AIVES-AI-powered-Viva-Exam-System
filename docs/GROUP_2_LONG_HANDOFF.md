# Long — quản lý roster kỳ thi

Branch: `feature/exam-roster`, base `main` tại `01e4d7d`.

## Trạng thái bàn giao

Đã triển khai module L1–L4: Application service, SQL repository, MVC, phân quyền,
transaction và kiểm thử tự động. Chưa đánh dấu hoàn thành demo tích hợp toàn nhóm:
Exam CRUD/lifecycle, scheduling và SignalR thật cần ghép với các branch tương ứng.

| Task | Đã kiểm chứng | Phần cần phối hợp |
|---|---|---|
| L1 | Thêm bằng email chuẩn hóa; list/remove; chỉ nhận STUDENT active, chưa xóa; chặn trùng và sai ownership | Dùng tài khoản đã có, không tạo tài khoản/import |
| L2 | `/ExamStudents`, `/ExamStudents/Roster/{examId}`; tên/email/giờ ca UTC+7; thêm 3 sinh viên qua HTTP MVC, bỏ sinh viên, giữ email khi lỗi | Demo trình duyệt với dữ liệu dev sau khi có Exam CRUD |
| L3 | Chỉ sửa Draft; không bỏ enrollment đã có session; counts SQL phản ánh roster; không ghi đè giờ ca của người khác | Tuệ Minh dùng counts cho publish; Dương dùng roster và RowVersion cho preview |
| L4 | Test duplicate/role/ownership/concurrency/rollback; notifier sau commit; delivery lỗi không rollback SQL | Main đang dùng PendingExamNotifier; SignalR thật chờ tích hợp Dũng |

## Contracts và đường đi dữ liệu

- `IExamRosterService` trong `Application/Exams/ModuleContracts.cs`: giữ nguyên
  `ListAsync(examId, ct)`, `AddAsync(examId, studentEmail, ct)`,
  `RemoveAsync(examId, enrollmentId, ct)`.
- `RosterEntry` có thêm tham số cuối `string Email = ""`; các constructor 5 tham số
  vẫn biên dịch. Các entry sắp theo StudentId để Dương dùng thứ tự ổn định.
- `IExamRosterReader` thuộc `Application/ExamStudents`: metadata có guard và danh
  sách kỳ thi phân trang 20 dòng. Trang chi tiết truy vấn theo ID trực tiếp.
- `ExamRosterService` kiểm tra `ExamAccessGuard` ở mọi entry point; repository giữ
  query filters của request. Controller không truy vấn DbContext.
- `IExamRosterStore` chỉ stage add/remove. `IExamUnitOfWork.ExecuteAsync` sở hữu
  SaveChanges/transaction và gọi `NotifyChangedAsync(examId)` sau commit.
- Duplicate trả `ResourceConflictException`, không tạo thêm dòng. Email sai,
  không tồn tại hoặc user không hợp lệ trả `DomainValidationException`. Enrollment
  không thuộc exam trả `ResourceNotFoundException`. Trái quyền dùng cơ chế forbid
  hiện tại; MVC cookie chuyển đến trang 403.

## Đồng bộ với lịch, publish và session

Mutation khóa hàng exam bằng `UPDLOCK,HOLDLOCK` trong transaction chung, sau đó
đọc lại trạng thái/quyền rồi mới kiểm tra roster. Thứ tự khóa exam trước enrollment
phù hợp contract của Dũng. Unique `(ExamId, StudentId)` có sẵn trong foundation.

Add/remove cập nhật `Exam.UpdatedAt`, làm SQL sinh RowVersion mới. Dương cần đưa
RowVersion exam và roster vào preview token, đọc lại trước Apply và dùng cùng thứ
tự khóa. Module roster không tự xếp lại lịch và không sửa ScheduledAt của những
enrollment còn lại. Sinh viên mới có ScheduledAt null, vì vậy readiness phải kiểm
tra cả số sinh viên và số đã xếp lịch.

Tuệ Minh có thể dùng `ExamFoundationService.CountsAsync` để lấy `Students` và
`ScheduledStudents` từ SQL. Publish cần `Students > 0` và mọi ca hợp lệ; roster
không triển khai endpoint publish hoặc thay thế kiểm tra chồng giờ của Dương.
Lifecycle và schedule phải kiểm tra lại dữ liệu trong transaction; không dùng
counts/preview cũ làm căn cứ commit.

Thông báo dùng contract chung `ExamChanged` thông qua notifier, không tạo event
`RosterChanged` riêng. Không gửi email, câu hỏi hoặc dữ liệu sinh viên qua payload.
Main hiện chỉ ghi log; tích hợp adapter SignalR của Dũng sẽ dùng cùng điểm gọi.

## Các file chung cần Kiệt biết

- Application DI đăng ký `ExamRosterService`, `IExamRosterService`, `IExamRosterReader`.
- Persistence DI đăng ký `IExamRosterStore` → `ExamRosterStore`.
- Layout có thêm menu **Sinh viên kỳ thi**, chỉ hiện cho ADMIN/LECTURER.
- `RosterEntry` thêm Email tương thích constructor cũ.
- Không đổi entity/schema/DbContext/migration/snapshot/Program.cs hoặc phiên bản .NET.
  Branch giữ .NET 10 theo main; cần thống nhất khi tích hợp branch Dũng dùng .NET 9.

Các comment trong service/store/controller giải thích lý do kiểm tra quyền,
khóa SQL, cập nhật version và ranh giới commit để thành viên khác đọc được.

## Kiểm thử và cách chạy

Từ thư mục repository, sau khi cấu hình môi trường theo README:

```powershell
dotnet restore AIpoweredVivaExamSystem.slnx
dotnet build AIpoweredVivaExamSystem.slnx --no-restore
$env:AIVES_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
dotnet test AIpoweredVivaExamSystem.Tests --no-restore
# Chỉ chạy module roster:
dotnet test AIpoweredVivaExamSystem.Tests --no-restore --filter 'FullyQualifiedName~ExamRoster'
```

Kết quả ngày 20/10/2026: **121 passed, 0 failed, 0 skipped**, gồm **22 test SQL
roster + 7 test HTTP roster + 92 test hiện có**. Fixture migrate một DB riêng
`AivesRubricTests_<guid>` và xóa khi kết thúc; không thay dữ liệu ứng dụng.

Đã kiểm tra: email/role/status/deleted; owner/admin/revoked assignment; tất cả
trạng thái không phải Draft; session và enrollment khác exam; hai request thêm
đồng thời; RowVersion; rollback sau SQL save trước commit; notifier đọc được SQL
đã commit; notifier lỗi vẫn giữ dữ liệu; counts và lịch cũ; cookie login thật,
antiforgery, POST-only, lỗi form, link kỳ thi cũ và phân trang hơn 100 kỳ thi.

Build/restore còn cảnh báo NU1902/NU1903 từ dependency có sẵn
`System.Security.Cryptography.Pkcs` 9.0.11. Module này không thay package/version.

Để demo trên ứng dụng: tạo Draft qua module Exam, đăng nhập lecturer owner hoặc
admin, mở menu Sinh viên kỳ thi, thêm email của 3 STUDENT active rồi xem/bỏ từng
người. Sau publish trang chỉ đọc và server từ chối form cũ. Không seed exam hoặc
tài khoản đăng nhập vào DB ứng dụng trong branch này.

## Phạm vi còn lại

Không có import CSV/XLSX, cấp tài khoản, chỉnh lịch, màn hình thi hoặc SignalR
adapter riêng. HTTP test dùng host MVC thật nhưng chưa phải demo trình duyệt toàn
luồng hoặc kiểm tra hai cửa sổ SignalR. Nghiệm thu cuối cần Kiệt ghép đủ module
và chạy checklist nhóm 2 với dữ liệu dev đã thống nhất.
