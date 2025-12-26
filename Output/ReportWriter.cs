using MtgDeckCli.Models;

namespace MtgDeckCli.Output;

public static class ReportWriter
{
    public static string WriteHumanReport(DeckResult deck)
    {
        var r = deck.Report;

        var lines = new List<string>
        {
            $"Commander: {deck.Commander.Name}",
            $"Cards: 100 (Commander + 99)",
            "",
            "Breakdown:",
            $"  Lands:        {r.Lands}",
            $"  Ramp:         {r.Ramp}",
            $"  Draw (total):  {r.DrawTotal}",
            $"    - Engines:   {r.DrawEngines}",
            $"    - Cantrips:  {r.Cantrips}",
            $"    - Group:     {r.DrawGroup}",
            $"  Removal:      {r.Removal}",
            $"  Wipes:        {r.Wipes}",
            $"  Protection:   {r.Protection}",
            $"  Tutors:       {r.Tutors}",
            $"  Payoffs:      {r.Payoffs}",
        };

        if (r.EstimatedUsd is not null)
            lines.Add($"  Est. USD:    {r.EstimatedUsd:0.00}");

        if (deck.Warnings.Count > 0)
        {
            lines.Add("");
            lines.Add("Warnings:");
            foreach (var w in deck.Warnings)
                lines.Add($"  - {w}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}