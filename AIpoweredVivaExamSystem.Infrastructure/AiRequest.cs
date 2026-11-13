using System.Net;

namespace AIpoweredVivaExamSystem.Infrastructure;

internal static class AiRequest
{
    // HttpClient.Timeout is also the budget for all attempts, including buffering and backoff.
    public static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpRequestMessage template, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(http.Timeout);
        var token = budget.Token;
        var body = template.Content is null ? null : await template.Content.ReadAsByteArrayAsync(token);

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(template.Method, template.RequestUri);
            foreach (var header in template.Headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (body is not null)
            {
                request.Content = new ByteArrayContent(body);
                foreach (var header in template.Content!.Headers)
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            try
            {
                var response = await http.SendAsync(request, token);
                if (attempt >= 2 || !IsTransient(response.StatusCode))
                    return response;
                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < 2 && !token.IsCancellationRequested) { }

            await Task.Delay(TimeSpan.FromMilliseconds(attempt == 0 ? 250 : 750), token);
        }
    }

    private static bool IsTransient(HttpStatusCode status) => status is
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
        HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}
