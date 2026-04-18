using System.Text.Json;
using ModLauncher.Models;

namespace ModLauncher.Services;

public sealed class ModCatalogService
{
    public async Task<IReadOnlyList<ModCatalogEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "mods.json");

        if (!File.Exists(path))
            return Array.Empty<ModCatalogEntry>();

        await using var stream = File.OpenRead(path);

        var items = await JsonSerializer.DeserializeAsync<List<ModCatalogEntry>>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken);

        return items is null ? Array.Empty<ModCatalogEntry>() : items;
    }
}
