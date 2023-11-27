namespace KustoBlobSplitLib
{
    internal record QueueResult<T>(bool IsCompleted, T? Item);
}