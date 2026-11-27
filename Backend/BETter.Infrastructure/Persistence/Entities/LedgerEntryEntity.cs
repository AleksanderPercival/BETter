namespace BETter.Infrastructure.Persistence.Entities;

/// <summary>
/// Niemodyfikowalny wpis w Rejestrze operacji (Ledger) - PU-05 / WF-04.
/// </summary>
public class LedgerEntryEntity
{
    public Guid Id { get; init; }
    public Guid WalletId { get; init; }
    public Guid? BetSlipId { get; init; }
    public WalletTransactionType Type { get; init; }
    public decimal Amount { get; init; }
    public decimal AvailableBalanceAfter { get; init; }
    public decimal FrozenBalanceAfter { get; init; }
    public decimal BonusBalanceAfter { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
