using MtgDeckCli.Models;

namespace MtgDeckCli.Output;

public static class DeckListWriter
{
    public static string WriteDeckTxt(DeckResult deck)
    {
        var lines = new List<string>
        {
            $"1 {deck.Commander.Name}"
        };

        foreach (var g in deck.Mainboard
                     .Where(c => !c.Name.Equals(deck.Commander.Name, StringComparison.OrdinalIgnoreCase))
                     .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{g.Count()} {g.Key}");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}