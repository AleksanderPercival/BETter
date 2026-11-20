using BETter.Domain.Enums;
using BETter.Domain.Exceptions;

namespace BETter.Domain.Entities;

public class Selection
{
    public const decimal MinOdds = 1.00m;

    public Guid Id { get; private set; }
    public Guid MarketId { get; private set; }
    public Guid SportEventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Odds { get; private set; }
    public SelectionStatus Status { get; private set; }

    private Selection() { }

    public Selection(Guid id, Guid marketId, Guid sportEventId, string name, decimal odds)
    {
        if (id == Guid.Empty)
            throw new DomainException("Selection Id cannot be empty.");
        if (marketId == Guid.Empty)
            throw new DomainException("MarketId cannot be empty.");
        if (sportEventId == Guid.Empty)
            throw new DomainException("SportEventId cannot be empty.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Selection name is required.");
        if (odds < MinOdds)
            throw new DomainException($"Odds cannot be less than {MinOdds}.");

        Id = id;
        MarketId = marketId;
        SportEventId = sportEventId;
        Name = name;
        Odds = odds;
        Status = SelectionStatus.Pending;
    }

    public void UpdateOdds(decimal newOdds)
    {
        if (Status != SelectionStatus.Pending)
            throw new DomainException("Cannot update odds for a settled selection.");
        if (newOdds < MinOdds)
            throw new DomainException($"Odds cannot be less than {MinOdds}.");

        Odds = newOdds;
    }

    public void Settle(SelectionStatus status)
    {
        if (status == SelectionStatus.Pending)
            throw new DomainException("Cannot settle a selection to Pending status.");

        Status = status;

        if (status == SelectionStatus.Void)
        {
            Odds = 1.00m;
        }
    }
}
