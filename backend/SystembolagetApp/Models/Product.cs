namespace SystembolagetApp.Models;

public class Product
{
    public int Id { get; set; }
    public string SystembolagetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SubName { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public decimal Price { get; set; }
    public double? Volume { get; set; }
    public string? Country { get; set; }
    public string? Producer { get; set; }
    public string? Description { get; set; }
    public string? Taste { get; set; }
    public double? AlcoholPercentage { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsNewRelease { get; set; }

    // Vivino
    public double? VivinoRating { get; set; }
    public int? VivinoReviewCount { get; set; }
    public string? VivinoUrl { get; set; }
    public DateTime? VivinoFetchedAt { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    // AI-genererade fält
    public int? AiRating { get; set; }         
    public int? ValueRating { get; set; }     
    public string? AiSummary { get; set; }
    public string? FlavorProfile { get; set; }   
    public DateTime? AiAnalyzedAt { get; set; }

    // Användare
    public bool IsFavorite { get; set; }
}
