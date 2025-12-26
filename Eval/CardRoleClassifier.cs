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

        // One-shot: ETB/LTB/dies draw (Toothy-style, Titan ETB-only, etc.)
        if (Regex.IsMatch(o, @"\b(enters the battlefield|dies|leaves the battlefield)\b[^.\n]*\bdraw\b",
                RegexOptions.IgnoreCase))
            return false;

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
