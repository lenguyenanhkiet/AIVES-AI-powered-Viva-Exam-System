using System.Net;
using System.Text;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Infrastructure.QuestionBank;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public class QuestionGeneratorTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
    [Fact]
    public async Task AdapterSendsSourcesAndParsesCitedJson()
    {
        var chunk = new DocumentChunk(Guid.NewGuid(), 1, "MVC separates concerns.");
        using var client = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https://api.openai.com/v1/chat/completions", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("json_object", body); Assert.Contains(chunk.Id.ToString(), body);
            var result = System.Text.Json.JsonSerializer.Serialize(new { questions = new[] { new { content = "Explain MVC.", expectedAnswer = "It separates concerns.", chunkId = chunk.Id, quote = "MVC separates concerns." } } });
            var envelope = System.Text.Json.JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = result } } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope, Encoding.UTF8, "application/json") };
        }));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Ai:ApiKey"] = "test-key-not-real", ["Ai:Model"] = "test-model" }).Build();
        var drafts = await new OpenAiQuestionGenerator(client, configuration).GenerateAsync("MVC", BloomLevel.Understand, QuestionDifficulty.Medium, 1, [chunk], default);
        Assert.Equal(chunk.Id, Assert.Single(drafts).ChunkId);
    }
    [Theory]
    [InlineData(429, "{}")]
    [InlineData(200, "not json")]
    [InlineData(200, "{\"choices\":[{\"finish_reason\":\"length\"}]}")]
    public async Task AdapterRejectsProviderErrorsAndMalformedOrTruncatedResponses(int status, string body)
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) })));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Ai:ApiKey"] = "fake", ["Ai:Model"] = "fake" }).Build();
        await Assert.ThrowsAsync<ResourceConflictException>(() => new OpenAiQuestionGenerator(client, config).GenerateAsync("MVC", BloomLevel.Understand, QuestionDifficulty.Medium, 1, [], default));
    }
}
