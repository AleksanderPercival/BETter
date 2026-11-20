using BETter.Domain.Entities;

namespace BETter.Domain.Services;

public interface IAntiCorrelationValidator
{
    ValidationResult Validate(BetSlip betSlip);
    ValidationResult Validate(IEnumerable<BetSlipItem> items);
    bool CanAddSelection(BetSlip betSlip, Guid sportEventId);
}
