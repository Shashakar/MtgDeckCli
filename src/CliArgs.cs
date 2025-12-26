using MtgDeckCli.Models;

namespace MtgDeckCli;

public sealed class CliException : Exception
{
    public CliException(string message) : base(message) { }
}

public static class CliArgs
{
    public static DeckConfig Parse(string[] args)
    {
        // Minimal parsing: command + --key value + flags
        // Example:
        // mtg-deck build --commander "X" --theme "a,b" --power upgraded --budget 200 --no-stax --output deck.txt --output-json deck.json

        var cfg = new DeckConfig();

        if (args.Length == 0)
        {
            cfg.ShowHelp = true;
            return cfg;
        }

        cfg.Command = args[0].Trim().ToLowerInvariant();

        for (int i = 1; i < args.Length; i++)
        {
            var token = args[i];

            if (token is "-h" or "--help")
            {
                cfg.ShowHelp = true;
                return cfg;
            }

            if (!token.StartsWith("--"))
                throw new CliException($"Unexpected token: {token}");

            string key = token[2..].Trim().ToLowerInvariant();

            bool isFlag =
                key is "no-stax" or "no-infinite" or "allow-tutors" or "dry-run" or
                    "show-cards" or "show-card-roles";

            if (isFlag)
            {
                ApplyFlag(cfg, key);
                continue;
            }

            if (i + 1 >= args.Length)
                throw new CliException($"Missing value for --{key}");

            string value = args[++i];

            ApplyKeyValue(cfg, key, value);
        }

        cfg.Normalize();
        return cfg;
    }

    private static void ApplyFlag(DeckConfig cfg, string key)
    {
        switch (key)
        {
            case "no-stax":
                cfg.NoStax = true;
                break;
            case "no-infinite":
                cfg.NoInfinite = true;
                break;
            case "allow-tutors":
                cfg.AllowTutors = true;
                break;
            case "dry-run":
                cfg.DryRun = true;
                break;
            case "show-cards":
                cfg.ShowCards = true;
                break;
            case "show-card-roles":
                cfg.ShowCardRoles = true;
                break;
            default: throw new CliException($"Unknown flag: --{key}");
        }
    }

    private static void ApplyKeyValue(DeckConfig cfg, string key, string value)
    {
        switch (key)
        {
            case "commander":
                cfg.Commander = value;
                break;
            case "theme":
                cfg.ThemeCsv = value;
                break;
            case "power":
                cfg.Power = value;
                break;
            case "budget":
                if (!decimal.TryParse(value, out var b)) throw new CliException("Invalid --budget value.");
                cfg.BudgetUsd = b;
                break;
            case "seed":
                if (!int.TryParse(value, out var s)) throw new CliException("Invalid --seed value.");
                cfg.Seed = s;
                break;
            case "output":
                cfg.OutputPath = value;
                break;
            case "output-json":
                cfg.OutputJsonPath = value;
                break;
            case "max-candidates":
                if (!int.TryParse(value, out var mc)) throw new CliException("Invalid --max-candidates value.");
                cfg.MaxCandidates = mc;
                break;
            case "deck": 
            case "input": 
                cfg.DeckPath = value; break;
            case "show-cards-for":
                cfg.ShowCardsForCsv = value;
                break;
            default:
                throw new CliException($"Unknown option: --{key}");
        }
    }

    public static string HelpText() =>
        @"mtg-deck (v0.2) - Commander deck tools (Scryfall-backed)

        Usage:
          mtg-deck build --commander ""Name"" [options]
          mtg-deck eval  --deck deck.txt [--commander ""Name""] [options]

        Commands:
          build   Generate a 99-card mainboard + report
          eval    Evaluate an existing decklist (99 mainboard + commander)

        Build options:
          --commander ""Name""            Required.
          --theme ""tag1,tag2,...""       Example: ""group_hug,lifegain,punish_lifegain""
          --power precon|upgraded|optimized|cedh_adjacent   (default: upgraded)
          --budget 200                   Budget cap in USD (optional)
          --no-stax                      Exclude common stax-ish patterns (heuristic)
          --no-infinite                  Exclude common infinite pieces (small denylist)
          --allow-tutors                 Allow tutors (default is limited by heuristic)
          --seed 123                     Deterministic selection variation (optional)
          --max-candidates 900           Cap fetched candidates (default: 800)
          --output deck.txt              Write deck list
          --output-json deck.json        Write JSON artifact

        Eval options:
          --deck deck.txt                Deck list file (your deck.txt format works)
          --commander ""Name""            Optional if the deck file includes commander as the first line
          --theme ""tag1,tag2,...""       Used for theme tagging/suggestions
          --power precon|upgraded|optimized|cedh_adjacent   (default: upgraded)
          --output report.txt            Write evaluation report
          --output-json eval.json        Write evaluation JSON

        Other:
          -h|--help                      Show help

        Examples:
          mtg-deck eval --deck deck.txt
          mtg-deck eval --commander ""Kynaios and Tiro of Meletis"" --deck deck.txt --power upgraded
        ";
}
