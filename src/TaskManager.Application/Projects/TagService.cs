using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;

namespace TaskManager.Application.Projects;

/// <summary>タグ（REQ-TAG-01）。</summary>
public sealed class TagService(IAppDbContext db, ProjectAccessGuard guard)
{
    public async Task<IReadOnlyList<TagResponse>> ListAsync(string projectKey, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var tags = await db.Tags.AsNoTracking().Where(t => t.ProjectId == access.ProjectId).OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
        var ids = tags.Select(t => t.Id).ToList();
        var counts = await db.TaskItemTags.AsNoTracking().Where(x => ids.Contains(x.TagId))
            .GroupBy(x => x.TagId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return tags.Select(t => new TagResponse(t.Id, t.Name, t.Color, counts.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TagResponse> CreateAsync(string projectKey, CreateTagRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageTags, cancellationToken);
        var tags = await db.Tags.Where(t => t.ProjectId == access.ProjectId).ToListAsync(cancellationToken);
        if (tags.Count >= Tag.MaxPerProject)
        {
            throw Errors.Conflict(ErrorCodes.TagLimitReached, "タグは 50 個までです。");
        }

        Tag tag;
        try
        {
            tag = Tag.Create(access.ProjectId, request.Name, request.Color!.Value);
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        EnsureUniqueName(tags, tag.Name, null);
        db.Tags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);
        return new TagResponse(tag.Id, tag.Name, tag.Color, 0);
    }

    public async Task<TagResponse> UpdateAsync(string projectKey, Guid tagId, UpdateTagRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageTags, cancellationToken);
        var tags = await db.Tags.Where(t => t.ProjectId == access.ProjectId).ToListAsync(cancellationToken);
        var tag = tags.SingleOrDefault(t => t.Id == tagId) ?? throw Errors.NotFound();
        try
        {
            if (request.IsSpecified(nameof(request.Name)))
            {
                var name = Tag.NormalizeName(request.Name);
                EnsureUniqueName(tags, name, tag.Id);
                tag.Rename(name);
            }

            if (request.IsSpecified(nameof(request.Color)) && request.Color is { } color)
            {
                tag.ChangeColor(color);
            }
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        await db.SaveChangesAsync(cancellationToken);
        var count = await db.TaskItemTags.CountAsync(x => x.TagId == tag.Id, cancellationToken);
        return new TagResponse(tag.Id, tag.Name, tag.Color, count);
    }

    public async Task DeleteAsync(string projectKey, Guid tagId, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageTags, cancellationToken);
        var tag = await db.Tags.SingleOrDefaultAsync(t => t.ProjectId == access.ProjectId && t.Id == tagId, cancellationToken)
            ?? throw Errors.NotFound();
        await db.InTransactionAsync(async ct =>
        {
            await db.TaskItemTags.Where(x => x.TagId == tag.Id).ExecuteDeleteAsync(ct);
            db.Tags.Remove(tag);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    private static void EnsureUniqueName(IEnumerable<Tag> tags, string name, Guid? exceptId)
    {
        if (tags.Any(t => t.Id != exceptId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw Errors.Conflict(ErrorCodes.TagNameTaken, "同じ名前のタグが既にあります。");
        }
    }
}
