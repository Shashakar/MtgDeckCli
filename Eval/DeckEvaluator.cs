using MtgDeckCli.Build;
using MtgDeckCli.Models;
using MtgDeckCli.Scryfall;
using MtgDeckCli.Tags;

namespace MtgDeckCli.Eval;

public sealed class DeckEvaluator
{
    private readonly ScryfallClient _scryfall;

    public DeckEvaluator(ScryfallClient scryfall) => _scryfall = scryfall;

    public async Task<DeckEvaluationResult> EvaluateAsync(string commanderName, IReadOnlyList<string> cardNames, DeckConfig cfg)
    {
        var suggestions = new List<DeckSuggestion>();

        // Fetch commander
        var commanderDto = await _scryfall.GetCardNamedExactAsync(commanderName);
        var commander = ToCard(commanderDto, cfg.Themes);

        // Fetch mainboard
        var cards = new List<Card>(cardNames.Count);
        foreach (var name in cardNames)
        {
            var dto = await _scryfall.GetCardNamedExactAsync(name);
            cards.Add(ToCard(dto, cfg.Themes));
        }

        // Basic validation
        if (cards.Count != 99)
            suggestions.Add(new("DeckSize", $"Mainboard is {cards.Count} cards; expected 99 for Commander."));

        // Build role counts
        var quotas = Presets.ForPower(cfg.Power);
        var rolesReport = BuildRoleReport(cfg, cards);

        // Mana analysis
        var mana = ManaAnalyzer.Analyze(cards);

        // Simulation (keep it simple and useful)
        var sim = DeckSimulator.Run(cards, runs: 20000);

        // Suggestions
        AddQuotaSuggestions(quotas, rolesReport, suggestions);
        AddWinconSuggestions(quotas, cards, suggestions);
        AddManaSuggestions(mana, suggestions);
        AddLandCountSuggestions(sim, suggestions);

        // Score (0–100)
        int score = Score(quotas, rolesReport, mana, sim);

        double avgCmc = cards.Where(c => !c.IsLand).Select(c => (double)c.Cmc).DefaultIfEmpty(0).Average();

        var evalReport = new DeckEvaluationReport(
            Roles: rolesReport,
            Mana: mana,
            Simulation: sim,
            AvgCmc: avgCmc
        );

        return new DeckEvaluationResult(commander, cards, evalReport, suggestions, score);
    }

    private static void AddQuotaSuggestions(Quotas q, DeckReport r, List<DeckSuggestion> s)
    {
        if (r.Ramp > q.RampSoftCap)
            s.Add(new("Ramp", $"Ramp is high ({r.Ramp}). Consider trimming toward ~{q.Ramp}-{q.RampSoftCap} unless this is intentionally turbo."));

        if (r.DrawEngines < q.DrawEngines)
            s.Add(new("Draw", $"Draw engines are low ({r.DrawEngines}). Target ~{q.DrawEngines}+ engines for your power band."));

        if (r.DrawGroup < q.GroupDraw)
            s.Add(new("GroupHug", $"Group draw is low ({r.DrawGroup}). For group hug, aim ~{q.GroupDraw}+ symmetrical draw effects."));

        if (r.Removal < q.Removal)
            s.Add(new("Interaction", $"Removal is low ({r.Removal}). Target ~{q.Removal}+."));

        if (r.Wipes < q.Wipes)
            s.Add(new("Interaction", $"Board wipes are low ({r.Wipes}). Target ~{q.Wipes}."));
    }

    private static void AddWinconSuggestions(Quotas q, IReadOnlyList<Card> cards, List<DeckSuggestion> s)
    {
        int wincons = cards.Count(c => c.Roles.HasFlag(CardRole.WinCon));
        if (wincons < q.WinConsMin)
            s.Add(new("WinCons", $"Only {wincons} wincon-tagged cards detected. Consider adding {q.WinConsMin - wincons}+ clear finishers."));
    }

    private static void AddManaSuggestions(ManaColorStats m, List<DeckSuggestion> s)
    {
        // crude but useful: if demand is high but sources are low, warn
        // (You’ll refine this as you add produced_mana support)
        if (m.WhiteDemand > 0 && m.WhiteSources < 10) s.Add(new("Mana", "White demand exists but white sources look low (<10)."));
        if (m.BlueDemand  > 0 && m.BlueSources  < 10) s.Add(new("Mana", "Blue demand exists but blue sources look low (<10)."));
        if (m.BlackDemand > 0 && m.BlackSources < 10) s.Add(new("Mana", "Black demand exists but black sources look low (<10)."));
        if (m.RedDemand   > 0 && m.RedSources   < 10) s.Add(new("Mana", "Red demand exists but red sources look low (<10)."));
        if (m.GreenDemand > 0 && m.GreenSources < 10) s.Add(new("Mana", "Green demand exists but green sources look low (<10)."));
    }

    private static void AddLandCountSuggestions(DeckSimulationStats sim, List<DeckSuggestion> s)
    {
        if (sim.AtLeast2LandsIn7Pct < 80)
            s.Add(new("Lands", $"Opening 7 has <80% chance of 2+ lands ({sim.AtLeast2LandsIn7Pct:0.0}%). Consider +1–2 lands."));
    }

    private static int Score(Quotas q, DeckReport r, ManaColorStats m, DeckSimulationStats sim)
    {
        int score = 100;

        // penalize major structural issues
        if (sim.AtLeast2LandsIn7Pct < 80) score -= 15;
        if (r.DrawEngines < q.DrawEngines) score -= Math.Min(10, (q.DrawEngines - r.DrawEngines) * 2);
        if (r.Removal < q.Removal) score -= Math.Min(10, (q.Removal - r.Removal) * 2);

        // punish excess ramp/draw (bloat)
        if (r.Ramp > q.RampSoftCap) score -= Math.Min(12, (r.Ramp - q.RampSoftCap) * 2);

        return Math.Clamp(score, 0, 100);
    }

    private static DeckReport BuildRoleReport(DeckConfig cfg, IReadOnlyList<Card> cards)
    {
        int lands = cards.Count(c => c.IsLand);

        int ramp  = cards.Count(c => c.Roles.HasFlag(CardRole.Ramp));
        int drawEngines = cards.Count(CardRoleClassifier.IsDrawEngine);
        int drawSpells  = cards.Count(CardRoleClassifier.IsDrawSpell);
        int drawGroup   = cards.Count(c => c.Roles.HasFlag(CardRole.GroupDraw));
        int cantrips    = cards.Count(c => c.Roles.HasFlag(CardRole.Cantrip));
        int drawTotal   = cards.Count(c => c.Roles.HasFlag(CardRole.Draw));


        int removal = cards.Count(c => c.Roles.HasFlag(CardRole.Removal));
        int wipes   = cards.Count(c => c.Roles.HasFlag(CardRole.Wipe));
        int prot    = cards.Count(c => c.Roles.HasFlag(CardRole.Protection));
        int tutors  = cards.Count(c => c.Roles.HasFlag(CardRole.Tutor));
        int payoffs = cards.Count(c => c.Roles.HasFlag(CardRole.Payoff));

        decimal? total = null;
        if (cfg.BudgetUsd is not null)
            total = cards.Select(c => c.UsdPrice ?? 0m).Sum();

        return new DeckReport(
            Lands: lands,
            Ramp: ramp,

            DrawTotal: drawTotal,
            DrawPersonal: drawEngines + drawSpells, // legacy field
            DrawGroup: drawGroup,

            Removal: removal,
            Wipes: wipes,
            Protection: prot,
            Tutors: tutors,
            Payoffs: payoffs,
            EstimatedUsd: total,

            Cantrips: cantrips,
            DrawEngines: drawEngines,
            DrawSpells: drawSpells
        );

    }

    private static Card ToCard(ScryfallCardDto dto, IReadOnlyList<string> themes)
    {
        var oracle = RoleTagger.Oracle(dto);
        var roles  = RoleTagger.TagRoles(dto);
        var tags   = ThemeTagger.TagThemes(dto, themes);

        decimal? usd = null;
        if (dto.Prices?.Usd is { Length: > 0 } s && decimal.TryParse(s, out var p))
            usd = p;

        var card = new Card(
            Id: dto.Id,
            Name: dto.Name,
            OracleText: oracle ?? "",
            TypeLine: dto.TypeLine ?? "",
            Cmc: dto.Cmc,
            ColorIdentity: dto.ColorIdentity ?? Array.Empty<string>(),
            IsEligibleAsACommander: false,
            UsdPrice: usd,
            Roles: roles,
            ThemeTags: tags
        );

        return NormalizeDrawRoles(card);
    }

    private static Card NormalizeDrawRoles(Card c)
    {
        if (c.Roles.HasFlag(CardRole.Cantrip)) return c;
        if (!c.Roles.HasFlag(CardRole.Draw)) return c;
        if (c.Roles.HasFlag(CardRole.GroupDraw)) return c;

        var o = c.OracleText ?? "";
        var isInstantOrSorcery =
            (c.TypeLine?.Contains("Instant", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (c.TypeLine?.Contains("Sorcery", StringComparison.OrdinalIgnoreCase) ?? false);

        bool looksLikeSingleCardDraw =
            o.Contains("Draw a card", StringComparison.OrdinalIgnoreCase) &&
            !o.Contains("draw two", StringComparison.OrdinalIgnoreCase) &&
            !o.Contains("draw three", StringComparison.OrdinalIgnoreCase) &&
            !o.Contains("draw X", StringComparison.OrdinalIgnoreCase);

        if (isInstantOrSorcery && looksLikeSingleCardDraw)
            return c with { Roles = c.Roles | CardRole.Cantrip };

        return c;
    }
}
