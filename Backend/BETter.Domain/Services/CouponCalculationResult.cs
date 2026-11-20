namespace BETter.Domain.Services;

public sealed record CouponCalculationResult(
    decimal GrossStake,
    decimal NetStake,
    decimal TaxMultiplier,
    decimal TotalOdds,
    decimal PotentialPayout,
    bool IsWithinLimits,
    string? ValidationError = null);
