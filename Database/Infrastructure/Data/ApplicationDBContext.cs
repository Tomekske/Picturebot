using Database.Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Database.Infrastructure.Data;

/// <summary>
///     The primary database context for the application, managing the persistence of settings and the node hierarchy.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> dbContextOptions)
    : DbContext(dbContextOptions) {
    /// <summary>
    ///     Gets or sets the collection of global application settings.
    /// </summary>
    public DbSet<Settings> Settings { get; set; }

    /// <summary>
    ///     Gets or sets the collection of all hierarchy nodes (Folders, Albums, and Pictures).
    /// </summary>
    public DbSet<Node> Nodes { get; set; }

    /// <summary>
    ///     Gets or sets the collection of album nodes.
    /// </summary>
    public DbSet<Album> Albums { get; set; }

    /// <summary>
    ///     Gets or sets the collection of folder nodes.
    /// </summary>
    public DbSet<Folder> Folders { get; set; }

    /// <summary>
    ///     Gets or sets the collection of picture nodes.
    /// </summary>
    public DbSet<Picture> Pictures { get; set; }

    /// <summary>
    ///     Gets or sets the collection of picture metrics.
    /// </summary>
    public DbSet<Metrics> Metrics { get; set; }

    /// <inheritdoc />
    /// <remarks>
    ///     Configures the Table-per-Type (TPT) inheritance mapping, property conversions (e.g., enums to strings,
    ///     ulong to long for SQLite compatibility), and seeds the initial application settings.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        // TPT Configuration
        modelBuilder.Entity<Node>().ToTable("nodes");
        modelBuilder.Entity<Album>().ToTable("albums");
        modelBuilder.Entity<Folder>().ToTable("folders");
        modelBuilder.Entity<Picture>().ToTable("pictures");

        modelBuilder.Entity<Metrics>().ToTable("metrics");

        modelBuilder.Entity<Node>()
            .Property(n => n.Type)
            .HasConversion<string>();



        modelBuilder.Entity<Picture>()
            .Property(p => p.ProcessingState)
            .HasConversion<string>();

        // Seed default Settings
        modelBuilder.Entity<Settings>().HasData(
            new Settings {
                Id = 1,
                ThemeMode = ThemeMode.System,
                LibraryPath = "",
                GroupingThreshold = 8,
                BurstTimeThresholdSeconds = 3,
                BurstFallbackTimeThresholdSeconds = 10,
                LaunchMaximized = false,
                RedLabelName = "Red",
                OrangeLabelName = "Orange",
                YellowLabelName = "Yellow",
                GreenLabelName = "Green",
                BlueLabelName = "Blue",
                PinkLabelName = "Pink",
                PurpleLabelName = "Purple",
                RedLabelShortcut = "Ctrl+NumPad1",
                OrangeLabelShortcut = "Ctrl+NumPad2",
                YellowLabelShortcut = "Ctrl+NumPad3",
                GreenLabelShortcut = "Ctrl+NumPad4",
                BlueLabelShortcut = "Ctrl+NumPad5",
                PinkLabelShortcut = "Ctrl+NumPad6",
                PurpleLabelShortcut = "Ctrl+NumPad7",
                NoneLabelShortcut = "Ctrl+NumPad0"
            }
        );

        modelBuilder.Entity<Node>()
            .HasIndex(n => new { n.ParentId, n.Name, n.Type })
            .IsUnique();

        // This line tells EF Core: 
        // "In C# treat PHash as ulong, but in the DB store it as a long."
        modelBuilder.Entity<Metrics>()
            .Property(p => p.PHash)
            .HasConversion<long>();

        modelBuilder.Entity<Picture>()
            .Property(p => p.Hash)
            .HasConversion<long>();
    }
}
