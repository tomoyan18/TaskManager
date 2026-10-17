using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Testing;

/// <summary>
/// テストデータの作成（docs/design/testing-strategy.md §3）。テスト対象のサービスを経由せず、
/// Domain の生成メソッドで DB に直接作る。値はすべて架空（example.test）。
/// </summary>
public sealed class TestDataBuilder(IServiceProvider services)
{
    /// <summary>password を指定しない利用者の共通パスワード（ハッシュ計算を 1 回にして作成を速くする。架空の値）。</summary>
    public const string DefaultPassword = "Tm-Test-Default-2026";

    private static readonly Lazy<string> DefaultPasswordHash = new(() => new Infrastructure.Security.PasswordHasherAdapter().Hash(DefaultPassword));

    public async Task<TestUser> CreateUserAsync(
        string? displayName = null, bool administrator = false, bool mustChangePassword = false, bool active = true,
        string? email = null, string? password = null)
    {
        var plain = password ?? DefaultPassword;
        var name = displayName ?? Unique.Name("テスト利用者");
        var address = EmailAddress.Create(email ?? Unique.Email());
        return await WithDbAsync(async (db, sp) =>
        {
            var hash = password is null ? DefaultPasswordHash.Value : sp.GetRequiredService<IPasswordHasher>().Hash(plain);
            var user = UserAccount.Create(address, name, administrator ? SystemRole.Administrator : SystemRole.Member, hash);
            if (!mustChangePassword)
            {
                user.ChangePassword(hash);
            }

            if (!active)
            {
                user.Deactivate();
            }

            db.UserAccounts.Add(user);
            await db.SaveChangesAsync();
            return new TestUser(user.Id, user.Email, user.DisplayName, plain, administrator);
        });
    }

    /// <summary>プロジェクトを作成し、owner を管理者として所属させる（既定のステータス 5 件つき）。</summary>
    public async Task<TestProject> CreateProjectAsync(TestUser owner, string? name = null, string? key = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return await WithDbAsync(async (db, sp) =>
        {
            var now = sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var project = Project.Create(ProjectKey.Create(key ?? Unique.ProjectKey()), name ?? Unique.Name("テストプロジェクト"), null);
            project.CreatedByUserId = owner.Id;
            db.Projects.Add(project);
            db.WorkflowStatuses.AddRange(project.CreateDefaultStatuses());
            db.ProjectMemberships.Add(ProjectMembership.Create(project.Id, owner.Id, ProjectRole.Owner, owner.Id, now));
            await db.SaveChangesAsync();
            return new TestProject(project.Id, project.Key, project.Name);
        });
    }

    public Task AddMemberAsync(TestProject project, TestUser user, ProjectRole role) =>
        WithDbAsync(async (db, sp) =>
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(user);
            var now = sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            db.ProjectMemberships.Add(ProjectMembership.Create(project.Id, user.Id, role, user.Id, now));
            await db.SaveChangesAsync();
            return true;
        });

    public Task ArchiveProjectAsync(TestProject project) =>
        WithDbAsync(async (db, sp) =>
        {
            ArgumentNullException.ThrowIfNull(project);
            var entity = await db.Projects.SingleAsync(p => p.Id == project.Id);
            entity.Archive(sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
            return true;
        });

    public Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(TestProject project) =>
        WithDbAsync<IReadOnlyList<WorkflowStatus>>(async (db, _) =>
            await db.WorkflowStatuses.AsNoTracking().Where(s => s.ProjectId == project.Id).OrderBy(s => s.SortOrder).ToListAsync());

    public async Task<WorkflowStatus> GetStatusAsync(TestProject project, StatusCategory category) =>
        (await GetStatusesAsync(project)).First(s => s.Category == category);

    public Task<Tag> CreateTagAsync(TestProject project, string? name = null) =>
        WithDbAsync(async (db, _) =>
        {
            var tag = Tag.Create(project.Id, name ?? Unique.Name("タグ"), StatusColor.Blue);
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            return tag;
        });

    public Task<Milestone> CreateMilestoneAsync(TestProject project, string? name = null, bool closed = false) =>
        WithDbAsync(async (db, _) =>
        {
            var milestone = Milestone.Create(project.Id, name ?? Unique.Name("マイルストーン"), null, null);
            if (closed)
            {
                milestone.ChangeState(MilestoneState.Closed);
            }

            db.Milestones.Add(milestone);
            await db.SaveChangesAsync();
            return milestone;
        });

    /// <summary>タスクを作成する（番号は本番と同じ採番処理で割り当てる）。</summary>
    public Task<TaskItem> CreateTaskAsync(
        TestProject project, TestUser creator, string? title = null, Guid? assigneeUserId = null, DateOnly? dueDate = null,
        StatusCategory category = StatusCategory.NotStarted, TaskItem? parent = null, DateOnly? startDate = null) =>
        WithDbAsync(async (db, sp) =>
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(creator);
            var allocator = sp.GetRequiredService<ITaskNumberAllocator>();
            var now = sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var status = await db.WorkflowStatuses.FirstAsync(s => s.ProjectId == project.Id && s.Category == category);
            return await db.InTransactionAsync(async ct =>
            {
                var number = await allocator.AllocateAsync(project.Id, ct)
                    ?? throw new InvalidOperationException("アーカイブ中のプロジェクトにはタスクを作れません。");
                var task = TaskItem.Create(project.Id, number, title ?? Unique.Name("テストタスク"), status, TaskPriority.Normal, now);
                task.ChangeDates(startDate, dueDate);
                task.AssignTo(assigneeUserId);
                if (parent is not null)
                {
                    task.ChangeParent(parent, hasSubtasks: false);
                }

                task.CreatedByUserId = creator.Id;
                task.UpdatedByUserId = creator.Id;
                db.TaskItems.Add(task);
                db.TaskHistoryEntries.Add(TaskHistoryEntry.Create(task.Id, TaskHistoryKind.Created, creator.Id, null, task.Title, now));
                await db.SaveChangesAsync(ct);
                return task;
            }, CancellationToken.None);
        });

    public Task<TaskComment> CreateCommentAsync(TaskItem task, TestUser author, string body = "テストのコメント") =>
        WithDbAsync(async (db, sp) =>
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(author);
            var comment = TaskComment.Create(task.Id, author.Id, body, sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            db.TaskComments.Add(comment);
            await db.SaveChangesAsync();
            return comment;
        });

    /// <summary>テキストの添付ファイルを保存先とメタ情報の両方に作る。</summary>
    public Task<TaskAttachment> CreateAttachmentAsync(TaskItem task, TestUser uploader, string fileName = "memo.txt", string content = "メモ") =>
        WithDbAsync(async (db, sp) =>
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(uploader);
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var id = Guid.CreateVersion7();
            using var stream = new MemoryStream(bytes);
            var key = await sp.GetRequiredService<IAttachmentStorage>().SaveAsync(id, stream, CancellationToken.None);
            var attachment = TaskAttachment.Create(id, task.Id, fileName, "text/plain", bytes.Length, key,
                System.Security.Cryptography.SHA256.HashData(bytes), uploader.Id, sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            db.TaskAttachments.Add(attachment);
            await db.SaveChangesAsync();
            return attachment;
        });

    public Task<WorkflowStatus> CreateStatusAsync(TestProject project, string name, StatusCategory category = StatusCategory.InProgress) =>
        WithDbAsync(async (db, _) =>
        {
            ArgumentNullException.ThrowIfNull(project);
            var count = await db.WorkflowStatuses.CountAsync(s => s.ProjectId == project.Id);
            var status = WorkflowStatus.Create(project.Id, name, category, StatusColor.Orange, count + 1, isDefault: false);
            db.WorkflowStatuses.Add(status);
            await db.SaveChangesAsync();
            return status;
        });

    /// <summary>DB を直接参照する（検証用）。</summary>
    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await WithDbAsync((db, _) => query(db));
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, IServiceProvider, Task<T>> action)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db, scope.ServiceProvider);
    }
}
