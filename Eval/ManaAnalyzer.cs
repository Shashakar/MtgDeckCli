using System.Text.RegularExpressions;
using MtgDeckCli.Models;

namespace MtgDeckCli.Eval;

public static class ManaAnalyzer
{
    private static readonly Regex ManaCostSymbol = new(@"\{([WUBRG])\}", RegexOptions.Compiled);
    private static readonly Regex ProducesSymbol = new(@"Add\s+(.+?)(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BracedSymbol = new(@"\{([WUBRG])\}", RegexOptions.Compiled);

    public static ManaColorStats Analyze(IReadOnlyList<Card> cards)
    {
        int wD=0,uD=0,bD=0,rD=0,gD=0;
        int wS=0,uS=0,bS=0,rS=0,gS=0;

        foreach (var c in cards)
        {
            // Demand: parse mana cost-like symbols if you store them; otherwise approximate from OracleText is hard.
            // If your DTO includes ManaCost, use that instead. For now, use OracleText as fallback only if it contains explicit {W} style costs.
            foreach (Match m in ManaCostSymbol.Matches(c.OracleText ?? ""))
            {
                switch (m.Groups[1].Value)
                {
                    case "W": wD++; break;
                    case "U": uD++; break;
                    case "B": bD++; break;
                    case "R": rD++; break;
                    case "G": gD++; break;
                }
            }

            // Sources: count cards that produce mana (lands + rocks + dorks)
            // (In future: prefer dto.produced_mana when available :contentReference[oaicite:2]{index=2})
            var text = c.OracleText ?? "";
            if (!text.Contains("Add", StringComparison.OrdinalIgnoreCase)) continue;

            // crude: count if it can add each color at least once
            if (text.Contains("{W}", StringComparison.OrdinalIgnoreCase)) wS++;
            if (text.Contains("{U}", StringComparison.OrdinalIgnoreCase)) uS++;
            if (text.Contains("{B}", StringComparison.OrdinalIgnoreCase)) bS++;
            if (text.Contains("{R}", StringComparison.OrdinalIgnoreCase)) rS++;
            if (text.Contains("{G}", StringComparison.OrdinalIgnoreCase)) gS++;
        }

        return new ManaColorStats(wS,uS,bS,rS,gS, wD,uD,bD,rD,gD);
    }
}
