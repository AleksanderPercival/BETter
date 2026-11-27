namespace BETter.Api.Contracts;

public sealed record SportEventResponse(
    Guid Id,
    string ExternalId,
    string Sport,
    string League,
    string StartTime,
    DateTimeOffset StartTimeUtc,
    string HomeTeam,
    string AwayTeam,
    string Status,
    bool IsLive,
    int? HomeScore,
    int? AwayScore,
    IReadOnlyList<MarketResponse> Markets);

public sealed record MarketResponse(
    Guid Id,
    string Name,
    bool IsSuspended,
    IReadOnlyList<SelectionResponse> Selections);

public sealed record SelectionResponse(
    Guid Id,
    string Name,
    decimal Odds,
    bool IsSuspended);

public sealed record DepositRequest(
    decimal Amount,
    Guid? UserId = null);

public sealed record WalletBalanceResponse(
    Guid UserId,
    Guid WalletId,
    decimal AvailableBalance,
    decimal FrozenBalance,
    decimal BonusBalance);

public sealed record SlipSelectionInput(
    Guid SportEventId,
    Guid MarketId,
    Guid SelectionId,
    decimal? Odds = null,
    string? EventName = null,
    string? MarketName = null,
    string? SelectionName = null);

public sealed record CalculateSlipRequest(
    decimal? Stake,
    decimal? GrossStake,
    bool IsTaxFreePromo = false,
    IReadOnlyList<SlipSelectionInput>? Items = null)
{
    public decimal EffectiveGrossStake => Stake ?? GrossStake ?? 0m;
}

public sealed record CalculateSlipResponse(
    string SlipType,
    decimal GrossStake,
    decimal NetStake,
    decimal TaxMultiplier,
    decimal TotalOdds,
    decimal PotentialPayout,
    bool IsWithinLimits,
    string? ValidationError = null);

public sealed record PlaceSlipRequest(
    decimal? Stake,
    decimal? GrossStake,
    string? SlipType = null,
    bool IsTaxFreePromo = false,
    Guid? UserId = null,
    IReadOnlyList<SlipSelectionInput>? Items = null)
{
    public decimal EffectiveGrossStake => Stake ?? GrossStake ?? 0m;
}

public sealed record PlaceSlipResponse(
    Guid SlipId,
    string Status,
    string SlipType,
    decimal GrossStake,
    decimal NetStake,
    decimal TaxMultiplier,
    decimal TotalOdds,
    decimal PotentialPayout,
    DateTimeOffset CreatedAt,
    decimal AvailableBalance,
    decimal FrozenBalance,
    decimal BonusBalance);
