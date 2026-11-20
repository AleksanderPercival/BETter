using BETter.Domain.Entities;

namespace BETter.Domain.Services;

public class AntiCorrelationValidator : IAntiCorrelationValidator
{
    public ValidationResult Validate(BetSlip betSlip)
    {
        ArgumentNullException.ThrowIfNull(betSlip);
        return Validate(betSlip.Items);
    }

    public ValidationResult Validate(IEnumerable<BetSlipItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.ToList();
        if (itemList.Count <= 1)
        {
            return ValidationResult.Success();
        }

        var duplicateEventIds = itemList
            .GroupBy(i => i.SportEventId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateEventIds.Count > 0)
        {
            return ValidationResult.Failure(
                "Correlated selections detected: an AKO bet slip cannot contain multiple selections from the same SportEvent.",
                duplicateEventIds.AsReadOnly());
        }

        return ValidationResult.Success();
    }

    public bool CanAddSelection(BetSlip betSlip, Guid sportEventId)
    {
        ArgumentNullException.ThrowIfNull(betSlip);
        return betSlip.Items.All(i => i.SportEventId != sportEventId);
    }
}
