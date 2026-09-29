using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    public string ConnectionString { get; private set; } = string.Empty;
    public HttpClient Client { get; private set; } = null!;

    public ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("AIVES_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set AIVES_TEST_CONNECTION to a SQL Server connection. Tests create and remove a separate AivesRubricTests_<guid> database.");
        var builder = new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = "AivesRubricTests_" + Guid.NewGuid().ToString("N")
        };
        ConnectionString = builder.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Testing");
            web.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ConnectionString));
            });
        });
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
    }

    public async Task<Guid> SeedQuestionAsync()
    {
        await using var context = CreateContext();
        var subject = new Subject("TEST_" + Guid.NewGuid().ToString("N"), "Test subject");
        var topic = new Topic(subject.Id, "Test topic");
        var question = new Question(subject.Id, "Explain dependency injection.", BloomLevel.Understand,
            QuestionDifficulty.Medium, topic: topic);
        context.AddRange(subject, topic, question);
        await context.SaveChangesAsync();
        return question.Id;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
        if (ConnectionString.Length > 0)
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}

[CollectionDefinition("SQL Server")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
