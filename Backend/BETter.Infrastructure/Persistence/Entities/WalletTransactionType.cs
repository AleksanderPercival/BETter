namespace BETter.Infrastructure.Persistence.Entities;

/// <summary>
/// Typy operacji w Ledgerze zgodne z Żelaznym Kontraktem Nazewniczym (TransactionType).
/// Zdefiniowane wewnątrz BETter.Infrastructure do czasu zmergowania modułu BETter.Wallet.
/// </summary>
public enum WalletTransactionType
{
    Deposit = 1,
    Withdrawal = 2,
    Hold = 3,
    Payout = 4,
    RefundVoid = 5,
    Cashout = 6,
    SettleLoss = 7
}
