# Login API — task của Võ Viết Long

## Phạm vi branch

- `feature/long-login-api`: xác minh email/mật khẩu qua `POST /api/v1/auth/login`.
- `feature/long-jwt-authentication`: dành cho task JWT tiếp theo, hiện chưa triển khai.
- Response thành công của task 1 chỉ là thông tin user. Chưa tạo phiên đăng nhập,
  cookie hay access token; chưa cho phép truy cập API cần xác thực.
- Không thay đổi entity/migration, không làm đăng ký hoặc Role/UserRole thay Minh.

## Request và response

```http
POST /api/v1/auth/login
Content-Type: application/json

{"email":"long@example.com","password":"your-password"}
```

- `200`: `{ "id": "<guid>", "email": "long@example.com", "fullName": "Long" }`.
- `400`: ValidationProblemDetails khi email sai định dạng, quá 255 ký tự hoặc thiếu email/mật khẩu.
- `401`: ProblemDetails với title `Invalid email or password.` cho email không tồn tại,
  mật khẩu sai, hash hỏng, tài khoản Inactive/Suspended/Locked hoặc đã xóa mềm.
- Không trả PasswordHash. Không ghi log password. Không trim password.
- Email được đối chiếu theo collation database hiện tại; chưa tự lowercase/normalize email.
  Nhóm cần thống nhất normalization với đăng ký và unique index nếu thay đổi quy ước.

## Đọc code theo luồng

1. `Api/Contracts/Authentication/LoginRequest.cs`: validate dữ liệu đầu vào bằng DataAnnotations.
2. `Api/Controllers/AuthController.cs`: nhận request, gọi service và chuyển kết quả sang HTTP.
3. `Application/Authentication/LoginService.cs`: xác minh tài khoản và tạo DTO công khai.
4. `Persistence/Repositories/LoginUserRepository.cs`: đọc Users bằng EF, loại user đã xóa mềm.
5. `Infrastructure/Authentication/IdentityPasswordVerifier.cs`: verify hash ASP.NET Core Identity.
6. Các `DependencyInjection.cs` và `Program.cs`: đăng ký, ghép những thành phần trên.

Các đường dẫn rút gọn trên tương ứng project `AIpoweredVivaExamSystem.<layer>`.

## Ghép với phần đăng ký của Minh

Dùng cùng `IPasswordHasher<User>` đã đăng ký trong Infrastructure:

```csharp
// Minh đặt đoạn tương đương trong xử lý đăng ký, sau khi validate dữ liệu.
var user = new User(email, "", fullName, UserStatus.Active);
user.ChangePassword(passwordHasher.HashPassword(user, password));
// Lưu user vào database qua module đăng ký; không lưu password gốc.
```

Nếu Minh chọn BCrypt, thay implementation của `IPasswordVerifier` và đăng ký DI.
Hash Identity đã lưu không tự chuyển thành BCrypt; dữ liệu demo cần tạo lại hoặc
nhóm thiết kế cơ chế migration/reset mật khẩu. Không viết fallback so sánh plaintext.
`SuccessRehashNeeded` được chấp nhận nhưng Login hiện không tự cập nhật hash.

## Kiểm thử

```powershell
dotnet restore AIpoweredVivaExamSystem.slnx
dotnet test AIpoweredVivaExamSystem.Tests/AIpoweredVivaExamSystem.Tests.csproj --no-restore
```

Test khởi động API thật trong TestServer, sử dụng SQLite in-memory và PasswordHasher thật.
Tài khoản test được seed tự động theo từng test rồi hủy; không có tài khoản mặc định
được thêm vào SQL Server hoặc production. Không cần Minh hoàn thành đăng ký để chạy test.

Để thử thủ công bằng Swagger, cần SQL Server đã apply migration và user có hash hợp lệ.
Test SQLite không thay thế kiểm thử collation/migration trên SQL Server của nhóm.

## Bàn giao task JWT

Sau khi nhóm trưởng review Login, cập nhật branch JWT theo code Login đã được duyệt.
Task JWT bổ sung phát token/expiry, cấu hình Bearer authentication và Swagger security.
Thống nhất role claims với Kiệt sau khi Minh bổ sung Role/UserRole.
Không trả token giả hoặc user ID làm token trong task Login.

Trước khi triển khai công khai, nhóm cần bổ sung rate limiting/lockout cho đăng nhập;
task hiện tại chỉ kiểm tra trạng thái tài khoản sẵn có, chưa tự khóa sau nhiều lần sai.
Review cũng ghi nhận email không tồn tại bỏ qua bước verify nên thời gian phản hồi
có thể khác email tồn tại. Cần dummy-hash verification khi tăng cường chống dò tài khoản.
