using MtgDeckCli.Scryfall;

namespace MtgDeckCli.Tags;

public static class ThemeTagger
{
    // Minimal keyword mapping. Add as you go—this is intentionally small.
    private static readonly Dictionary<string, string[]> ThemeToKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lifegain"] = new[] { "gain life" },
        ["punish_lifegain"] = new[] { "whenever an opponent gains life", "if an opponent would gain life" },
        ["group_hug"] = new[] { "each player draws", "each player may", "each opponent may", "for each player" },
        ["tokens"] = new[] { "create", "token" },
        ["graveyard"] = new[] { "from your graveyard", "return target", "mill" },
        ["spellslinger"] = new[] { "instant or sorcery", "whenever you cast" },
    };

    public static IReadOnlyList<string> TagThemes(ScryfallCardDto c, IReadOnlyList<string> requestedThemes)
    {
        var oracle = RoleTagger.Oracle(c);
        var tags = new List<string>();

        foreach (var theme in requestedThemes)
        {
            if (!ThemeToKeywords.TryGetValue(theme, out var keys)) continue;

            foreach (var k in keys)
            {
                if (oracle.Contains(k, StringComparison.OrdinalIgnoreCase))
                {
                    tags.Add(theme.ToLowerInvariant());
                    break;
                }
            }
        }

        return tags.Distinct().ToArray();
    }
}