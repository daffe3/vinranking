using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystembolagetApp.Data;
using SystembolagetApp.Models;

namespace SystembolagetApp.Services;

/// <summary>
/// Hämtar community-betyg via Wine-Searcher och CellarTracker.
/// Wine-Searcher har ett publik sök-API som fungerar från Docker.
/// </summary>
public class VivinoService
{
    private readonly ILogger<VivinoService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;

    public VivinoService(ILogger<VivinoService> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
    }

    public async Task EnrichWithVivinoAsync(int batchSize = 50, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.Products
            .Where(p => p.VivinoFetchedAt == null && p.AiAnalyzedAt != null)
            .OrderByDescending(p => p.AiRating)
            .Take(batchSize)
            .ToListAsync(ct);

        if (products.Count == 0) return;

        _logger.LogInformation("Hämtar Wine-Searcher betyg för {Count} produkter", products.Count);
        var client = _httpClientFactory.CreateClient("WineSearcher");
        int hits = 0;

        foreach (var product in products)
        {
            try
            {
                var result = await SearchWineSearcherAsync(client, product, ct);
                if (result != null)
                {
                    product.VivinoRating = result.Rating;
                    product.VivinoReviewCount = result.ReviewCount;
                    product.VivinoUrl = result.Url;
                    hits++;
                    _logger.LogInformation("Betyg {R} för {N}", result.Rating, product.Name);
                }
                product.VivinoFetchedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await Task.Delay(Random.Shared.Next(500, 1000), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Wine-Searcher-fel för {Name}", product.Name);
                product.VivinoFetchedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        _logger.LogInformation("Wine-Searcher: {Hits}/{Total} träffar", hits, products.Count);
    }

    private async Task<WineResult?> SearchWineSearcherAsync(HttpClient client, Product product, CancellationToken ct)
    {
        var query = Uri.EscapeDataString(product.Name.Trim());
        var url = $"https://www.wine-searcher.com/api/wine/search?q={query}&fmt=json&currency=SEK";

        HttpResponseMessage resp;
        try { resp = await client.GetAsync(url, ct); }
        catch { return null; }

        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrEmpty(json) || json.TrimStart().StartsWith("<")) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement wines = default;
            if (root.TryGetProperty("wines", out wines) ||
                root.TryGetProperty("results", out wines) ||
                root.TryGetProperty("data", out wines))
            {
                foreach (var wine in wines.EnumerateArray().Take(3))
                {
                    var name = GetStr(wine, "name") ?? GetStr(wine, "wine_name") ?? "";
                    if (!IsMatch(product.Name, name)) continue;

                    double? rating = null;
                    int? count = null;

                    if (wine.TryGetProperty("community_review_count", out var cr) && cr.ValueKind == JsonValueKind.Number)
                        count = cr.GetInt32();
                    if (wine.TryGetProperty("community_average_rating", out var car) && car.ValueKind == JsonValueKind.Number)
                    {
                        rating = car.GetDouble();
                    }
                    else if (wine.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number)
                    {
                        var raw = sc.GetDouble();
                        if (raw > 10) rating = Math.Round((raw - 50) / 10.0 + 1, 1);
                        else rating = raw;
                    }

                    if (rating == null || rating <= 0) continue;

                    var wineId = GetStr(wine, "id") ?? GetStr(wine, "wine_id") ?? "";
                    return new WineResult
                    {
                        Rating = Math.Clamp(Math.Round(rating.Value, 1), 1, 5),
                        ReviewCount = count,
                        Url = string.IsNullOrEmpty(wineId) ? null : $"https://www.wine-searcher.com/find/{Uri.EscapeDataString(product.Name)}"
                    };
                }
            }
        }
        catch { }

        return null;
    }

    private static bool IsMatch(string a, string b)
    {
        if (string.IsNullOrEmpty(b)) return false;
        var wa = a.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToHashSet();
        return b.ToLower().Split(' ').Any(w => wa.Contains(w));
    }

    private static string? GetStr(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private class WineResult
    {
        public double Rating { get; set; }
        public int? ReviewCount { get; set; }
        public string? Url { get; set; }
    }
}
