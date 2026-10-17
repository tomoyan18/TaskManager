namespace TaskManager.Domain.Tasks;

/// <summary>タスクとタグの対応（task_item_tag）。</summary>
public sealed class TaskItemTag
{
    public TaskItemTag(Guid taskId, Guid tagId)
    {
        TaskId = taskId;
        TagId = tagId;
    }

    public Guid TaskId { get; private set; }

    public Guid TagId { get; private set; }
}
