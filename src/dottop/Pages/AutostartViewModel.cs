using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Models;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class AutostartViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<StartupActor> _startupActorRef;
    private IActorRef? _startupActor;

    public ReactiveProperty<List<StartupEntry>> Entries { get; } = new([]);
    public ReactiveProperty<int> SelectedIndex { get; } = new(0);
    public ReactiveProperty<string> StatusMessage { get; } = new("");

    public AutostartViewModel(IRequiredActor<StartupActor> startupActor)
    {
        _startupActorRef = startupActor;
    }

    public override void OnActivated()
    {
        _startupActor = _startupActorRef.GetAsync(CancellationToken.None).GetAwaiter().GetResult();
        RefreshEntries();
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private async void RefreshEntries()
    {
        if (_startupActor is null) return;
        try
        {
            var result = await _startupActor.Ask<List<StartupEntry>>(new GetStartupEntries(), TimeSpan.FromSeconds(10));
            Entries.Value = result;
            StatusMessage.Value = $" {result.Count} Einträge | Space: Aktivieren/Deaktivieren";
        }
        catch { StatusMessage.Value = " Fehler beim Laden der Einträge"; }
    }

    private void HandleKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.UpArrow: SelectedIndex.Value = Math.Max(0, SelectedIndex.Value - 1); break;
            case ConsoleKey.DownArrow: SelectedIndex.Value = Math.Min(Entries.Value.Count - 1, SelectedIndex.Value + 1); break;
            case ConsoleKey.Spacebar: ToggleSelected(); break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    private async void ToggleSelected()
    {
        if (_startupActor is null || Entries.Value.Count == 0) return;
        var idx = Math.Clamp(SelectedIndex.Value, 0, Entries.Value.Count - 1);
        var entry = Entries.Value[idx];
        try
        {
            await _startupActor.Ask<object>(new SetStartupEnabled(entry.Name, !entry.Enabled), TimeSpan.FromSeconds(5));
            RefreshEntries();
        }
        catch { }
    }

    public override void Dispose()
    {
        Entries.Dispose(); SelectedIndex.Dispose(); StatusMessage.Dispose();
        base.Dispose();
    }
}
