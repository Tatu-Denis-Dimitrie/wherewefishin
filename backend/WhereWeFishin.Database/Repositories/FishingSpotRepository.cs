using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Database.Context;

namespace WhereWeFishin.Database.Repositories;

public class FishingSpotRepository : Repository<FishingSpot>
{
    public FishingSpotRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<FishingSpot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.Manager)
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);
    }

    public override async Task<IEnumerable<FishingSpot>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(f => f.Manager)
            .Where(f => !f.IsDeleted)
            .ToListAsync(cancellationToken);
    }
}
