using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using dtop.Services;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

public sealed class DockerMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Docker");

    private sealed record SampleCompleted(IReadOnlyList<ContainerSnapshot> Containers);
    private sealed record SampleFailed(Exception Cause);

    private readonly IDockerProvider _docker;
    private readonly IMetricSink _sink;
    private bool _sampling;

    public static Props Props(IDockerProvider docker, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new DockerMonitorActor(docker, sink));

    public DockerMonitorActor(IDockerProvider docker, IMetricSink sink)
    {
        _docker = docker;
        _sink = sink;

        Receive<Tick>(_ =>
        {
            if (_sampling) return;
            _sampling = true;
            SampleAsync()
                .PipeTo(Self,
                    success: containers => new SampleCompleted(containers),
                    failure: ex => new SampleFailed(ex));
        });

        Receive<SampleCompleted>(m =>
        {
            _sampling = false;
            _sink.Publish(m.Containers);
        });

        Receive<SampleFailed>(m =>
        {
            _sampling = false;
            Trace.Warning(this, "Docker sample failed: {0}", m.Cause.Message);
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

    private async Task<IReadOnlyList<ContainerSnapshot>> SampleAsync()
    {
        var result = await _docker.GetContainersAsync(CancellationToken.None);
        return result.ToList();
    }

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
