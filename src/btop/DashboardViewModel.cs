using System.Reactive.Linq;
using System.Reactive.Subjects;
using Hardware.Info;
using Termina.Input;
using Termina.Reactive;

namespace btop;

public partial class DashboardViewModel : ReactiveViewModel
{
    private readonly IHardwareInfo _hardwareInfo = new HardwareInfo(TimeSpan.FromSeconds(2));

    private static readonly int[] RefreshSteps = [250, 500, 1000, 2000, 5000];
    private int _refreshStepIndex = 2;
    private int RefreshMs => RefreshSteps[_refreshStepIndex];

    private readonly BehaviorSubject<IObservable<long>> _intervalSubject = new(
        Observable.Interval(TimeSpan.FromMilliseconds(RefreshSteps[2])).StartWith(0));

    // --- Reactive Properties ---
    [Reactive] private string _cpuName = "Loading...";
    [Reactive] private double _cpuUsage = 0;
    [Reactive] private List<double> _cpuHistory = new(Enumerable.Repeat(0.0, 60));

    [Reactive] private ulong _ramTotal = 0;
    [Reactive] private ulong _ramUsed = 0;
    [Reactive] private List<double> _ramHistory = new(Enumerable.Repeat(0.0, 60));

    [Reactive] private List<DiskInfo> _disks = [];
    [Reactive] private List<NetworkInfo> _networks = [];

    [Reactive] private string _statusMessage = "";

    public override void OnActivated()
    {
        _hardwareInfo.RefreshCPUList(includePercentProcessorTime: false);
        _hardwareInfo.RefreshMemoryList();
        CpuName = _hardwareInfo.CpuList.FirstOrDefault()?.Name ?? "Unknown CPU";
        RamTotal = _hardwareInfo.MemoryList.Aggregate(0UL, (sum, m) => sum + m.Capacity);

        UpdateStatusMessage();

        _intervalSubject
            .Switch()
            .Subscribe(_ => RefreshHardware())
            .DisposeWith(Subscriptions);

        Input.OfType<KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);
    }

    private void ChangeRefreshRate(int delta)
    {
        _refreshStepIndex = Math.Clamp(_refreshStepIndex + delta, 0, RefreshSteps.Length - 1);
        _intervalSubject.OnNext(
            Observable.Interval(TimeSpan.FromMilliseconds(RefreshMs)).StartWith(0));
        UpdateStatusMessage();
    }

    private void UpdateStatusMessage()
    {
        var faster = _refreshStepIndex > 0
            ? "↑ faster"
            : "↑ (fastest)";
        var slower = _refreshStepIndex < RefreshSteps.Length - 1
            ? "↓ slower"
            : "↓ (slowest)";
        var rate = RefreshMs >= 1000 ? $"{RefreshMs / 1000}s" : $"{RefreshMs}ms";
        StatusMessage = $" Q quit  |  {faster}  |  {slower}  |  refresh: {rate}";
    }

    private void RefreshHardware()
    {
        _hardwareInfo.RefreshCPUList(includePercentProcessorTime: true, millisecondsDelayBetweenTwoMeasurements: 250);
        _hardwareInfo.RefreshMemoryStatus();
        _hardwareInfo.RefreshDriveList();
        _hardwareInfo.RefreshNetworkAdapterList();

        // CPU
        var cpu = _hardwareInfo.CpuList.FirstOrDefault();
        var usage = cpu?.PercentProcessorTime ?? 0;
        CpuUsage = usage;
        var newCpuHistory = new List<double>(CpuHistory) { usage };
        if (newCpuHistory.Count > 60) newCpuHistory.RemoveAt(0);
        CpuHistory = newCpuHistory;

        // RAM
        var memStatus = _hardwareInfo.MemoryStatus;
        RamUsed = memStatus.TotalPhysical - memStatus.AvailablePhysical;
        var ramPercent = RamTotal > 0 ? (double)RamUsed / RamTotal * 100 : 0;
        var newRamHistory = new List<double>(RamHistory) { ramPercent };
        if (newRamHistory.Count > 60) newRamHistory.RemoveAt(0);
        RamHistory = newRamHistory;

        Disks = _hardwareInfo.DriveList
            .Select(d => new DiskInfo(d.Name, d.Size, d.PartitionList[1].VolumeList[0].FreeSpace))
            .ToList();

        Networks = _hardwareInfo.NetworkAdapterList
            .Where(n => n.Speed > 0)
            .Select(n => new NetworkInfo(
                n.Name.Length > 20 ? n.Name[..20] + "..." : n.Name,
                n.BytesReceivedPersec,
                n.BytesSentPersec))
            .ToList();
    }

    private void HandleKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Q or ConsoleKey.Escape:
                Shutdown();
                break;
            case ConsoleKey.UpArrow:
                ChangeRefreshRate(-1);
                break;
            case ConsoleKey.DownArrow:
                ChangeRefreshRate(+1);
                break;
        }
    }
}