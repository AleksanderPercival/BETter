using BETter.Domain.Entities;
using BETter.Domain.Enums;

namespace BETter.Domain.Ports;

public interface ISportEventRepository
{
    Task<SportEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SportEvent?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SportEvent>> GetActiveEventsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SportEvent>> GetByStatusAsync(EventStatus status, CancellationToken cancellationToken = default);
    Task<Selection?> GetSelectionByIdAsync(Guid selectionId, CancellationToken cancellationToken = default);
    Task SaveAsync(SportEvent sportEvent, CancellationToken cancellationToken = default);
}
