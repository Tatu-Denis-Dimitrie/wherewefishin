using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Database.Context;

namespace WhereWeFishin.Database.Repositories;

public class PontoonRepository : Repository<Pontoon>
{
    public PontoonRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<Pontoon?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.FishingSpot)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<Pontoon>> GetByFishingSpotIdAsync(int fishingSpotId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(p => p.FishingSpotId == fishingSpotId && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
