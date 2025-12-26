namespace MtgDeckCli.Models;

public sealed class DeckConfig
{
    public string Command { get; set; } = "";
    public bool ShowHelp { get; set; }
    public string DeckPath { get; set; } = "";

    // ===== Eval reporting / debugging =====
    // Prints the card names that contributed to each category count.
    public bool ShowCards { get; set; }

    // Optional CSV filter for ShowCards (ex: "lands,draw_engines,wincons").
    // Empty => print all buckets.
    public string ShowCardsForCsv { get; set; } = "";

    // Prints a per-card roles line item (useful to see mis-tags quickly).
    public bool ShowCardRoles { get; set; }

    public IReadOnlyList<string> ShowCardsFor { get; private set; } = Array.Empty<string>();

    public string Commander { get; set; } = "";
    public string ThemeCsv { get; set; } = "";
    public string Power { get; set; } = "upgraded";
    public decimal? BudgetUsd { get; set; }

    public bool NoStax { get; set; }
    public bool NoInfinite { get; set; }
    public bool AllowTutors { get; set; } = false;

    public int Seed { get; set; } = 0;
    public int MaxCandidates { get; set; } = 800;

    public string OutputPath { get; set; } = "";
    public string OutputJsonPath { get; set; } = "";
    public bool DryRun { get; set; }

    public IReadOnlyList<string> Themes { get; private set; } = Array.Empty<string>();

    public void Normalize()
    {
        Themes = (ThemeCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToArray();

        Power = string.IsNullOrWhiteSpace(Power) ? "upgraded" : Power.Trim().ToLowerInvariant();
        if (Power is not ("precon" or "upgraded" or "optimized" or "cedh_adjacent"))
            Power = "upgraded";

        if (MaxCandidates < 200) MaxCandidates = 200;
        if (MaxCandidates > 2500) MaxCandidates = 2500;
        
        ShowCardsFor = (ShowCardsForCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToArray();

    }
}