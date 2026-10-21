# Kiet — group two integration foundation

Branch: `g2-integration`. Base: `main` at `25a77aa` (MVC/group one and the one-day plan). This branch implements Kiet's shared foundation, not the other members' modules.

## Task status and practical limits

## Checklist đối chiếu ngày 19/10/2026

Kết luận: **phần foundation K1–K3 đã có implementation và test; toàn bộ K1–K5 chưa hoàn thành 100%**. K4/K5 còn phụ thuộc code của các module khác. Merge foundation vào main không đồng nghĩa hoàn thành toàn bộ nhóm chức năng 2.

- [x] K1: scaffold Exam, ExamStudent, ExamQuestionPool, VivaSession, SessionQuestion và các Application contracts.
- [x] K2: guard ownership/subject assignment, own enrollment, DI, phân quyền trang MVC và test truy cập trái quyền.
- [x] K3: migration/snapshot, FK/unique indexes/RowVersion/check constraints và fixture database test riêng.
- [x] K4 (foundation): DI, menu, trang đọc trạng thái, transaction boundary và abstraction notifier.
- [ ] K4 (tích hợp cuối): ghép Exam CRUD/lifecycle của Tuệ Minh, pool của Sơn, roster của Long, lịch của Dương và session/SignalR của Dũng; demo tạo exam → publish.
- [x] K5 (foundation): regression nhóm 1 + foundation, kiểm tra migration/model, tài liệu và commit bàn giao.
- [ ] K5 (nghiệm thu cuối): chạy checklist demo toàn luồng sau khi tích hợp đủ module và sửa lỗi liên module.

Bằng chứng: `ExamFoundationTests.cs` kiểm tra quyền, query filters, unique constraints, rollback, concurrency và notification sau commit; `LoginMvcTests.cs` kiểm tra trang quản lý. Notifier thật là `SignalRExamNotifier` của Dũng. Roster, lịch và session đã implement `ModuleContracts.cs`; `IExamService` và `IExamPoolService` chưa có nên chưa thể đánh dấu demo hoàn thành.

### Chi tiết bàn giao

| Task | Delivered | Remaining dependency |
|---|---|---|
| K1 | Five shared entities; validated initial configuration; Application contracts for Exam, pool, roster, scheduling, sessions and notifier; safe student DTO. | Team review of interface changes before implementing each module. |
| K2 | ExamAccessGuard, request-scoped query filters, DI, manager-only integration MVC route, existing HTTP 403/404 mapping and ResourceConflictException convention. Tests cover cross-owner access, unassigned lecturers, own enrollment and student management denial. | New modules must invoke guards on every entry point; filters alone do not authorize writes. |
| K3 | AddExamFoundation migration/snapshot, FKs with restricted delete, unique roster/pool/session/question-position constraints, RowVersion, SQL check constraints; isolated reusable ExamDemo test fixture. | Future schema changes need reviewed migrations. Fixture is test data, not application user provisioning. |
| K4 | DI wired; read-only `/ExamFoundation` and guarded `/ExamFoundation/Counts/{id}`; menu; transactional write boundary; notifier contract plus clearly identified pending adapter. | Exam CRUD, pool, roster, scheduling, session selection and actual SignalR adapter are not delivered by their owners yet. No full publish/demo flow is available. |
| K5 | Group-one regression plus foundation tests, migration/model verification and this handoff. | Final end-to-end demo/review waits for K4 module integration. K4/K5 must not be marked fully complete before that. |

## Shared schema and ownership

- `Exam`: Draft initially; subject/lecturer/name/window/duration/main question/follow-up configuration; rowversion. Tue Minh owns CRUD and lifecycle operations. No API silently sets Published or pretends readiness checks exist.
- `ExamQuestionPool`: unique exam/question, FK to bank question. Son owns eligibility, batch changes and revalidation.
- `ExamStudent`: unique exam/student, nullable ScheduledAt, rowversion. Long owns enrollment; Duong owns scheduling through the agreed service boundary. `Schedule()` only assigns a value; the service must validate lifecycle, overlaps, bounds and ownership.
- `VivaSession`: unique ExamStudentId, start/completion timestamps, rowversion. Dung owns lifecycle, slot checks, locking and idempotent session creation.
- `SessionQuestion`: unique session/question and session/position; question content and JSON rubric snapshots. Dung owns creation and selection. This schema is for main questions; follow-up endpoints remain out of the one-day MVP.

SQL constraints enforce structural integrity, not all application business rules: correct student role, approved pool/rubric, matching subject, valid slot window and scheduling conflicts still require module services and tests. No selection/randomization or enrollment service is implemented here.

## Application contracts and registration

`Application/Exams/ModuleContracts.cs` defines `IExamService`, `IExamPoolService`, `IExamRosterService`, `IExamScheduleService`, `IVivaSessionService` and transport records. These interfaces deliberately have no fake implementation or DI registration: each owner implements them and supplies the registration to Kiet. `ExamConfiguration` includes SubjectId for create; update must reject subject changes. Versioned updates must compare ExpectedVersion with the entity's RowVersion.

Implemented registrations: `ExamAccessGuard`, `ExamFoundationService`, `IExamAccessStore`, `IExamFoundationReader`, `IExamUnitOfWork`, `IExamNotifier` (now `SignalRExamNotifier`, registered in `Program.cs`). `ExamFoundationStore` implements read/access facts, not module mutations.

Guard rules:

1. `CanManageExamAsync`: active request identity must be admin, or lecturer who owns the exam and still has subject assignment. Nonexistent exam → ResourceNotFoundException; denied access → UnauthorizedAccessException, translated to HTTP forbid by the Web exception filter.
2. `RequireSubjectAsync`: call before creating an exam; module must additionally validate subject existence/active status, including for administrators.
3. `OwnExamStudentAsync`: identity must match enrollment.StudentId. Manager inspection uses CanManageExam instead; this method does not grant admin impersonation.
4. `CanReadExamAsync`: manager ownership or student's own enrollment. Student time/lifecycle checks belong to the session service, not this membership-only guard.

Request-aware EF filters make managed exams visible only to their owner in assigned subjects; students can query their enrollment/session but not manager exam lists or raw SessionQuestion/rubric snapshots. Access-store methods bypass filters solely to return minimal access facts to the guard. Direct test/design-time contexts intentionally omit the request identity. Do not use such unrestricted contexts for Web requests.

Student session repositories must first call the own-enrollment guard, then make an explicit scoped query and project only `StudentQuestion`/`StudentSession`. Never serialize SessionQuestion entities or internal rubric snapshots to student HTML/JSON. An `IgnoreQueryFilters()` call requires an explicit authorization guard and must remain inside a repository.

## Transactions and notifications

`IExamUnitOfWork.ExecuteAsync(work, changedExamIds, ct)` opens its own transaction, executes staged mutations, saves and commits, disposes the transaction, then notifies each distinct changed exam. Nested EF/ambient transactions are rejected. DbUpdateConcurrencyException and SQL duplicate/FK/check failures become ResourceConflictException. Failure clears tracked state and rolls back; the supplied work must not perform external calls, independent commits or irreversible side effects.

The unit of work does not implement selection locking, scheduling isolation or automatic retries. Dung/Duong must choose the required locks/isolation with Kiet and test their concurrent operations. Ordinary default isolation plus unique indexes prevents duplicate structural rows but does not on its own prevent every race.

Notification failure after commit is logged and does not report a failed database write. Delivery is best-effort, not durable; clients must reread SQL. `Program.cs` registers Dung's `SignalRExamNotifier`; hub `/hubs/exams`, group `exam:{id}`, event `ExamChanged`.

## Build, migration and isolated demo fixture

```powershell
dotnet build AIpoweredVivaExamSystem.slnx
$env:AIVES_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
dotnet test AIpoweredVivaExamSystem.Tests
```

The fixture creates and removes `AivesRubricTests_<guid>`. `ExamDemo.SeedAsync` in `ExamFoundationTests.cs` creates lecturer/student metadata, assigned subject, Draft exam, enrollment and question only inside that test database. Its noncredential test hash cannot be used to sign in. No production seed endpoint or default account password is introduced.

Review/apply migrations explicitly on a chosen development database:

```powershell
$env:ConnectionStrings__DefaultConnection = '<development connection string>'
dotnet ef database update --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Web
dotnet ef migrations has-pending-model-changes --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Web
```

The host does not apply migrations on startup. The MVC integration page is read-only and intentionally says the module implementations are pending. Existing sign-in requires configured JWT settings as before. No AI key is needed for these foundation tests.

## Merge order for the rest of the team

1. Tue Minh implements Exam service/Razor Pages/ViewModels against ModuleContracts.
2. Son and Long implement pool/roster and provide persistence mappings/registrations.
3. Duong implements slot preview/apply and student schedule, including stale-preview and overlap handling.
4. Dung implements session persistence/selection and replaces PendingExamNotifier with authorized SignalR Hub/client.
5. Kiet integrates migrations and routes, reruns regression, then verifies the full one-day checklist. Until these modules exist, the main workflow demo is blocked by missing implementations, not by this foundation's build.
