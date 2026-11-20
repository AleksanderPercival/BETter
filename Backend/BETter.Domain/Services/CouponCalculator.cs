using BETter.Domain.Entities;
using BETter.Domain.Exceptions;

namespace BETter.Domain.Services;

public class CouponCalculator : ICouponCalculator
{
    public const decimal StandardTaxMultiplier = 0.88m;
    public const decimal PromoTaxFreeMultiplier = 1.00m;
    public const decimal DefaultMinStake = 2.00m;
    public const decimal DefaultMaxPayout = 500_000.00m;

    public decimal MinStake { get; }
    public decimal MaxPayout { get; }

    public CouponCalculator(
        decimal minStake = DefaultMinStake,
        decimal maxPayout = DefaultMaxPayout)
    {
        if (minStake <= 0m)
            throw new DomainException("Minimum stake must be greater than zero.");
        if (maxPayout <= 0m)
            throw new DomainException("Maximum payout must be greater than zero.");

        MinStake = minStake;
        MaxPayout = maxPayout;
    }

    public decimal CalculateNetStake(decimal grossStake, bool isTaxFreePromo = false)
    {
        if (grossStake < 0m)
            throw new DomainException("Gross stake cannot be negative.");

        var multiplier = isTaxFreePromo ? PromoTaxFreeMultiplier : StandardTaxMultiplier;
        return Math.Round(grossStake * multiplier, 2, MidpointRounding.AwayFromZero);
    }

    public decimal CalculateTotalOdds(IEnumerable<BetSlipItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.ToList();
        if (itemList.Count == 0)
            return 0.00m;

        decimal totalOdds = 1.00m;
        foreach (var item in itemList)
        {
            totalOdds *= item.EffectiveOdds;
        }

        return Math.Round(totalOdds, 4, MidpointRounding.AwayFromZero);
    }

    public decimal CalculatePotentialPayout(decimal netStake, decimal totalOdds)
    {
        if (netStake < 0m)
            throw new DomainException("Net stake cannot be negative.");
        if (totalOdds < 0m)
            throw new DomainException("Total odds cannot be negative.");

        return Math.Round(netStake * totalOdds, 2, MidpointRounding.AwayFromZero);
    }

    public CouponCalculationResult Calculate(
        BetSlip betSlip,
        decimal grossStake,
        bool isTaxFreePromo = false)
    {
        ArgumentNullException.ThrowIfNull(betSlip);

        var multiplier = isTaxFreePromo ? PromoTaxFreeMultiplier : StandardTaxMultiplier;
        var netStake = CalculateNetStake(grossStake, isTaxFreePromo);
        var totalOdds = CalculateTotalOdds(betSlip.Items);
        var potentialPayout = CalculatePotentialPayout(netStake, totalOdds);

        if (betSlip.Items.Count < BetSlip.MinSelections)
        {
            return new CouponCalculationResult(
                grossStake, netStake, multiplier, totalOdds, potentialPayout,
                IsWithinLimits: false,
                ValidationError: $"A bet slip must contain at least {BetSlip.MinSelections} selection.");
        }

        if (betSlip.Items.Count > BetSlip.MaxSelections)
        {
            return new CouponCalculationResult(
                grossStake, netStake, multiplier, totalOdds, potentialPayout,
                IsWithinLimits: false,
                ValidationError: $"A bet slip cannot contain more than {BetSlip.MaxSelections} selections.");
        }

        if (grossStake < MinStake)
        {
            return new CouponCalculationResult(
                grossStake, netStake, multiplier, totalOdds, potentialPayout,
                IsWithinLimits: false,
                ValidationError: $"Gross stake ({grossStake:F2}) is below the minimum stake ({MinStake:F2}).");
        }

        if (potentialPayout > MaxPayout)
        {
            return new CouponCalculationResult(
                grossStake, netStake, multiplier, totalOdds, potentialPayout,
                IsWithinLimits: false,
                ValidationError: $"Potential payout ({potentialPayout:F2}) exceeds the maximum allowed payout ({MaxPayout:F2}).");
        }

        return new CouponCalculationResult(
            grossStake, netStake, multiplier, totalOdds, potentialPayout,
            IsWithinLimits: true);
    }
}
