using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstagramStoryArchiver.Infrastructure.Persistence.Repositories;

public sealed class MonitoredUserRepository : IMonitoredUserRepository
{
    private readonly AppDbContext _dbContext;

    public MonitoredUserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MonitoredInstagramUser>> GetDueUsersAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // SQLite EF provider cannot reliably translate nullable DateTimeOffset comparisons.
        // Active set is small (max ~15), so filter due users in memory.
        var activeUsers = await _dbContext.MonitoredUsers
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        return activeUsers
            .Where(x => x.NextCheckAt is null || x.NextCheckAt <= now)
            .OrderBy(x => x.NextCheckAt)
            .ThenBy(x => x.Username)
            .ToList();
    }

    public async Task<IReadOnlyList<MonitoredInstagramUser>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.MonitoredUsers
            .OrderBy(x => x.Username)
            .ToListAsync(cancellationToken);
    }

    public Task<MonitoredInstagramUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return _dbContext.MonitoredUsers
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    public async Task<MonitoredInstagramUser> AddAsync(MonitoredInstagramUser user, CancellationToken cancellationToken)
    {
        await _dbContext.MonitoredUsers.AddAsync(user, cancellationToken);
        return user;
    }

    public Task UpdateAsync(MonitoredInstagramUser user, CancellationToken cancellationToken)
    {
        _dbContext.MonitoredUsers.Update(user);
        return Task.CompletedTask;
    }

    public Task<int> GetActiveCountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.MonitoredUsers.CountAsync(x => x.IsActive, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
