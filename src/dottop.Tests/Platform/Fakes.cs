using System.Diagnostics;
using dottop.Actors;
using dottop.Models;
using dottop.Platform;

namespace dottop.Tests.Platform;

public sealed class FakeDiskMetrics : IDiskMetricsProvider
{
    public bool Initialized { get; private set; }
    public Dictionary<string, (ulong Read, ulong Write, double Active)> Data { get; } = new();

    public void Initialize() => Initialized = true;

    public (ulong ReadBytesPerSec, ulong WriteBytesPerSec, double ActivePercent) GetMetrics(string diskName) =>
        Data.TryGetValue(diskName, out var m) ? (m.Read, m.Write, m.Active) : (0, 0, 0);

    public void Dispose() { }
}

public sealed class FakeProcessTree : IProcessTreeProvider
{
    public ProcessTreeResult BuildTree(int rootPid) =>
        new(rootPid, $"FakeProcess-{rootPid}",
        [
            new ProcessTreeResult(rootPid + 1, "child1", []),
            new ProcessTreeResult(rootPid + 2, "child2", [])
        ]);
}

public sealed class FakeServiceManager : IServiceManager
{
    public List<ServiceInfo> Services { get; set; } =
    [
        new("TestSvc1", "Test Service 1", ServiceStatus.Running, ServiceStartType.Automatic, 1234),
        new("TestSvc2", "Test Service 2", ServiceStatus.Stopped, ServiceStartType.Manual, null),
    ];

    public string LastAction { get; private set; } = "";

    public List<ServiceInfo> GetServices() => Services;
    public string Start(string name) { LastAction = $"start:{name}"; return $"Started {name}"; }
    public string Stop(string name) { LastAction = $"stop:{name}"; return $"Stopped {name}"; }
    public string Restart(string name) { LastAction = $"restart:{name}"; return $"Restarted {name}"; }
}

public sealed class FakeProcessClassifier : IProcessClassifier
{
    public ProcessGroup DefaultGroup { get; set; } = ProcessGroup.Apps;
    public ProcessGroup Classify(Process process) => DefaultGroup;
}
