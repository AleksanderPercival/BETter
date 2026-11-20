using BETter.Domain.Entities;

namespace BETter.Domain.Services;

public interface ICouponCalculator
{
    decimal CalculateNetStake(decimal grossStake, bool isTaxFreePromo = false);
    decimal CalculateTotalOdds(IEnumerable<BetSlipItem> items);
    decimal CalculatePotentialPayout(decimal netStake, decimal totalOdds);
    CouponCalculationResult Calculate(BetSlip betSlip, decimal grossStake, bool isTaxFreePromo = false);
}
