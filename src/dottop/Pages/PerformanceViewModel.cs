using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Models;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class PerformanceViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<CpuMonitorActor> _cpuRef;
    private readonly IRequiredActor<MemoryMonitorActor> _memRef;
    private readonly IRequiredActor<DiskMonitorActor> _diskRef;
    private readonly IRequiredActor<NetworkMonitorActor> _netRef;
    private CancellationTokenSource? _cts;

    public ReactiveProperty<double> CpuTotal { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<double>> CpuCores { get; } = new([]);
    public ReactiveProperty<string> CpuName { get; } = new("Loading...");
    public ReactiveProperty<ulong> RamTotal { get; } = new(0);
    public ReactiveProperty<ulong> RamUsed { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);

    public PerformanceViewModel(
        IRequiredActor<CpuMonitorActor> cpuRef,
        IRequiredActor<MemoryMonitorActor> memRef,
        IRequiredActor<DiskMonitorActor> diskRef,
        IRequiredActor<NetworkMonitorActor> netRef)
    {
        _cpuRef = cpuRef;
        _memRef = memRef;
        _diskRef = diskRef;
        _netRef = netRef;
    }

    public override void OnActivated()
    {
        _cts = new CancellationTokenSource();
        _ = InitializeAsync();

        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);
    }

    private async Task InitializeAsync()
    {
        var ct = _cts!.Token;

        var cpuActor = await _cpuRef.GetAsync(ct);
        var memActor = await _memRef.GetAsync(ct);
        var diskActor = await _diskRef.GetAsync(ct);
        var netActor = await _netRef.GetAsync(ct);

        var cpuStream = await cpuActor.Ask<MonitoringStream<CpuSnapshot>>(new StartMonitoring(), TimeSpan.FromSeconds(5));
        var memStream = await memActor.Ask<MonitoringStream<MemorySnapshot>>(new StartMonitoring(), TimeSpan.FromSeconds(5));
        var diskStream = await diskActor.Ask<MonitoringStream<List<DiskSnapshot>>>(new StartMonitoring(), TimeSpan.FromSeconds(5));
        var netStream = await netActor.Ask<MonitoringStream<List<NetworkSnapshot>>>(new StartMonitoring(), TimeSpan.FromSeconds(5));

        _ = ConsumeAsync(cpuStream.Data, ct, snapshot =>
        {
            CpuName.Value = snapshot.Name;
            CpuTotal.Value = snapshot.TotalPercent;
            CpuCores.Value = snapshot.CorePercents;
        });

        _ = ConsumeAsync(memStream.Data, ct, snapshot =>
        {
            RamTotal.Value = snapshot.TotalBytes;
            RamUsed.Value = snapshot.UsedBytes;
        });

        _ = ConsumeAsync(diskStream.Data, ct, disks => Disks.Value = disks);
        _ = ConsumeAsync(netStream.Data, ct, nets => Networks.Value = nets);
    }

    private static async Task ConsumeAsync<T>(IAsyncEnumerable<T> stream, CancellationToken ct, Action<T> handler)
    {
        try
        {
            await foreach (var item in stream.WithCancellation(ct))
                handler(item);
        }
        catch (OperationCanceledException) { }
    }

    private void HandleKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
            case ConsoleKey.D5: Navigate("/autostart"); break;
            case ConsoleKey.Q or ConsoleKey.Escape: Shutdown(); break;
        }
    }

    public override void OnDeactivating()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDeactivating();
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        CpuTotal.Dispose();
        CpuCores.Dispose();
        CpuName.Dispose();
        RamTotal.Dispose();
        RamUsed.Dispose();
        Disks.Dispose();
        Networks.Dispose();
        base.Dispose();
    }
}
