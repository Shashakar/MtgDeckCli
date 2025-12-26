using MtgDeckCli.Models;

namespace MtgDeckCli.Eval;

public static class DeckSimulator
{
    public static DeckSimulationStats Run(IReadOnlyList<Card> deck, int runs)
    {
        var rng = new Random(12345);
        int keep7=0, keep6=0, atLeast2In7=0, hit3rdByT3=0;

        for (int i = 0; i < runs; i++)
        {
            // shuffle indices
            var idx = Enumerable.Range(0, deck.Count).OrderBy(_ => rng.Next()).ToArray();

            // opening 7
            var hand7 = idx.Take(7).Select(j => deck[j]).ToList();
            int lands7 = hand7.Count(c => c.IsLand);

            bool keepable7 = lands7 is >= 2 and <= 4;
            if (keepable7) keep7++;
            if (lands7 >= 2) atLeast2In7++;

            // “mulligan to 6” naive model: draw 6
            var hand6 = idx.Take(6).Select(j => deck[j]).ToList();
            int lands6 = hand6.Count(c => c.IsLand);
            bool keepable6 = lands6 is >= 2 and <= 4;
            if (keepable6) keep6++;

            // hit 3rd land drop by turn 3 on play:
            // look at first 10 cards seen (7 + draws on turns 1-3)
            var first10 = idx.Take(10).Select(j => deck[j]).ToList();
            int lands10 = first10.Count(c => c.IsLand);
            if (lands10 >= 3) hit3rdByT3++;
        }

        return new DeckSimulationStats(
            Keepable7Pct: keep7 * 100.0 / runs,
            Keepable6Pct: keep6 * 100.0 / runs,
            AtLeast2LandsIn7Pct: atLeast2In7 * 100.0 / runs,
            Hit3rdLandDropByTurn3OnPlayPct: hit3rdByT3 * 100.0 / runs
        );
    }
}