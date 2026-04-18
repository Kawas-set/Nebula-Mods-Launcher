using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModLauncher.Models;

namespace ModLauncher.Services;

public sealed class GitHubReleaseClient
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan IncompleteRepositoryCacheLifetime = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private const int MaxAttempts = 3;

    private static readonly HttpClient ApiHttp = CreateApiClient();
    private static readonly HttpClient PageHttp = CreatePageClient();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, ReleaseCacheEntry> ReleaseCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RepositoryCacheEntry> RepositoryCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex MarkdownBodyRegex = new(
        "<div[^>]*class=\"[^\"]*markdown-body[^\"]*\"[^>]*>(?<body>[\\s\\S]*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RelativeTimeRegex = new(
        "<relative-time[^>]*datetime=\"(?<datetime>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleRegex = new(
        "<title>\\s*Release\\s+(?<name>.*?)\\s+·",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(
        "\\s+",
        RegexOptions.Compiled);

    private static readonly Regex AssetHrefRegex = new(
        "href=\"(?<href>/[^\"#?]+/releases/download/[^\"#?]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReleaseTagHrefRegex = new(
        "href=\"/(?<owner>[^\"/]+)/(?<repo>[^\"/]+)/releases/tag/(?<tag>[^\"#?]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LicenseRegex = new(
        "(?<license>(?:AGPL|Apache|Artistic|BSD|BSL|CC0|EPL|GPL|ISC|LGPL|MIT|MPL|MS-PL|MS-RL|NCSA|OFL|PostgreSQL|Unlicense|Zlib|GNU Affero General Public|GNU General Public|GNU Lesser General Public|Mozilla Public|Boost Software|Creative Commons Zero|European Union Public|Microsoft Public|Microsoft Reciprocal|SIL Open Font)[A-Za-z0-9.\\-+ ,()]*?(?:license|version\\s*[0-9.]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LicenseTabNameRegex = new(
        "\"preferredFileType\":\"license\"[\\s\\S]{0,1200}?\"tabName\":\"(?<license>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SpdxIdentifierRegex = new(
        "^(?:AGPL|Apache|Artistic|BSD|BSL|CC0|EPL|GPL|ISC|LGPL|MIT|MPL|MS-PL|MS-RL|NCSA|OFL|PostgreSQL|Unlicense|Zlib)[A-Za-z0-9.\\-+]*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static HttpClient CreateApiClient()
    {
        var client = new HttpClient
        {
            Timeout = RequestTimeout
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaModsLauncher/1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static HttpClient CreatePageClient()
    {
        var client = new HttpClient
        {
            Timeout = RequestTimeout
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaModsLauncher/1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        return client;
    }

    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            throw new ArgumentException("GitHub owner/repo is empty.");

        var cacheKey = $"{owner}/{repo}";

        if (TryGetCachedRelease(cacheKey, out var cachedRelease))
            return cachedRelease;

        try
        {
            var release = await GetLatestReleaseFromApiAsync(owner, repo, cancellationToken);
            CacheRelease(cacheKey, release);
            return release;
        }
        catch (GitHubReleaseException ex) when (ShouldUseHtmlFallback(ex))
        {
            var fallbackRelease = await TryGetLatestReleaseFromPageAsync(owner, repo, cancellationToken);
            if (fallbackRelease is not null)
            {
                CacheRelease(cacheKey, fallbackRelease);
                return fallbackRelease;
            }

            throw;
        }
    }

    public async Task<GitHubRepositoryInfo> GetRepositoryInfoAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            throw new ArgumentException("GitHub owner/repo is empty.");

        var cacheKey = $"{owner}/{repo}";
        if (TryGetCachedRepository(cacheKey, out var cachedRepository))
            return cachedRepository;

        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}";
            var repositoryInfo = await SendApiAsync(
                url,
                owner,
                repo,
                allowNotFound: false,
                static async (stream, cancellationToken) =>
                    await JsonSerializer.DeserializeAsync<GitHubRepositoryInfo>(
                        stream,
                        JsonOptions,
                        cancellationToken),
                cancellationToken)
                ?? CreateFallbackRepositoryInfo(owner, repo);

            if (string.IsNullOrWhiteSpace(repositoryInfo.HtmlUrl))
                repositoryInfo.HtmlUrl = BuildRepositoryUrl(owner, repo);

            if (!HasUsefulLicense(repositoryInfo.License))
            {
                var fallbackLicense = await GetRepositoryLicenseAsync(owner, repo, cancellationToken);
                if (HasUsefulLicense(fallbackLicense))
                {
                    repositoryInfo.License = fallbackLicense;
                }
                else
                {
                    fallbackLicense = await GetRepositoryLicenseFromPageAsync(owner, repo, cancellationToken);
                    if (HasUsefulLicense(fallbackLicense))
                        repositoryInfo.License = fallbackLicense;
                }
            }

            CacheRepository(cacheKey, repositoryInfo);
            return repositoryInfo;
        }
        catch
        {
            var fallback = CreateFallbackRepositoryInfo(owner, repo);
            var fallbackLicense = await GetRepositoryLicenseFromPageAsync(owner, repo, cancellationToken);
            if (HasUsefulLicense(fallbackLicense))
                fallback.License = fallbackLicense;

            CacheRepository(cacheKey, fallback);
            return fallback;
        }
    }

    private async Task<GitHubReleaseInfo?> GetLatestReleaseFromApiAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var latestRelease = await GetLatestStableReleaseAsync(owner, repo, cancellationToken);
        if (latestRelease is not null)
            return latestRelease;

        return await GetFallbackReleaseAsync(owner, repo, cancellationToken);
    }

    private async Task<GitHubReleaseInfo?> GetLatestStableReleaseAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        return await SendApiAsync(
            url,
            owner,
            repo,
            allowNotFound: true,
            static async (stream, cancellationToken) =>
                await JsonSerializer.DeserializeAsync<GitHubReleaseInfo>(
                    stream,
                    JsonOptions,
                    cancellationToken),
            cancellationToken);
    }

    private async Task<GitHubReleaseInfo?> GetFallbackReleaseAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=10";
        var releases = await SendApiAsync(
            url,
            owner,
            repo,
            allowNotFound: true,
            static async (stream, cancellationToken) =>
                await JsonSerializer.DeserializeAsync<List<GitHubReleaseInfo>>(
                    stream,
                    JsonOptions,
                    cancellationToken),
            cancellationToken);

        return releases?
            .Where(release => !release.IsDraft)
            .OrderByDescending(release => release.PublishedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    private async Task<GitHubRepositoryLicense?> GetRepositoryLicenseAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/license";
        var response = await SendApiAsync(
            url,
            owner,
            repo,
            allowNotFound: true,
            static async (stream, cancellationToken) =>
                await JsonSerializer.DeserializeAsync<GitHubRepositoryLicenseResponse>(
                    stream,
                    JsonOptions,
                    cancellationToken),
            cancellationToken);

        if (response?.License is null)
            return null;

        return new GitHubRepositoryLicense
        {
            SpdxId = response.License.SpdxId,
            Name = response.License.Name
        };
    }

    private async Task<GitHubRepositoryLicense?> GetRepositoryLicenseFromPageAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = BuildRepositoryUrl(owner, repo);

            using var request = CreatePageRequest(url);
            using var response = await PageHttp.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractLicenseFromRepositoryHtml(html);
        }
        catch
        {
            return null;
        }
    }

    private async Task<GitHubReleaseInfo?> TryGetLatestReleaseFromPageAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetLatestReleaseFromPageAsync(owner, repo, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<GitHubReleaseInfo?> GetLatestReleaseFromPageAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken)
    {
        var url = $"https://github.com/{owner}/{repo}/releases/latest";

        using var request = CreatePageRequest(url);
        using var response = await PageHttp.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var finalUri = response.RequestMessage?.RequestUri;
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var tagName = finalUri is null
            ? null
            : ExtractTagFromReleaseUri(finalUri);

        var detailHtml = html;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            tagName = ExtractTagFromReleaseListHtml(html, owner, repo);
            if (string.IsNullOrWhiteSpace(tagName))
                return null;

            detailHtml = await TryGetReleaseDetailHtmlAsync(owner, repo, tagName, cancellationToken) ?? html;
        }

        var assets = await GetReleaseAssetsFromPageAsync(owner, repo, tagName, cancellationToken);

        return new GitHubReleaseInfo
        {
            TagName = tagName,
            Name = ExtractReleaseNameFromHtml(detailHtml) ??
                   ExtractReleaseNameFromHtml(html) ??
                   tagName,
            Body = ExtractReleaseBodyFromHtml(detailHtml),
            PublishedAt = ExtractPublishedAtFromHtml(detailHtml) ??
                          ExtractPublishedAtFromHtml(html),
            Assets = assets
        };
    }

    private async Task<string?> TryGetReleaseDetailHtmlAsync(
        string owner,
        string repo,
        string tagName,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://github.com/{owner}/{repo}/releases/tag/{Uri.EscapeDataString(tagName)}";

            using var request = CreatePageRequest(url);
            using var response = await PageHttp.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<GitHubReleaseAsset>> GetReleaseAssetsFromPageAsync(
        string owner,
        string repo,
        string tagName,
        CancellationToken cancellationToken)
    {
        var url = $"https://github.com/{owner}/{repo}/releases/expanded_assets/{Uri.EscapeDataString(tagName)}";

        using var request = CreatePageRequest(url);
        using var response = await PageHttp.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var assets = new List<GitHubReleaseAsset>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AssetHrefRegex.Matches(html))
        {
            var relativeHref = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (!seen.Add(relativeHref))
                continue;

            var fileName = Path.GetFileName(relativeHref);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            assets.Add(new GitHubReleaseAsset
            {
                Name = Uri.UnescapeDataString(fileName),
                BrowserDownloadUrl = $"https://github.com{relativeHref}",
                Size = 0
            });
        }

        return assets;
    }

    private static async Task<T?> SendApiAsync<T>(
        string url,
        string owner,
        string repo,
        bool allowNotFound,
        Func<Stream, CancellationToken, Task<T?>> deserializeAsync,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var request = CreateApiRequest(url);
                using var response = await ApiHttp.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                    return default;

                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    return await deserializeAsync(stream, cancellationToken);
                }

                var bodyMessage = await ReadErrorMessageAsync(response, cancellationToken);
                var rateLimitResetAt = TryGetRateLimitResetAt(response);
                var shouldRetry = attempt < MaxAttempts &&
                    ShouldRetry(response.StatusCode, bodyMessage, rateLimitResetAt);

                if (shouldRetry)
                {
                    await Task.Delay(GetRetryDelay(attempt, rateLimitResetAt), cancellationToken);
                    continue;
                }

                throw CreateException(owner, repo, response.StatusCode, bodyMessage, rateLimitResetAt);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(GetRetryDelay(attempt, rateLimitResetAt: null), cancellationToken);
                    continue;
                }

                throw new GitHubReleaseException(
                    $"GitHub не ответил вовремя для {owner}/{repo}.",
                    innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(GetRetryDelay(attempt, rateLimitResetAt: null), cancellationToken);
                    continue;
                }

                throw new GitHubReleaseException(
                    $"Не удалось подключиться к GitHub для {owner}/{repo}: {ex.Message}",
                    innerException: ex);
            }
        }

        throw new GitHubReleaseException($"Не удалось получить релиз для {owner}/{repo}.");
    }

    private static HttpRequestMessage CreateApiRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var token = GetGitHubToken();

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private static HttpRequestMessage CreatePageRequest(string url)
    {
        return new HttpRequestMessage(HttpMethod.Get, url);
    }

    private static string? GetGitHubToken()
    {
        return Environment.GetEnvironmentVariable("GITHUB_TOKEN") ??
               Environment.GetEnvironmentVariable("GH_TOKEN");
    }

    private static bool TryGetCachedRelease(string cacheKey, out GitHubReleaseInfo? release)
    {
        release = null;

        if (!ReleaseCache.TryGetValue(cacheKey, out var entry))
            return false;

        if (DateTimeOffset.UtcNow - entry.FetchedAt > CacheLifetime)
        {
            ReleaseCache.TryRemove(cacheKey, out _);
            return false;
        }

        release = entry.Release;
        return true;
    }

    private static bool TryGetCachedRepository(string cacheKey, out GitHubRepositoryInfo repositoryInfo)
    {
        repositoryInfo = CreateFallbackRepositoryInfo("", "");

        if (!RepositoryCache.TryGetValue(cacheKey, out var entry))
            return false;

        var cacheLifetime = HasUsefulLicense(entry.Repository.License)
            ? CacheLifetime
            : IncompleteRepositoryCacheLifetime;

        if (DateTimeOffset.UtcNow - entry.FetchedAt > cacheLifetime)
        {
            RepositoryCache.TryRemove(cacheKey, out _);
            return false;
        }

        repositoryInfo = entry.Repository;
        return true;
    }

    private static void CacheRelease(string cacheKey, GitHubReleaseInfo? release)
    {
        ReleaseCache[cacheKey] = new ReleaseCacheEntry(release, DateTimeOffset.UtcNow);
    }

    private static void CacheRepository(string cacheKey, GitHubRepositoryInfo repositoryInfo)
    {
        RepositoryCache[cacheKey] = new RepositoryCacheEntry(repositoryInfo, DateTimeOffset.UtcNow);
    }

    private static bool ShouldUseHtmlFallback(GitHubReleaseException exception)
    {
        return exception.IsRateLimited ||
               exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests;
    }

    private static bool ShouldRetry(
        HttpStatusCode statusCode,
        string? bodyMessage,
        DateTimeOffset? rateLimitResetAt)
    {
        if (statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout or HttpStatusCode.TooManyRequests)
            return true;

        if (statusCode != HttpStatusCode.Forbidden)
            return false;

        if (!LooksLikeRateLimit(bodyMessage))
            return false;

        if (rateLimitResetAt is null)
            return true;

        var waitTime = rateLimitResetAt.Value - DateTimeOffset.UtcNow;
        return waitTime <= TimeSpan.FromSeconds(8);
    }

    private static TimeSpan GetRetryDelay(int attempt, DateTimeOffset? rateLimitResetAt)
    {
        if (rateLimitResetAt is not null)
        {
            var waitTime = rateLimitResetAt.Value - DateTimeOffset.UtcNow;
            if (waitTime > TimeSpan.Zero && waitTime <= TimeSpan.FromSeconds(8))
                return waitTime;
        }

        return TimeSpan.FromSeconds(Math.Min(attempt * 2, 6));
    }

    private static async Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<GitHubErrorResponse>(content, JsonOptions);
            if (!string.IsNullOrWhiteSpace(payload?.Message))
                return payload.Message;
        }
        catch
        {
            // Fall back to plain text below.
        }

        return content.Trim();
    }

    private static DateTimeOffset? TryGetRateLimitResetAt(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
            return DateTimeOffset.UtcNow.Add(delta);

        if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
            return date;

        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues) &&
            remainingValues.FirstOrDefault() == "0" &&
            response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues) &&
            long.TryParse(resetValues.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        return null;
    }

    private static GitHubReleaseException CreateException(
        string owner,
        string repo,
        HttpStatusCode statusCode,
        string? bodyMessage,
        DateTimeOffset? rateLimitResetAt)
    {
        var repoDisplay = $"{owner}/{repo}";

        if (statusCode == HttpStatusCode.Unauthorized)
        {
            return new GitHubReleaseException(
                "GitHub отклонил токен. Проверь GITHUB_TOKEN или GH_TOKEN.",
                statusCode);
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            return new GitHubReleaseException(
                $"Репозиторий или релизы для {repoDisplay} не найдены.",
                statusCode);
        }

        if (statusCode == HttpStatusCode.Forbidden || statusCode == HttpStatusCode.TooManyRequests)
        {
            var isRateLimited = LooksLikeRateLimit(bodyMessage) || statusCode == HttpStatusCode.TooManyRequests;

            if (isRateLimited)
            {
                var resetSuffix = rateLimitResetAt is null
                    ? ""
                    : $" Следующая попытка после {rateLimitResetAt.Value.ToLocalTime():g}.";

                return new GitHubReleaseException(
                    $"GitHub временно ограничил запросы.{resetSuffix} Можно подождать, задать GITHUB_TOKEN/GH_TOKEN или использовать HTML fallback.",
                    statusCode,
                    isRateLimited: true,
                    rateLimitResetAt: rateLimitResetAt);
            }
        }

        if ((int)statusCode >= 500)
        {
            return new GitHubReleaseException(
                $"GitHub временно недоступен для {repoDisplay}: {(int)statusCode}. Попробуй ещё раз чуть позже.",
                statusCode);
        }

        var details = string.IsNullOrWhiteSpace(bodyMessage)
            ? $"{(int)statusCode} {statusCode}"
            : bodyMessage;

        return new GitHubReleaseException(
            $"Ошибка GitHub для {repoDisplay}: {details}",
            statusCode);
    }

    private static bool LooksLikeRateLimit(string? bodyMessage)
    {
        if (string.IsNullOrWhiteSpace(bodyMessage))
            return false;

        return bodyMessage.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               bodyMessage.Contains("secondary rate limit", StringComparison.OrdinalIgnoreCase) ||
               bodyMessage.Contains("abuse detection", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractTagFromReleaseUri(Uri uri)
    {
        var marker = "/releases/tag/";
        var absoluteUri = uri.AbsoluteUri;
        var index = absoluteUri.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var encodedTag = absoluteUri[(index + marker.Length)..];
        if (string.IsNullOrWhiteSpace(encodedTag))
            return null;

        return Uri.UnescapeDataString(encodedTag.TrimEnd('/'));
    }

    private static string? ExtractTagFromReleaseListHtml(
        string html,
        string owner,
        string repo)
    {
        foreach (Match match in ReleaseTagHrefRegex.Matches(html))
        {
            var matchedOwner = WebUtility.HtmlDecode(match.Groups["owner"].Value);
            var matchedRepo = WebUtility.HtmlDecode(match.Groups["repo"].Value);

            if (!matchedOwner.Equals(owner, StringComparison.OrdinalIgnoreCase) ||
                !matchedRepo.Equals(repo, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var encodedTag = WebUtility.HtmlDecode(match.Groups["tag"].Value);
            if (string.IsNullOrWhiteSpace(encodedTag))
                continue;

            return Uri.UnescapeDataString(encodedTag.TrimEnd('/'));
        }

        return null;
    }

    private static string? ExtractReleaseNameFromHtml(string html)
    {
        var match = TitleRegex.Match(html);
        return match.Success
            ? WebUtility.HtmlDecode(match.Groups["name"].Value.Trim())
            : null;
    }

    private static DateTimeOffset? ExtractPublishedAtFromHtml(string html)
    {
        var match = RelativeTimeRegex.Match(html);
        if (!match.Success)
            return null;

        return DateTimeOffset.TryParse(
            match.Groups["datetime"].Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var publishedAt)
            ? publishedAt
            : null;
    }

    private static string ExtractReleaseBodyFromHtml(string html)
    {
        var match = MarkdownBodyRegex.Matches(html)
            .Select(found => found.Groups["body"].Value)
            .OrderByDescending(value => value.Length)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(match))
            return "Описание релиза доступно на GitHub.";

        var normalized = match
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<li>", "- ", StringComparison.OrdinalIgnoreCase)
            .Replace("</h1>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h2>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h3>", "\n", StringComparison.OrdinalIgnoreCase);

        normalized = HtmlTagRegex.Replace(normalized, string.Empty);
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = Regex.Replace(normalized, @"\r", "");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");

        return normalized.Trim();
    }

    private sealed record ReleaseCacheEntry(GitHubReleaseInfo? Release, DateTimeOffset FetchedAt);
    private sealed record RepositoryCacheEntry(GitHubRepositoryInfo Repository, DateTimeOffset FetchedAt);

    private static GitHubRepositoryInfo CreateFallbackRepositoryInfo(string owner, string repo)
    {
        return new GitHubRepositoryInfo
        {
            HtmlUrl = string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo)
                ? ""
                : BuildRepositoryUrl(owner, repo)
        };
    }

    private static string BuildRepositoryUrl(string owner, string repo)
    {
        return $"https://github.com/{owner}/{repo}";
    }

    private static bool HasUsefulLicense(GitHubRepositoryLicense? license)
    {
        if (license is null)
            return false;

        var spdx = license.SpdxId;
        if (!string.IsNullOrWhiteSpace(spdx) &&
            !spdx.Equals("NOASSERTION", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(license.Name);
    }

    private static GitHubRepositoryLicense? ExtractLicenseFromRepositoryHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var structuredLicense = ExtractStructuredLicenseFromRepositoryHtml(html);
        if (HasUsefulLicense(structuredLicense))
            return structuredLicense;

        var searchRegion = html.Length > 160_000
            ? html[..160_000]
            : html;

        var match = LicenseRegex.Match(searchRegion);
        if (match.Success)
            return CreateRepositoryLicense(match.Groups["license"].Value);

        var plainText = NormalizeHtmlToPlainText(html);
        if (string.IsNullOrWhiteSpace(plainText))
            return null;

        var focusedRegion = ExtractLicenseSearchRegion(plainText);
        match = LicenseRegex.Match(focusedRegion);
        if (!match.Success && !ReferenceEquals(focusedRegion, plainText))
            match = LicenseRegex.Match(plainText);

        return match.Success
            ? CreateRepositoryLicense(match.Groups["license"].Value)
            : null;
    }

    private static GitHubRepositoryLicense? ExtractStructuredLicenseFromRepositoryHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var tabNameMatch = LicenseTabNameRegex.Match(html);
        if (!tabNameMatch.Success)
            return null;

        var license = tabNameMatch.Groups["license"].Value;
        return CreateRepositoryLicense(license, license);
    }

    private static string NormalizeHtmlToPlainText(string html)
    {
        var text = HtmlTagRegex.Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        text = WhitespaceRegex.Replace(text, " ");
        return text.Trim();
    }

    private static string ExtractLicenseSearchRegion(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return plainText;

        var licenseMarkerIndex = plainText.IndexOf(" License ", StringComparison.OrdinalIgnoreCase);
        if (licenseMarkerIndex < 0)
            licenseMarkerIndex = plainText.IndexOf(" licensed under ", StringComparison.OrdinalIgnoreCase);

        if (licenseMarkerIndex < 0)
            return plainText;

        var start = Math.Max(0, licenseMarkerIndex - 32);
        var length = Math.Min(plainText.Length - start, 640);
        return plainText.Substring(start, length);
    }

    private static GitHubRepositoryLicense? CreateRepositoryLicense(string? rawDisplay, string? rawSpdxId = null)
    {
        if (string.IsNullOrWhiteSpace(rawDisplay) && string.IsNullOrWhiteSpace(rawSpdxId))
            return null;

        var display = NormalizeLicenseValue(rawDisplay);
        var spdxCandidate = NormalizeLicenseValue(rawSpdxId);

        if (string.IsNullOrWhiteSpace(display))
            display = spdxCandidate;

        if (string.IsNullOrWhiteSpace(display))
            return null;

        if (string.IsNullOrWhiteSpace(spdxCandidate) &&
            display.EndsWith(" license", StringComparison.OrdinalIgnoreCase))
        {
            var compactCandidate = display[..^" license".Length].Trim();
            if (compactCandidate.IndexOf(' ') < 0 && SpdxIdentifierRegex.IsMatch(compactCandidate))
                spdxCandidate = compactCandidate;
        }

        if (string.IsNullOrWhiteSpace(spdxCandidate) && SpdxIdentifierRegex.IsMatch(display))
            spdxCandidate = display;

        return new GitHubRepositoryLicense
        {
            SpdxId = spdxCandidate,
            Name = display
        };
    }

    private static string? NormalizeLicenseValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = WhitespaceRegex.Replace(WebUtility.HtmlDecode(value), " ").Trim();
        normalized = normalized.Trim('"', '\'', ',', '.', ';', ':');

        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Equals("NOASSERTION", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    private sealed class GitHubErrorResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private sealed class GitHubRepositoryLicenseResponse
    {
        [JsonPropertyName("license")]
        public GitHubRepositoryLicense? License { get; set; }
    }
}
