using System.Threading.Channels;
using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;

namespace dtop.UI.Tests.Fixtures;

/// <summary>
/// Replaces MonitoringSupervisor in tests. Responds to all typed monitoring commands
/// with pre-built test data from <see cref="TestData"/>.
/// </summary>
public sealed class TestSupervisorActor : ReceiveActor
{
    public TestSupervisorActor()
    {
        Receive<StartCpuMonitoring>(_ =>
            Sender.Tell(CreateStream(TestData.Cpu)));

        Receive<StartMemoryMonitoring>(_ =>
            Sender.Tell(CreateStream(TestData.Memory)));

        Receive<StartDiskMonitoring>(_ =>
            Sender.Tell(CreateStream(TestData.Disks)));

        Receive<StartNetworkMonitoring>(_ =>
            Sender.Tell(CreateStream(TestData.Networks)));

        Receive<StartGpuMonitoring>(_ =>
            Sender.Tell(CreateStream(TestData.Gpu)));

        Receive<StartProcessMonitoring>(_ =>
            Sender.Tell(CreateStream(TestData.Processes)));

        Receive<GetServices>(_ =>
            Sender.Tell(TestData.Services));

        Receive<KillProcess>(msg =>
            Sender.Tell(new ActionSuccess($"Killed {msg.Pid}")));

        Receive<StartService>(msg =>
            Sender.Tell(new ActionSuccess($"Started {msg.Name}")));

        Receive<StopService>(msg =>
            Sender.Tell(new ActionSuccess($"Stopped {msg.Name}")));

        Receive<RestartService>(msg =>
            Sender.Tell(new ActionSuccess($"Restarted {msg.Name}")));

        Receive<SetProcessPriority>(msg =>
            Sender.Tell(new ActionSuccess($"Priority set for {msg.Pid}")));

        Receive<SetProcessAffinity>(msg =>
            Sender.Tell(new ActionSuccess($"Affinity set for {msg.Pid}")));

        Receive<GetProcessTree>(msg =>
            Sender.Tell(new ProcessTreeResult(msg.Pid, "test", [])));

        Receive<GetProcessEnvironment>(_ =>
            Sender.Tell(new ProcessEnvironmentResult(
                new Dictionary<string, string> { ["PATH"] = "/usr/bin" })));

        Receive<GetProcessHandles>(_ =>
            Sender.Tell(new ProcessHandlesResult(["handle1", "handle2"])));

        Receive<StartDockerMonitoring>(_ =>
            Sender.Tell(CreateStream(TestData.Containers)));

        Receive<StartContainer>(msg =>
            Sender.Tell(new ActionSuccess($"Started {msg.Id}")));

        Receive<StopContainer>(msg =>
            Sender.Tell(new ActionSuccess($"Stopped {msg.Id}")));

        Receive<RestartContainer>(msg =>
            Sender.Tell(new ActionSuccess($"Restarted {msg.Id}")));

        Receive<GetContainerLogs>(msg =>
            Sender.Tell(new ContainerLogsResult(["Log line 1", "Log line 2"])));
    }

    private static MonitoringStream<T> CreateStream<T>(T snapshot)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        channel.Writer.TryWrite(snapshot);

        var cts = new CancellationTokenSource();
        return new MonitoringStream<T>(
            ChannelHelper.ReadFromChannelAsync(channel.Reader, cts.Token),
            cts);
    }
}
