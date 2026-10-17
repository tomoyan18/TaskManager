namespace TaskManager.Domain.Common;

/// <summary>作成・更新の日時と利用者を自動設定する対象（database-design.md §1 監査列）。</summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }

    Guid? CreatedByUserId { get; set; }

    DateTime UpdatedAtUtc { get; set; }

    Guid? UpdatedByUserId { get; set; }
}

/// <summary>楽観的排他の対象（row_version）。更新時にインフラ層で +1 する。</summary>
public interface IVersioned
{
    uint RowVersion { get; set; }
}
