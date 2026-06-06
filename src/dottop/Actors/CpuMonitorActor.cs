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
    private CancellationTokenSource? _streamCts;
    private bool _baselined;

    public static Props Props(TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new CpuMonitorActor(interval));

    public CpuMonitorActor(TimeSpan interval)
    {
        var interval1 = interval;

        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            _streamCts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<CpuSnapshot>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, interval1, Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
            Sender.Tell(new MonitoringStream<CpuSnapshot>(stream, _streamCts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

            try
            {
                if (!_baselined)
                {
                    _hw.RefreshCPUList(includePercentProcessorTime: false);
                    _baselined = true;
                }
                _hw.RefreshCPUList(includePercentProcessorTime: true, 100, false);
            }
            catch { }

            var totalPercent = _hw.CpuList.Count > 0
                ? _hw.CpuList.Average(c => (double)c.PercentProcessorTime) : 0;
            var cores = _hw.CpuList.SelectMany(c => c.CpuCoreList)
                .Select(c => (double)c.PercentProcessorTime).ToList();
            var name = _hw.CpuList.FirstOrDefault()?.Name ?? "Unknown";
            _channel.Writer.TryWrite(new CpuSnapshot(name, totalPercent, cores));
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
