using BETter.Domain.Enums;
using BETter.Domain.Exceptions;

namespace BETter.Domain.Entities;

public class BetSlipItem
{
    public const decimal VoidOdds = 1.00m;

    public Guid Id { get; private set; }
    public Guid BetSlipId { get; private set; }
    public Guid SportEventId { get; private set; }
    public Guid MarketId { get; private set; }
    public Guid SelectionId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string MarketName { get; private set; } = string.Empty;
    public string SelectionName { get; private set; } = string.Empty;
    public decimal LockedOdds { get; private set; }
    public SelectionStatus Status { get; private set; }

    public decimal EffectiveOdds => Status == SelectionStatus.Void ? VoidOdds : LockedOdds;

    private BetSlipItem() { }

    public BetSlipItem(
        Guid id,
        Guid betSlipId,
        Guid sportEventId,
        Guid marketId,
        Guid selectionId,
        string eventName,
        string marketName,
        string selectionName,
        decimal lockedOdds)
    {
        if (id == Guid.Empty)
            throw new DomainException("BetSlipItem Id cannot be empty.");
        if (betSlipId == Guid.Empty)
            throw new DomainException("BetSlipId cannot be empty.");
        if (sportEventId == Guid.Empty)
            throw new DomainException("SportEventId cannot be empty.");
        if (marketId == Guid.Empty)
            throw new DomainException("MarketId cannot be empty.");
        if (selectionId == Guid.Empty)
            throw new DomainException("SelectionId cannot be empty.");
        if (lockedOdds < Selection.MinOdds)
            throw new DomainException($"LockedOdds cannot be less than {Selection.MinOdds}.");

        Id = id;
        BetSlipId = betSlipId;
        SportEventId = sportEventId;
        MarketId = marketId;
        SelectionId = selectionId;
        EventName = eventName;
        MarketName = marketName;
        SelectionName = selectionName;
        LockedOdds = lockedOdds;
        Status = SelectionStatus.Pending;
    }

    public void Settle(SelectionStatus status)
    {
        if (status == SelectionStatus.Pending)
            throw new DomainException("Cannot settle a BetSlipItem to Pending status.");

        Status = status;
    }
}
