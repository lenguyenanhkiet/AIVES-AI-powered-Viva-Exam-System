# Dương — xếp lịch thi và lịch cá nhân

Module D1–D4 đã có trên `main`. Chưa demo toàn luồng: chưa có Exam CRUD/lifecycle (Tuệ Minh) nên chưa publish/start kỳ thi qua giao diện được.

## Sử dụng

| Route | Vai trò | Chức năng |
|---|---|---|
| `/Schedules` | ADMIN, LECTURER | Danh sách kỳ thi mình quản lý |
| `/Schedules/Exam/{id}` | ADMIN, LECTURER | Lịch hiện tại; **Xem trước** (POST `Preview`) rồi **Lưu lịch** (POST `Apply`) |
| `/VivaSessions` | STUDENT | Ca thi của chính mình và mở/xem lại phiên thi |

Giờ lưu UTC, hiển thị giờ Việt Nam (UTC+7) qua `ToLocalText()`.

## Quy tắc

- Một kênh, ca liên tiếp không nghỉ: ca thứ *i* bắt đầu `StartAt + i × DurationMinutes`, sinh viên sắp theo `StudentId` nên cùng dữ liệu luôn ra cùng lịch.
- Cửa sổ thi không đủ cho cả danh sách → báo lỗi, không cắt bớt sinh viên. Ca cuối kết thúc đúng `EndAt` được chấp nhận.
- Chỉ xếp lịch khi kỳ thi ở **Draft**. Không xếp lại sinh viên đã có phiên thi.
- `PreviewToken` gắn với RowVersion của exam và từng enrollment. Roster hoặc exam đổi sau khi xem trước → Apply bị từ chối, phải xem trước lại. Apply cập nhật `exam.UpdatedAt` nên preview cũ của người khác cũng hết hiệu lực.
- Apply kiểm tra trùng giờ trong SQL với các kỳ thi khác (không Cancelled): cùng sinh viên, hoặc cùng giảng viên. Lỗi bất kỳ → rollback toàn bộ.
- Các lần Apply được tuần tự hóa trên toàn database (`sp_getapplock` `AIVES:ExamScheduling`) để chặn hai kỳ thi có chung sinh viên lưu cùng lúc. Đủ cho MVP; timeout 10 giây báo "đang xếp lịch".
- Sinh viên chỉ thấy ca của kỳ thi Published/Ongoing/Completed; lịch Draft còn có thể đổi nên bị ẩn.
- Lưu xong gọi `IExamNotifier` sau commit (`ExamChanged`, chỉ mang examId).

## Cho module khác

- **Tuệ Minh (publish):** gọi `IScheduleReadiness.IsFullyScheduledAsync(examId, ct)` — true khi roster không rỗng và mọi sinh viên có ca nằm trong cửa sổ thi. Nên kiểm tra lại trong transaction publish.
- **Dũng (session):** slot được mở là `[ScheduledAt, ScheduledAt + DurationMinutes)`, `VivaSessionStore` đã tự kiểm tra; module lịch không làm thêm.
- **Long (roster):** thêm/bỏ sinh viên cập nhật exam RowVersion nên preview lịch cũ tự hết hiệu lực.

## Code và test

`Application/Scheduling` (`SequentialSchedule`, `ExamScheduleService`, contracts), `Persistence/Repositories/ExamScheduleStore.cs`, `Web/Controllers/SchedulesController.cs`, `Web/ViewModels/Schedules`, `Web/Views/Schedules`.

`Tests/ExamScheduleTests.cs` (18 test; 14 test SQL cần `AIVES_TEST_CONNECTION`): sinh ca và biên `EndAt`, lưu đủ ca, preview cũ, trùng giờ sinh viên/giảng viên rollback, Apply cạnh tranh, sai quyền, không phải Draft, đã có session, sinh viên chỉ thấy ca của mình, readiness, notifier chỉ chạy sau commit.
