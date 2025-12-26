using System.Text.Json.Serialization;

namespace MtgDeckCli.Scryfall;

public sealed class ScryfallCardDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("oracle_text")] public string? OracleText { get; set; }
    [JsonPropertyName("type_line")] public string TypeLine { get; set; } = "";
    [JsonPropertyName("cmc")] public decimal Cmc { get; set; }
    [JsonPropertyName("color_identity")] public string[] ColorIdentity { get; set; } = Array.Empty<string>();
    [JsonPropertyName("legalities")] public Dictionary<string, string>? Legalities { get; set; }
    [JsonPropertyName("prices")] public ScryfallPricesDto? Prices { get; set; }
    [JsonPropertyName("card_faces")] public ScryfallFaceDto[]? Faces { get; set; }
}

public sealed class ScryfallFaceDto
{
    [JsonPropertyName("oracle_text")] public string? OracleText { get; set; }
    [JsonPropertyName("type_line")] public string? TypeLine { get; set; }
}

public sealed class ScryfallPricesDto
{
    [JsonPropertyName("usd")] public string? Usd { get; set; }
}

public sealed class ScryfallSearchDto
{
    [JsonPropertyName("data")] public ScryfallCardDto[] Data { get; set; } = Array.Empty<ScryfallCardDto>();
    [JsonPropertyName("has_more")] public bool HasMore { get; set; }
    [JsonPropertyName("next_page")] public string? NextPage { get; set; }
}