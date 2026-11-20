using BETter.Domain.Entities;

namespace BETter.Domain.Ports;

public interface IBetSlipRepository
{
    Task<BetSlip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BetSlip>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BetSlip>> GetOpenSlipsBySportEventIdAsync(Guid sportEventId, CancellationToken cancellationToken = default);
    Task AddAsync(BetSlip betSlip, CancellationToken cancellationToken = default);
    Task UpdateAsync(BetSlip betSlip, CancellationToken cancellationToken = default);
}
