﻿using MtgDeckCli.Build;
using MtgDeckCli.Eval;
using MtgDeckCli.Models;
using MtgDeckCli.Output;
using MtgDeckCli.Scryfall;

namespace MtgDeckCli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var config = CliArgs.Parse(args);

            if (config.ShowHelp)
            {
                Console.WriteLine(CliArgs.HelpText());
                return 0;
            }

            if (string.IsNullOrWhiteSpace(config.Command))
            {
                Console.WriteLine("Missing command. Use: mtg-deck build ... or mtg-deck eval ...");
                Console.WriteLine(CliArgs.HelpText());
                return 2;
            }

            var cmd = config.Command.Trim().ToLowerInvariant();
            if (cmd is "evaluate") cmd = "eval";

            var http = new HttpClient();
            var cache = new DiskCache(Path.Combine(AppContext.BaseDirectory, ".cache", "scryfall"));
            var scryfall = new ScryfallClient(http, cache);

            return cmd switch
            {
                "build" => await RunBuildAsync(config, scryfall),
                "eval"  => await RunEvalAsync(config, scryfall),
                _ => UnknownCommand(cmd)
            };
        }
        catch (CliException ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine();
            Console.WriteLine(CliArgs.HelpText());
            return 2;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unexpected error:");
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static int UnknownCommand(string cmd)
    {
        Console.WriteLine($"Unknown command: {cmd}");
        Console.WriteLine("Use: mtg-deck build ... or mtg-deck eval ...");
        Console.WriteLine(CliArgs.HelpText());
        return 2;
    }

    private static async Task<int> RunBuildAsync(DeckConfig config, ScryfallClient scryfall)
    {
        if (string.IsNullOrWhiteSpace(config.Commander))
        {
            Console.WriteLine("Missing --commander \"Card Name\"");
            return 2;
        }

        var builder = new CommanderDeckBuilder(scryfall);
        var result = await builder.BuildAsync(config);

        var reportText = ReportWriter.WriteHumanReport(result);
        Console.WriteLine(reportText);

        if (!string.IsNullOrWhiteSpace(config.OutputPath))
        {
            var deckTxt = DeckListWriter.WriteDeckTxt(result);
            File.WriteAllText(config.OutputPath, deckTxt);
            Console.WriteLine($"Wrote deck list: {config.OutputPath}");
        }

        if (!string.IsNullOrWhiteSpace(config.OutputJsonPath))
        {
            var json = JsonWriter.WriteDeckJson(result);
            File.WriteAllText(config.OutputJsonPath, json);
            Console.WriteLine($"Wrote JSON: {config.OutputJsonPath}");
        }

        return 0;
    }

    private static async Task<int> RunEvalAsync(DeckConfig config, ScryfallClient scryfall)
    {
        // Read deck text from file (preferred) or stdin (fallback)
        string deckText;
        if (!string.IsNullOrWhiteSpace(config.DeckPath))
        {
            if (!File.Exists(config.DeckPath))
            {
                Console.WriteLine($"Deck file not found: {config.DeckPath}");
                return 2;
            }

            deckText = await File.ReadAllTextAsync(config.DeckPath);
        }
        else
        {
            deckText = await Console.In.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(deckText))
            {
                Console.WriteLine("Missing --deck deck.txt (or provide deck text via stdin).");
                return 2;
            }
        }

        // Split commander/mainboard from deck list
        var (commanderName, mainboardNames) = DeckListParser.ParseCommanderAndMainboard(deckText, config.Commander);

        if (string.IsNullOrWhiteSpace(commanderName))
        {
            Console.WriteLine("Could not determine commander. Provide --commander \"Name\" or include commander as first line in the deck file.");
            return 2;
        }

        var evaluator = new DeckEvaluator(scryfall);
        var eval = await evaluator.EvaluateAsync(commanderName, mainboardNames, config);

        var reportText = EvalReportWriter.WriteHumanEvalReport(eval);
        if (config.ShowCards)
            reportText += Environment.NewLine + Environment.NewLine
                                              + EvalReportWriter.WriteCategoryCardsSection(eval, config);

        if (config.ShowCardRoles)
            reportText += Environment.NewLine + Environment.NewLine
                                              + EvalReportWriter.WritePerCardRolesSection(eval);

        Console.WriteLine(reportText);

        if (!string.IsNullOrWhiteSpace(config.OutputPath))
        {
            File.WriteAllText(config.OutputPath, reportText);
            Console.WriteLine($"Wrote evaluation report: {config.OutputPath}");
        }

        if (!string.IsNullOrWhiteSpace(config.OutputJsonPath))
        {
            var json = JsonWriter.WriteEvaluationJson(eval);
            File.WriteAllText(config.OutputJsonPath, json);
            Console.WriteLine($"Wrote JSON: {config.OutputJsonPath}");
        }

        return 0;
    }
}
