using Microsoft.EntityFrameworkCore;
using HistoricalService.Models;

namespace HistoricalService.Data
{
    public class HistoryDbContext : DbContext
    {
        public HistoryDbContext(DbContextOptions<HistoryDbContext> options) : base(options) { }

        // Add the Vehicles table
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<VehicleTelemetryRecord> TelemetryRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("postgis");

            // 1. Configure Vehicle -> Trip relationship
            modelBuilder.Entity<Trip>()
                .HasOne(t => t.Vehicle)
                .WithMany(v => v.Trips)
                .HasForeignKey(t => t.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. Configure Trip -> TelemetryRecord relationship
            modelBuilder.Entity<Trip>()
                .HasMany(t => t.RoutePoints)
                .WithOne(r => r.Trip)
                .HasForeignKey(r => r.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}