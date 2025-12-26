namespace MtgDeckCli.Models;

public sealed record DeckReport(
    int Lands,
    int Ramp,

    int DrawTotal,
    int DrawPersonal,
    int DrawGroup,

    int Removal,
    int Wipes,
    int Protection,
    int Tutors,
    int Payoffs,

    decimal? EstimatedUsd,

    int Cantrips = 0,
    int DrawEngines = 0,
    int DrawSpells = 0
);

public sealed record DeckResult(
    Card Commander,
    IReadOnlyList<Card> Mainboard, // 99 cards
    DeckReport Report,
    IReadOnlyList<string> Warnings
);