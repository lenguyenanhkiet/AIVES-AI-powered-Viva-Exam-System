# Subject và Topic API

Một Subject (môn học) có nhiều Topic (chủ đề). Question tham chiếu tới Subject và
có thể tham chiếu tới một Topic cùng Subject.

## Phạm vi

- CRUD Subject: tìm theo `keyword` (Code hoặc Name), lọc `status`, phân trang.
- CRUD Topic: lọc theo `subjectId`, `keyword` (Name), `status`, phân trang.
- Dùng lại entity và mapping Subject/Topic đã có. Migration
  `SubjectCodeUniqueAmongActive` đổi unique index của `Subjects.Code` thành filtered index.
- Chưa tích hợp JWT/phân quyền.

## Luồng xử lý

```text
SubjectsController / TopicsController
  → ISubjectService / ITopicService (Application)
  → Subject / Topic (Domain)
  → ISubjectRepository / ITopicRepository (Persistence)
  → ApplicationDbContext → SQL Server
```

## Quy tắc

- Subject `Code` bắt buộc, tối đa 50 ký tự, **duy nhất trong các Subject chưa xóa**
  (filtered unique index `WHERE DeletedAt IS NULL`). Trùng trả 409. Xóa mềm một
  Subject sẽ giải phóng mã để tạo lại.
- `Name` bắt buộc, tối đa 255 ký tự. Code/Name được trim.
- `Status`: `Active` (mặc định) hoặc `Inactive`. JSON trả enum dạng chuỗi; request
  nhận cả chuỗi lẫn số (`1`, `2`).
- Topic phải thuộc một Subject còn tồn tại (chưa xóa), nếu không trả 404.
- Tên Topic không trùng trong cùng Subject (chỉ tính Topic chưa xóa). Trùng trả 409.
- `SubjectId` của Topic **không đổi được** qua PUT, vì `(Id, SubjectId)` là khóa
  thay thế mà Question dùng làm khóa ngoại.
- DELETE là **xóa mềm** (gán `DeletedAt`); dữ liệu đã xóa không xuất hiện ở GET/list.
  - Không xóa được Subject còn Topic hoặc Question chưa xóa → 409.
  - Không xóa được Topic còn Question chưa xóa → 409.

## Endpoint

| HTTP | URL | Thành công |
|---|---|---|
| POST | `/api/subjects` | 201 + `Location` |
| GET | `/api/subjects/{id}` | 200 |
| GET | `/api/subjects?keyword=&status=&page=1&pageSize=20` | 200 |
| PUT | `/api/subjects/{id}` | 200 |
| DELETE | `/api/subjects/{id}` | 204 |
| POST | `/api/topics` | 201 + `Location` |
| GET | `/api/topics/{id}` | 200 |
| GET | `/api/topics?subjectId=&keyword=&status=&page=1&pageSize=20` | 200 |
| PUT | `/api/topics/{id}` | 200 |
| DELETE | `/api/topics/{id}` | 204 |

`page` từ 1, `pageSize` từ 1 đến 100. Lỗi trả ProblemDetails: 400 dữ liệu không
hợp lệ, 404 không tìm thấy, 409 xung đột.

## Ví dụ

```text
POST /api/subjects
{ "code": "PRN222", "name": "Programming with C#", "description": null }

POST /api/topics
{ "subjectId": "<subject-id>", "name": "LINQ", "description": "Truy vấn dữ liệu" }

PUT /api/topics/{id}
{ "name": "LINQ nâng cao", "description": null, "status": "Inactive" }
```
