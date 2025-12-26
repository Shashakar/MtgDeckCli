using MtgDeckCli.Eval;
using MtgDeckCli.Models;

namespace MtgDeckCli.Output;

public static class EvalReportWriter
{
    public static string WriteHumanEvalReport(DeckEvaluationResult eval)
    {
        var r = eval.Report;
        var lines = new List<string>
        {
            $"Commander: {eval.Commander.Name}",
            $"Score: {eval.Score0To100}/100",
            $"Avg CMC (nonlands): {r.AvgCmc:0.00}",
            "",
            "Breakdown:",
            $"  Lands:        {r.Roles.Lands}",
            $"  Ramp:         {r.Roles.Ramp}",
            $"  Draw (total):  {r.Roles.DrawTotal}",
            $"    - Engines:   {r.Roles.DrawEngines}",
            $"    - Spells:    {r.Roles.DrawSpells}",
            $"    - Cantrips:  {r.Roles.Cantrips}",
            $"    - Group:     {r.Roles.DrawGroup}",
            $"  Removal:      {r.Roles.Removal}",
            $"  Wipes:        {r.Roles.Wipes}",
            $"  Protection:   {r.Roles.Protection}",
            $"  Tutors:       {r.Roles.Tutors}",
            $"  Payoffs:      {r.Roles.Payoffs}",
            "",
            "Opening Hand / Land Drops (simulation):",
            $"  Keepable 7:              {r.Simulation.Keepable7Pct:0.0}%",
            $"  Keepable 6:              {r.Simulation.Keepable6Pct:0.0}%",
            $"  2+ lands in opening 7:    {r.Simulation.AtLeast2LandsIn7Pct:0.0}%",
            $"  Hit 3rd land by T3 (OTP): {r.Simulation.Hit3rdLandDropByTurn3OnPlayPct:0.0}%",
            "",
            "Suggestions:"
        };

        if (eval.Suggestions.Count == 0)
        {
            lines.Add("  (none) Looks structurally sound. Now go lose to a turn-2 Dockside like the rest of us.");
        }
        else
        {
            foreach (var s in eval.Suggestions)
                lines.Add($"  - [{s.Category}] {s.Message}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Prints the exact cards that contributed to each bucket count, grouped by name.
    /// Buckets can be filtered via cfg.ShowCardsFor (CSV) and are keyed like: lands, draw_engines, wincons, etc.
    /// </summary>
    public static string WriteCategoryCardsSection(DeckEvaluationResult eval, DeckConfig cfg)
    {
        var cards = eval.Mainboard;

        // Keep ordering stable and human-friendly.
        var buckets = new (string Key, string Title, Func<Card, bool> Predicate)[]
        {
            ("lands",        "Lands",        c => c.IsLand),
            ("ramp",         "Ramp",         c => c.Roles.HasFlag(CardRole.Ramp)),
            ("draw_engines", "Draw Engines", CardRoleClassifier.IsDrawEngine),
            ("draw_spells",  "Draw Spells",  CardRoleClassifier.IsDrawSpell),
            ("cantrips",     "Cantrips",     c => c.Roles.HasFlag(CardRole.Cantrip)),
            ("group_draw",   "Group Draw",   c => c.Roles.HasFlag(CardRole.GroupDraw)),
            ("removal",      "Removal",      c => c.Roles.HasFlag(CardRole.Removal)),
            ("wipes",        "Board Wipes",  c => c.Roles.HasFlag(CardRole.Wipe)),
            ("protection",   "Protection",   c => c.Roles.HasFlag(CardRole.Protection)),
            ("tutors",       "Tutors",       c => c.Roles.HasFlag(CardRole.Tutor)),
            ("payoffs",      "Payoffs",      c => c.Roles.HasFlag(CardRole.Payoff)),
            ("wincons",      "Wincons",      c => c.Roles.HasFlag(CardRole.WinCon)),
        };

        var filter = (cfg.ShowCardsFor ?? Array.Empty<string>())
            .Select(NormalizeBucketKey)
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var known = buckets.Select(b => b.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownFilters = filter.Count == 0
            ? Array.Empty<string>()
            : filter.Where(f => !known.Contains(f)).OrderBy(x => x).ToArray();

        bool includeAll = filter.Count == 0;

        var lines = new List<string>
        {
            "Cards counted per category:" + (includeAll
                ? ""
                : $" (filtered: {string.Join(", ", filter.Where(known.Contains).OrderBy(x => x))})")
        };

        if (unknownFilters.Length > 0)
            lines.Add($"(ignored unknown buckets: {string.Join(", ", unknownFilters)})");

        foreach (var b in buckets)
        {
            if (!includeAll && !filter.Contains(b.Key))
                continue;

            var matches = cards.Where(b.Predicate).ToList();
            lines.Add("");
            lines.Add($"{b.Title} ({matches.Count}):");

            if (matches.Count == 0)
            {
                lines.Add("  (none)");
                continue;
            }

            foreach (var g in GroupByName(matches))
            {
                var suffix = g.Count > 1 ? $" x{g.Count}" : "";
                lines.Add($"  - {g.Name}{suffix}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Prints every card (grouped by name) and the roles it was tagged with.
    /// Also includes derived draw labels (DrawEngine/DrawSpell) and Land.
    /// </summary>
    public static string WritePerCardRolesSection(DeckEvaluationResult eval)
    {
        var lines = new List<string> { "Per-card roles (grouped):" };

        foreach (var g in GroupByName(eval.Mainboard))
        {
            var c = g.Sample;
            var parts = new List<string>();

            if (c.IsLand) parts.Add("Land");

            // Keep order stable.
            AddIf(parts, c.Roles, CardRole.Ramp, "Ramp");
            AddIf(parts, c.Roles, CardRole.Draw, "Draw");
            AddIf(parts, c.Roles, CardRole.Cantrip, "Cantrip");
            AddIf(parts, c.Roles, CardRole.GroupDraw, "GroupDraw");
            AddIf(parts, c.Roles, CardRole.Removal, "Removal");
            AddIf(parts, c.Roles, CardRole.Wipe, "Wipe");
            AddIf(parts, c.Roles, CardRole.Protection, "Protection");
            AddIf(parts, c.Roles, CardRole.Tutor, "Tutor");
            AddIf(parts, c.Roles, CardRole.Payoff, "Payoff");
            AddIf(parts, c.Roles, CardRole.WinCon, "WinCon");
            AddIf(parts, c.Roles, CardRole.NarrowHate, "NarrowHate");

            // Derived draw types (these are what your report buckets are based on).
            if (CardRoleClassifier.IsDrawEngine(c)) parts.Add("DrawEngine");
            else if (CardRoleClassifier.IsDrawSpell(c)) parts.Add("DrawSpell");

            var roleText = parts.Count == 0 ? "(no roles)" : string.Join(", ", parts);
            var suffix = g.Count > 1 ? $" x{g.Count}" : "";
            lines.Add($"- {g.Name}{suffix}: {roleText}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddIf(List<string> parts, CardRole roles, CardRole flag, string label)
    {
        if (roles.HasFlag(flag)) parts.Add(label);
    }

    private static string NormalizeBucketKey(string s)
        => (s ?? "").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

    private static IEnumerable<(string Name, int Count, Card Sample)> GroupByName(IEnumerable<Card> cards)
    {
        // Group to avoid printing 18 Islands as 18 lines.
        return cards
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.First().Name, Count: g.Count(), Sample: g.First()))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }
}
