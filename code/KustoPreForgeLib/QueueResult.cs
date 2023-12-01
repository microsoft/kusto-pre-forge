namespace KustoPreForgeLib
{
    internal record QueueResult<T>(bool IsCompleted, T? Item);
}