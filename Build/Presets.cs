namespace MtgDeckCli.Build;

public sealed record Quotas(
    int Lands,

    int Ramp,
    int DrawEngines,
    int GroupDraw,

    int Removal,
    int Wipes,
    int Protection,

    int Payoffs,

    int TutorsSoftCap,

    // Soft caps (avoid the “oops all ramp” problem)
    int RampSoftCap,
    int DrawEnginesSoftCap,
    int GroupDrawSoftCap,
    int CantripsSoftCap,
    int RemovalSoftCap,
    int WipesSoftCap,
    int ProtectionSoftCap,

    // Minimum “this deck should have at least X ways to actually win”
    int WinConsMin
);

public static class Presets
{
    public static Quotas ForPower(string power) => power switch
    {
        "precon" => new Quotas(
            Lands: 38,
            Ramp: 10, DrawEngines: 6, GroupDraw: 6,
            Removal: 8, Wipes: 3, Protection: 3,
            Payoffs: 18, TutorsSoftCap: 2,

            RampSoftCap: 14,
            DrawEnginesSoftCap: 10,
            GroupDrawSoftCap: 10,
            CantripsSoftCap: 10,
            RemovalSoftCap: 11,
            WipesSoftCap: 4,
            ProtectionSoftCap: 5,
            WinConsMin: 2
        ),

        "optimized" => new Quotas(
            Lands: 36,
            Ramp: 12, DrawEngines: 8, GroupDraw: 6,
            Removal: 12, Wipes: 3, Protection: 5,
            Payoffs: 22, TutorsSoftCap: 6,

            RampSoftCap: 16,
            DrawEnginesSoftCap: 12,
            GroupDrawSoftCap: 10,
            CantripsSoftCap: 12,
            RemovalSoftCap: 15,
            WipesSoftCap: 4,
            ProtectionSoftCap: 7,
            WinConsMin: 2
        ),

        _ => new Quotas(
            Lands: 37,
            Ramp: 11, DrawEngines: 7, GroupDraw: 6,
            Removal: 10, Wipes: 3, Protection: 4,
            Payoffs: 20, TutorsSoftCap: 4,

            RampSoftCap: 15,
            DrawEnginesSoftCap: 11,
            GroupDrawSoftCap: 10,
            CantripsSoftCap: 12,
            RemovalSoftCap: 13,
            WipesSoftCap: 4,
            ProtectionSoftCap: 6,
            WinConsMin: 2
        ),
    };
}
