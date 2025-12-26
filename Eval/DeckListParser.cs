using System.Text.RegularExpressions;

namespace MtgDeckCli.Eval;

public static class DeckListParser
{
    private static readonly Regex CountLine = new(@"^\s*(\d+)\s+(.+?)\s*$", RegexOptions.Compiled);

    public static IReadOnlyList<string> ParseNames(string text)
    {
        var names = new List<string>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#")) continue;
            if (line.StartsWith("//")) continue;
            if (line.StartsWith("Sideboard", StringComparison.OrdinalIgnoreCase)) break;

            // common headers your writer may include
            if (line.StartsWith("Commander:", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("Mainboard", StringComparison.OrdinalIgnoreCase)) continue;

            var m = CountLine.Match(line);
            if (m.Success)
            {
                int count = int.Parse(m.Groups[1].Value);
                string name = m.Groups[2].Value.Trim();
                for (int i = 0; i < count; i++) names.Add(name);
            }
            else
            {
                // tolerate “Card Name” lines
                names.Add(line);
            }
        }

        return names;
    }
    
    public static (string CommanderName, IReadOnlyList<string> MainboardNames)
        ParseCommanderAndMainboard(string text, string? commanderOverride)
    {
        var names = ParseNames(text).ToList();

        // If caller supplied commander explicitly, trust it and remove one copy if present
        if (!string.IsNullOrWhiteSpace(commanderOverride))
        {
            var cmd = commanderOverride.Trim();

            // Remove ONE instance of commander from the parsed list if it exists
            var idx = names.FindIndex(n => n.Equals(cmd, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) names.RemoveAt(idx);

            return (cmd, names);
        }

        // Heuristic: if the list is 100 cards, assume first entry is commander (matches your writer output)
        if (names.Count == 100)
        {
            var cmd = names[0];
            names.RemoveAt(0);
            return (cmd, names);
        }

        // If it’s already 99, commander is unknown unless explicitly provided
        if (names.Count == 99)
            return ("", names);

        // Otherwise: unknown commander + whatever mainboard we have
        return ("", names);
    }

}