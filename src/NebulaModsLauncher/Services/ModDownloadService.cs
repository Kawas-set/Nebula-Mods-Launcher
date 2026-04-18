using System.Net.Http.Headers;
using ModLauncher.Models;

namespace ModLauncher.Services;

public sealed class ModDownloadService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaModsLauncher/1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public async Task<string> DownloadAssetAsync(
        string modId,
        GitHubReleaseAsset asset,
        CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(LauncherPaths.DownloadsRoot, modId);
        Directory.CreateDirectory(folder);

        var filePath = Path.Combine(folder, asset.Name);

        using var response = await Http.GetAsync(
            asset.BrowserDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(filePath);

        await source.CopyToAsync(target, cancellationToken);

        return filePath;
    }
}
