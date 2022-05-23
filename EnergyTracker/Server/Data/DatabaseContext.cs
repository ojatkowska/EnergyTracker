using Microsoft.EntityFrameworkCore;
using EnergyTracker.Shared.Models;

namespace EnergyTracker.Server.Data;

public class DatabaseContext : DbContext
{
    public DbSet<Kse> Kses { get; set; }
    public DbSet<Weather> Weathers { get; set; }
    public DbSet<Forecast> Forecasts { get; set; }
    public DbSet<Prediction> Predictions { get; set; }

    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {
        Database.Migrate();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

    }
}