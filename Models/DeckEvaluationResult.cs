namespace MtgDeckCli.Models;

public sealed record DeckSuggestion(string Category, string Message);

public sealed record DeckSimulationStats(
    double Keepable7Pct,
    double Keepable6Pct,
    double AtLeast2LandsIn7Pct,
    double Hit3rdLandDropByTurn3OnPlayPct
);

public sealed record ManaColorStats(
    int WhiteSources, int BlueSources, int BlackSources, int RedSources, int GreenSources,
    int WhiteDemand,  int BlueDemand,  int BlackDemand,  int RedDemand,  int GreenDemand
);

public sealed record DeckEvaluationReport(
    DeckReport Roles,
    ManaColorStats Mana,
    DeckSimulationStats Simulation,
    double AvgCmc
);

public sealed record DeckEvaluationResult(
    Card Commander,
    IReadOnlyList<Card> Mainboard,
    DeckEvaluationReport Report,
    IReadOnlyList<DeckSuggestion> Suggestions,
    int Score0To100
);