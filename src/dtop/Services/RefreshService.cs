using dtop.Core.Messages;
using dtop.Plugin;
using R3;

namespace dtop.Services;

public sealed class RefreshService : IRefreshService, ITickSource, IDisposable
{
    private static readonly TimeSpan[] Steps =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1000),
        TimeSpan.FromMilliseconds(2000),
        TimeSpan.FromMilliseconds(4000),
    ];

    private readonly TimeProvider _timeProvider;
    private readonly Subject<Tick> _ticks = new();
    private readonly object _gate = new();
    private IDisposable? _timer;
    private long _seq;

    public ReactiveProperty<TimeSpan> Interval { get; }
    public ReactiveProperty<bool> IsPaused { get; } = new(false);
    public Observable<Tick> Ticks => _ticks.AsObservable();

    TimeSpan ITickSource.CurrentInterval => Interval.Value;

    public RefreshService(TimeSpan initialInterval, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        Interval = new ReactiveProperty<TimeSpan>(SnapToStep(initialInterval));
        StartTimer();
    }

    public void SpeedUp() => Shift(-1);
    public void SlowDown() => Shift(+1);

    private void Shift(int direction)
    {
        var idx = Array.IndexOf(Steps, Interval.Value);
        if (idx < 0)
        {
            idx = 2;
        }

        var next = Math.Clamp(idx + direction, 0, Steps.Length - 1);
        if (Steps[next] == Interval.Value)
        {
            return;
        }

        Interval.Value = Steps[next];
        StartTimer();
    }

    private void StartTimer()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = Observable.Interval(Interval.Value, _timeProvider)
                .Subscribe(_ =>
                {
                    if (IsPaused.Value)
                    {
                        return;
                    }

                    _ticks.OnNext(new Tick(_seq++, Interval.Value));
                });
        }
    }

    private static TimeSpan SnapToStep(TimeSpan value) =>
        Steps.MinBy(s => Math.Abs((s - value).Ticks));

    IDisposable ITickSource.Subscribe(Action onTick) =>
        Ticks.Subscribe(_ => onTick());

    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
        }

        _ticks.OnCompleted();
        _ticks.Dispose();
        Interval.Dispose();
        IsPaused.Dispose();
    }
}
