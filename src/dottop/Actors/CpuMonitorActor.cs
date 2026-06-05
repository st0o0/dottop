using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class CpuMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    private Channel<CpuSnapshot>? _channel;
    private ICancelable? _tickSchedule;

    public static Props Props() => Akka.Actor.Props.Create<CpuMonitorActor>();

    public CpuMonitorActor()
    {
        _hw.RefreshCPUList(includePercentProcessorTime: false, 250, false);

        Receive<StartMonitoring>(_ =>
        {
            var cts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<CpuSnapshot>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, cts.Token);
            Sender.Tell(new MonitoringStream<CpuSnapshot>(stream, cts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;
            _hw.RefreshCPUList(includePercentProcessorTime: true, 250, false);
            var totalPercent = _hw.CpuList.Count > 0
                ? _hw.CpuList.Average(c => (double)c.PercentProcessorTime) : 0;
            var cores = _hw.CpuList.SelectMany(c => c.CpuCoreList)
                .Select(c => (double)c.PercentProcessorTime).ToList();
            var name = _hw.CpuList.FirstOrDefault()?.Name ?? "Unknown";
            _channel.Writer.TryWrite(new CpuSnapshot(name, totalPercent, cores));
        });
    }

    protected override void PostStop()
    {
        _tickSchedule?.Cancel();
        _channel?.Writer.TryComplete();
        base.PostStop();
    }
}
