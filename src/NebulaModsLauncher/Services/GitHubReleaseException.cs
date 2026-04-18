using System.Net;

namespace ModLauncher.Services;

public sealed class GitHubReleaseException : Exception
{
    public GitHubReleaseException(
        string message,
        HttpStatusCode? statusCode = null,
        bool isRateLimited = false,
        DateTimeOffset? rateLimitResetAt = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        IsRateLimited = isRateLimited;
        RateLimitResetAt = rateLimitResetAt;
    }

    public HttpStatusCode? StatusCode { get; }

    public bool IsRateLimited { get; }

    public DateTimeOffset? RateLimitResetAt { get; }
}
