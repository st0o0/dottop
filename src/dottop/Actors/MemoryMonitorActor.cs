using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class MemoryMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    private readonly ulong _totalCapacity;
    private Channel<MemorySnapshot>? _channel;
    private ICancelable? _tickSchedule;

    public static Props Props() => Akka.Actor.Props.Create<MemoryMonitorActor>();

    public MemoryMonitorActor()
    {
        _hw.RefreshMemoryList();
        _totalCapacity = _hw.MemoryList.Aggregate(0UL, (sum, m) => sum + m.Capacity);

        Receive<StartMonitoring>(_ =>
        {
            var cts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<MemorySnapshot>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, cts.Token);
            Sender.Tell(new MonitoringStream<MemorySnapshot>(stream, cts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;
            _hw.RefreshMemoryStatus();
            var status = _hw.MemoryStatus;
            var used = status.TotalPhysical - status.AvailablePhysical;
            _channel.Writer.TryWrite(new MemorySnapshot(_totalCapacity, used));
        });
    }

    protected override void PostStop()
    {
        _tickSchedule?.Cancel();
        _channel?.Writer.TryComplete();
        base.PostStop();
    }
}
