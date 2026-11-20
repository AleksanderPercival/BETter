using BETter.Domain.Exceptions;

namespace BETter.Domain.Entities;

public class Market
{
    private readonly List<Selection> _selections = [];

    public Guid Id { get; private set; }
    public Guid SportEventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsSuspended { get; private set; }

    public IReadOnlyCollection<Selection> Selections => _selections.AsReadOnly();

    private Market() { }

    public Market(Guid id, Guid sportEventId, string name, bool isSuspended = false)
    {
        if (id == Guid.Empty)
            throw new DomainException("Market Id cannot be empty.");
        if (sportEventId == Guid.Empty)
            throw new DomainException("SportEventId cannot be empty.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Market name is required.");

        Id = id;
        SportEventId = sportEventId;
        Name = name;
        IsSuspended = isSuspended;
    }

    public void AddSelection(Selection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.MarketId != Id || selection.SportEventId != SportEventId)
            throw new DomainException("Selection does not belong to this Market or SportEvent.");

        _selections.Add(selection);
    }

    public void Suspend() => IsSuspended = true;

    public void Resume() => IsSuspended = false;
}
