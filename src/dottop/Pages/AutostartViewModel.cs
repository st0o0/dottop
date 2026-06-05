using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Models;
using dottop.Nodes;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class AutostartViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<StartupActor> _startupActorRef;
    private IActorRef? _startupActor;

    public DataListNode<StartupEntry>? ListNode { get; set; }

    public ReactiveProperty<List<StartupEntry>> Entries { get; } = new([]);
    public ReactiveProperty<string> StatusMessage { get; } = new("");

    public AutostartViewModel(IRequiredActor<StartupActor> startupActor)
    {
        _startupActorRef = startupActor;
    }

    public override void OnActivated()
    {
        _ = InitializeAsync();
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private async Task InitializeAsync()
    {
        _startupActor = await _startupActorRef.GetAsync(CancellationToken.None);
        RefreshEntries();
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
            case ConsoleKey.UpArrow: ListNode?.MoveUp(); break;
            case ConsoleKey.DownArrow: ListNode?.MoveDown(); break;
            case ConsoleKey.Home: ListNode?.MoveToTop(); break;
            case ConsoleKey.End: ListNode?.MoveToEnd(); break;
            case ConsoleKey.PageUp: ListNode?.PageUp(); break;
            case ConsoleKey.PageDown: ListNode?.PageDown(); break;
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
        if (_startupActor is null || ListNode?.SelectedItem is not { } entry) return;

        var toggled = entry with { Enabled = !entry.Enabled };
        Entries.Value = Entries.Value
            .Select(e => e.Name == entry.Name ? toggled : e)
            .ToList();

        try
        {
            var result = await _startupActor.Ask<object>(
                new SetStartupEnabled(entry.Name, !entry.Enabled), TimeSpan.FromSeconds(5));
            StatusMessage.Value = result is ActionSuccess s ? $" {s.Message}" : $" {((ActionFailure)result).Error}";
        }
        catch (Exception ex) { StatusMessage.Value = $" Fehler: {ex.Message}"; }
    }

    public override void Dispose()
    {
        Entries.Dispose(); StatusMessage.Dispose();
        base.Dispose();
    }
}
