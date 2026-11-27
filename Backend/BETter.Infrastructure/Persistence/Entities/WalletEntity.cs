namespace BETter.Infrastructure.Persistence.Entities;

public class WalletEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal FrozenBalance { get; set; }
    public decimal BonusBalance { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Token kontroli współbieżności (ochrona przed Double-Spending - WNF-02).
    /// </summary>
    public long Version { get; set; }

    public UserEntity? User { get; set; }
    public BettingLimitsEntity? BettingLimits { get; set; }
    public List<LedgerEntryEntity> LedgerEntries { get; set; } = [];
}
