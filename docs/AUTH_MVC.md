# Login MVC và dịch vụ JWT của Long

Branch: `feature/long-mvc-login-jwt`, bắt đầu từ main `4fdda29` (MVC, không còn REST API).

## Luồng và trách nhiệm từng tầng

1. `GET /Account/Login` hiển thị form Razor và token antiforgery.
2. `POST /Account/Login` kiểm tra input/antiforgery rồi gọi `LoginService.AuthenticateAsync`.
3. `LoginService` trim/lowercase email, giữ nguyên password; chỉ chấp nhận User Active, chưa soft-delete và hash hợp lệ.
4. `LoginUserRepository` đọc SQL bằng EF Core `AsNoTracking`. Connection lấy từ `ConnectionStrings:DefaultConnection`; DbContext không chứa secret.
5. `IdentityPasswordVerifier` dùng `Microsoft.AspNetCore.Identity.PasswordHasher<User>`, cùng thuật toán với branch Register mới của Minh (`ad7c208`).
6. Controller tạo ticket cookie `AIVES.Auth`, redirect tới ReturnUrl nội bộ hoặc `/`. Không đưa hash/token/mật khẩu vào trang tài khoản.
7. `MvcCookieEvents.ValidatePrincipal` kiểm tra User từ SQL trên mỗi request có cookie; khóa/ngừng hoạt động/xóa tài khoản sẽ thu hồi phiên ở request tiếp theo.
8. `POST /Account/Logout` có antiforgery và xóa cookie của trình duyệt hiện tại.

| Thành phần | Reference trong code | Mục đích |
|---|---|---|
| Web | `Controllers/AccountController.cs`, `Models/LoginViewModel.cs`, `Views/Account` | Form, validation, redirect và phiên trình duyệt |
| Application | `Authentication/LoginService.cs`, `ILoginUserRepository`, `IPasswordVerifier` | Nghiệp vụ độc lập MVC/SQL/thuật toán hash |
| Persistence | `Repositories/LoginUserRepository.cs` | Triển khai truy vấn tài khoản từ DB thật |
| Infrastructure | `Authentication/IdentityPasswordVerifier.cs` | Xác thực hash từ Register |
| Infrastructure | `Authentication/JwtAccessTokenIssuer.cs`, `JwtOptions` | Phát JWT HS256 cho consumer đã xác thực |
| Web | `Authentication/AuthConfiguration.cs`, `MvcCookieEvents.cs` | Chọn scheme, validate JWT và bảo vệ phiên MVC |

Các hàm và đoạn xử lý quan trọng có XML summary/comment giải thích mục đích. Các tầng được nối qua `AddApplication`, `AddInfrastructure`, `AddPersistence` và `AddAivesAuthentication` trong `Web/Program.cs`.

## Cookie và JWT

MVC dùng scheme mặc định `Cookies`, cookie HttpOnly, SameSite=Lax, hạn phiên 60 phút, không sliding và không persistent. Khi chạy HTTPS cookie có Secure; HTTP local/Docker vẫn dùng được. Triển khai production cần HTTPS.

Fallback policy yêu cầu đăng nhập cho các trang nghiệp vụ. Login, trang lỗi và static assets được phép truy cập ẩn danh. POST Login/Logout đều kiểm tra antiforgery. ReturnUrl chỉ chấp nhận URL nội bộ.

JWT là dịch vụ được giữ để tích hợp API/mobile sau này: consumer gọi `LoginService.AuthenticateAsync`, sau khi thành công gọi `IAccessTokenIssuer.Issue`. Không có endpoint REST phát token trên branch này; form MVC không phát token cho trình duyệt. Endpoint muốn dùng JWT phải chọn rõ `[Authorize(AuthenticationSchemes = "Bearer")]`.

Token chứa `sub`, `email`, `name`, `jti`, `iat`, `nbf`, `exp`; chữ ký HS256, validate issuer/audience/signature/lifetime, clock skew = 0. Key ít nhất 32 byte UTF-8; issuer/audience bắt buộc; thời hạn 1–1440 phút. Cấu hình sai làm startup thất bại. Token không chứa PasswordHash. Logout cookie không thu hồi JWT đã phát; JWT có hiệu lực tới exp, chưa có refresh token/revocation store.

## Cấu hình cá nhân và chạy local

`Web/appsettings.json` được commit chỉ có mẫu connection string và Jwt key trống. Cấu hình thật của Long từ checkout cũ đã được giữ nguyên trong `Web/appsettings.Development.json` của worktree mới. File Development và `.env` bị ignore; Docker build và publish loại bỏ file Development. Cả hai appsettings đều Always copy khi build để sửa cấu hình có hiệu lực sau build.

Đọc cấu hình trực tiếp qua `ConnectionStrings:DefaultConnection`, không User Secrets. Với Docker, connection string môi trường của service `web` ghi đè cấu hình SQL local; `Jwt__Key` lấy từ `JWT_KEY` trong `.env`. Cần key ngẫu nhiên thật, không dùng giá trị mẫu `.env.example`. SQL local và SQL Docker là hai instance khác nhau; có thể chạy song song nếu khác port. Override riêng của Long dùng Docker SQL `127.0.0.1:14330`; file override không commit.

Chạy từ root worktree mới:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project AIpoweredVivaExamSystem.Web --no-launch-profile --urls http://127.0.0.1:5088
```

Mở `http://127.0.0.1:5088/Account/Login`. Tài khoản cần tồn tại trong Users với hash Identity, Status=Active và DeletedAt=NULL. Smoke tool dùng tài khoản tạm và dọn ngay sau test, nên không để lại một tài khoản/mật khẩu mẫu trong DB.

## Test và smoke test với SQL thật

Suite dùng SQL Server local nhưng tạo/xóa DB test riêng `AivesRubricTests_<guid>`; không xóa DB ứng dụng. Test factory thay DbContext bằng connection của fixture, inject Jwt key test riêng. Probe `/_tests/jwt` nằm trong assembly Tests, chỉ factory test nạp vào MVC; production không có route đó.

```powershell
$localConfig = Get-Content AIpoweredVivaExamSystem.Web/appsettings.Development.json -Raw | ConvertFrom-Json
$env:AIVES_TEST_CONNECTION = $localConfig.ConnectionStrings.DefaultConnection
dotnet test AIpoweredVivaExamSystem.slnx
Remove-Item Env:AIVES_TEST_CONNECTION
```

Trong terminal khác, khi Web đang chạy:

```powershell
# Login/JWT trên schema Users hiện có, không cập nhật schema.
dotnet run --project tools/AuthSmokeTest -- --base-url http://127.0.0.1:5088 --login-only

# Smoke toàn luồng, gồm dashboard và Subjects: DB phải có đủ migration main.
dotnet run --project tools/AuthSmokeTest -- --base-url http://127.0.0.1:5088
```

Tool đọc cùng appsettings/biến môi trường như Web, chỉ cho phép URL và SQL local. Tạo User với email/Guid và password ngẫu nhiên trong bộ nhớ; gửi HTTP thật, kiểm tra CSRF, cookie, identity SQL, issuer JWT và logout. `finally` xóa đúng User vừa tạo theo cả Id/email. Không in mật khẩu, hash, JWT hoặc connection string. Không xóa database.

Nếu DB thiếu migration, chế độ toàn luồng dừng trước khi seed. Chỉ sau khi chủ DB đã duyệt cập nhật schema mới chạy thêm `--apply-migrations`; chế độ này áp dụng các migration hiện có của main. Login không thêm migration mới.

## Giới hạn tích hợp hiện tại

Main chưa có Role/UserRole và Register; branch của Minh chưa được merge. Login/JWT ở đây không tự gán role hoặc thay code Register. Khi tích hợp User constructor mới của Minh, cần cập nhật seed/test/smoke tool và nối role claims/policy theo quy ước nhóm. Hiện các tài khoản Active đã đăng nhập có thể truy cập trang nghiệp vụ; chưa phân quyền ADMIN/LECTURER/STUDENT.

Cookie kiểm tra trạng thái User ở mỗi request nhưng chưa có security stamp để thu hồi toàn bộ phiên khi đổi mật khẩu. JWT chỉ kiểm tra chữ ký/claims/hạn dùng; không kiểm tra lại trạng thái tài khoản từ SQL trên mỗi request. Các chức năng đổi mật khẩu, refresh token, phân quyền và khóa do nhiều lần đăng nhập sai thuộc task tiếp theo.

## Kết quả kiểm chứng ngày 01/10/2026

- Main baseline: 25/25 test chạy thành công trên SQL Server local.
- Suite sau Login/JWT: 55/55 test thành công ở cả Debug và Release, không skip; gồm 25 test nghiệp vụ cũ, 15 test MVC Login/session và 15 test JWT/configuration.
- Smoke qua HTTP Kestrel thật: 14 kiểm tra thành công trên DB ứng dụng local, gồm mật khẩu sai/đúng, profile khớp SQL, dashboard, Subjects, CSRF, JWT, logout và dọn tài khoản tạm.
- Sau khi Long cho phép cập nhật schema, đã áp dụng 2 migration có sẵn trên main: `20260927074412_AddRubricsAndQuestionDependencies` và `20260929193713_SubjectCodeUniqueAmongActive`. Lần smoke cuối không còn pending migration.
- Publish Release thành công, 0 warning/error; `appsettings.Development.json` không có trong thư mục publish.
- Cấu hình Development local được so sánh hash file và giữ nguyên nội dung từ checkout cũ. Không push/merge trong lần triển khai này.
