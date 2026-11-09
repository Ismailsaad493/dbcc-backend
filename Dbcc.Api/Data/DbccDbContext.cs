using Dbcc.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Dbcc.Api.Data;

public sealed class DbccDbContext(DbContextOptions<DbccDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<CollectionEntity> Collections => Set<CollectionEntity>();
    public DbSet<MatchEntity> Matches => Set<MatchEntity>();
    public DbSet<MatchPlayerScoreEntity> MatchPlayerScores => Set<MatchPlayerScoreEntity>();
    public DbSet<MatchExpenseEntity> MatchExpenses => Set<MatchExpenseEntity>();
    public DbSet<ExpenseTrackerEntity> ExpenseTrackers => Set<ExpenseTrackerEntity>();
    public DbSet<ExpensePeriodEntity> ExpensePeriods => Set<ExpensePeriodEntity>();
    public DbSet<ExpenseLineItemEntity> ExpenseLineItems => Set<ExpenseLineItemEntity>();
    public DbSet<ReportSettingsEntity> ReportSettings => Set<ReportSettingsEntity>();
    public DbSet<InitializationRecordEntity> InitializationRecords => Set<InitializationRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.ToTable("Players");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ExtraJson).IsRequired();
        });

        modelBuilder.Entity<CollectionEntity>(entity =>
        {
            entity.ToTable("Collections");
            entity.HasKey(x => x.PlayerId);
            entity.Property(x => x.ExtraJson).IsRequired();
            entity.HasOne(x => x.Player).WithOne(x => x.Collection)
                .HasForeignKey<CollectionEntity>(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchEntity>(entity =>
        {
            entity.ToTable("Matches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(128);
            entity.Property(x => x.Date).HasMaxLength(40).IsRequired();
            entity.Property(x => x.MatchNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ExtraJson).IsRequired();
        });

        modelBuilder.Entity<MatchPlayerScoreEntity>(entity =>
        {
            entity.ToTable("MatchPlayerScores");
            entity.HasKey(x => new { x.MatchId, x.PlayerId });
            entity.Property(x => x.MatchId).HasMaxLength(128);
            entity.Property(x => x.PlayerName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ExtraJson).IsRequired();
            entity.HasOne(x => x.Match).WithMany(x => x.Points).HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Player).WithMany(x => x.MatchScores).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchExpenseEntity>(entity =>
        {
            entity.ToTable("MatchExpenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MatchId).HasMaxLength(128);
            entity.Property(x => x.Label).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ExtraJson).IsRequired();
            entity.HasOne(x => x.Match).WithMany(x => x.Expenses).HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExpenseTrackerEntity>(entity =>
        {
            entity.ToTable("ExpenseTrackerState");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<ExpensePeriodEntity>(entity =>
        {
            entity.ToTable("ExpensePeriods");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.Label).HasMaxLength(200).IsRequired();
            entity.Property(x => x.OpeningBalanceLabel).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CollectionLabel).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CollectionAmountJson).IsRequired();
            entity.Property(x => x.ExtraJson).IsRequired();
            entity.HasOne(x => x.Tracker).WithMany(x => x.Periods).HasForeignKey(x => x.TrackerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExpenseLineItemEntity>(entity =>
        {
            entity.ToTable("ExpenseLineItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(12).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(300).IsRequired();
            entity.Property(x => x.AmountJson).IsRequired();
            entity.Property(x => x.Date).HasMaxLength(40);
            entity.Property(x => x.ExtraJson).IsRequired();
            entity.HasIndex(x => new { x.PeriodId, x.Kind, x.SortOrder });
            entity.HasOne(x => x.Period).WithMany(x => x.Items).HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportSettingsEntity>(entity =>
        {
            entity.ToTable("ReportSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Json).IsRequired();
        });

        modelBuilder.Entity<InitializationRecordEntity>(entity =>
        {
            entity.ToTable("DatabaseInitialization");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
        });
    }
}
