using BETter.Domain.Entities;
using BETter.Domain.Enums;
using BETter.Domain.Ports;
using BETter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BETter.Infrastructure.Repositories;

public class SportEventRepository : ISportEventRepository
{
    private readonly BetterDbContext _dbContext;

    public SportEventRepository(BetterDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SportEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SportEvents
            .Include(e => e.Markets)
                .ThenInclude(m => m.Selections)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<SportEvent?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SportEvents
            .Include(e => e.Markets)
                .ThenInclude(m => m.Selections)
            .FirstOrDefaultAsync(e => e.ExternalId == externalId, cancellationToken);
    }

    public async Task<IReadOnlyList<SportEvent>> GetActiveEventsAsync(CancellationToken cancellationToken = default)
    {
        var events = await _dbContext.SportEvents
            .Include(e => e.Markets)
                .ThenInclude(m => m.Selections)
            .Where(e => e.Status == EventStatus.Prematch || e.Status == EventStatus.Live)
            .ToListAsync(cancellationToken);

        return events
            .OrderByDescending(e => e.Status == EventStatus.Live)
            .ThenBy(e => e.StartTime)
            .ToList();
    }

    public async Task<IReadOnlyList<SportEvent>> GetByStatusAsync(EventStatus status, CancellationToken cancellationToken = default)
    {
        var events = await _dbContext.SportEvents
            .Include(e => e.Markets)
                .ThenInclude(m => m.Selections)
            .Where(e => e.Status == status)
            .ToListAsync(cancellationToken);

        return events
            .OrderBy(e => e.StartTime)
            .ToList();
    }

    public async Task<Selection?> GetSelectionByIdAsync(Guid selectionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Selections
            .FirstOrDefaultAsync(s => s.Id == selectionId, cancellationToken);
    }

    public async Task SaveAsync(SportEvent sportEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sportEvent);

        var existsInDb = await _dbContext.SportEvents
            .AsNoTracking()
            .AnyAsync(e => e.Id == sportEvent.Id, cancellationToken);

        if (!existsInDb)
        {
            await _dbContext.SportEvents.AddAsync(sportEvent, cancellationToken);
        }
        else
        {
            if (_dbContext.Entry(sportEvent).State == EntityState.Detached)
            {
                _dbContext.SportEvents.Attach(sportEvent);
                _dbContext.Entry(sportEvent).State = EntityState.Modified;
            }

            var existingMarketIds = await _dbContext.Markets
                .AsNoTracking()
                .Where(m => m.SportEventId == sportEvent.Id)
                .Select(m => m.Id)
                .ToHashSetAsync(cancellationToken);

            var existingSelectionIds = await _dbContext.Selections
                .AsNoTracking()
                .Where(s => s.SportEventId == sportEvent.Id)
                .Select(s => s.Id)
                .ToHashSetAsync(cancellationToken);

            foreach (var market in sportEvent.Markets)
            {
                _dbContext.Entry(market).State = existingMarketIds.Contains(market.Id)
                    ? EntityState.Modified
                    : EntityState.Added;

                foreach (var selection in market.Selections)
                {
                    _dbContext.Entry(selection).State = existingSelectionIds.Contains(selection.Id)
                        ? EntityState.Modified
                        : EntityState.Added;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
