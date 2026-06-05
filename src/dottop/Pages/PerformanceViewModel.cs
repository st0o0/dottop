using Akka.Actor;
using R3;
using dottop.Models;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class PerformanceViewModel : ReactiveViewModel
{
    private readonly ActorSystem _system;
    private readonly List<IActorRef> _bridges = [];

    public ReactiveProperty<double> CpuTotal { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<double>> CpuCores { get; } = new([]);
    public ReactiveProperty<string> CpuName { get; } = new("Loading...");
    public ReactiveProperty<ulong> RamTotal { get; } = new(0);
    public ReactiveProperty<ulong> RamUsed { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);

    public PerformanceViewModel(ActorSystem system)
    {
        _system = system;
    }

    public override void OnActivated()
    {
        SubscribeToEvent<CpuSnapshot>(snapshot =>
        {
            CpuName.Value = snapshot.Name;
            CpuTotal.Value = snapshot.TotalPercent;
            CpuCores.Value = snapshot.CorePercents;
        });

        SubscribeToEvent<MemorySnapshot>(snapshot =>
        {
            RamTotal.Value = snapshot.TotalBytes;
            RamUsed.Value = snapshot.UsedBytes;
        });

        SubscribeToEvent<List<DiskSnapshot>>(disks => Disks.Value = disks);
        SubscribeToEvent<List<NetworkSnapshot>>(nets => Networks.Value = nets);

        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);
    }

    private void SubscribeToEvent<T>(Action<T> handler)
    {
        var bridge = _system.ActorOf(Props.Create(() => new BridgeActor<T>(handler)));
        _system.EventStream.Subscribe(bridge, typeof(T));
        _bridges.Add(bridge);
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
        foreach (var bridge in _bridges)
        {
            _system.EventStream.Unsubscribe(bridge);
            _system.Stop(bridge);
        }
        _bridges.Clear();
        base.OnDeactivating();
    }

    public override void Dispose()
    {
        CpuTotal.Dispose();
        CpuCores.Dispose();
        CpuName.Dispose();
        RamTotal.Dispose();
        RamUsed.Dispose();
        Disks.Dispose();
        Networks.Dispose();
        base.Dispose();
    }

    private sealed class BridgeActor<T> : ReceiveActor
    {
        public BridgeActor(Action<T> handler) { Receive<T>(msg => handler(msg)); }
    }
}
