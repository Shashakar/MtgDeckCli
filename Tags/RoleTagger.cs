using System.Text.RegularExpressions;
using MtgDeckCli.Models;
using MtgDeckCli.Scryfall;

namespace MtgDeckCli.Tags;

public static class RoleTagger
{
    public static CardRole TagRoles(ScryfallCardDto c)
    {
        var o = Oracle(c);
        var type = c.TypeLine ?? "";

        // Lands: keep roles empty (mana source evaluation is separate)
        if (type.Contains("Land", StringComparison.OrdinalIgnoreCase))
            return CardRole.None;

        CardRole roles = CardRole.None;

        // ===== Ramp (nonland) =====
        // Tap-for-mana artifacts/creatures (rocks/dorks)
        if (Regex.IsMatch(o, @"\{T\}:\s*Add\b", RegexOptions.IgnoreCase))
            roles |= CardRole.Ramp;

        // Land ramp spells
        if (o.Contains("search your library for a land", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("search your library for up to", StringComparison.OrdinalIgnoreCase) && o.Contains("land", StringComparison.OrdinalIgnoreCase))
            roles |= CardRole.Ramp;
        
        // ===== Draw =====
        // Payoff triggers: "Whenever you draw..." / "When you draw..." should NOT imply the card draws.
        bool drawPayoffTrigger = Regex.IsMatch(o, @"\b(when|whenever)\s+you\s+draw\b", RegexOptions.IgnoreCase);
        if (drawPayoffTrigger)
            roles |= CardRole.Payoff;

        // Strip "when/whenever you draw ..." clauses before checking for actual draw instructions.
        string oNoDrawPayoffs = Regex.Replace(
            o,
            @"\b(when|whenever)\s+you\s+draw\b[^.\n]*[.\n]?",
            "",
            RegexOptions.IgnoreCase
        );

        // Actual draw sources (after stripping draw-payoff clauses)
        bool anyDraw = HasActualDrawInstruction(o);
        
        if (anyDraw) roles |= CardRole.Draw;


        if (anyDraw)
            roles |= CardRole.Draw;

        bool groupDraw =
            oNoDrawPayoffs.Contains("each player draws", StringComparison.OrdinalIgnoreCase) ||
            oNoDrawPayoffs.Contains("each opponent draws", StringComparison.OrdinalIgnoreCase);

        if (groupDraw)
        {
            roles |= CardRole.Draw;
            roles |= CardRole.GroupDraw;
        }

        // ===== Removal =====
        if (o.Contains("destroy target", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("exile target", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("counter target", StringComparison.OrdinalIgnoreCase))
            roles |= CardRole.Removal;

        // ===== Wipes =====
        if (o.Contains("destroy all", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("exile all", StringComparison.OrdinalIgnoreCase))
            roles |= CardRole.Wipe;

        // ===== Protection =====
        if (o.Contains("hexproof", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("indestructible", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("phase out", StringComparison.OrdinalIgnoreCase))
            roles |= CardRole.Protection;

        // recursion-ish protection (keep conservative)
        if (o.Contains("return", StringComparison.OrdinalIgnoreCase) &&
            o.Contains("from your graveyard", StringComparison.OrdinalIgnoreCase))
            roles |= CardRole.Protection;

        // ===== Tutor =====
        if (o.Contains("search your library for", StringComparison.OrdinalIgnoreCase) &&
            !o.Contains("search your library for a land", StringComparison.OrdinalIgnoreCase))
            roles |= CardRole.Tutor;

        // ===== WinCons (conservative) =====
        if (ContainsAny(o,
                "you win the game",
                "target player loses the game",
                "each opponent loses the game"))
        {
            roles |= CardRole.WinCon;
        }

        // Named “classic” wincons that don’t always contain “you win the game”
        if (c.Name != null && NamedWinCons.Contains(c.Name))
            roles |= CardRole.WinCon;
        
        var m = Regex.Match(o, @"create (?:a|an) (\d+)\/(\d+)", RegexOptions.IgnoreCase);
        if (m.Success &&
            int.TryParse(m.Groups[1].Value, out var p) &&
            int.TryParse(m.Groups[2].Value, out var t) &&
            Math.Max(p, t) >= 5)
        {
            roles |= CardRole.WinCon;
        }

        if (!roles.HasFlag(CardRole.WinCon) && o.Contains("creatures you control", StringComparison.OrdinalIgnoreCase))
        {
            bool grantsEvasion =
                o.Contains("can't be blocked", StringComparison.OrdinalIgnoreCase) ||
                o.Contains("have flying", StringComparison.OrdinalIgnoreCase) ||
                o.Contains("have trample", StringComparison.OrdinalIgnoreCase) ||
                o.Contains("have menace", StringComparison.OrdinalIgnoreCase) ||
                o.Contains("double strike", StringComparison.OrdinalIgnoreCase);

            bool bigPump = Regex.IsMatch(o, @"creatures you control get \+(2|3|4|5)\/\+(2|3|4|5)", RegexOptions.IgnoreCase);

            if (grantsEvasion || bigPump)
                roles |= CardRole.WinCon;
        }
        
        if (!roles.HasFlag(CardRole.WinCon) && o.Contains("extra turn", StringComparison.OrdinalIgnoreCase))
            roles |= CardRole.WinCon;


        // ===== Payoffs =====
        // IMPORTANT: do NOT tag "whenever" as payoff. That labels half the game.
        // Instead: only tag payoff if it matches one of your requested themes
        // (ThemeTagger already handles this), OR keep it empty and let evaluator suggest.
        // For now: leave Payoff unset here.

        return roles;
    }

    private static readonly HashSet<string> NamedWinCons = new(StringComparer.OrdinalIgnoreCase)
    {
        "Approach of the Second Sun",
        "Thassa's Oracle",
        "Laboratory Maniac",
        "Jace, Wielder of Mysteries",
        "Revel in Riches",
        "Felidar Sovereign",
        "Test of Endurance"
    };

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (text.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static string Oracle(ScryfallCardDto c)
    {
        if (!string.IsNullOrWhiteSpace(c.OracleText)) return c.OracleText!;
        if (c.Faces is null || c.Faces.Length == 0) return "";
        return string.Join("\n", c.Faces.Select(f => f.OracleText).Where(t => !string.IsNullOrWhiteSpace(t)));
    }
    
    private static string StripDrawPayoffClauses(string o)
    {
        if (string.IsNullOrWhiteSpace(o)) return "";

        // Remove clauses like:
        // "Whenever you draw a card, ...", "When you draw your second card each turn, ..."
        // Keep it conservative: just strip the trigger clause up to sentence end.
        return Regex.Replace(
            o,
            @"\b(when|whenever)\s+you\s+draw\b[^.\n]*[.\n]?",
            "",
            RegexOptions.IgnoreCase
        );
    }

    private static bool IsDrawPayoffTrigger(string o)
    {
        return Regex.IsMatch(o, @"\b(when|whenever)\s+you\s+draw\b", RegexOptions.IgnoreCase);
    }

    private static bool HasActualDrawInstruction(string o)
    {
        if (string.IsNullOrWhiteSpace(o)) return false;
    
        // Pseudo-draw (Impulse draw)
        if (o.Contains("exile the top", StringComparison.OrdinalIgnoreCase) &&
            o.Contains("you may play", StringComparison.OrdinalIgnoreCase))
            return true;
    
        // Look for "draw ... card(s)" occurrences and reject the ones that are only payoffs/modifiers.
        foreach (Match m in Regex.Matches(o, @"\bdraw\b[^.\n]{0,60}\bcards?\b", RegexOptions.IgnoreCase))
        {
            // Grab some prefix context immediately before "draw"
            var start = Math.Max(0, m.Index - 80);
            var prefix = o.Substring(start, m.Index - start);
    
            // Ignore payoff triggers: "When/Whenever you draw..."
            // We only care if the phrase right before draw looks like "... when/whenever you"
            if (Regex.IsMatch(prefix, @"\b(when|whenever)\s+you\s*$", RegexOptions.IgnoreCase))
                continue;
    
            // Ignore draw modifiers: "If you would draw..., draw ... instead"
            // These don't generate draws on their own; they amplify other draw sources.
            if (Regex.IsMatch(prefix, @"\bif\s+you\s+would\s*$", RegexOptions.IgnoreCase))
                continue;
    
            return true;
        }
    
        return false;
    }



}
