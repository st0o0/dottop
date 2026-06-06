using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class MemoryMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw;
    private readonly TimeSpan _interval;
    private Channel<MemorySnapshot>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;
    private ulong _totalCapacity;

    public static Props Props(HardwareInfo hw, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new MemoryMonitorActor(hw, interval));

    public MemoryMonitorActor(HardwareInfo hw, TimeSpan interval)
    {
        _hw = hw;
        _interval = interval;

        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            _streamCts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<MemorySnapshot>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, _interval, Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
            Sender.Tell(new MonitoringStream<MemorySnapshot>(stream, _streamCts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

            if (_totalCapacity == 0)
            {
                try
                {
                    _hw.RefreshMemoryList();
                    _totalCapacity = _hw.MemoryList.Aggregate(0UL, (sum, m) => sum + m.Capacity);
                }
                catch { }
            }

            try { _hw.RefreshMemoryStatus(); }
            catch { }

            var status = _hw.MemoryStatus;
            var used = status.TotalPhysical - status.AvailablePhysical;
            _channel.Writer.TryWrite(new MemorySnapshot(_totalCapacity, used));
        });
    }

    private void CleanupPreviousStream()
    {
        _tickSchedule?.Cancel();
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _channel?.Writer.TryComplete();
        _tickSchedule = null;
        _streamCts = null;
        _channel = null;
    }

    protected override void PostStop()
    {
        CleanupPreviousStream();
        base.PostStop();
    }
}
