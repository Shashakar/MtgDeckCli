using System.Text.Json;
using MtgDeckCli.Models;

namespace MtgDeckCli.Output;

public static class JsonWriter
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string WriteDeckJson(DeckResult deck)
    {
        var obj = new
        {
            commander = deck.Commander,
            mainboard = deck.Mainboard,
            report = deck.Report,
            warnings = deck.Warnings
        };

        return JsonSerializer.Serialize(obj, Opts);
    }
    
    public static string WriteEvaluationJson(DeckEvaluationResult eval)
    {
        var obj = new
        {
            commander = eval.Commander,
            mainboard = eval.Mainboard,
            report = eval.Report,
            suggestions = eval.Suggestions,
            score0To100 = eval.Score0To100
        };

        return JsonSerializer.Serialize(obj, Opts);
    }

}