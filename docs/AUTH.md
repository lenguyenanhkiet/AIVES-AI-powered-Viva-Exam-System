# Đăng nhập Razor Pages và JWT

## Luồng và trách nhiệm từng tầng

1. `GET /Account/Login` hiển thị form Razor và token antiforgery.
2. `POST /Account/Login` kiểm tra input/antiforgery rồi gọi `LoginService.AuthenticateAsync`.
3. `LoginService` trim/lowercase email, giữ nguyên password; chỉ chấp nhận User Active, chưa soft-delete và hash hợp lệ.
4. `LoginUserRepository` đọc SQL bằng EF Core `AsNoTracking`. Connection lấy từ `ConnectionStrings:DefaultConnection`; DbContext không chứa secret.
5. `IdentityPasswordVerifier` dùng `Microsoft.AspNetCore.Identity.PasswordHasher<User>`, cùng thuật toán với branch Register mới của Minh (`ad7c208`).
6. Trang `Pages/Account/Login` tạo ticket cookie `AIVES.Auth`, redirect tới ReturnUrl nội bộ hoặc `/`. Không đưa hash/token/mật khẩu vào trang tài khoản.
7. `MvcCookieEvents.ValidatePrincipal` kiểm tra User từ SQL trên mỗi request có cookie; khóa/ngừng hoạt động/xóa tài khoản sẽ thu hồi phiên ở request tiếp theo.
8. `POST /Account/Logout` có antiforgery và xóa cookie của trình duyệt hiện tại.

| Thành phần | Reference trong code | Mục đích |
|---|---|---|
| Presentation | `Pages/Account`, `ViewModels/LoginViewModel.cs` | Form, validation, redirect và phiên trình duyệt |
| Business | `Services/Authentication/LoginService.cs`, `Interfaces/Authentication/IPasswordVerifier.cs` | Nghiệp vụ xác thực |
| DataAccess | `Repositories/LoginUserRepository.cs`, `Contracts/Authentication/ILoginUserRepository.cs` | Truy vấn tài khoản từ DB thật |
| Business | `Integrations/Authentication/IdentityPasswordVerifier.cs` | Xác thực hash Identity |
| Business | `Integrations/Authentication/JwtAccessTokenIssuer.cs`, `JwtOptions.cs` | Phát JWT HS256 cho consumer đã xác thực |
| Presentation | `Authentication/AuthConfiguration.cs`, `MvcCookieEvents.cs` | Chọn scheme, validate JWT và bảo vệ phiên trình duyệt |

Cookie events và PageModel nhận LoginService qua constructor injection. Business đăng ký dịch vụ xác thực và gọi DataAccess đăng ký repository; Presentation không reference DataAccess. Chiều tham chiếu và vòng đời đối tượng được mô tả tại [ARCHITECTURE.md](ARCHITECTURE.md).

## Cookie và JWT

MVC dùng scheme mặc định `Cookies`, cookie HttpOnly, SameSite=Lax, hạn phiên 60 phút, không sliding và không persistent. Khi chạy HTTPS cookie có Secure; HTTP local/Docker vẫn dùng được. Triển khai production cần HTTPS.

Fallback policy yêu cầu đăng nhập cho các trang nghiệp vụ. Login, trang lỗi và static assets được phép truy cập ẩn danh. POST Login/Logout đều kiểm tra antiforgery. ReturnUrl chỉ chấp nhận URL nội bộ.

JWT là dịch vụ được giữ để tích hợp API/mobile sau này: consumer gọi `LoginService.AuthenticateAsync`, sau khi thành công gọi `IAccessTokenIssuer.Issue`. Không có endpoint REST phát token trên branch này; form MVC không phát token cho trình duyệt. Endpoint muốn dùng JWT phải chọn rõ `[Authorize(AuthenticationSchemes = "Bearer")]`.

Token chứa `sub`, `email`, `name`, `jti`, `iat`, `nbf`, `exp`; chữ ký HS256, validate issuer/audience/signature/lifetime, clock skew = 0. Key ít nhất 32 byte UTF-8; issuer/audience bắt buộc; thời hạn 1–1440 phút. Cấu hình sai làm startup thất bại. Token không chứa PasswordHash. Logout cookie không thu hồi JWT đã phát; JWT có hiệu lực tới exp, chưa có refresh token/revocation store.

## Cấu hình cá nhân và chạy local

`AIpoweredVivaExamSystem.Presentation/appsettings.json` được commit chỉ có mẫu connection string và Jwt key trống. File `appsettings.Development.json` và `.env` chứa cấu hình riêng, bị ignore; Docker build và publish loại bỏ file Development. Cả hai appsettings đều Always copy khi build để sửa cấu hình có hiệu lực sau build.

Đọc cấu hình trực tiếp qua `ConnectionStrings:DefaultConnection`, không User Secrets. Với Docker, connection string môi trường của service `web` ghi đè cấu hình SQL local; `Jwt__Key` lấy từ `JWT_KEY` trong `.env`. Cần key ngẫu nhiên thật, không dùng giá trị mẫu `.env.example`. SQL local và SQL Docker là hai instance khác nhau; có thể chạy song song nếu khác port. Override riêng của Long dùng Docker SQL `127.0.0.1:14330`; file override không commit.

Chạy từ root worktree mới:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project AIpoweredVivaExamSystem.Presentation --no-launch-profile --urls http://127.0.0.1:5088
```

Mở `http://127.0.0.1:5088/Account/Login`. Tài khoản cần tồn tại trong Users với hash Identity, Status=Active và DeletedAt=NULL. Smoke tool dùng tài khoản tạm và dọn ngay sau test, nên không để lại một tài khoản/mật khẩu mẫu trong DB.

## Test và smoke test với SQL thật

Suite dùng SQL Server local nhưng tạo/xóa DB test riêng `AivesRubricTests_<guid>`; không xóa DB ứng dụng. Test factory cấu hình connection của fixture và Jwt key test qua IConfiguration. Probe `/_tests/jwt` nằm trong assembly Tests, chỉ factory test nạp vào host; production không có route đó.

```powershell
$localConfig = Get-Content AIpoweredVivaExamSystem.Presentation/appsettings.Development.json -Raw | ConvertFrom-Json
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

## Current limits

Cookie authentication reloads account status and roles from SQL on each request. There is no security stamp to revoke all sessions after a password change. JWT validates signature, claims and expiry; it does not recheck the SQL account on every request and has no refresh-token or revocation store. Cookie logout does not revoke an already-issued JWT.
