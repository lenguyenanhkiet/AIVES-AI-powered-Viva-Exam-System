using AIpoweredVivaExamSystem.Persistence;
var builder = WebApplication.CreateBuilder(args);
// Ghép nghiệp vụ Login và verifier mật khẩu qua DI.
AIpoweredVivaExamSystem.Application.DependencyInjection.AddApplication(builder.Services);
AIpoweredVivaExamSystem.Infrastructure.DependencyInjection.AddInfrastructure(builder.Services);
builder.Services.AddPersistence(builder.Configuration);
AIpoweredVivaExamSystem.Api.Authentication.JwtConfiguration.AddJwtAuthentication(builder.Services, builder.Configuration);
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
AIpoweredVivaExamSystem.Api.Authentication.JwtConfiguration.AddJwtSwagger(builder.Services);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Xác định danh tính từ JWT trước khi đánh giá quyền trên endpoint.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Cho phép test khởi động API thật bằng WebApplicationFactory.
public partial class Program { }
