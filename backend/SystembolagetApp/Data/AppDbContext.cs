using Microsoft.EntityFrameworkCore;
using SystembolagetApp.Models;

namespace SystembolagetApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.SystembolagetId).IsUnique();
            entity.Property(p => p.Price).HasColumnType("decimal(10,2)");
        });
    }
}
