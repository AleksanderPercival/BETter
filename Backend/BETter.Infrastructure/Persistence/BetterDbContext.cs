using System.Data;
using BETter.Domain.Entities;
using BETter.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BETter.Infrastructure.Persistence;

public class BetterDbContext : DbContext
{
    public BetterDbContext(DbContextOptions<BetterDbContext> options)
        : base(options)
    {
    }

    public DbSet<SportEvent> SportEvents => Set<SportEvent>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Selection> Selections => Set<Selection>();
    public DbSet<BetSlip> BetSlips => Set<BetSlip>();
    public DbSet<BetSlipItem> BetSlipItems => Set<BetSlipItem>();

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<WalletEntity> Wallets => Set<WalletEntity>();
    public DbSet<LedgerEntryEntity> LedgerEntries => Set<LedgerEntryEntity>();
    public DbSet<BettingLimitsEntity> BettingLimits => Set<BettingLimitsEntity>();

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return await operation(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                ct);

            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureDomainEntities(modelBuilder);
        ConfigureWalletEntities(modelBuilder);
    }

    private static void ConfigureDomainEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SportEvent>(entity =>
        {
            entity.ToTable("sport_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.Property(e => e.ExternalId).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.ExternalId).IsUnique();

            entity.Property(e => e.Sport).HasMaxLength(64).IsRequired();
            entity.Property(e => e.League).HasMaxLength(128).IsRequired();
            entity.Property(e => e.HomeTeam).HasMaxLength(128).IsRequired();
            entity.Property(e => e.AwayTeam).HasMaxLength(128).IsRequired();
            entity.Property(e => e.StartTime).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(e => e.HomeScore);
            entity.Property(e => e.AwayScore);

            entity.Ignore(e => e.DisplayName);

            entity.HasMany(e => e.Markets)
                .WithOne()
                .HasForeignKey(m => m.SportEventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(e => e.Markets)
                .HasField("_markets")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Market>(entity =>
        {
            entity.ToTable("markets");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).ValueGeneratedNever();

            entity.Property(m => m.SportEventId).IsRequired();
            entity.HasIndex(m => m.SportEventId);

            entity.Property(m => m.Name).HasMaxLength(128).IsRequired();
            entity.Property(m => m.IsSuspended).IsRequired();

            entity.HasMany(m => m.Selections)
                .WithOne()
                .HasForeignKey(s => s.MarketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(m => m.Selections)
                .HasField("_selections")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Selection>(entity =>
        {
            entity.ToTable("selections");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedNever();

            entity.Property(s => s.MarketId).IsRequired();
            entity.Property(s => s.SportEventId).IsRequired();
            entity.HasIndex(s => s.SportEventId);

            entity.Property(s => s.Name).HasMaxLength(128).IsRequired();
            entity.Property(s => s.Odds).HasPrecision(18, 4).IsRequired();
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<BetSlip>(entity =>
        {
            entity.ToTable("bet_slips");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).ValueGeneratedNever();

            entity.Property(b => b.UserId).IsRequired();
            entity.HasIndex(b => b.UserId);

            entity.Property(b => b.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(b => b.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

            entity.Property(b => b.GrossStake).HasPrecision(18, 2).IsRequired();
            entity.Property(b => b.NetStake).HasPrecision(18, 2).IsRequired();
            entity.Property(b => b.TaxMultiplier).HasPrecision(18, 4).IsRequired();
            entity.Property(b => b.TotalOdds).HasPrecision(18, 4).IsRequired();
            entity.Property(b => b.PotentialPayout).HasPrecision(18, 2).IsRequired();
            entity.Property(b => b.SettledPayout).HasPrecision(18, 2);

            entity.Property(b => b.CreatedAt).IsRequired();
            entity.Property(b => b.SettledAt);

            entity.HasMany(b => b.Items)
                .WithOne()
                .HasForeignKey(i => i.BetSlipId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(b => b.Items)
                .HasField("_items")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<BetSlipItem>(entity =>
        {
            entity.ToTable("bet_slip_items");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).ValueGeneratedNever();

            entity.Property(i => i.BetSlipId).IsRequired();
            entity.Property(i => i.SportEventId).IsRequired();
            entity.HasIndex(i => i.SportEventId);

            entity.Property(i => i.MarketId).IsRequired();
            entity.Property(i => i.SelectionId).IsRequired();

            entity.Property(i => i.EventName).HasMaxLength(256).IsRequired();
            entity.Property(i => i.MarketName).HasMaxLength(128).IsRequired();
            entity.Property(i => i.SelectionName).HasMaxLength(128).IsRequired();
            entity.Property(i => i.LockedOdds).HasPrecision(18, 4).IsRequired();
            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

            entity.Ignore(i => i.EffectiveOdds);
        });
    }

    private static void ConfigureWalletEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).ValueGeneratedNever();

            entity.Property(u => u.Username).HasMaxLength(64).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(128).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();

            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(u => u.IsAdultConfirmed).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();

            entity.HasOne(u => u.Wallet)
                .WithOne(w => w.User)
                .HasForeignKey<WalletEntity>(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WalletEntity>(entity =>
        {
            entity.ToTable("wallets");
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Id).ValueGeneratedNever();

            entity.Property(w => w.UserId).IsRequired();
            entity.HasIndex(w => w.UserId).IsUnique();

            entity.Property(w => w.AvailableBalance).HasPrecision(18, 2).IsRequired();
            entity.Property(w => w.FrozenBalance).HasPrecision(18, 2).IsRequired();
            entity.Property(w => w.BonusBalance).HasPrecision(18, 2).IsRequired();
            entity.Property(w => w.UpdatedAt).IsRequired();

            entity.Property(w => w.Version).IsConcurrencyToken();

            entity.HasOne(w => w.BettingLimits)
                .WithOne()
                .HasForeignKey<BettingLimitsEntity>(l => l.WalletId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(w => w.LedgerEntries)
                .WithOne()
                .HasForeignKey(le => le.WalletId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LedgerEntryEntity>(entity =>
        {
            entity.ToTable("ledger_entries");
            entity.HasKey(le => le.Id);
            entity.Property(le => le.Id).ValueGeneratedNever();

            entity.Property(le => le.WalletId).IsRequired();
            entity.HasIndex(le => le.WalletId);
            entity.Property(le => le.BetSlipId);

            entity.Property(le => le.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(le => le.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(le => le.AvailableBalanceAfter).HasPrecision(18, 2).IsRequired();
            entity.Property(le => le.FrozenBalanceAfter).HasPrecision(18, 2).IsRequired();
            entity.Property(le => le.BonusBalanceAfter).HasPrecision(18, 2).IsRequired();
            entity.Property(le => le.Description).HasMaxLength(256).IsRequired();
            entity.Property(le => le.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<BettingLimitsEntity>(entity =>
        {
            entity.ToTable("betting_limits");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Id).ValueGeneratedNever();

            entity.Property(l => l.WalletId).IsRequired();
            entity.HasIndex(l => l.WalletId).IsUnique();

            entity.Property(l => l.DailyDepositLimit).HasPrecision(18, 2);
            entity.Property(l => l.SingleDepositMaxLimit).HasPrecision(18, 2);
            entity.Property(l => l.MaxSingleStakeLimit).HasPrecision(18, 2);
            entity.Property(l => l.UpdatedAt).IsRequired();
        });
    }
}
