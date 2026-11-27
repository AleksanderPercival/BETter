namespace BETter.Infrastructure.Persistence.Entities;

public class BettingLimitsEntity
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public decimal? DailyDepositLimit { get; set; }
    public decimal? SingleDepositMaxLimit { get; set; }
    public decimal? MaxSingleStakeLimit { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
