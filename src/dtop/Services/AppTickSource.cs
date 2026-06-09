using dtop.Plugin;

namespace dtop.Services;

public sealed class AppTickSource : ITickSource
{
    private readonly List<Action> _subscribers = [];
    private readonly Timer? _timer;

    public TimeSpan CurrentInterval { get; private set; }

    public AppTickSource(TimeSpan initialInterval)
    {
        CurrentInterval = initialInterval;
        _timer = new Timer(_ => NotifySubscribers(), null, initialInterval, initialInterval);
    }

    public void ChangeInterval(TimeSpan newInterval)
    {
        CurrentInterval = newInterval;
        _timer?.Change(newInterval, newInterval);
    }

    public IDisposable Subscribe(Action onTick)
    {
        _subscribers.Add(onTick);
        return new Unsubscriber(() => _subscribers.Remove(onTick));
    }

    private void NotifySubscribers()
    {
        foreach (var sub in _subscribers)
        {
            try
            {
                sub();
            }
            catch
            {
                // noop
            }
        }
    }

    private sealed class Unsubscriber(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}