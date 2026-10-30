using System.Threading.Channels;

namespace Kestry.Server.Downloads;

/// <summary>Fans the latest snapshot out to every connected SSE client; slow clients only ever see the newest one.</summary>
public sealed class SnapshotHub
{
    private readonly List<Channel<Snapshot>> _subscribers = [];

    public Snapshot? Latest { get; private set; }

    public int SubscriberCount
    {
        get { lock (_subscribers) return _subscribers.Count; }
    }

    public void Publish(Snapshot snapshot)
    {
        Latest = snapshot;
        lock (_subscribers)
            foreach (var c in _subscribers) c.Writer.TryWrite(snapshot);
    }

    public async IAsyncEnumerable<Snapshot> SubscribeAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<Snapshot>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_subscribers) _subscribers.Add(channel);
        try
        {
            if (Latest is { } first) channel.Writer.TryWrite(first);
            await foreach (var s in channel.Reader.ReadAllAsync(ct)) yield return s;
        }
        finally
        {
            lock (_subscribers) _subscribers.Remove(channel);
        }
    }
}
