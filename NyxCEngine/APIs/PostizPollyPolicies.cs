using Polly;
using Polly.Extensions.Http;
using System.Net;

namespace NyxCEngine.APIs
{
  internal static class PostizPollyPolicies
  {
    // Retry transient HTTP + 429/5xx with jittered backoff
    public static IAsyncPolicy<HttpResponseMessage> RetryWithJitter()
    {
      var jitterer = new Random();

      return HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx + HttpRequestException + 408
        .OrResult(r =>
          r.StatusCode == (HttpStatusCode)429 ||
          r.StatusCode == HttpStatusCode.ServiceUnavailable ||
          r.StatusCode == HttpStatusCode.BadGateway ||
          r.StatusCode == HttpStatusCode.GatewayTimeout
        )
        .WaitAndRetryAsync(
          retryCount: 5,
          sleepDurationProvider: retryAttempt =>
          {
            // exponential backoff + jitter
            var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
            var jitter = TimeSpan.FromMilliseconds(jitterer.Next(0, 750));
            return baseDelay + jitter;
          },
          onRetryAsync: async (outcome, delay, attempt, ctx) =>
          {
            // Optional: add logging here if you have ILogger available
            await Task.CompletedTask;
          });
    }

    // Extra timeout guard (in addition to HttpClient.Timeout)
    public static IAsyncPolicy<HttpResponseMessage> TimeoutPolicy() => Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(10));
  }
}
