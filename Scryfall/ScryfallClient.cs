using System.Text.Json;

namespace MtgDeckCli.Scryfall;

public sealed class ScryfallClient
{
    private readonly HttpClient _http;
    private readonly DiskCache _cache;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CacheAge = TimeSpan.FromDays(14);

    // Be nice to Scryfall.
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;

    public ScryfallClient(HttpClient http, DiskCache cache)
    {
        _http = http;
        _cache = cache;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MtgDeckCli/0.1 (+https://example.local)");
    }

    public async Task<ScryfallCardDto> GetCardNamedExactAsync(string name)
    {
        var url = $"https://api.scryfall.com/cards/named?exact={Uri.EscapeDataString(name)}";
        var json = await GetStringAsync(url);
        return JsonSerializer.Deserialize<ScryfallCardDto>(json, JsonOpts)
               ?? throw new InvalidOperationException("Failed to parse Scryfall card response.");
    }

    public async Task<IReadOnlyList<ScryfallCardDto>> SearchAsync(string query, int maxCards)
    {
        var url = $"https://api.scryfall.com/cards/search?q={Uri.EscapeDataString(query)}";
        var results = new List<ScryfallCardDto>(capacity: Math.Min(maxCards, 512));

        while (url is not null && results.Count < maxCards)
        {
            var json = await GetStringAsync(url);
            var page = JsonSerializer.Deserialize<ScryfallSearchDto>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Failed to parse Scryfall search response.");

            results.AddRange(page.Data);

            url = (page.HasMore && !string.IsNullOrWhiteSpace(page.NextPage))
                ? page.NextPage
                : null;
        }

        return results.Take(maxCards).ToArray();
    }

    private async Task<string> GetStringAsync(string url)
    {
        var cached = await _cache.TryGetAsync(url, CacheAge);
        if (cached is not null) return cached;

        await RateGate.WaitAsync();
        try
        {
            var elapsed = DateTime.UtcNow - _lastRequestUtc;
            if (elapsed < TimeSpan.FromMilliseconds(130))
                await Task.Delay(TimeSpan.FromMilliseconds(130) - elapsed);

            using var resp = await _http.GetAsync(url);
            _lastRequestUtc = DateTime.UtcNow;

            resp.EnsureSuccessStatusCode();
            var text = await resp.Content.ReadAsStringAsync();
            await _cache.PutAsync(url, text);
            return text;
        }
        finally
        {
            RateGate.Release();
        }
    }
}
