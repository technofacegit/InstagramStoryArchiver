using InstagramStoryArchiver.Domain.Entities;

namespace InstagramStoryArchiver.Application.Abstractions;

public interface IMonitoredUserRepository
{
    Task<IReadOnlyList<MonitoredInstagramUser>> GetDueUsersAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<MonitoredInstagramUser>> GetAllAsync(CancellationToken cancellationToken);
    Task<MonitoredInstagramUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<MonitoredInstagramUser> AddAsync(MonitoredInstagramUser user, CancellationToken cancellationToken);
    Task UpdateAsync(MonitoredInstagramUser user, CancellationToken cancellationToken);
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
