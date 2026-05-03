using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using SystembolagetApp;
using SystembolagetApp.Data;
using SystembolagetApp.Models;
using SystembolagetApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Använd PostgreSQL i produktion (DATABASE_URL satt), SQLite lokalt
// CORS – tillåt lokal dev och Vercel-produktion
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3001";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3001",
                "http://localhost:5173",
                frontendUrl
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        // Render.com sätter DATABASE_URL i postgres:// format – konvertera till Npgsql-format
        var connStr = databaseUrl.StartsWith("postgres://")
            ? ConvertPostgresUrl(databaseUrl)
            : databaseUrl;
        opt.UseNpgsql(connStr);
    }
    else
    {
        opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=systembolaget.db");
    }
});

builder.Services.AddHttpClient("Systembolaget", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 SystembolagetApp/1.0");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient("WineSearcher", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept", "application/json, text/html, */*");
    client.DefaultRequestHeaders.Add("Accept-Language", "sv-SE,sv;q=0.9,en;q=0.8");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient();

// Bind externa API-nycklar från miljövariabler
builder.Configuration["GWS:ApiKey"] = Environment.GetEnvironmentVariable("GWS_API_KEY") ?? builder.Configuration["GWS:ApiKey"] ?? "";
builder.Configuration["Systembolaget:ApiKey"] = Environment.GetEnvironmentVariable("SYSTEMBOLAGET_API_KEY") ?? "cfc702aed3094c86b92d6d4ff7a54c84";

builder.Services.AddScoped<SystembolagetFetcherService>();
builder.Services.AddScoped<AiAnalyzerService>();
builder.Services.AddScoped<VivinoService>();
builder.Services.AddHostedService<DataRefreshWorker>();

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Hjälpmetod för att konvertera Render.com postgres:// URL till Npgsql connection string
static string ConvertPostgresUrl(string url)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    var user = userInfo[0];
    var password = userInfo.Length > 1 ? userInfo[1] : "";
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var db = uri.AbsolutePath.TrimStart('/');
    return $"Host={host};Port={port};Database={db};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}

var app = builder.Build();
app.UseCors();

app.MapGet("/api/products", async (
    AppDbContext db,
    string? category,
    string? subCategory,
    decimal? minPrice,
    decimal? maxPrice,
    int? minRating,
    int? exactRating,
    bool? newOnly,
    bool? favoritesOnly,
    string? sort,
    int page = 1,
    int pageSize = 24) =>
{
    var query = db.Products.AsQueryable();

    if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(subCategory))
        query = query.Where(p => p.Category == category && p.SubCategory == subCategory);
    else if (!string.IsNullOrEmpty(category))
        query = query.Where(p => p.Category == category);
    if (minPrice.HasValue)
        query = query.Where(p => p.Price >= minPrice.Value);
    if (maxPrice.HasValue)
        query = query.Where(p => p.Price <= maxPrice.Value);
    if (exactRating.HasValue)
        query = query.Where(p => p.AiRating == exactRating.Value);
    else if (minRating.HasValue)
        query = query.Where(p => p.AiRating >= minRating.Value);
    if (newOnly == true)
        query = query.Where(p => p.IsNewRelease);
    if (favoritesOnly == true)
        query = query.Where(p => p.IsFavorite);

    // SQLite stöder inte decimal i ORDER BY – casta till double
    query = sort switch
    {
        "price_asc"  => query.OrderBy(p => (double)p.Price),
        "price_desc" => query.OrderByDescending(p => (double)p.Price),
        "value"      => query.OrderByDescending(p => p.ValueRating),
        "vivino"     => query.OrderByDescending(p => p.VivinoRating),
        _            => query.OrderByDescending(p => p.AiRating)
    };

    var total = await query.CountAsync();
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    return Results.Ok(new { total, page, pageSize, items });
});

app.MapGet("/api/products/{id}", async (AppDbContext db, int id) =>
    await db.Products.FindAsync(id) is Product p ? Results.Ok(p) : Results.NotFound());

app.MapPatch("/api/products/{id}/favorite", async (AppDbContext db, int id) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();
    product.IsFavorite = !product.IsFavorite;
    await db.SaveChangesAsync();
    return Results.Ok(new { product.IsFavorite });
});

app.UseCors();

app.MapGet("/api/reset-ai", async (AppDbContext db) =>
{
    // Nollställ bara viner med generiska sammanfattningar (gamla mock-data)
    var products = await db.Products
        .Where(p => p.AiAnalyzedAt != null && (
            p.AiSummary == null ||
            p.AiSummary.Contains("I premiumsegmentet") ||
            p.AiSummary.Contains("Medelprisigt") ||
            p.AiSummary.Contains("Prisvärt val")
        ))
        .ToListAsync();
    foreach (var p in products)
    {
        p.AiAnalyzedAt = null;
        p.AiSummary = null;
        p.FlavorProfile = null;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { reset = products.Count });
});

app.MapGet("/api/debug/news", async (AppDbContext db) =>
{
    var total = await db.Products.CountAsync();
    var withNews = await db.Products.CountAsync(p => p.IsNewRelease);
    var sample = await db.Products.Where(p => p.IsNewRelease).Take(3).Select(p => p.Name).ToListAsync();
    return new { total, withNews, sample };
});

app.MapGet("/api/debug/categories", async (AppDbContext db) =>
{
    var cats = await db.Products
        .GroupBy(p => new { p.Category, p.SubCategory })
        .Select(g => new { g.Key.Category, g.Key.SubCategory, count = g.Count() })
        .OrderBy(x => x.Category).ThenBy(x => x.SubCategory)
        .ToListAsync();
    return cats;
});

app.MapGet("/api/categories", async (AppDbContext db) =>
{
    var cats = await db.Products
        .GroupBy(p => p.Category)
        .Select(g => new { category = g.Key, count = g.Count() })
        .OrderByDescending(x => x.count)
        .ToListAsync();
    return cats;
});

app.MapGet("/api/stats", async (AppDbContext db) => new
{
    Total      = await db.Products.CountAsync(),
    Analyzed   = await db.Products.CountAsync(p => p.AiAnalyzedAt != null),
    NewReleases= await db.Products.CountAsync(p => p.IsNewRelease),
    Favorites  = await db.Products.CountAsync(p => p.IsFavorite),
    WithVivino = await db.Products.CountAsync(p => p.VivinoRating != null),
    AvgRating  = await db.Products.Where(p => p.AiRating != null).AverageAsync(p => (double?)p.AiRating) ?? 0,
});

app.MapPost("/api/admin/refresh", async (
    SystembolagetFetcherService fetcher,
    AiAnalyzerService analyzer,
    VivinoService vivino) =>
{
    await fetcher.FetchAndSaveAsync();
    await analyzer.AnalyzeUnanalyzedProductsAsync(50);
    await vivino.EnrichWithVivinoAsync(30);
    return Results.Ok("Refresh klar!");
});

app.MapPost("/api/admin/vivino", async (VivinoService vivino) =>
{
    await vivino.EnrichWithVivinoAsync(50);
    return Results.Ok("Vivino-berikande klart!");
});

app.Run();
