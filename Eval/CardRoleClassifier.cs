using System.Text.RegularExpressions;
using MtgDeckCli.Models;

namespace MtgDeckCli.Eval;

public static class CardRoleClassifier
{
    public static bool IsDrawEngine(Card c)
    {
        if (!c.Roles.HasFlag(CardRole.Draw)) return false;
        if (c.Roles.HasFlag(CardRole.Cantrip)) return false;
        if (c.Roles.HasFlag(CardRole.GroupDraw)) return false;
        if (c.IsLand) return false;

        var type = c.TypeLine ?? "";
        var o = c.OracleText ?? "";

        // Instants/sorceries are draw spells.
        if (type.Contains("Instant", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Sorcery", StringComparison.OrdinalIgnoreCase))
            return false;

        // One-shot: sacrifice-to-draw (Mind Stone, Commander's Sphere).
        if (Regex.IsMatch(o, @"\bsacrifice\b[^.\n]*\bdraw\b", RegexOptions.IgnoreCase))
            return false;

        // One-shot: counter-threshold burst draw (Midnight Clock, etc.)
        if (Regex.IsMatch(o, @"\bwhen\b[^.\n]{0,160}\b(counter|counters)\b[^.\n]{0,160}\bdraw\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(o, @"\b(\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|twelfth)\b", RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (Regex.IsMatch(o, @"\bbecomes level\b[^.\n]{0,120}\bdraw\b", RegexOptions.IgnoreCase))
            return false;
        
        // One-shot: the CARD ITSELF ETB/LTB/dies draw (Toothy, Titan ETB-only, etc.)
// Do NOT exclude things like "equipped creature dies" (Skullclamp) — those are repeatable engines.
        var name = c.Name ?? "";
        var selfRef = $@"(?:this|~|{Regex.Escape(name)})";

        if (Regex.IsMatch(o,
                $@"\b(when|whenever)\b[^.\n]{{0,60}}\b{selfRef}\b[^.\n]{{0,60}}\b(enters the battlefield|dies|leaves the battlefield)\b[^.\n]{{0,120}}\bdraw\b",
                RegexOptions.IgnoreCase))
        {
            return false;
        }


        // True engine patterns:
        // Activated draw (planeswalkers, repeatable activations) - but not sacrifice (excluded above)
        if (Regex.IsMatch(o, @":\s*Draw\b", RegexOptions.IgnoreCase))
            return true;

        // Replacement/extra draw effects (Ageless Insight)
        if (o.Contains("if you would draw", StringComparison.OrdinalIgnoreCase))
            return true;

        // Triggered draw that ISN’T “when/whenever you draw...” (those should be Payoffs, not Draw now)
        if (Regex.IsMatch(o, @"\b(when|whenever)\b(?![^.\n]*\byou\s+draw\b)[^.\n]{0,160}\bdraw\b",
                RegexOptions.IgnoreCase))
            return true;

        // Upkeep/turn-cycle engines: keep sentence-local
        if (Regex.IsMatch(o, @"\bat the beginning\b[^.\n]{0,120}\bdraw\b", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    public static bool IsDrawSpell(Card c)
    {
        if (!c.Roles.HasFlag(CardRole.Draw)) return false;
        if (c.Roles.HasFlag(CardRole.Cantrip)) return false;
        if (c.Roles.HasFlag(CardRole.GroupDraw)) return false;
        return !IsDrawEngine(c);
    }
}
