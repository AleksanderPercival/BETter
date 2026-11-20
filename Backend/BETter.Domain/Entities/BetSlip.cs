using BETter.Domain.Enums;
using BETter.Domain.Exceptions;
using BETter.Domain.Services;

namespace BETter.Domain.Entities;

public class BetSlip
{
    public const int MinSelections = 1;
    public const int MaxSelections = 30;

    private readonly List<BetSlipItem> _items = [];

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public SlipType Type { get; private set; }
    public SlipStatus Status { get; private set; }
    public decimal GrossStake { get; private set; }
    public decimal NetStake { get; private set; }
    public decimal TaxMultiplier { get; private set; }
    public decimal TotalOdds { get; private set; }
    public decimal PotentialPayout { get; private set; }
    public decimal? SettledPayout { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }

    public IReadOnlyCollection<BetSlipItem> Items => _items.AsReadOnly();

    private BetSlip() { }

    public BetSlip(Guid id, Guid userId, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new DomainException("BetSlip Id cannot be empty.");
        if (userId == Guid.Empty)
            throw new DomainException("UserId cannot be empty.");

        Id = id;
        UserId = userId;
        Type = SlipType.Solo;
        Status = SlipStatus.Open;
        TaxMultiplier = CouponCalculator.StandardTaxMultiplier;
        CreatedAt = createdAt;
    }

    public void AddItem(BetSlipItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status != SlipStatus.Open)
            throw new DomainException("Cannot add selections to a settled bet slip.");
        if (item.BetSlipId != Id)
            throw new DomainException("BetSlipItem does not belong to this BetSlip.");
        if (_items.Count >= MaxSelections)
            throw new DomainException($"A bet slip cannot contain more than {MaxSelections} selections.");

        _items.Add(item);
        UpdateSlipType();
    }

    public void RemoveItem(Guid selectionId)
    {
        if (Status != SlipStatus.Open)
            throw new DomainException("Cannot remove selections from a settled bet slip.");

        var item = _items.FirstOrDefault(i => i.SelectionId == selectionId);
        if (item is null)
            return;

        _items.Remove(item);
        UpdateSlipType();
    }

    public void ApplyCalculation(CouponCalculationResult calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);

        if (Status != SlipStatus.Open)
            throw new DomainException("Cannot recalculate a settled bet slip.");
        if (!calculation.IsWithinLimits)
            throw new DomainException(calculation.ValidationError ?? "Coupon calculation violates betting limits.");

        GrossStake = calculation.GrossStake;
        NetStake = calculation.NetStake;
        TaxMultiplier = calculation.TaxMultiplier;
        TotalOdds = calculation.TotalOdds;
        PotentialPayout = calculation.PotentialPayout;
    }

    public void MarkAsWon(decimal finalTotalOdds, decimal finalPayout, DateTimeOffset settledAt)
    {
        EnsureOpen();
        if (finalTotalOdds < 1.00m)
            throw new DomainException("Final total odds cannot be less than 1.00.");
        if (finalPayout < 0m)
            throw new DomainException("Final payout cannot be negative.");

        TotalOdds = finalTotalOdds;
        SettledPayout = finalPayout;
        Status = SlipStatus.Won;
        SettledAt = settledAt;
    }

    public void MarkAsLost(DateTimeOffset settledAt)
    {
        EnsureOpen();

        SettledPayout = 0.00m;
        Status = SlipStatus.Lost;
        SettledAt = settledAt;
    }

    public void MarkAsVoid(DateTimeOffset settledAt)
    {
        EnsureOpen();

        TotalOdds = 1.00m;
        SettledPayout = GrossStake;
        Status = SlipStatus.Void;
        SettledAt = settledAt;
    }

    public void MarkAsCashedOut(decimal cashOutAmount, DateTimeOffset settledAt)
    {
        EnsureOpen();
        if (cashOutAmount <= 0m)
            throw new DomainException("Cash-out amount must be greater than zero.");

        SettledPayout = cashOutAmount;
        Status = SlipStatus.CashedOut;
        SettledAt = settledAt;
    }

    private void UpdateSlipType()
    {
        Type = _items.Count > 1 ? SlipType.Ako : SlipType.Solo;
    }

    private void EnsureOpen()
    {
        if (Status != SlipStatus.Open)
            throw new DomainException($"BetSlip is already settled with status {Status}.");
    }
}
