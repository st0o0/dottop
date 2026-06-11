using dtop.Core.Messages;
using R3;

namespace dtop.Services;

public interface IRefreshService
{
    ReadOnlyReactiveProperty<TimeSpan> Interval { get; }
    ReactiveProperty<bool> IsPaused { get; }
    Observable<Tick> Ticks { get; }
    void SpeedUp();
    void SlowDown();
}
