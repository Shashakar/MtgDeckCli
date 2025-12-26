namespace MtgDeckCli.Models;

[Flags]
public enum CardRole
{
    None = 0,
    Ramp = 1 << 0,
    Draw = 1 << 1,
    GroupDraw = 1 << 2,
    Removal = 1 << 3,
    Wipe = 1 << 4,
    Protection = 1 << 5,
    Tutor = 1 << 6,
    Payoff = 1 << 7,
    Cantrip = 1 << 8,

    // NEW:
    WinCon = 1 << 9,
    NarrowHate = 1 << 10
}

public sealed record Card(
    string Id,
    string Name,
    string OracleText,
    string TypeLine,
    decimal Cmc,
    IReadOnlyList<string> ColorIdentity,
    bool IsEligibleAsACommander,
    decimal? UsdPrice,
    CardRole Roles,
    IReadOnlyList<string> ThemeTags
)
{
    public bool IsLand => TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);
}