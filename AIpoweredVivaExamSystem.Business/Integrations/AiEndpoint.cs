using Microsoft.Extensions.Configuration;

namespace AIpoweredVivaExamSystem.Business.Integrations;

internal static class AiEndpoint
{
    private const string DefaultBaseUrl = "https://api.z.ai/api/coding/paas/v4";

    public static string Model(IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration["Ai:Model"]) ? "glm-5.3-flash" : configuration["Ai:Model"]!.Trim();

    // Ai:BaseUrl points the app at any OpenAI-compatible provider; empty keeps z.ai.
    public static string Url(IConfiguration configuration, string path)
    {
        var baseUrl = configuration["Ai:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = DefaultBaseUrl;
        return $"{baseUrl.Trim().TrimEnd('/')}/{path}";
    }
}
