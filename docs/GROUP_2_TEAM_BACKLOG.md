# Group 2 — phân công cho 6 thành viên (chưa triển khai)

Phạm vi nhóm 2 chỉ là kế hoạch. Nhóm 1 và cấu trúc MVC là nền tảng đã có; không giao lại import/RAG như công việc chưa làm. Các thành viên giữ nguyên dependency direction trong [MVC_ARCHITECTURE.md](MVC_ARCHITECTURE.md).

## Cách sử dụng backlog

Chưa có tên thành viên nên tạm dùng TV1–TV6; thay bằng tên thật khi nhận việc. Mỗi mã task dưới đây có thể dùng làm tên issue/PR. Tất cả task đang ở trạng thái **TODO**; tài liệu này không có nghĩa chức năng nhóm 2 đã được triển khai.

Mỗi người chịu trách nhiệm cả Domain/Application, Persistence và MVC của module mình. TV1 quản lý hợp đồng dùng chung và tích hợp migration; TV6 cung cấp notifier để các module gọi sau commit. Các con số ngày công bên dưới chỉ là ước lượng để lập kế hoạch, cần điều chỉnh theo năng lực thực tế.

| Thành viên | Phạm vi chính | File/module dự kiến | Phụ thuộc |
|---|---|---|---|
| 1 | Kiến trúc, RBAC và tích hợp | Application/Exams contracts, policies, Persistence migration integration | Chốt contracts trước khi 2–6 bắt đầu |
| 2 | Cấu hình và lifecycle kỳ thi | Domain/Exam, Application/Exams, Web/Controllers/ExamsController, ViewModels/Exams, Views/Exams | 1 |
| 3 | Question pool và liên kết ngân hàng câu hỏi | ExamQuestionPool, pool service/repository, MVC pool views | 1, 2; dùng câu Approved/rubric từ nhóm 1 |
| 4 | Roster sinh viên và import danh sách | ExamStudent, roster service/parser, MVC roster views | 1, 2 |
| 5 | Lập lịch và điều phối ca thi | schedule service, MVC calendar/slot views, lịch cá nhân | 2, 4 |
| 6 | VivaSession, thuật toán chọn câu hỏi và SignalR | SessionQuestion, selection service, Web/Hubs, Web/Realtime, wwwroot/js | 1–5 |

## Thành viên 1 — hợp đồng, phân quyền và integration

- Chốt DTO/service/repository, trạng thái, timezone và convention lỗi với các thành viên.
- Thêm authorization ở Application cho `CanManageExam` và `StudentOwnExam`, tái dùng `ICurrentUser` và môn được phân công.
- Chốt transaction boundary, concurrency token, unique constraints và abstraction `IExamNotifier` trong Application.
- Tích hợp migration từ các branch theo thứ tự; review snapshot để tránh conflict. Không tự merge snapshot bằng cách bỏ một bên.
- Review PR mỗi module, chạy regression nhóm 1, kiểm tra Controller không gọi DbContext/provider/Hub trực tiếp.
- Nghiệm thu: lecturer không truy cập exam của người khác, student chỉ truy cập ca thi của mình, admin được quản trị toàn bộ. Có test 403/404 và truy cập ID trực tiếp.

## Thành viên 2 — Exam CRUD và lifecycle

- Model: SubjectId, LecturerId, Name, StartAt/EndAt (`DateTimeOffset`), DurationMinutes, NumberOfMainQuestions, MaxFollowUpPerQuestion, Language, Status, RowVersion.
- MVC: danh sách/lọc, chi tiết, tạo, sửa cấu hình Draft, publish, start, complete, cancel; dùng Razor Views/ViewModels.
- Luật: Draft → Published → Ongoing → Completed; Draft/Published/Ongoing có thể Cancelled, trạng thái cuối không mở lại tùy ý.
- Validate start < end, duration/main count > 0, follow-up >= 0; publish cần pool đủ câu hợp lệ và roster/lịch đã sẵn sàng.
- Nghiệm thu: không sửa cấu hình khi đã Published; transition và quyền được kiểm tra ở service/domain, không chỉ ở nút UI. Có domain/service tests cho các transition không hợp lệ.

## Thành viên 3 — ExamQuestionPool

- Bảng nối ExamId/QuestionId với unique index; chỉ sửa pool khi exam Draft.
- Chọn/lọc theo subject/topic/Bloom/difficulty; thêm/bỏ nhiều câu hỏi qua MVC.
- Chỉ nhận câu Approved, chưa xóa, đúng môn và có rubric. Revalidate khi publish/start để xử lý câu bị sửa sau khi thêm vào pool.
- Quyết định cùng 2/6 về snapshot nội dung/rubric tại publish hoặc start để kỳ thi ổn định khi ngân hàng thay đổi.
- Nghiệm thu: không thêm trùng; không dùng câu Draft/Rejected/sai môn/thiếu rubric. Có test race và rollback khi batch có câu không hợp lệ.

## Thành viên 4 — roster và import sinh viên

- ExamStudent: ExamId, StudentId, trạng thái đăng ký/tham dự, thông tin khung giờ theo contract với 5.
- Thêm/bỏ sinh viên thủ công; CSV/XLSX preview → validate → confirm với template email/student code.
- Resolve tài khoản STUDENT đang hoạt động; unique (ExamId, StudentId), idempotency receipt cho import và batch transaction.
- Nghiệm thu: không enroll lecturer/admin, tài khoản khóa hoặc sinh viên trùng; sửa roster phải tuân lifecycle. Có test import, quyền và lặp request.

## Thành viên 5 — lịch thi và ca thi

- Sinh slot từ exam window/duration và khoảng nghỉ được thống nhất; preview trước khi áp dụng.
- Kiểm tra slot không vượt exam window, không trùng ca của sinh viên/giảng viên theo quy tắc phòng/kênh thi được chốt.
- Lecturer xem lịch, reschedule/cancel theo lifecycle; student xem lịch cá nhân, chỉ thấy thông tin của mình.
- Lưu UTC/DateTimeOffset; hiển thị Asia/Ho_Chi_Minh rõ ràng. Không phụ thuộc timezone máy server.
- Nghiệm thu: batch scheduling atomic; ca đang thi không bị dịch chuyển; có test giao giờ, biên ngày và concurrent scheduling.

## Thành viên 6 — session, selection và real-time

- VivaSession gắn duy nhất ExamStudent; SessionQuestion lưu thứ tự, question ID, nội dung/rubric snapshot và quan hệ follow-up.
- Tạo session trong transaction, unique session/position/question; request lặp hoặc refresh trả bộ câu đã lưu, không random lại.
- Chọn đủ main questions, không trùng trong session; ưu tiên tránh bộ của sinh viên liền trước khi pool đủ. Khi pool nhỏ, dùng fallback rõ ràng và vẫn không lặp trong session.
- Enforce MaxFollowUpPerQuestion ở service; follow-up gắn main question. Không gửi đáp án/rubric nội bộ đến student.
- Hub, adapter và JS client nằm trong Web; event/notifier abstraction nằm trong Application. Mỗi `exam:{id}` group phải kiểm tra quyền trước khi join và xử lý việc thu hồi quyền/reconnect.
- Publish notification sau commit cho lifecycle, roster, schedule, pool và session. Nếu cần đảm bảo giao nhận, thêm transactional outbox; không coi SignalR là persistence.
- Nghiệm thu: hai request đồng thời chỉ tạo một session/bộ câu; refresh không đổi; client mất mạng vẫn đọc lại trạng thái SQL; group không lộ dữ liệu giữa exam/student. Có concurrency + authorization + SignalR integration tests.

## Thứ tự bàn giao

1. 1 chốt contracts/schema; 2 dựng Exam và lifecycle.
2. 3 và 4 triển khai pool/roster song song trên contracts ổn định.
3. 5 tích hợp lịch; 6 phát triển selection bằng fixture, sau đó nối persistence/Hub.
4. 1 tích hợp migration, cả nhóm chạy smoke flow: tạo exam → pool → roster → lịch → publish/start → session/refresh → complete.

Definition of Done chung: migration + snapshot khớp model, MVC có antiforgery cho mutation, service kiểm tra RBAC/ownership, dữ liệu batch atomic, test module và regression nhóm 1 qua, README cập nhật, PR có hướng dẫn kiểm thử. Chưa coi nhóm 2 hoàn thành chỉ vì có entity hoặc giao diện.

## Danh sách task chi tiết

### TV1 — Architecture, RBAC và integration (ước lượng 5–7 ngày công)

| Mã | Task và mô tả | Kết quả bàn giao / tiêu chí nghiệm thu | Phụ thuộc |
|---|---|---|---|
| G2-01 | Chốt contracts: mô tả Exam, ExamStudent, ExamQuestionPool, VivaSession, SessionQuestion; DTO, service/repository interfaces, lỗi và các trạng thái. Chốt thời điểm snapshot, quyền dùng câu hỏi và chính sách trùng lịch. | Tài liệu contracts + interface trong Application; TV2–TV6 thống nhất trước khi tích hợp. Không để Business tham chiếu Web/SignalR/EF. | Không |
| G2-02 | Triển khai quyền quản lý exam và quyền xem ca thi cá nhân; tái dùng ICurrentUser, lecturer assignment và ownership. Kiểm tra quyền ở service cho cả đọc và ghi. | Admin quản trị toàn bộ; lecturer quản lý exam của mình trong môn được phân công; student chỉ xem session/lịch của mình. Test ID truy cập trực tiếp và thu hồi quyền. | G2-01 |
| G2-03 | Tích hợp EF schema, transaction boundary, RowVersion và unique indexes từ các module; review migration/snapshot theo thứ tự dependency. | Migration chạy được trên database tạm; hai request cạnh tranh không tạo dữ liệu trùng hoặc mất cập nhật. Có hướng dẫn migration trong PR. | G2-05, G2-09, G2-13, G2-21 |
| G2-04 | Review dependency, tích hợp DI/routes/menu và chạy regression nhóm 1 cùng smoke flow nhóm 2. | Build/test qua; demo đầy đủ từ tạo exam đến complete; không làm hỏng import/RAG/CRUD câu hỏi đã có. | Tất cả module |

### TV2 — Exam CRUD và lifecycle (ước lượng 5–7 ngày công)

| Mã | Task và mô tả | Kết quả bàn giao / tiêu chí nghiệm thu | Phụ thuộc |
|---|---|---|---|
| G2-05 | Xây dựng Exam aggregate, validation cấu hình và EF mapping: môn, chủ sở hữu, tên, cửa sổ thi, thời lượng, số câu chính, follow-up, ngôn ngữ, trạng thái, RowVersion. | Domain tests cho thời gian/ngưỡng cấu hình; schema có FK và concurrency token. Không nhận LecturerId tùy ý từ form. | G2-01 |
| G2-06 | Làm MVC danh sách/lọc/chi tiết/tạo/sửa kỳ thi; ViewModels riêng, form hiển thị validation và giới hạn theo quyền. Xóa chỉ với Draft chưa có dữ liệu phụ thuộc; trường hợp khác dùng Cancel. | Lecturer tạo và sửa Draft được; không sửa exam người khác; mọi mutation dùng POST + antiforgery. | G2-02, G2-05 |
| G2-07 | Triển khai publish/start/complete/cancel với state machine và kiểm tra pool/roster/lịch. Chốt thao tác start thủ công hay tự động trong contracts. | Không publish khi thiếu điều kiện; không start ngoài cửa sổ thi; không sửa cấu hình Published; trạng thái cuối không mở lại. Có test transition sai. | G2-05, G2-10, G2-14, G2-18 |
| G2-08 | Bổ sung concurrency handling, audit và event thay đổi lifecycle qua abstraction của TV6 sau commit. | UI báo conflict khi phiên sửa cũ; transaction lỗi không gửi event; test service/MVC và hướng dẫn demo. | G2-06, G2-07, G2-24 |

### TV3 — Question pool (ước lượng 4–6 ngày công)

| Mã | Task và mô tả | Kết quả bàn giao / tiêu chí nghiệm thu | Phụ thuộc |
|---|---|---|---|
| G2-09 | Tạo ExamQuestionPool, unique (ExamId, QuestionId), repository và service thêm/bỏ câu hỏi theo batch. | Thêm trùng không tạo thêm bản ghi; pool chỉ sửa được khi Draft; batch sai rollback toàn bộ. | G2-01, G2-05 |
| G2-10 | Xây dựng bộ kiểm tra eligibility: đúng môn, Approved, chưa xóa, có rubric và người dùng được phép sử dụng. Revalidate khi publish/start. | Test câu Draft/Rejected/sai môn/thiếu rubric và câu bị sửa sau khi thêm pool; không bypass ownership bằng ID. | G2-02, G2-09 |
| G2-11 | Làm MVC chọn/lọc câu hỏi và xem/bỏ khỏi pool; hiển thị số câu hợp lệ so với số câu chính. | Form thêm nhiều câu có validation rõ; không lộ đáp án ra màn hình sinh viên; có MVC tests. | G2-09, G2-10 |
| G2-12 | Chốt với TV2/TV6 việc snapshot câu hỏi/rubric và khóa thay đổi trong kỳ thi; nối event PoolChanged sau commit. | Kỳ thi đã bắt đầu không bị đổi nội dung do sửa ngân hàng; có integration test snapshot và event sau commit. | G2-07, G2-21, G2-24 |

### TV4 — Roster và import sinh viên (ước lượng 5–7 ngày công)

| Mã | Task và mô tả | Kết quả bàn giao / tiêu chí nghiệm thu | Phụ thuộc |
|---|---|---|---|
| G2-13 | Xây dựng ExamStudent và trạng thái tham dự, unique (ExamId, StudentId), quan hệ tới tài khoản và exam. | Không enroll lecturer/admin hoặc tài khoản khóa/xóa; trạng thái ban đầu và transition được kiểm thử. | G2-01, G2-05 |
| G2-14 | Service và MVC thêm/bỏ sinh viên thủ công, tìm kiếm theo email/student code, danh sách roster và thống kê. | Không thêm trùng; lecturer không sửa roster exam khác; không bỏ sinh viên đã bắt đầu thi. | G2-02, G2-13 |
| G2-15 | Import CSV/XLSX roster theo template, preview lỗi từng dòng, confirm theo batch và chống submit lại. Tái dùng kỹ thuật đọc file khi phù hợp, không sao chép nghiệp vụ import câu hỏi. | Template tải được; dòng lỗi không tạo dữ liệu; file hợp lệ lưu atomic; request lặp không enroll trùng; có parser/service/MVC tests. | G2-13, G2-14 |
| G2-16 | Tích hợp roster với lịch và session; hiển thị Scheduled/InProgress/Completed/Absent theo contracts. Nối event RosterChanged sau commit. | Trạng thái khớp session/lịch trong SQL; không tự đánh dấu vắng trước khi slot kết thúc; có test integration và event. | G2-18, G2-21, G2-24 |

### TV5 — Scheduling và lịch cá nhân (ước lượng 5–7 ngày công)

| Mã | Task và mô tả | Kết quả bàn giao / tiêu chí nghiệm thu | Phụ thuộc |
|---|---|---|---|
| G2-17 | Chốt cấu trúc slot và thuật toán sinh lịch từ cửa sổ thi, duration, break, số sinh viên và số kênh/phòng theo cấu hình thống nhất. | Service sinh preview xác định được; không tạo slot vượt exam window; báo rõ khi không đủ thời gian. | G2-01, G2-05, G2-13 |
| G2-18 | Kiểm tra giao giờ giữa student/lecturer/phòng; lưu lịch theo batch transaction và chống hai request đặt cùng slot. | Không lưu một phần lịch; test cạnh tranh và biên start/end; có unique constraint/locking phù hợp với mô hình slot đã chốt. | G2-17 |
| G2-19 | Làm MVC lịch giảng viên, danh sách slot và lịch cá nhân sinh viên; hỗ trợ đổi ca/hủy ca theo lifecycle. | Student chỉ thấy lịch của mình; không reschedule ca đang thi; hiển thị rõ múi giờ Việt Nam từ DateTimeOffset/UTC. | G2-02, G2-18 |
| G2-20 | Nối ScheduleChanged sau commit và cơ chế đọc lại lịch mới; bổ sung test quyền, lệch múi giờ và biên ngày. | Refresh/reconnect luôn lấy lịch từ SQL; rollback không phát lịch sai; có kịch bản demo reschedule. | G2-19, G2-24 |

### TV6 — Session, selection và SignalR (ước lượng 7–9 ngày công)

| Mã | Task và mô tả | Kết quả bàn giao / tiêu chí nghiệm thu | Phụ thuộc |
|---|---|---|---|
| G2-21 | Tạo VivaSession/SessionQuestion, snapshot nội dung/rubric, thứ tự câu hỏi, quan hệ main/follow-up và unique constraints. | Một session trên một ExamStudent; unique vị trí/câu chính; snapshot ổn định khi ngân hàng bị sửa; có EF integration tests. | G2-01, G2-05, G2-09, G2-13 |
| G2-22 | Thuật toán chọn câu chính: random từ pool hợp lệ, không trùng trong session, tránh bộ liền trước nếu đủ pool, fallback khi pool nhỏ. Lưu kết quả idempotent trong transaction. | Refresh không random lại; hai request đồng thời trả cùng bộ đã lưu; luôn đủ số câu hoặc báo lỗi trước khi commit. Có test pool nhỏ và cạnh tranh. | G2-10, G2-18, G2-21 |
| G2-23 | Service/MVC bắt đầu, tiếp tục, hoàn thành session; chỉ mở đúng slot; quản lý giới hạn follow-up và bảo vệ dữ liệu nội bộ. | Student không xem session người khác; không truy cập đáp án/rubric; follow-up không vượt MaxFollowUpPerQuestion; request lặp không tạo câu mới. Sinh nội dung follow-up bằng AI chỉ bổ sung khi yêu cầu nghiệp vụ được xác nhận. | G2-02, G2-19, G2-22 |
| G2-24 | Cung cấp IExamNotifier/events trong Application, Hub/adapter trong Web và JavaScript client có reconnect. Kiểm tra quyền khi join group; mỗi module gửi event sau commit. | Event cho lifecycle/pool/roster/schedule/session; rollback không phát event; không lộ nhóm giữa exam/student; reconnect đọc lại SQL. Bàn giao contract sớm để TV2–TV5 tích hợp; delivery bền vững bằng outbox là hạng mục mở rộng nếu cần. | Contract: G2-01, G2-02; tích hợp: G2-08, G2-12, G2-16, G2-20, G2-23 |

## Checklist theo dõi tiến độ

- [ ] TV1: G2-01, G2-02, G2-03, G2-04.
- [ ] TV2: G2-05, G2-06, G2-07, G2-08.
- [ ] TV3: G2-09, G2-10, G2-11, G2-12.
- [ ] TV4: G2-13, G2-14, G2-15, G2-16.
- [ ] TV5: G2-17, G2-18, G2-19, G2-20.
- [ ] TV6: G2-21, G2-22, G2-23, G2-24.

Các dependency tích hợp không yêu cầu chờ module khác xong hoàn toàn: thống nhất interface/fixture trước, triển khai module riêng, sau đó nối adapter và kiểm thử toàn luồng. G2-24 cần bàn giao abstraction trước khi làm Hub/client để tránh vòng chờ giữa các thành viên.
