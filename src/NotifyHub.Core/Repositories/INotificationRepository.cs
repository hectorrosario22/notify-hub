using NotifyHub.Core.Entities;

namespace NotifyHub.Core.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly = false,
        CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Notification notification, CancellationToken ct = default);

    Task UpdateAsync(Notification notification, CancellationToken ct = default);

    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
