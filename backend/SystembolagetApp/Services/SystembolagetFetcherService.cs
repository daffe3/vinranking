using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystembolagetApp.Data;
using SystembolagetApp.Models;

namespace SystembolagetApp.Services;

public class SystembolagetFetcherService
{
    private readonly ILogger<SystembolagetFetcherService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;

    private const string ApiBase = "https://api-extern.systembolaget.se/sb-api-ecommerce/v1/productsearch/search";
    private readonly string ApiKey;

    // categoryLevel1 = "Vin" för alla viner, filtrera på categoryLevel2
    private static readonly HashSet<string> WineSubCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rött vin", "Vitt vin", "Rosé och övrigt vin", "Mousserande vin", "Champagne", "Dessertvin"
    };

    // Bara tillfälliga lanseringar – INTE "Fast sortiment"
    private static readonly HashSet<string> AllowedAssortment = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tillfälligt sortiment", "Tillfällig", "Kommande", "Webblanseringen", "Nyhet"
    };

    public SystembolagetFetcherService(
        ILogger<SystembolagetFetcherService> logger,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IConfiguration config)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _config = config;
        ApiKey = config["Systembolaget:ApiKey"]
            ?? Environment.GetEnvironmentVariable("SYSTEMBOLAGET_API_KEY")
            ?? "cfc702aed3094c86b92d6d4ff7a54c84";
    }

    public async Task FetchAndSaveAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Startar hämtning av Systembolagets sortiment...");

        var client = _httpClientFactory.CreateClient("Systembolaget");
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", ApiKey);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        int added = 0, updated = 0, saved = 0, skipped = 0;
        int page = 1;
        bool loggedFields = false;

        while (true)
        {
            var url = $"{ApiBase}?page={page}&size=100&sortBy=Score&sortDirection=Ascending";

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fel vid hämtning sida {Page}", page);
                break;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("products", out var products)) break;
            var productArray = products.EnumerateArray().ToList();
            if (productArray.Count == 0) break;

            // Logga fältnamn + sortimentsvärden på sida 1
            if (!loggedFields && productArray.Count > 0)
            {
                var fields = string.Join(", ", productArray[0].EnumerateObject().Select(x => x.Name));
                _logger.LogInformation("API-fält: {Fields}", fields);

                // Logga tillfälligt-flaggor och bildstruktur
                var tsCount = productArray.Count(x => x.TryGetProperty("isTsAssortment", out var v) && v.ValueKind == JsonValueKind.True);
                _logger.LogInformation("Sida {Page}: {Ts} av {Total} har isTsAssortment=true", page, tsCount, productArray.Count);

                // Logga bildstrukturen för första produkten
                if (productArray.Count > 0 && productArray[0].TryGetProperty("images", out var sampleImgs))
                {
                    _logger.LogInformation("images-fält exempel: {Img}", sampleImgs.ToString()[..Math.Min(200, sampleImgs.ToString().Length)]);
                }
                loggedFields = true;
            }

            foreach (var p in productArray)
            {
                var cat1 = GetStr(p, "categoryLevel1") ?? "";
                var cat2 = GetStr(p, "categoryLevel2") ?? "";
                var assortment = GetStr(p, "assortmentText") ?? "";

                // Filtrera: bara vin (cat1="Vin")
                if (!cat1.Equals("Vin", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                // Hoppa över vinunderkategorier vi inte vill ha (öl, cider etc som råkar ha cat1=Vin)
                if (!string.IsNullOrEmpty(cat2) && !WineSubCategories.Contains(cat2))
                {
                    skipped++;
                    continue;
                }

                // Filtrera på assortmentText – vi vill bara ha "Tillfälligt sortiment"
                if (!assortment.Equals("Tillfälligt sortiment", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                // Filtrera bort slutsålda produkter
                var isOutOfStock = p.TryGetProperty("isCompletelyOutOfStock", out var oosEl) && oosEl.ValueKind == JsonValueKind.True;
                if (isOutOfStock)
                {
                    skipped++;
                    continue;
                }

                var id = GetStr(p, "productId") ?? GetStr(p, "productNumber");
                if (string.IsNullOrEmpty(id)) continue;

                saved++;
                var existing = await db.Products.FirstOrDefaultAsync(x => x.SystembolagetId == id, ct);
                var product = existing ?? new Product { SystembolagetId = id };

                product.Name        = GetStr(p, "productNameBold") ?? string.Empty;
                product.SubName     = GetStr(p, "productNameThin");
                // Normalisera kategorin mot exakt det frontend förväntar sig
                product.Category    = NormalizeCategory(cat2);
                product.SubCategory = GetStr(p, "categoryLevel3");
                product.Country     = GetStr(p, "country");
                product.Producer    = GetStr(p, "producerName");
                product.Description = GetStr(p, "usage");
                product.Taste       = GetStr(p, "taste");
                product.Price       = GetDecimal(p, "price");
                product.Volume      = GetDouble(p, "volume");
                product.AlcoholPercentage = GetDouble(p, "alcoholPercentage");

                var artNr = GetStr(p, "productNumber") ?? id;
                product.Url = $"https://www.systembolaget.se/produkt/vin/{artNr}/";

                // Spara TasteClock-data i Taste-fältet för smart mock-analys
                // (FlavorProfile sätts av AI-analyzern till läsbar text)
                if (p.TryGetProperty("tasteClockBody", out var tcBody) && tcBody.ValueKind == JsonValueKind.Number)
                {
                    var clockData = new
                    {
                        body       = tcBody.GetInt32(),
                        sweetness  = p.TryGetProperty("tasteClockSweetness", out var sw) ? sw.GetInt32() : 0,
                        roughness  = p.TryGetProperty("tasteClockRoughness", out var ro) ? ro.GetInt32() : 0,
                        fruitAcid  = p.TryGetProperty("tasteClockFruitacid", out var fa) ? fa.GetInt32() : 0,
                        bitterness = p.TryGetProperty("tasteClockBitter", out var bi) ? bi.GetInt32() : 0,
                    };
                    // Lägg till tasteclock-data sist i Taste-strängen som JSON-suffix
                    var existingTaste = GetStr(p, "taste") ?? "";
                    product.Taste = existingTaste + "|||" + System.Text.Json.JsonSerializer.Serialize(clockData);
                }

                // Bilderna ligger i images[0].imageUrl, format: https://.../{id}/{id}
                product.ImageUrl = null;
                if (p.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var img in imgs.EnumerateArray())
                    {
                        var imgUrl = GetStr(img, "imageUrl");
                        if (!string.IsNullOrEmpty(imgUrl))
                        {
                            // Lägg till _400.png om det saknas
                            if (!imgUrl.EndsWith(".png") && !imgUrl.EndsWith(".jpg"))
                                imgUrl += "_400.png";
                            product.ImageUrl = imgUrl;
                            break;
                        }
                    }
                }

                // isNews = nyhet på Systembolaget, isWebLaunch = webblansering
                var isNews = p.TryGetProperty("isNews", out var newsEl) && newsEl.ValueKind == JsonValueKind.True;
                var isWebLaunch = p.TryGetProperty("isWebLaunch", out var wlEl) && wlEl.ValueKind == JsonValueKind.True;
                var isNewRelease2 = p.TryGetProperty("isNewRelease", out var nrEl) && nrEl.ValueKind == JsonValueKind.True;
                // Använd productLaunchDate som fallback – om lanserad inom 90 dagar = nyhet
                var launchDateStr = GetStr(p, "productLaunchDate") ?? GetStr(p, "sellStartDate");
                var isRecentLaunch = false;
                if (!string.IsNullOrEmpty(launchDateStr) && DateTime.TryParse(launchDateStr, out var launchDate))
                    isRecentLaunch = launchDate >= DateTime.UtcNow.AddDays(-10);
                product.IsNewRelease = isNews || isWebLaunch || isNewRelease2 || isRecentLaunch;

                product.FetchedAt = DateTime.UtcNow;

                if (existing == null) { db.Products.Add(product); added++; }
                else updated++;
            }

            await db.SaveChangesAsync(ct);

            var totalHits = root.TryGetProperty("metadata", out var meta) &&
                            meta.TryGetProperty("docCount", out var th) ? th.GetInt32() : 0;

            _logger.LogInformation("Sida {Page}/{Total}: sparade {Saved}, hoppade {Skip}",
                page, (totalHits / 100) + 1, saved, skipped);

            if (totalHits == 0 || page * 100 >= totalHits) break;
            page++;
            await Task.Delay(200, ct);
        }

        _logger.LogInformation("Klart! {Added} nya, {Updated} uppdaterade, {Skip} filtrerade bort",
            added, updated, skipped);
    }

    // Exakta kategorinamn från Systembolagets API mappade till vad frontend visar
    private static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Rött vin",            "Rött vin" },
        { "Rott vin",            "Rött vin" },
        { "Vitt vin",            "Vitt vin" },
        { "Rosé och övrigt vin", "Rosé och övrigt vin" },
        { "Rose och ovrigt vin", "Rosé och övrigt vin" },
        { "Rosévin",             "Rosé och övrigt vin" },
        { "Rosevin",             "Rosé och övrigt vin" },
        { "Mousserande vin",     "Mousserande vin" },
        { "Champagne",           "Champagne" },
        { "Dessertvin",          "Dessertvin" },
        { "Dessert",             "Dessertvin" },
    };

    private static string NormalizeCategory(string cat2)
    {
        var c = cat2.Trim();
        if (CategoryMap.TryGetValue(c, out var mapped)) return mapped;

        // Logga omappade kategorier för felsökning
        Console.WriteLine($"[UNMAPPED CATEGORY] '{c}' (len={c.Length}, bytes={string.Join(",", System.Text.Encoding.UTF8.GetBytes(c).Take(10))})");

        // Fallback: fuzzy match
        if (c.StartsWith("Rö") || (c.StartsWith("R") && c.Contains("tt vin"))) return "Rött vin";
        if (c.StartsWith("Vi")) return "Vitt vin";
        if (c.StartsWith("Ros")) return "Rosé och övrigt vin";
        if (c.StartsWith("Mo")) return "Mousserande vin";
        if (c.StartsWith("Ch")) return "Champagne";
        if (c.StartsWith("De")) return "Dessertvin";

        return c;
    }

    private static string? GetStr(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal GetDecimal(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? (decimal)v.GetDouble() : 0;

    private static double? GetDouble(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}
