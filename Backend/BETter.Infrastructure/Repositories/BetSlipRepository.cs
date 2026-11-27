using BETter.Domain.Entities;
using BETter.Domain.Enums;
using BETter.Domain.Ports;
using BETter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BETter.Infrastructure.Repositories;

public class BetSlipRepository : IBetSlipRepository
{
    private readonly BetterDbContext _dbContext;

    public BetSlipRepository(BetterDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BetSlip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BetSlips
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BetSlip>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var slips = await _dbContext.BetSlips
            .Include(b => b.Items)
            .Where(b => b.UserId == userId)
            .ToListAsync(cancellationToken);

        return slips
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<BetSlip>> GetOpenSlipsBySportEventIdAsync(Guid sportEventId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BetSlips
            .Include(b => b.Items)
            .Where(b => b.Status == SlipStatus.Open && b.Items.Any(i => i.SportEventId == sportEventId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BetSlip betSlip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(betSlip);

        await _dbContext.BetSlips.AddAsync(betSlip, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BetSlip betSlip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(betSlip);

        if (_dbContext.Entry(betSlip).State == EntityState.Detached)
        {
            _dbContext.BetSlips.Update(betSlip);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
