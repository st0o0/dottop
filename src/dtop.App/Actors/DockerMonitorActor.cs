using System.Threading.Channels;
using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.App.Actors;

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
            if (_channel is null)
            {
                return;
            }

            // Write cached data immediately (non-blocking, like CpuMonitorActor)
            if (_cached.Count > 0)
            {
                _channel.Writer.TryWrite(_cached);
            }

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
            {
                _cached = msg.Containers;
            }
        });

        // Actions use PipeTo to stay non-blocking
        ReceiveAsync<StartContainer>(async msg =>
        {
            try
            {
                await _docker.StartAsync(msg.Id);
                Sender.Tell(new ActionSuccess($"Started {msg.Id}"));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        ReceiveAsync<StopContainer>(async msg =>
        {
            try
            {
                await _docker.StopAsync(msg.Id);
                Sender.Tell(new ActionSuccess($"Stopped {msg.Id}"));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        ReceiveAsync<RestartContainer>(async msg =>
        {
            try
            {
                await _docker.RestartAsync(msg.Id);
                Sender.Tell(new ActionSuccess($"Restarted {msg.Id}"));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        ReceiveAsync<GetContainerLogs>(async msg =>
        {
            try
            {
                var logs = await _docker.GetLogsAsync(msg.Id, msg.TailLines);
                Sender.Tell(new ContainerLogsResult(logs));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        ReceiveAsync<GetNetworks>(async _ =>
        {
            try { Sender.Tell(new NetworksResult(await _docker.GetNetworksAsync())); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<GetVolumes>(async _ =>
        {
            try { Sender.Tell(new VolumesResult(await _docker.GetVolumesAsync())); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<GetImages>(async _ =>
        {
            try { Sender.Tell(new ImagesResult(await _docker.GetImagesAsync())); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<CreateNetwork>(async msg =>
        {
            try { await _docker.CreateNetworkAsync(msg.Name, msg.Driver); Sender.Tell(new ActionSuccess($"Network '{msg.Name}' created")); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<CreateVolume>(async msg =>
        {
            try { await _docker.CreateVolumeAsync(msg.Name, msg.Driver); Sender.Tell(new ActionSuccess($"Volume '{msg.Name}' created")); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<PullImage>(async msg =>
        {
            try { await _docker.PullImageAsync(msg.Image); Sender.Tell(new ActionSuccess($"Image '{msg.Image}' pulled")); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<DeleteNetwork>(async msg =>
        {
            try { await _docker.DeleteNetworkAsync(msg.Id); Sender.Tell(new ActionSuccess("Network deleted")); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<DeleteVolume>(async msg =>
        {
            try { await _docker.DeleteVolumeAsync(msg.Name); Sender.Tell(new ActionSuccess("Volume deleted")); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<DeleteImage>(async msg =>
        {
            try { await _docker.DeleteImageAsync(msg.Id); Sender.Tell(new ActionSuccess("Image deleted")); }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        ReceiveAsync<PruneVolumes>(async _ =>
        {
            await Task.CompletedTask;
            Sender.Tell(new ActionSuccess("Not implemented yet"));
        });

        ReceiveAsync<PruneImages>(async _ =>
        {
            await Task.CompletedTask;
            Sender.Tell(new ActionSuccess("Not implemented yet"));
        });
    }

    private void HandleStartMonitoring()
    {
        CleanupPreviousStream();

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
