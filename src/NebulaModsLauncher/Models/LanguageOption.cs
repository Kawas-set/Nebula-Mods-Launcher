namespace ModLauncher.Models;

public sealed record LanguageOption(string Key, string DisplayName)
{
    public static readonly LanguageOption Russian = new("ru", "Русский");
    public static readonly LanguageOption English = new("en", "English");

    public static IReadOnlyList<LanguageOption> All { get; } =
    [
        Russian,
        English
    ];

    public static LanguageOption FromKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return English;

        return All.FirstOrDefault(option =>
                   option.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
               ?? English;
    }

    public override string ToString() => DisplayName;
}
