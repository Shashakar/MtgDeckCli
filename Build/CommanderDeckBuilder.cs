using MtgDeckCli.Models;
using MtgDeckCli.Scryfall;
using MtgDeckCli.Tags;

namespace MtgDeckCli.Build;

public sealed class CommanderDeckBuilder
{
    private readonly ScryfallClient _scryfall;

    public CommanderDeckBuilder(ScryfallClient scryfall)
    {
        _scryfall = scryfall;
    }

    public async Task<DeckResult> BuildAsync(DeckConfig cfg)
    {
        var warnings = new List<string>();

        var commanderDto = await _scryfall.GetCardNamedExactAsync(cfg.Commander);
        var commander = ToCard(commanderDto, cfg.Themes);

        if (!commander.IsEligibleAsACommander)
        {
            warnings.Add($"Commander may not be a legal commander (heuristic). You chose: {commander.Name}");
        }

        var ci = string.Concat(commander.ColorIdentity).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ci))
            ci = "c"; // colorless commander

        var quotas = Presets.ForPower(cfg.Power);

        // Candidate pools (targeted searches so we don’t try to ingest the entire card pool)
        var pool = new List<Card>();

        async Task AddSearch(string label, string q, int max)
        {
            var dtos = await _scryfall.SearchAsync(q, max);
            foreach (var dto in dtos)
            {
                var card = ToCard(dto, cfg.Themes);
                if (IsCommanderCard(commander, card))
                    continue;
                if (PassesFilters(cfg, card, commander.ColorIdentity))
                    pool.Add(card);
            }
        }

        // NOTE: Scryfall search syntax is powerful; these are intentionally simple.
        // All searches are constrained by Commander legality + color identity + nonland.
        string baseFilter = $"legal:commander ci:{ci} -t:land -is:digital game:paper";

        await AddSearch("ramp", $"{baseFilter} (o:\"add {{\" OR o:\"search your library for a land\")", cfg.MaxCandidates / 6);
        await AddSearch("draw", $"{baseFilter} (o:\"draw a card\" OR (o:\"exile the top\" o:\"you may play\"))", cfg.MaxCandidates / 6);
        await AddSearch("removal", $"{baseFilter} (o:\"destroy target\" OR o:\"exile target\" OR o:\"counter target\")", cfg.MaxCandidates / 6);
        await AddSearch("wipes", $"{baseFilter} (o:\"destroy all\" OR o:\"exile all\")", cfg.MaxCandidates / 10);
        await AddSearch("protection", $"{baseFilter} (o:hexproof OR o:indestructible OR o:\"phase out\" OR (o:return o:\"from your graveyard\"))", cfg.MaxCandidates / 10);

        // Theme searches (one per requested theme)
        foreach (var theme in cfg.Themes)
        {
            var themeQuery = ThemeQuery(theme);
            if (themeQuery is null) continue;

            await AddSearch($"theme:{theme}", $"{baseFilter} ({themeQuery})", cfg.MaxCandidates / 6);
        }

        // Add staple artifacts by exact name (cheap value, helps consistency)
        var staples = StapleNamesFor(ci);
        foreach (var name in staples)
        {
            try
            {
                var dto = await _scryfall.GetCardNamedExactAsync(name);
                var card = ToCard(dto, cfg.Themes);
                if (!IsCommanderCard(commander, card) && PassesFilters(cfg, card, commander.ColorIdentity) && !card.IsLand)
                    pool.Add(card);
            }
            catch
            {
                // ignore if not found
            }
        }

        // De-dupe by name (singleton)
        pool = pool
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // Score and pick
        var rng = cfg.Seed == 0 ? new Random(12345) : new Random(cfg.Seed);
        var selected = AssembleNonlands(cfg, quotas, commander, pool, rng, warnings);
        
        var commanderInMain = selected.Where(c => IsCommanderCard(commander, c)).ToList();
        if (commanderInMain.Count > 0)
        {
            warnings.Add($"Commander was found in mainboard and removed: {commander.Name}");
            selected = selected.Where(c => !IsCommanderCard(commander, c)).ToList();
        }
        
        // Ensure we still have the intended nonland count after any removals
        int targetNonlands = 99 - quotas.Lands;
        if (selected.Count < targetNonlands)
        {
            var selectedNames = selected.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var refill = pool
                .Where(c => !c.IsLand)
                .Where(c => !IsCommanderCard(commander, c))
                .Where(c => !selectedNames.Contains(c.Name))
                .OrderByDescending(c => ScoreCard(cfg, commander, c))
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Take(targetNonlands - selected.Count);

            selected.AddRange(refill);
        }


        // Lands
        var lands = await BuildManaBaseAsync(cfg, commander, quotas.Lands, rng);

        // Final validation & fill
        var all99 = selected.Concat(lands).ToList();

        // Ensure exactly 99 (trim/fill basics)
        if (all99.Count > 99)
            all99 = all99.Take(99).ToList();

        while (all99.Count < 99)
            all99.Add(BasicLandFor(commander.ColorIdentity, rng));

        // Report
        var report = BuildReport(cfg, quotas, all99);

        return new DeckResult(commander, all99, report, warnings);
    }

    private static DeckReport BuildReport(DeckConfig cfg, Quotas quotas, IReadOnlyList<Card> cards)
    {
        int lands = cards.Count(c => c.IsLand);
        int ramp  = cards.Count(c => c.Roles.HasFlag(CardRole.Ramp));

        int cantrips = cards.Count(c => c.Roles.HasFlag(CardRole.Cantrip));

        int drawEngines = cards.Count(c =>
            c.Roles.HasFlag(CardRole.Draw) &&
            !c.Roles.HasFlag(CardRole.GroupDraw) &&
            !c.Roles.HasFlag(CardRole.Cantrip));

        int drawGroup = cards.Count(c => c.Roles.HasFlag(CardRole.GroupDraw));

        int drawTotal = cards.Count(c => c.Roles.HasFlag(CardRole.Draw));

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
            DrawPersonal: drawEngines, // keep as alias if you still want it
            DrawGroup: drawGroup,

            Removal: removal,
            Wipes: wipes,
            Protection: prot,
            Tutors: tutors,
            Payoffs: payoffs,

            EstimatedUsd: total,

            Cantrips: cantrips,
            DrawEngines: drawEngines
        );
    }

    private static List<Card> AssembleNonlands(
        DeckConfig cfg,
        Quotas quotas,
        Card commander,
        List<Card> pool,
        Random rng,
        List<string> warnings)
    {
        // deterministic-ish ordering: score then name
        var scored = pool
            .Select(c => new { Card = c, Score = ScoreCard(cfg, commander, c) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Card.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var chosen = new List<Card>(capacity: 99 - quotas.Lands);
        var chosenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Take(Func<Card, bool> predicate, int count, string label)
        {
            foreach (var x in scored)
            {
                if (chosen.Count >= (99 - quotas.Lands)) break;
                if (count <= 0) break;

                var c = x.Card;
                if (chosenNames.Contains(c.Name)) continue;
                if (!predicate(c)) continue;

                // budget check (rough)
                if (cfg.BudgetUsd is not null && (c.UsdPrice ?? 0m) > 25m && cfg.Power == "precon")
                    continue;

                chosen.Add(c);
                chosenNames.Add(c.Name);
                count--;
            }

            if (count > 0)
                warnings.Add($"Could not fully satisfy {label} quota; short by {count}.");
        }

        Take(c => c.Roles.HasFlag(CardRole.Ramp), quotas.Ramp, "Ramp");

        // Draw engines
        Take(
            c => c.Roles.HasFlag(CardRole.Draw) &&
                 !c.Roles.HasFlag(CardRole.GroupDraw) &&
                 !c.Roles.HasFlag(CardRole.Cantrip),
            quotas.DrawEngines,
            "Draw (engines)"
        );

        // Group draw
        Take(
            c => c.Roles.HasFlag(CardRole.GroupDraw),
            quotas.GroupDraw,
            "Draw (group)"
        );


        Take(c => c.Roles.HasFlag(CardRole.Removal), quotas.Removal, "Removal");
        Take(c => c.Roles.HasFlag(CardRole.Wipe), quotas.Wipes, "Wipes");
        Take(c => c.Roles.HasFlag(CardRole.Protection), quotas.Protection, "Protection");

        // Theme/payoff push: prefer cards that match requested theme tags
        Take(c => c.ThemeTags.Count > 0 || c.Roles.HasFlag(CardRole.Payoff), quotas.Payoffs, "Payoffs/Theme");

        // Soft cap tutors unless explicitly allowed
        if (!cfg.AllowTutors)
        {
            // remove excess tutors if we accidentally grabbed too many
            var tutors = chosen.Where(c => c.Roles.HasFlag(CardRole.Tutor)).ToList();
            if (tutors.Count > quotas.TutorsSoftCap)
            {
                int removeCount = tutors.Count - quotas.TutorsSoftCap;
                foreach (var t in tutors.OrderByDescending(t => t.Cmc).Take(removeCount))
                {
                    chosen.Remove(t);
                    chosenNames.Remove(t.Name);
                }
                warnings.Add($"Trimmed tutors to soft cap ({quotas.TutorsSoftCap}). Use --allow-tutors to permit more.");
            }
        }

        int drawEngineSoftCap = quotas.DrawEngines + 4;
        int cantripSoftCap = quotas.CantripsSoftCap;

        int drawEnginesChosen = chosen.Count(c =>
            c.Roles.HasFlag(CardRole.Draw) &&
            !c.Roles.HasFlag(CardRole.GroupDraw) &&
            !c.Roles.HasFlag(CardRole.Cantrip));

        int cantripsChosen = chosen.Count(c => c.Roles.HasFlag(CardRole.Cantrip));

        int rampSoftCap = quotas.RampSoftCap;
        int removalSoftCap = quotas.RemovalSoftCap;
        int wipesSoftCap = quotas.WipesSoftCap;
        int protSoftCap = quotas.ProtectionSoftCap;

        
        // Fill remaining slots with best-scoring nonlands
        foreach (var x in scored)
        {
            if (chosen.Count >= (99 - quotas.Lands)) break;

            var c = x.Card;
            if (chosenNames.Contains(c.Name)) continue;
            if (c.IsLand) continue;

            bool isCantrip = c.Roles.HasFlag(CardRole.Cantrip);
            bool isDrawEngine = c.Roles.HasFlag(CardRole.Draw)
                                && !c.Roles.HasFlag(CardRole.GroupDraw)
                                && !c.Roles.HasFlag(CardRole.Cantrip);
            bool isRamp = c.Roles.HasFlag(CardRole.Ramp);
            bool isRemoval = c.Roles.HasFlag(CardRole.Removal);
            bool isWipe = c.Roles.HasFlag(CardRole.Wipe);
            bool isProtection = c.Roles.HasFlag(CardRole.Protection);

            bool isPayoff = c.Roles.HasFlag(CardRole.Payoff);

            if (!isPayoff)
            {
                if (isCantrip && cantripsChosen >= cantripSoftCap) continue;
                if (isDrawEngine && drawEnginesChosen >= drawEngineSoftCap) continue;
                if (isRamp && rampSoftCap > 0 && chosen.Count(c => c.Roles.HasFlag(CardRole.Ramp)) >= rampSoftCap) continue;
                if (isRemoval && removalSoftCap > 0 && chosen.Count(c => c.Roles.HasFlag(CardRole.Removal)) >= removalSoftCap) continue;
                if (isWipe && wipesSoftCap > 0 && chosen.Count(c => c.Roles.HasFlag(CardRole.Wipe)) >= wipesSoftCap) continue;
                if (isProtection && protSoftCap > 0 && chosen.Count(c => c.Roles.HasFlag(CardRole.Protection)) >= protSoftCap) continue;
            }


            chosen.Add(c);
            chosenNames.Add(c.Name);

            if (isCantrip) cantripsChosen++;
            if (isDrawEngine) drawEnginesChosen++;
        }


        // If still short, warn (lands will fill later)
        if (chosen.Count < (99 - quotas.Lands))
        {
            warnings.Add($"Nonland pool was too small; only selected {chosen.Count} nonlands. Consider raising --max-candidates.");
        }

        return chosen.Take(99 - quotas.Lands).ToList();
    }

    private static double ScoreCard(DeckConfig cfg, Card commander, Card c)
    {
        double score = 0;

        score += Math.Max(0, 6 - (double)c.Cmc) * 0.35;

        if (c.Roles.HasFlag(CardRole.Ramp)) score += 2.2;

        if (c.Roles.HasFlag(CardRole.Draw))
            score += c.Roles.HasFlag(CardRole.Cantrip) ? 0.35 : 2.0;

        if (c.Roles.HasFlag(CardRole.Removal)) score += 1.8;
        if (c.Roles.HasFlag(CardRole.Wipe)) score += 1.2;
        if (c.Roles.HasFlag(CardRole.Protection)) score += 1.0;

        score += c.ThemeTags.Count * 1.4;

        if (c.Roles.HasFlag(CardRole.Tutor) && !cfg.AllowTutors) score -= 1.0;

        if (cfg.BudgetUsd is not null && c.UsdPrice is not null)
            score -= Math.Min(2.0, (double)c.UsdPrice.Value / 20.0);

        unchecked { score += (c.Name.GetHashCode() % 17) * 0.001; }

        return score;
    }


    private static bool PassesFilters(DeckConfig cfg, Card c, IReadOnlyList<string> commanderCi)
    {
        // color identity containment (Scryfall already filters by ci:, but keep it honest)
        foreach (var sym in c.ColorIdentity)
        {
            if (!commanderCi.Contains(sym, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        // commander legality
        // (we only need cards to be legal in Commander; commander-legal is a separate property)
        // We'll rely on Scryfall search "legal:commander", but keep a tiny sanity:
        // If we can’t determine, allow.
        // (done via query, so no extra check here)

        // no-stax heuristic
        if (cfg.NoStax)
        {
            var o = c.OracleText;
            if (ContainsAny(o,
                    "players can't",
                    "can't cast",
                    "can't search",
                    "skip your",
                    "skip their",
                    "doesn't untap",
                    "can't untap",
                    "each player can't"))
                return false;
        }

        // no-infinite denylist (tiny; expand later)
        if (cfg.NoInfinite)
        {
            var deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Isochron Scepter",
                "Dramatic Reversal",
                "Food Chain",
                "Thassa's Oracle",
                "Demonic Consultation",
                "Tainted Pact"
            };
            if (deny.Contains(c.Name)) return false;
        }

        return true;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (text.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task<List<Card>> BuildManaBaseAsync(DeckConfig cfg, Card commander, int landCount, Random rng)
    {
        // MVP mana base:
        // - a small curated list of good lands (Command Tower etc.)
        // - then basics split across colors
        var lands = new List<Card>();

        var curated = new List<string>
        {
            "Command Tower",
            "Path of Ancestry",
            "Exotic Orchard",
            "Terramorphic Expanse",
            "Evolving Wilds",
            "Myriad Landscape"
        };

        // Add a couple more for 3+ colors
        if (commander.ColorIdentity.Count >= 3)
        {
            //curated.Add("City of Brass");
            //curated.Add("Mana Confluence");
        }

        foreach (var name in curated.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var dto = await _scryfall.GetCardNamedExactAsync(name);
                var card = ToCard(dto, cfg.Themes);
                if (card.IsLand && PassesFilters(cfg, card, commander.ColorIdentity))
                    lands.Add(card);
            }
            catch
            {
                // ignore
            }
        }

        // Fill the rest with basics
        while (lands.Count < landCount)
            lands.Add(BasicLandFor(commander.ColorIdentity, rng));

        return lands.Take(landCount).ToList();
    }

    private static Card BasicLandFor(IReadOnlyList<string> ci, Random rng)
    {
        if (ci.Count == 0) return BasicStub("Wastes");

        var sym = ci[rng.Next(ci.Count)].ToUpperInvariant();
        return sym switch
        {
            "W" => BasicStub("Plains"),
            "U" => BasicStub("Island"),
            "B" => BasicStub("Swamp"),
            "R" => BasicStub("Mountain"),
            "G" => BasicStub("Forest"),
            _ => BasicStub("Wastes")
        };
    }

    private static Card BasicStub(string name) =>
        new(
            Id: $"basic:{name.ToLowerInvariant()}",
            Name: name,
            OracleText: "",
            TypeLine: "Basic Land",
            Cmc: 0,
            ColorIdentity: Array.Empty<string>(),
            IsEligibleAsACommander: false,
            UsdPrice: 0m,
            Roles: CardRole.None,
            ThemeTags: Array.Empty<string>()
        );

    private static Card ToCard(ScryfallCardDto dto, IReadOnlyList<string> themes)
    {
        var oracle = RoleTagger.Oracle(dto);
        var roles = RoleTagger.TagRoles(dto);
        var tags = ThemeTagger.TagThemes(dto, themes);

        bool canBeACommander = IsProbablyCommander(dto, oracle);

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
            IsEligibleAsACommander: canBeACommander,
            UsdPrice: usd,
            Roles: roles,
            ThemeTags: tags
        );

        return NormalizeDrawRoles(card);
    }


    private static bool IsProbablyCommander(ScryfallCardDto dto, string oracle)
    {
        // Heuristic: legendary creature OR oracle says it can be your commander
        var legal = dto.Legalities is not null
                    && dto.Legalities.TryGetValue("commander", out var v)
                    && v.Equals("legal", StringComparison.OrdinalIgnoreCase);

        bool typeOk = (dto.TypeLine ?? "").Contains("Legendary", StringComparison.OrdinalIgnoreCase) &&
                      (dto.TypeLine ?? "").Contains("Creature", StringComparison.OrdinalIgnoreCase);

        bool textOk = oracle.Contains("can be your commander", StringComparison.OrdinalIgnoreCase);

        return legal && (typeOk || textOk);
    }

    private static string? ThemeQuery(string theme) => theme switch
    {
        "lifegain" => "o:\"gain life\"",
        "punish_lifegain" => "(o:\"whenever an opponent gains life\" OR o:\"if an opponent would gain life\")",
        "group_hug" => "(o:\"each player draws\" OR o:\"each player may\" OR o:\"each opponent may\")",
        "tokens" => "(o:create o:token)",
        "graveyard" => "(o:\"from your graveyard\" OR o:mill)",
        "spellslinger" => "(o:\"instant or sorcery\" OR o:\"whenever you cast\")",
        _ => null
    };

    private static IReadOnlyList<string> StapleNamesFor(string ci)
    {
        // Minimal staples; safe and common.
        var list = new List<string>
        {
            "Sol Ring",
            "Arcane Signet",
            "Fellwar Stone",
            "Mind Stone"
        };

        // Add Signets based on colors (very rough; you can expand)
        if (ci.Contains("w") && ci.Contains("u")) list.Add("Azorius Signet");
        if (ci.Contains("u") && ci.Contains("b")) list.Add("Dimir Signet");
        if (ci.Contains("b") && ci.Contains("r")) list.Add("Rakdos Signet");
        if (ci.Contains("r") && ci.Contains("g")) list.Add("Gruul Signet");
        if (ci.Contains("g") && ci.Contains("w")) list.Add("Selesnya Signet");
        if (ci.Contains("w") && ci.Contains("b")) list.Add("Orzhov Signet");
        if (ci.Contains("u") && ci.Contains("r")) list.Add("Izzet Signet");
        if (ci.Contains("b") && ci.Contains("g")) list.Add("Golgari Signet");
        if (ci.Contains("r") && ci.Contains("w")) list.Add("Boros Signet");
        if (ci.Contains("g") && ci.Contains("u")) list.Add("Simic Signet");

        return list;
    }
    
    private static bool IsCommanderCard(Card commander, Card candidate) =>
        candidate.Name.Equals(commander.Name, StringComparison.OrdinalIgnoreCase);
    
    private static Card NormalizeDrawRoles(Card c)
    {
        // If it's already marked, don't fight it
        if (c.Roles.HasFlag(CardRole.Cantrip)) return c;
        if (!c.Roles.HasFlag(CardRole.Draw)) return c;
        if (c.Roles.HasFlag(CardRole.GroupDraw)) return c; // group draw is its own bucket

        // Heuristic: "Draw a card." (single) on an instant/sorcery is usually a cantrip,
        // especially when stapled onto interaction (e.g., Bind, Aura Blast, Azorius Charm, etc.)
        var o = c.OracleText ?? "";
        var isInstantOrSorcery =
            (c.TypeLine?.Contains("Instant", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (c.TypeLine?.Contains("Sorcery", StringComparison.OrdinalIgnoreCase) ?? false);

        bool looksLikeSingleCardDraw =
            o.Contains("Draw a card", StringComparison.OrdinalIgnoreCase) &&
            !o.Contains("draw two", StringComparison.OrdinalIgnoreCase) &&
            !o.Contains("draw three", StringComparison.OrdinalIgnoreCase) &&
            !o.Contains("draw X", StringComparison.OrdinalIgnoreCase) &&
            !o.Contains("each player draws", StringComparison.OrdinalIgnoreCase);

        if (isInstantOrSorcery && looksLikeSingleCardDraw)
            return c with { Roles = c.Roles | CardRole.Cantrip };

        return c;
    }

}
