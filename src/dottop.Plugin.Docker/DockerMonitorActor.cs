using System.Threading.Channels;
using Akka.Actor;
using dottop.Core.Messages;
using dottop.Core.Models;
using dottop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dottop.Plugin.Docker;

public sealed class DockerMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Docker");

    private sealed record Tick;
    private sealed record ContainersRefreshed(List<ContainerSnapshot> Containers);

    private readonly IDockerProvider _docker;
    private readonly TimeSpan _interval;
    private Channel<List<ContainerSnapshot>>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;
    private List<ContainerSnapshot> _cached = [];
    private bool _refreshing;

    public static Props Props(IDockerProvider docker, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new DockerMonitorActor(docker, interval));

    public DockerMonitorActor(IDockerProvider docker, TimeSpan interval)
    {
        _docker = docker;
        _interval = interval;

        Receive<StartDockerMonitoring>(_ => HandleStartMonitoring());

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

            // Write cached data immediately (non-blocking, like CpuMonitorActor)
            if (_cached.Count > 0)
                _channel.Writer.TryWrite(_cached);

            // Trigger background refresh if not already running
            if (!_refreshing)
            {
                _refreshing = true;
                _docker.GetContainersAsync(_streamCts?.Token ?? CancellationToken.None)
                    .PipeTo(Self,
                        success: result => new ContainersRefreshed(result.ToList()),
                        failure: _ => new ContainersRefreshed([]));
            }
        });

        Receive<ContainersRefreshed>(msg =>
        {
            _refreshing = false;
            if (msg.Containers.Count > 0)
                _cached = msg.Containers;
        });

        // Actions use PipeTo to stay non-blocking
        ReceiveAsync<StartContainer>(async msg =>
        {
            try
            {
                await _docker.StartAsync(msg.Id);
                Sender.Tell(new ActionSuccess($"Started {msg.Id}"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<StopContainer>(async msg =>
        {
            try
            {
                await _docker.StopAsync(msg.Id);
                Sender.Tell(new ActionSuccess($"Stopped {msg.Id}"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<RestartContainer>(async msg =>
        {
            try
            {
                await _docker.RestartAsync(msg.Id);
                Sender.Tell(new ActionSuccess($"Restarted {msg.Id}"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<GetContainerLogs>(async msg =>
        {
            try
            {
                var logs = await _docker.GetLogsAsync(msg.Id, msg.TailLines);
                Sender.Tell(new ContainerLogsResult(logs));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });
    }

    private void HandleStartMonitoring()
    {
        CleanupPreviousStream();

        if (!_docker.IsAvailable)
        {
            var cts = new CancellationTokenSource();
            var emptyChannel = Channel.CreateBounded<List<ContainerSnapshot>>(1);
            emptyChannel.Writer.TryWrite([]);
            Sender.Tell(new MonitoringStream<List<ContainerSnapshot>>(
                ChannelHelper.ReadFromChannelAsync(emptyChannel.Reader, cts.Token), cts));
            Trace.Info(this, "Docker not available");
            return;
        }

        _streamCts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<List<ContainerSnapshot>>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
            TimeSpan.Zero, _interval, Self, new Tick(), Self);

        var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
        Sender.Tell(new MonitoringStream<List<ContainerSnapshot>>(stream, _streamCts));
        Trace.Info(this, "Monitoring started, interval={0}ms", _interval.TotalMilliseconds);
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
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
