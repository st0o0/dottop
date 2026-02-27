using System.Diagnostics;
using R3;
using Hardware.Info;
using Termina.Input;
using Termina.Reactive;

namespace dottop;

public class DashboardViewModel : ReactiveViewModel
{
    private readonly HardwareInfo _hardwareInfo = new(TimeSpan.FromSeconds(2));

    private static readonly int[] RefreshSteps = [250, 500, 1000, 2000, 5000];
    private int _refreshStepIndex = 2;
    private int RefreshMs => RefreshSteps[_refreshStepIndex];

    private readonly SerialDisposable _refreshSubscription = new();

    public ReactiveProperty<string> CpuName { get; } = new("Loading...");
    public ReactiveProperty<List<double>> CpuCores { get; } = new([]);
    public ReactiveProperty<double> CpuUsage { get; } = new(0);

    public ReactiveProperty<ulong> RamTotal { get; } = new(0);
    public ReactiveProperty<ulong> RamUsed { get; } = new(0);

    public ReactiveProperty<List<DiskInfo>> Disks { get; } = new([]);
    public ReactiveProperty<List<NetworkInfo>> Networks { get; } = new([]);

    public ReactiveProperty<List<ProcessInfo>> Processes { get; } = new([]);
    public ReactiveProperty<int> ScrollEvent { get; } = new(0);
    public ReactiveProperty<string> StatusMessage { get; } = new("");

    public override void OnActivated()
    {
        _hardwareInfo.RefreshCPUList(includePercentProcessorTime: false, 250, false);
        _hardwareInfo.RefreshNetworkAdapterList(true, true, 250);
        _hardwareInfo.RefreshMemoryList();
        _hardwareInfo.RefreshDriveList();

        CpuName.Value = _hardwareInfo.CpuList.FirstOrDefault()?.Name ?? "Unknown CPU";
        CpuCores.Value = _hardwareInfo.CpuList
            .SelectMany(x => x.CpuCoreList)
            .Select(x => (double)x.PercentProcessorTime)
            .ToList();
        RamTotal.Value = _hardwareInfo.MemoryList.Aggregate(0UL, (sum, m) => sum + m.Capacity);
        Disks.Value = _hardwareInfo.DriveList
            .Where(x => x.PartitionList.Count > 0)
            .Where(x => x.PartitionList.Any(partition => partition.VolumeList.Count > 0))
            .SelectMany(x =>
            {
                return x.PartitionList
                    .SelectMany(partition => partition.VolumeList)
                    .Select(volume => new DiskInfo(volume.VolumeName, volume.Size, volume.FreeSpace));
            })
            .ToList();

        UpdateStatusMessage();
        StartHardwareRefresh();

        // Subscribe to keyboard input
        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKeyPress)
            .DisposeWith(Subscriptions);
    }

    private void StartHardwareRefresh()
    {
        _refreshSubscription.Disposable = Observable
            .Interval(TimeSpan.FromMilliseconds(RefreshMs))
            .Subscribe(_ => RefreshHardware());
    }

    private void ChangeRefreshRate(int delta)
    {
        _refreshStepIndex = Math.Clamp(_refreshStepIndex + delta, 0, RefreshSteps.Length - 1);
        _refreshSubscription.Disposable?.Dispose();
        StartHardwareRefresh();
        UpdateStatusMessage();
    }

    private void UpdateStatusMessage()
    {
        var faster = _refreshStepIndex > 0 ? "w faster" : "w (fastest)";
        var slower = _refreshStepIndex < RefreshSteps.Length - 1 ? "s slower" : "s (slowest)";
        var rate = RefreshMs >= 1000 ? $"{RefreshMs / 1000}s" : $"{RefreshMs}ms";
        StatusMessage.Value = $" Q quit  |  {faster}  |  {slower}  |  refresh: {rate}";
    }

    private void RefreshHardware()
    {
        _hardwareInfo.RefreshCPUList(includePercentProcessorTime: true, millisecondsDelayBetweenTwoMeasurements: 250,
            false);
        _hardwareInfo.RefreshMemoryStatus();
        _hardwareInfo.RefreshDriveList();
        _hardwareInfo.RefreshNetworkAdapterList();

        // CPU
        CpuUsage.Value = _hardwareInfo.CpuList.Average(x => (long)x.PercentProcessorTime);
        CpuCores.Value = _hardwareInfo.CpuList
            .SelectMany(x => x.CpuCoreList)
            .Select(x => (double)x.PercentProcessorTime)
            .ToList();

        // RAM
        var memStatus = _hardwareInfo.MemoryStatus;
        RamUsed.Value = memStatus.TotalPhysical - memStatus.AvailablePhysical;

        // Disks
        Disks.Value = _hardwareInfo.DriveList
            .Where(x => x.PartitionList.Count > 0)
            .Where(x => x.PartitionList.Any(partition => partition.VolumeList.Count > 0))
            .SelectMany(x =>
            {
                return x.PartitionList
                    .SelectMany(partition => partition.VolumeList)
                    .Select(volume => new DiskInfo(volume.VolumeName, volume.Size, volume.FreeSpace));
            })
            .ToList();

        // Networks
        Networks.Value = _hardwareInfo.NetworkAdapterList
            .Where(n => n.Speed > 0)
            .Select(n => new NetworkInfo(
                n.Name.Length > 20 ? n.Name[..20] + "..." : n.Name,
                n.BytesReceivedPersec,
                n.BytesSentPersec))
            .ToList();

        // Processes
        Processes.Value = Process.GetProcesses()
            .OrderByDescending(p => p.WorkingSet64)
            .Select(p => new ProcessInfo(
                p.Id.ToString(),
                p.ProcessName,
                p.WorkingSet64))
            .ToList();
    }

    private void HandleKeyPress(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Q or ConsoleKey.Escape:
                Shutdown();
                break;
            case ConsoleKey.W:
                ChangeRefreshRate(-1);
                break;
            case ConsoleKey.S:
                ChangeRefreshRate(+1);
                break;
            case ConsoleKey.UpArrow:
                ScrollEvent.Value = 1;
                break;
            case ConsoleKey.DownArrow:
                ScrollEvent.Value = -1;
                break;
        }
    }

    public override void Dispose()
    {
        _refreshSubscription.Dispose();
        CpuName.Dispose();
        CpuUsage.Dispose();
        RamTotal.Dispose();
        RamUsed.Dispose();
        Disks.Dispose();
        Networks.Dispose();
        StatusMessage.Dispose();
        base.Dispose();
    }
}