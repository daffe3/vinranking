using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystembolagetApp.Data;
using SystembolagetApp.Models;

namespace SystembolagetApp.Services;

public class AiAnalyzerService
{
    private readonly ILogger<AiAnalyzerService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    public AiAnalyzerService(
        ILogger<AiAnalyzerService> logger,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IConfiguration config)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _config = config;
    }

    public async Task AnalyzeUnanalyzedProductsAsync(int batchSize = 50, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.Products
            .Where(p => p.AiAnalyzedAt == null)
            .OrderByDescending(p => p.FetchedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        _logger.LogInformation("AI-analyserar {Count} produkter...", products.Count);

        var apiKey = _config["AI:ApiKey"] ?? "";
        var hasKey = !string.IsNullOrWhiteSpace(apiKey);

        if (!hasKey)
            _logger.LogInformation("Ingen AI-nyckel – använder tasteclock-baserade mock-betyg");

        foreach (var product in products)
        {
            try
            {
                if (hasKey)
                    await AnalyzeWithAiAsync(product, apiKey, ct);
                else
                    SetSmartMockData(product);

                product.AiAnalyzedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                if (hasKey) await Task.Delay(400, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kunde inte analysera {Name}", product.Name);
                SetSmartMockData(product);
                product.AiAnalyzedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("AI-analys klar for {Count} produkter", products.Count);
    }

    private async Task AnalyzeWithAiAsync(Product product, string apiKey, CancellationToken ct)
    {
        var provider = _config["AI:Provider"] ?? "openai";
        var prompt = BuildPrompt(product);
        var json = provider switch
        {
            "anthropic"   => await CallAnthropicAsync(prompt, apiKey, ct),
            "gemini"      => await CallGeminiAsync(prompt, apiKey, ct),
            "openrouter"  => await CallOpenRouterAsync(prompt, apiKey, ct),
            _             => await CallOpenAiAsync(prompt, apiKey, ct),
        };
        ParseAiResponse(product, json);
    }

    private static string BuildPrompt(Product product)
    {
        var tastePart = product.Taste?.Contains("|||") == true
            ? product.Taste.Split("|||")[0]
            : product.Taste ?? "";

        var pricePerLiter = product.Volume is > 0
            ? Math.Round((double)(decimal)product.Price / (product.Volume.Value / 1000.0), 0)
            : (double)(decimal)product.Price;

        var sb = new StringBuilder();
        sb.AppendLine("Du ar en vinexpert. Returnera ENBART ett JSON-objekt, inga backticks, ingen forklaring.");
        sb.AppendLine($"Vin: {product.Name} {product.SubName}".Trim());
        sb.AppendLine($"Typ: {product.Category}, Land: {product.Country}, Producent: {product.Producer}");
        sb.AppendLine($"Pris: {product.Price} kr ({pricePerLiter} kr/l), Alkohol: {product.AlcoholPercentage}%");
        if (!string.IsNullOrEmpty(tastePart))
            sb.AppendLine($"Smak (fran Systembolaget): {tastePart}");
        if (!string.IsNullOrEmpty(product.Description))
            sb.AppendLine($"Beskrivning: {product.Description}");
        sb.AppendLine();
        sb.AppendLine("Svara ENDAST med ett JSON-objekt, inget annat:");
        sb.AppendLine("{\"rating\": X, \"valueRating\": Y, \"summary\": \"TEXT\", \"flavorProfile\": \"ORD1, ORD2, ORD3\"}");
        sb.AppendLine();
        sb.AppendLine("rating: 1-5 baserat pa producentens/regionens rykte (1=okänt, 5=världsberömt som Krug/DRC)");
        sb.AppendLine("valueRating: 1-5 hur prisvärt det är givet kvaliteten (5=kap, 1=överprissatt)");
        sb.AppendLine($"summary: En mening pa svenska specifikt om {product.Name} - nämn druvsort och region. INTE 'kraftig karaktär' eller 'premiumsegmentet'.");
        sb.AppendLine("flavorProfile: 3 smakord pa svenska specifika för denna vintyp");
        return sb.ToString();
    }

    private async Task<string> CallOpenAiAsync(string prompt, string apiKey, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
        var body = new { model = "gpt-4o-mini", messages = new[] { new { role = "user", content = prompt } }, temperature = 0.3 };
        var resp = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", body, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
    }

    private async Task<string> CallAnthropicAsync(string prompt, string apiKey, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var body = new { model = "claude-haiku-4-5-20251001", max_tokens = 300, messages = new[] { new { role = "user", content = prompt } } };
        var resp = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", body, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "{}";
    }

    private async Task<string> CallOpenRouterAsync(string prompt, string apiKey, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
        client.DefaultRequestHeaders.Add("HTTP-Referer", "https://vinranking.local");
        var body = new
        {
            model = "meta-llama/llama-3.1-8b-instruct:free",
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.3
        };
        var resp = await client.PostAsJsonAsync("https://openrouter.ai/api/v1/chat/completions", body, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
    }

    private async Task<string> CallGeminiAsync(string prompt, string apiKey, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key=" + apiKey;
        var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
        var resp = await client.PostAsJsonAsync(url, body, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Gemini {resp.StatusCode}: {errBody[..Math.Min(300, errBody.Length)]}");
        }
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "{}";
    }

    private static void ParseAiResponse(Product product, string json)
    {
        try
        {
            json = json.Trim().TrimStart('`');
            if (json.StartsWith("json", StringComparison.OrdinalIgnoreCase)) json = json[4..];
            json = json.TrimEnd('`').Trim();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("rating", out var r)) product.AiRating = r.GetInt32();
            if (root.TryGetProperty("valueRating", out var v)) product.ValueRating = v.GetInt32();
            if (root.TryGetProperty("summary", out var s)) product.AiSummary = s.GetString();
            if (root.TryGetProperty("flavorProfile", out var f)) product.FlavorProfile = f.GetString();
        }
        catch { SetSmartMockData(product); }
    }

    /// <summary>
    /// Smarta mock-betyg baserade på TasteClock-data och pris/liter.
    /// TasteClock-fält sparas i FlavorProfile-kolumnen som JSON under datainsamling.
    /// </summary>
    private static void SetSmartMockData(Product product)
    {
        var pricePerLiter = product.Volume is > 0
            ? (double)(product.Price / (decimal)(product.Volume / 1000.0))
            : (double)product.Price;

        // Parsea TasteClock-data från Taste-fältet (sparas som "smaktext|||{json}")
        int body = 5, sweetness = 0, roughness = 5, fruitAcid = 5, bitterness = 0;
        if (!string.IsNullOrEmpty(product.Taste) && product.Taste.Contains("|||"))
        {
            try
            {
                var jsonPart = product.Taste.Split("|||")[1];
                using var doc = JsonDocument.Parse(jsonPart);
                var r = doc.RootElement;
                if (r.TryGetProperty("body", out var b)) body = b.GetInt32();
                if (r.TryGetProperty("sweetness", out var sw)) sweetness = sw.GetInt32();
                if (r.TryGetProperty("roughness", out var ro)) roughness = ro.GetInt32();
                if (r.TryGetProperty("fruitAcid", out var fa)) fruitAcid = fa.GetInt32();
                if (r.TryGetProperty("bitterness", out var bi)) bitterness = bi.GetInt32();
            }
            catch { }
        }

        // Betyg: kombinerar komplexitet (body + roughness) med pris/liter-ratio
        var complexity = (body + roughness + fruitAcid) / 3.0;
        var valueScore = pricePerLiter switch
        {
            < 80  => 5,
            < 130 => 4,
            < 200 => 3,
            < 350 => 2,
            _     => 1
        };

        // AI-rating: hög komplexitet + rimligt pris = högt betyg
        var rawRating = (complexity / 10.0) * 3.0 + (valueScore / 5.0) * 2.0;
        product.AiRating = Math.Clamp((int)Math.Round(rawRating), 1, 5);
        product.ValueRating = valueScore;

        // Smakprofil baserad på tasteClocks
        var flavors = new List<string>();
        if (body >= 7) flavors.Add("Fylligt");
        else if (body <= 3) flavors.Add("Lätt");
        else flavors.Add("Medelfylligt");

        if (sweetness >= 5) flavors.Add("Sött");
        else if (fruitAcid >= 7) flavors.Add("Fruktsyra");
        else flavors.Add("Torrt");

        if (roughness >= 7) flavors.Add("Tanninrikt");
        else if (bitterness >= 5) flavors.Add("Beskt");
        else flavors.Add("Mjukt");

        product.FlavorProfile = string.Join(", ", flavors);

        // Sammanfattning baserad på kategori och land
        var origin = string.IsNullOrEmpty(product.Country) ? "okänt ursprung" : product.Country;
        var priceComment = valueScore >= 4 ? "Prisvärt val." : valueScore == 3 ? "Medelprisigt." : "I premiumsegmentet.";
        product.AiSummary = product.Category switch
        {
            "Rött vin"            => $"Rött vin från {origin} med {(body >= 7 ? "kraftig" : "mjuk")} karaktär. {priceComment}",
            "Vitt vin"            => $"Vitt vin från {origin} med {(fruitAcid >= 7 ? "frisk syra" : "rund smak")}. {priceComment}",
            "Mousserande vin"     => $"Mousserande vin från {origin}. Passar som aperitif eller till skaldjur. {priceComment}",
            "Champagne"           => $"Champagne från {origin} med elegant mousse. {priceComment}",
            "Rosé och övrigt vin" => $"Rosévin från {origin} med {(sweetness >= 3 ? "fruktig" : "torr")} stil. {priceComment}",
            "Dessertvin"          => $"Dessertvin från {origin} med {(sweetness >= 7 ? "rik sötma" : "delikat sötma")}. {priceComment}",
            _                     => $"Vin från {origin}. {priceComment}"
        };
    }
}
