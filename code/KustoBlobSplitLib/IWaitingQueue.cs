namespace KustoBlobSplitLib
{
    internal interface IWaitingQueue<T>
    {
        bool HasCompleted { get; }

        bool HasData { get; }

        void Enqueue(T item);

        ValueTask<QueueResult<T>> DequeueAsync();

        void Complete();
    }
}