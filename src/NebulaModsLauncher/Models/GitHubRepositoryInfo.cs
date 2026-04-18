using System.Text.Json.Serialization;

namespace ModLauncher.Models;

public sealed class GitHubRepositoryInfo
{
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("license")]
    public GitHubRepositoryLicense? License { get; set; }
}

public sealed class GitHubRepositoryLicense
{
    [JsonPropertyName("spdx_id")]
    public string? SpdxId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
