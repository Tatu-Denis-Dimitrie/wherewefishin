using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Database.Context;

namespace WhereWeFishin.Database.Repositories;

public class ReviewRepository : Repository<Review>
{
    public ReviewRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<Review?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.User)
            .Include(r => r.FishingSpot)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetByFishingSpotIdAsync(int fishingSpotId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.User)
            .Where(r => r.FishingSpotId == fishingSpotId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Review?> GetByUserAndSpotAsync(int userId, int fishingSpotId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.FishingSpotId == fishingSpotId && !r.IsDeleted, cancellationToken);
    }

    public async Task<double?> GetAverageRatingAsync(int fishingSpotId, CancellationToken cancellationToken = default)
    {
        var reviews = await _dbSet
            .Where(r => r.FishingSpotId == fishingSpotId && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        return reviews.Count > 0 ? reviews.Average(r => r.Rating) : null;
    }
}
