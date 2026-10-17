using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;
using TaskManager.Domain.Notices;

namespace TaskManager.Application.Notices;

/// <summary>
/// お知らせの作成（REQ-NTC-01）。業務データと同じトランザクションで追加する（保存は呼び出し側）。
/// N-01: 操作者本人には作らない。N-02: 5 分以内の未読の同種お知らせはまとめる。
/// </summary>
public sealed class NoticeWriter(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task NotifyAsync(
        Guid recipientUserId, NoticeKind kind, Guid? actorUserId, Guid? projectId, Guid? taskId,
        string message, string? linkPath, CancellationToken cancellationToken)
    {
        if (actorUserId == recipientUserId)
        {
            return;
        }

        var now = timeProvider.UtcNow();
        if (taskId is not null)
        {
            var since = now - Notice.MergeWindow;
            var existing = db.Notices.Local.FirstOrDefault(n =>
                    n.RecipientUserId == recipientUserId && n.TaskId == taskId && n.Kind == kind && !n.IsRead && n.CreatedAtUtc >= since)
                ?? await db.Notices.FirstOrDefaultAsync(n =>
                    n.RecipientUserId == recipientUserId && n.TaskId == taskId && n.Kind == kind
                    && n.ReadAtUtc == null && n.CreatedAtUtc >= since, cancellationToken);
            if (existing is not null)
            {
                existing.Refresh(actorUserId, message, now);
                return;
            }
        }

        db.Notices.Add(Notice.Create(recipientUserId, kind, actorUserId, projectId, taskId, message, linkPath, now));
    }

    public static string TaskLink(string projectKey, int number) =>
        $"/projects/{projectKey}/tasks/{number.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static string TaskLabel(string projectKey, int number, string title) =>
        $"{TaskKey.Format(projectKey, number)}「{title}」";
}
