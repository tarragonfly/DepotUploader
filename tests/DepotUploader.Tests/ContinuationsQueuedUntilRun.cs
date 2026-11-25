using System.Collections.Concurrent;

namespace DepotUploader.Tests;

internal sealed class ContinuationsQueuedUntilRun : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Continuation, object? State)> _queued =
        new ConcurrentQueue<(SendOrPostCallback, object?)>();

    public int Count => _queued.Count;

    public override void Post(SendOrPostCallback continuation, object? state)
    {
        _queued.Enqueue((continuation, state));
    }

    public void RunQueued()
    {
        while (_queued.TryDequeue(out (SendOrPostCallback Continuation, object? State) queued))
        {
            queued.Continuation(queued.State);
        }
    }
}
