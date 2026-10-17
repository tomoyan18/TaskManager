using TaskManager.Domain.Common;

namespace TaskManager.Application.Common;

/// <summary>利用者の参照（docs/design/api-design.md §2.5）。</summary>
public sealed record UserRef(Guid Id, string DisplayName, bool IsActive);

public sealed record StatusRef(Guid Id, string Name, StatusCategory Category, StatusColor Color);

public sealed record TagRef(Guid Id, string Name, StatusColor Color);

public sealed record MilestoneRef(Guid Id, string Name, MilestoneState State);

/// <summary>タスクの参照（キー・番号・タイトル・区分）。</summary>
public sealed record TaskRef(string Key, int Number, string Title, StatusCategory StatusCategory);

public sealed record ProjectRef(string Key, string Name);
