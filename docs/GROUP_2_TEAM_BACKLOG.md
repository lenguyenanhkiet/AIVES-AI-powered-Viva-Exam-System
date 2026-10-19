# Nhóm chức năng 2 — kế hoạch hoàn thiện MVP trong 1 ngày

## Mục tiêu và giới hạn cam kết

**Thành viên:** Kiệt, Tuệ Minh, Sơn, Long, Dương, Dũng.

**Thời lượng:** 08:00–17:00, nghỉ trưa 12:00–13:00, tổng cộng 8 giờ/người. Đây là lịch tương đối cho ngày nhóm chọn triển khai, theo giờ Việt Nam, không phải lịch hẹn đã được tạo.

**Kết quả cuối ngày:** demo được luồng tạo kỳ thi → chọn question pool → thêm sinh viên → xếp lịch → publish/start → sinh viên mở bộ câu hỏi đã lưu → refresh không đổi câu → giảng viên thấy cập nhật real-time → hoàn thành kỳ thi.

Để khả thi trong một ngày, kế hoạch chốt **MVP** với một giảng viên, một kênh thi tuần tự, một môn mỗi kỳ thi, tài khoản sinh viên đã có và dữ liệu demo nhỏ. Không cam kết hoàn thành toàn bộ backlog mở rộng trong một ngày. Ước lượng giả định cả 6 người có thể làm việc tập trung, máy đã chạy được .NET/SQL Server và nhóm 1 đã được cấu hình; nếu không đạt mốc 10:00 hoặc 14:00 phải giảm phạm vi theo mục dự phòng.

Đây là tài liệu phân công, **chưa triển khai code nhóm 2**. Nhóm 1/MVC là nền tảng đã có. Tuân thủ [MVC_ARCHITECTURE.md](MVC_ARCHITECTURE.md), dùng Controllers + Razor Views + ViewModels, không thêm PageModel.

## Phạm vi bắt buộc và phần hoãn

| Bắt buộc hoàn thành trong ngày | Hoãn sau MVP |
|---|---|
| Exam CRUD cơ bản, Draft/Published/Ongoing/Completed/Cancelled | Xóa vĩnh viễn kỳ thi đã có dữ liệu, mở lại kỳ thi đã đóng |
| Cấu hình số câu chính, thời lượng, MaxFollowUpPerQuestion | AI sinh follow-up và chấm điểm câu trả lời |
| Pool chỉ nhận câu Approved, đúng môn, có rubric | Cân bằng nâng cao theo Bloom/độ khó, chia sẻ ngân hàng giữa giảng viên |
| Thêm/bỏ sinh viên thủ công, không trùng | Import roster CSV/XLSX, tạo tài khoản hàng loạt |
| Xếp ca tuần tự, preview và lưu, lịch cá nhân | Calendar kéo/thả, nhiều phòng/kênh song song, tối ưu lịch |
| Session/câu hỏi lưu SQL, request lặp không random lại | Lưu đáp án, media, resume timer và toàn bộ màn hình thi hoàn chỉnh |
| Kiểm tra quyền, ownership, slot thi ở server | Trang quản trị quyền mới ngoài phần nhóm 1 đã có |
| Một event SignalR để dashboard đọc lại trạng thái SQL | Outbox, hàng đợi, notification bền vững, dashboard nhiều widget |
| Migration và kiểm thử các rủi ro chính | Bộ end-to-end toàn diện, load test, UI polish |

MVP vẫn lưu MaxFollowUpPerQuestion nhưng **không cung cấp endpoint tạo follow-up**; vì vậy không được báo cáo tính năng follow-up đã hoàn thành. Tránh trùng với bộ ngay trước là ưu tiên khi đủ pool; không trùng trong cùng session và refresh ổn định là yêu cầu bắt buộc.

## Phân công tổng quan

Phân công dưới đây là đề xuất theo module, không giả định năng lực hay vai trò sẵn có của từng người. Kiệt được giao tích hợp để mỗi file dùng chung có một người chịu trách nhiệm cuối cùng.

| Người | Trách nhiệm chính | Kết quả phải bàn giao trước 15:30 | Branch gợi ý |
|---|---|---|---|
| Kiệt | Contracts, schema/migration, RBAC, tích hợp | Model thống nhất, DI, migration, kiểm tra ownership, build tích hợp | `codex/g2-integration` |
| Tuệ Minh | Exam CRUD và lifecycle | Tạo/sửa/xem kỳ thi; publish/start/complete/cancel có validation | `codex/g2-exams` |
| Sơn | Question pool | Chọn/bỏ câu hợp lệ, đếm pool đủ điều kiện | `codex/g2-question-pool` |
| Long | Roster sinh viên | Thêm/bỏ/xem danh sách sinh viên; chặn trùng và sai role | `codex/g2-roster` |
| Dương | Lịch thi | Sinh ca tuần tự, preview/lưu, lịch cá nhân | `codex/g2-scheduling` |
| Dũng | Session, chọn câu và SignalR | Bộ câu lưu idempotent, trang mở lại session, dashboard cập nhật | `codex/g2-session-realtime` |

Tên branch chỉ là gợi ý, chưa được tạo tự động. Không thêm lại import/RAG nhóm 1 vào công việc của ngày này.

## Hợp đồng chung phải chốt lúc 08:30

Kiệt chủ trì 30 phút đầu, cả nhóm xác nhận interface trước khi viết implementation:

| Entity/contract | Nội dung tối thiểu và người phụ trách |
|---|---|
| Exam | Id, SubjectId, LecturerId, Name, StartAt, EndAt, DurationMinutes, NumberOfMainQuestions, MaxFollowUpPerQuestion, Status, RowVersion. Kiệt scaffold, Tuệ Minh hoàn thiện invariant/lifecycle. |
| ExamQuestionPool | Id, ExamId, QuestionId; unique (ExamId, QuestionId). Kiệt scaffold, Sơn hoàn thiện mapping/logic. |
| ExamStudent | Id, ExamId, StudentId, ScheduledAt nullable; unique (ExamId, StudentId). Long sở hữu roster, Dương sở hữu thao tác xếp giờ qua service, không cùng sửa entity tùy ý. |
| VivaSession | Id, ExamStudentId, StartedAt, CompletedAt nullable; unique ExamStudentId. Kiệt scaffold, Dũng hoàn thiện. |
| SessionQuestion | Id, VivaSessionId, QuestionId, Position, ContentSnapshot, RubricSnapshot; unique (session, position) và (session, question). Kiệt scaffold, Dũng hoàn thiện. |
| Current user/quyền | Tái dùng ICurrentUser. Lecturer chỉ quản lý exam của mình trong môn được phân công; student chỉ đọc roster/lịch/session của mình; admin quản trị toàn bộ. Kiệt cung cấp guard dùng chung. |
| Pool eligibility | Câu Approved, đúng môn, chưa xóa, có rubric, lecturer có quyền sử dụng theo nhóm 1. Sơn cung cấp truy vấn và count dùng lại cho publish/selection. |
| IExamNotifier | `NotifyChangedAsync(examId, cancellationToken)` trong Application. Dũng cung cấp adapter Web. Các service gọi sau commit; payload chỉ có examId, không mang câu hỏi/đáp án. |
| Schedule rule | Một exam = một hàng ca tuần tự, không nghỉ giữa ca trong MVP. Mỗi ca = DurationMinutes. Không ghi đè ca đã bắt đầu; kiểm tra chồng giờ student/lecturer trong SQL. |
| Thời gian | Lưu DateTimeOffset/UTC; form chuyển giờ Việt Nam rõ ràng. Không dùng timezone mặc định của server. |

Chốt lifecycle: Draft → Published → Ongoing → Completed; Draft/Published/Ongoing có thể Cancelled. Publish yêu cầu pool đủ, có roster và mọi sinh viên đã có ca hợp lệ. Start là thao tác thủ công trong cửa sổ thời gian; exam Completed/Cancelled không mở lại. Complete chỉ khi mọi session đã bắt đầu đều đóng; sinh viên chưa bắt đầu không được mở session mới sau khi exam đóng.

## Task của Kiệt — tích hợp, quyền và database

| Mã | Thời gian dự kiến | Mô tả thực hiện | Tiêu chí bàn giao |
|---|---|---|---|
| K1 | 08:00–09:00 | Chốt contracts 08:30; scaffold 5 entity và interface dùng chung, ghi quyết định vào tài liệu. Bàn giao sớm entity cho các bạn viết service/ViewModel. | Cả nhóm build được với contracts; không chờ module khác mới bắt đầu. Các thay đổi interface sau 09:00 phải báo cả nhóm. |
| K2 | 09:00–10:00 | Cung cấp guard CanManageExam/OwnExamStudent dựa trên user hiện tại; đăng ký DI chung, quy ước lỗi 403/404/conflict. | Test lecturer khác không truy cập exam bằng ID, student không gọi mutation quản lý. |
| K3 | 10:00–12:00 | Ghép cấu hình EF từ các module, tạo migration trên branch tích hợp, thêm unique indexes/FK/RowVersion. Cung cấp dữ liệu demo bằng fixture/seed dành riêng môi trường dev. | Migration chạy trên DB tạm; không đổi hoặc xóa dữ liệu nhóm 1; snapshot đúng model. Không hardcode credential. |
| K4 | 13:00–15:30 | Tích hợp lần lượt Exam → pool/roster → lịch → session/SignalR; xử lý DI, route, menu và conflict file chung. | Build tích hợp qua 14:00; demo bước tạo exam đến publish qua 15:30. Ghi blocker cụ thể cho từng người. |
| K5 | 15:30–17:00 | Chạy checklist tích hợp cùng cả nhóm, sửa lỗi chặn demo, rà migration/README và tạo commit bàn giao. | Có kết quả test và hướng dẫn chạy; working tree không chứa secret; không merge khi còn lỗi quyền hoặc mất dữ liệu. |

**File Kiệt sở hữu:** `Program.cs`, DI dùng chung, `ApplicationDbContext`, migration/snapshot, contracts chung, menu. Mọi người gửi mapping/đăng ký cần thêm để Kiệt tích hợp, không cùng sửa các file này.

## Task của Tuệ Minh — Exam CRUD và lifecycle

| Mã | Thời gian dự kiến | Mô tả thực hiện | Tiêu chí bàn giao |
|---|---|---|---|
| TM1 | 08:30–10:00 | Hoàn thiện rule Exam: tên bắt buộc, start < end, duration/main count > 0, follow-up >= 0. Tạo Application service/repository cho list/get/create/update. | LecturerId lấy từ ICurrentUser; chỉ tạo trong môn có quyền; chỉ sửa Draft; domain tests cho dữ liệu sai. |
| TM2 | 10:00–12:00 | Viết ExamsController, Exam ViewModels và Razor Views Index/Create/Edit/Details. Tái dùng CSS/layout sẵn có, không thiết kế giao diện mới. | Tạo và sửa Draft từ trình duyệt; form sai giữ dữ liệu và hiện lỗi; POST có antiforgery. |
| TM3 | 13:00–14:30 | Thêm Publish/Start/Complete/Cancel, gọi guard/pool count/roster readiness; cấu hình đã publish không được sửa. | Không publish khi thiếu pool/roster/lịch; không start ngoài cửa sổ; transition sai bị từ chối ở service. |
| TM4 | 14:30–15:30 | Nối event sau save/commit; xử lý conflict RowVersion; cùng Sơn/Long/Dương kiểm tra điều kiện publish. | Refresh trang thể hiện trạng thái từ SQL; lỗi DB không gửi thông báo thành công; có test lifecycle chính. |

**File/module:** `Application/Exams`, `Persistence/Repositories/ExamRepository`, `Controllers/ExamsController`, `ViewModels/Exams`, `Views/Exams`. Sau 15:30 chỉ sửa lỗi demo/kiểm thử chung.

## Task của Sơn — pool câu hỏi

| Mã | Thời gian dự kiến | Mô tả thực hiện | Tiêu chí bàn giao |
|---|---|---|---|
| S1 | 08:30–10:00 | Tạo truy vấn câu hỏi hợp lệ theo quyền hiện có, subject, Approved và rubric. Viết service thêm/bỏ batch cùng unique mapping. | Sai môn/Draft/Rejected/thiếu rubric không vào pool; thêm trùng không tạo hàng mới; pool chỉ sửa Draft. |
| S2 | 10:00–12:00 | MVC trang pool: danh sách câu có thể chọn, checkbox thêm nhiều câu, bảng pool hiện tại và nút bỏ câu. | Lecturer quản lý pool exam của mình; hiển thị số câu đang có và số cần; mutation POST + antiforgery. |
| S3 | 13:00–14:30 | Bàn giao truy vấn eligible/count cho Tuệ Minh và Dũng; thống nhất snapshot nội dung/rubric khi tạo session. Revalidate trước publish và lúc mở session. | Câu bị sửa về Draft sau khi thêm pool không được chọn cho session mới; pool thiếu báo lỗi trước commit. |
| S4 | 14:30–15:30 | Test batch sai rollback, câu trùng và quyền; nối PoolChanged qua notifier dùng chung, hỗ trợ tích hợp. | Test đủ 4 trường hợp không hợp lệ; không lưu một phần batch; pool SQL khớp UI. |

**File/module:** `Application/ExamPools`, pool repository/configuration, `Controllers/ExamPoolsController`, `ViewModels/ExamPools`, `Views/ExamPools`.

## Task của Long — roster sinh viên

| Mã | Thời gian dự kiến | Mô tả thực hiện | Tiêu chí bàn giao |
|---|---|---|---|
| L1 | 08:30–10:00 | Service roster add/remove/list, resolve tài khoản theo email đã có, kiểm tra STUDENT active và unique (ExamId, StudentId). | Không thêm lecturer/admin, user khóa/xóa hoặc sinh viên trùng; lecturer không sửa exam người khác. |
| L2 | 10:00–12:00 | MVC roster: tìm bằng email, thêm một sinh viên, bỏ sinh viên và xem danh sách. Hiển thị tên/email và thời gian ca nếu có. | Thêm ít nhất 3 tài khoản demo qua UI; lỗi từng thao tác rõ ràng; không cần upload/import. |
| L3 | 13:00–14:30 | Bàn giao roster/readiness cho Dương và Tuệ Minh; khóa thay đổi roster khi Published để lịch và session ổn định. | Publish kiểm tra roster không rỗng; không xóa người đã có session; không mất liên kết schedule. |
| L4 | 14:30–15:30 | Test duplicate/role/ownership, nối RosterChanged sau commit và hỗ trợ dữ liệu demo. | Request thêm lặp không trùng; DB lỗi không gửi event; roster page đọc lại trạng thái SQL. |

**File/module:** `Application/ExamStudents`, roster repository/configuration, `Controllers/ExamStudentsController`, `ViewModels/ExamStudents`, `Views/ExamStudents`. Import CSV/XLSX roster chuyển backlog sau MVP.

## Task của Dương — scheduling và lịch cá nhân

| Mã | Thời gian dự kiến | Mô tả thực hiện | Tiêu chí bàn giao |
|---|---|---|---|
| D1 | 08:30–10:00 | Viết hàm sinh slot từ StartAt + thứ tự roster ổn định theo StudentId. Mỗi slot dài DurationMinutes, một kênh, không có break. Test bằng fixture trước khi nối repository. | Cùng input ra cùng preview; tất cả slot nằm trong exam window; thiếu thời gian thì báo lỗi, không cắt bớt sinh viên. |
| D2 | 10:00–12:00 | Service Preview/Apply với transaction và kiểm tra trùng lịch student/lecturer; đánh version roster/exam để reject preview cũ. | Hai apply cạnh tranh không ghi đè lịch mới; thao tác sai rollback toàn bộ; chỉ áp dụng khi Draft. |
| D3 | 13:00–14:30 | MVC lịch lecturer (bảng theo giờ, không calendar kéo/thả) và MySchedule cho student. Nối roster thật từ Long. | Lecturer xem lịch exam mình; student chỉ xem lịch cá nhân; hiển thị giờ Việt Nam nhất quán. |
| D4 | 14:30–15:30 | Nối readiness cho publish và slot access cho session; phát ScheduleChanged sau commit. Test giao giờ và biên end. | Không mở session trước ca/sau ca; ca kết thúc đúng EndAt được chấp nhận; preview đủ cho 3 sinh viên demo. |

**File/module:** `Application/Scheduling`, schedule repository, `Controllers/SchedulesController`, `ViewModels/Schedules`, `Views/Schedules`. Dương cập nhật ScheduledAt qua service thống nhất, không thêm migration riêng xung đột với Long.

## Task của Dũng — session, selection và real-time

| Mã | Thời gian dự kiến | Mô tả thực hiện | Tiêu chí bàn giao |
|---|---|---|---|
| DG1 | 08:30–09:00 | Bàn giao IExamNotifier và adapter tạm không làm gì để các bạn gọi cùng một contract; chốt tên event `ExamChanged` và group `exam:{id}`. | Business không tham chiếu SignalR; tích hợp module khác không phải chờ Hub hoàn thiện. |
| DG2 | 09:00–12:00 | Service GetOrCreateSession/GetQuestions: kiểm tra actor, enrollment, Ongoing và slot; trong transaction khóa theo exam/roster, tìm session đã có trước khi random. Lưu question IDs, thứ tự, content/rubric snapshots. | Refresh và hai request đồng thời trả cùng bộ; câu trong session không trùng; pool thiếu không lưu session dở. Ưu tiên tránh bộ session liền trước nếu còn đủ câu. |
| DG3 | 13:00–14:00 | MVC tối thiểu cho student mở/xem lại câu hỏi và bấm hoàn thành session. DTO student chỉ có câu hỏi/thứ tự/trạng thái, không có rubric/expected answer. | Session hoàn thành không tạo lại; student không xem session của người khác; snapshot không đổi khi sửa ngân hàng. Không thêm tính năng trả lời/chấm điểm. |
| DG4 | 14:00–15:00 | Hub/SignalR adapter trong Web, client JS tự reconnect trên trang chi tiết exam. Chỉ lecturer owner/admin được vào group; nhận event thì fetch lại phần trạng thái/roster đã được kiểm tra quyền. | Hai cửa sổ lecturer cập nhật khi thay lifecycle/roster/pool/lịch; không gửi câu hỏi/đáp án qua event; lỗi gửi SignalR không rollback dữ liệu đã commit. |
| DG5 | 15:00–15:30 | Test concurrency, refresh, group authorization/reconnect. Revalidate quyền cho connection dài hoặc thu hồi group/connection khi quyền bị thu hồi theo cơ chế đã chốt với Kiệt. | Test request lặp giữ cùng IDs; tài khoản không được phép không join group; reconnect đọc lại SQL. Hub không thay thế persistence. |

**File/module:** `Application/VivaSessions`, selection/session repository/configuration, `Controllers/VivaSessionsController`, `ViewModels/VivaSessions`, `Views/VivaSessions`, `Web/Hubs`, `Web/Realtime`, `wwwroot/js/exam-realtime.js`.

## Lịch phối hợp và các mốc kiểm soát

| Giờ | Việc cả nhóm / mốc bắt buộc |
|---|---|
| 08:00–08:30 | Chạy baseline trên máy từng người, chốt contracts, role/ownership, scope MVP và dữ liệu demo. Nếu baseline lỗi, Kiệt + người gặp lỗi xử lý ngay; các bạn còn lại viết service với fixture. |
| 08:30–09:00 | Kiệt bàn giao scaffold/guards theo tiến độ; Dũng bàn giao notifier contract. Cả nhóm xác nhận tên field/interface để tránh sửa chéo. |
| 09:00–10:00 | Từng người làm service/domain và test module mình. **Mốc 10:00:** contracts build; có rule Exam, eligible pool, roster guard, hàm slot, selection cơ bản. |
| 10:00–12:00 | Làm persistence/MVC song song; gửi thay đổi nhỏ để Kiệt tích hợp lần đầu. Không đợi code toàn module xong mới bàn giao. |
| 12:00–13:00 | Nghỉ trưa. |
| 13:00–14:00 | Nối service thật giữa Exam/pool/roster/lịch/session. **Mốc 14:00:** build chung qua; tạo Draft, thêm pool/roster và lưu lịch trên SQL được. |
| 14:00–15:30 | Hoàn thiện lifecycle/session/SignalR và test module. **Mốc 15:30:** toàn bộ luồng demo chạy được; đóng băng tính năng. |
| 15:30–16:30 | Cả nhóm chạy checklist bên dưới; mỗi người sửa lỗi module của mình. Kiệt điều phối, không thêm yêu cầu mới. |
| 16:30–17:00 | Chạy regression nhóm 1, xác minh migration, diễn tập demo, cập nhật README/test evidence và commit bàn giao. Chỉ báo hoàn thành các mục đã kiểm chứng. |

## Checklist nghiệm thu cuối ngày

Mỗi bước ghi người test, kết quả Pass/Fail và lỗi/commit liên quan trong PR hoặc báo cáo. Dữ liệu demo dùng tài khoản/DB dev riêng, không dùng dữ liệu thật.

- [ ] Kiệt + Tuệ Minh: tạo Draft có main questions = 2, follow-up tối đa = 1; cấu hình sai bị chặn.
- [ ] Sơn: pool có ít nhất 4 câu Approved đúng môn có rubric; thử thêm Draft/sai môn phải bị từ chối.
- [ ] Long: thêm 3 STUDENT active; thử thêm trùng, lecturer và tài khoản khóa phải bị từ chối.
- [ ] Dương: preview/lưu đủ 3 ca trong exam window; thử cửa sổ không đủ hoặc chồng ca phải rollback.
- [ ] Tuệ Minh: publish thiếu pool/roster/lịch phải thất bại; khi đủ thì Published; không sửa cấu hình sau publish.
- [ ] Tuệ Minh + Dương: start trong cửa sổ thi; ca demo được đặt gần thời điểm chạy, không tắt kiểm tra thời gian để demo.
- [ ] Dũng: student mở đúng ca nhận 2 câu; refresh và request đồng thời giữ cùng session/câu/thứ tự; không có rubric/đáp án trong HTML/JSON student.
- [ ] Dũng + Sơn: session tiếp theo tránh bộ trước khi pool cho phép; session hiện có giữ snapshot khi ngân hàng đổi.
- [ ] Kiệt + Dũng: student/lecturer khác truy cập ID trực tiếp bị chặn; Hub từ chối join group không có quyền.
- [ ] Dũng + Tuệ Minh: hai cửa sổ lecturer thấy thay đổi trạng thái; ngắt/kết nối lại client vẫn đọc đúng SQL; rollback không gửi event thành công.
- [ ] Dũng + Tuệ Minh: hoàn thành session đã bắt đầu rồi complete exam; exam đóng không cho tạo session mới.
- [ ] Cả nhóm: migration trên DB tạm chạy được, build/test liên quan và regression nhóm 1 qua; README có lệnh chạy và giới hạn MVP.

## Dự phòng khi trễ và quy tắc không được cắt

1. **Trễ mốc 10:00:** Kiệt và người phụ trách chốt lại interface ngay; bỏ lọc nâng cao, dùng form đơn giản và dữ liệu demo đã có. Không bỏ validation.
2. **Trễ mốc 14:00:** giảm hỗ trợ tránh bộ trước xuống thuật toán ưu tiên đơn giản, UI chỉ dùng bảng; Dương/Long hỗ trợ kiểm thử session sau khi service của mình đã chạy. Không thêm import, calendar hoặc widget mới.
3. **Trễ mốc 15:30:** dừng mọi tính năng mới, cả nhóm sửa đường chạy chính. Nếu SignalR hoặc concurrency chưa đạt, ghi rõ chưa hoàn thành thay vì báo đủ MVP; không giấu lỗi bằng seed hoặc random lại khi refresh.
4. **Không cắt:** server-side RBAC/ownership, antiforgery cho mutation, transaction, unique constraints, kiểm tra thời gian ca, snapshot và idempotency của bộ câu hỏi. Không gửi expected answer/rubric cho student.
5. **Không sửa chéo:** một người sở hữu một module; file chung/migration do Kiệt tích hợp. Commit nhỏ bằng tiếng Anh; PR mô tả kết quả và cách test. Không push trực tiếp vào branch người khác, không force-push để xử lý conflict.

## Bảng cập nhật khi nhận việc

| Người | Task | Trạng thái ban đầu | Giờ bàn giao module | Kết quả / link PR |
|---|---|---|---|---|
| Kiệt | K1–K5 | TODO | Tích hợp liên tục; chốt 17:00 | Chưa có |
| Tuệ Minh | TM1–TM4 | TODO | 15:30 | Chưa có |
| Sơn | S1–S4 | TODO | 15:30 | Chưa có |
| Long | L1–L4 | TODO | 15:30 | Chưa có |
| Dương | D1–D4 | TODO | 15:30 | Chưa có |
| Dũng | DG1–DG5 | Module đã triển khai, 119 tests pass; chờ demo tích hợp | 19/10/2026 | [Bàn giao Dũng](GROUP_2_DUNG_HANDOFF.md) |

**Definition of Done:** module có code + persistence + MVC theo phạm vi, quyền đúng, test rủi ro chính qua, tích hợp chạy được trong luồng demo, tài liệu cập nhật. Không đánh dấu xong chỉ vì đã có entity hoặc giao diện. Tổng thời gian là 48 giờ công của 6 người làm song song, không phải 48 giờ lịch.
