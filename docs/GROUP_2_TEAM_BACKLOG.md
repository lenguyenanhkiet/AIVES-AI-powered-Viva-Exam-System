# Group 2 — phân công cho 6 thành viên (chưa triển khai)

Phạm vi nhóm 2 chỉ là kế hoạch. Nhóm 1 và cấu trúc MVC là nền tảng đã có; không giao lại import/RAG như công việc chưa làm. Các thành viên giữ nguyên dependency direction trong [MVC_ARCHITECTURE.md](MVC_ARCHITECTURE.md).

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
