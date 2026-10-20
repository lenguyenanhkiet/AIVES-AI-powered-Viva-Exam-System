# Dũng — session, selection và SignalR

Đã triển khai DG1–DG5 và merge vào `main` (.NET 10). Chưa nghiệm thu demo toàn nhóm vì Exam CRUD/lifecycle và pool còn chờ module tương ứng; không đánh dấu checklist demo toàn luồng đã xong.

## Phần bàn giao

- DG1: tái sử dụng `IExamNotifier`; DI thay adapter log bằng `SignalRExamNotifier`. Application không phụ thuộc SignalR.
- DG2: `VivaSessionService` kiểm tra own enrollment; `VivaSessionStore` khóa hàng exam rồi enrollment bằng `UPDLOCK,HOLDLOCK` trong transaction chung, kiểm tra session có sẵn trước selection. Session mới yêu cầu Ongoing và `slotStart <= now < slotEnd`, slot nằm trong exam window. Lưu IDs, thứ tự, content/rubric/expected-answer snapshots trong SQL; thiếu pool rollback toàn bộ. Ưu tiên các câu không thuộc session ngay trước trong cùng kỳ thi.
- DG3: `/VivaSessions` liệt kê kỳ thi của sinh viên; Open bằng POST có antiforgery và trả trang câu hỏi đã lưu. Mở lại kể cả khi hết ca/exam đóng trả bộ cũ; chỉ tạo mới trong ca hợp lệ. Complete idempotent. Student DTO/HTML không chứa rubric hoặc expected answer, không có trả lời/chấm điểm/follow-up.
- DG4: `/ExamFoundation/Details/{examId}` hiển thị trạng thái, số lượng và roster/session status; Hub `/hubs/exams`, group `exam:{id}`, event `ExamChanged` chỉ mang examId. Client reconnect, rejoin và đọc lại SQL. Status endpoint kiểm tra quyền mỗi request; render roster bằng textContent. SignalR client 9.0.6 được lưu local.
- DG5: join chỉ cho lecturer owner còn được phân môn hoặc admin active. Adapter đọc lại user/role/assignment từ SQL trước từng lần gửi, loại connection mất quyền khỏi group; chỉ gửi trực tiếp tới connection vừa kiểm tra để tránh race khi join group. Connection đóng khi authentication hết hạn. Disconnect xóa registry, reconnect cần join lại. Registry trong bộ nhớ dành cho một web instance theo MVP.

## Tích hợp với các module khác

Các service lifecycle/pool/roster/schedule phải dùng `IExamUnitOfWork.ExecuteAsync` và cung cấp examIds thay đổi; notifier chỉ chạy sau commit. Lỗi gửi event không rollback SQL. Không sửa session đã tạo, không ghi đè schedule khi đã có session. Pool eligibility hiện áp dụng đúng quyền ngân hàng nhóm 1: Approved, cùng môn, chưa xóa, có rubric active, CreatedBy là lecturer của exam còn được phân môn. Sơn cần giữ quy tắc này đồng nhất khi bàn giao pool service.

Không cần migration mới: foundation đã có unique session/enrollment, session/question, session/position và snapshot columns. Khi merge vào `main`, các project giữ .NET 10; bản hạ .NET 9 của branch `dung` không được giữ.

## Kiểm thử

```powershell
dotnet build AIpoweredVivaExamSystem.slnx
$env:AIVES_TEST_CONNECTION = 'Server=.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True'
dotnet test AIpoweredVivaExamSystem.Tests
```

Fixture tạo/xóa database test riêng `AivesRubricTests_<guid>`; không dùng database ứng dụng.

`VivaSessionTests`/`SessionSelectionTests`: request đồng thời và refresh giữ IDs/thứ tự, snapshot không đổi sau sửa ngân hàng, không lộ grading data trong DTO, complete idempotent, notification lỗi sau commit vẫn giữ dữ liệu, từ chối pool thiếu/câu về Draft/ngoài ca/exam đóng/sinh viên khác, tránh bộ ngay trước.

`ExamHubTests`: join/rejoin, payload chỉ examId, revoke assignment, đổi role/khóa user, disconnect cleanup. SQL test xác minh quyền bị thu hồi được phản ánh ngay từ database. Regression toàn bộ: **119 passed, 0 failed**. Client JS đã kiểm tra bằng Node với transport/DOM giả: reconnect rejoin và fetch lại status, ExamChanged kích hoạt refresh. Kiểm thử Hub dùng transport giả; demo hai trình duyệt và reconnect mạng thật còn nằm trong nghiệm thu tích hợp toàn nhóm.
