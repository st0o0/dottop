using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Models;
using dottop.Nodes;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class ServicesViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<WindowsServiceActor> _serviceActorRef;
    private IActorRef? _serviceActor;

    public DataListNode<WindowsServiceInfo>? ListNode { get; set; }

    public ReactiveProperty<List<WindowsServiceInfo>> AllServices { get; } = new([]);
    public ReactiveProperty<List<WindowsServiceInfo>> FilteredServices { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<string> StatusMessage { get; } = new("");

    public ServicesViewModel(ActorSystem system, IRequiredActor<WindowsServiceActor> serviceActor)
    {
        _serviceActorRef = serviceActor;
    }

    public override void OnActivated()
    {
        _serviceActor = _serviceActorRef.GetAsync(CancellationToken.None).GetAwaiter().GetResult();
        RefreshServices();
        SearchText.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private async void RefreshServices()
    {
        if (_serviceActor is null) return;
        try
        {
            var result = await _serviceActor.Ask<List<WindowsServiceInfo>>(new GetServices(), TimeSpan.FromSeconds(10));
            AllServices.Value = result;
            ApplyFilter();
        }
        catch { StatusMessage.Value = " Fehler beim Laden der Dienste"; }
    }

    private void ApplyFilter()
    {
        var source = AllServices.Value.AsEnumerable();
        if (!string.IsNullOrEmpty(SearchText.Value))
            source = source.Where(s =>
                s.DisplayName.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase));
        FilteredServices.Value = source.ToList();
        StatusMessage.Value = $" {FilteredServices.Value.Count} Dienste | /: Suche | S: Start | X: Stop | R: Restart";
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsSearchActive.Value)
        {
            switch (key.KeyInfo.Key)
            {
                case ConsoleKey.Escape: IsSearchActive.Value = false; SearchText.Value = ""; break;
                case ConsoleKey.Backspace: if (SearchText.Value.Length > 0) SearchText.Value = SearchText.Value[..^1]; break;
                default: if (key.KeyInfo.KeyChar is >= ' ' and <= '~') SearchText.Value += key.KeyInfo.KeyChar; break;
            }
            return;
        }
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.UpArrow: ListNode?.MoveUp(); break;
            case ConsoleKey.DownArrow: ListNode?.MoveDown(); break;
            case ConsoleKey.Home: ListNode?.MoveToTop(); break;
            case ConsoleKey.End: ListNode?.MoveToEnd(); break;
            case ConsoleKey.PageUp: ListNode?.PageUp(); break;
            case ConsoleKey.PageDown: ListNode?.PageDown(); break;
            case ConsoleKey.Oem2: IsSearchActive.Value = true; break;
            case ConsoleKey.S: ActionOnSelected(); break;
            case ConsoleKey.X: ActionOnSelected(ActionType.Stop); break;
            case ConsoleKey.R: ActionOnSelected(ActionType.Restart); break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D4: Navigate("/network"); break;

            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    private enum ActionType { Start, Stop, Restart }

    private async void ActionOnSelected(ActionType action = ActionType.Start)
    {
        if (_serviceActor is null || ListNode?.SelectedItem is not { } svc) return;
        object msg = action switch
        {
            ActionType.Stop => new StopService(svc.Name),
            ActionType.Restart => new RestartService(svc.Name),
            _ => new StartService(svc.Name),
        };
        try
        {
            var result = await _serviceActor.Ask<object>(msg, TimeSpan.FromSeconds(10));
            StatusMessage.Value = result is ActionSuccess s ? $" {s.Message}" : $" {((ActionFailure)result).Error}";
            RefreshServices();
        }
        catch (Exception ex) { StatusMessage.Value = $" Fehler: {ex.Message}"; }
    }

    public override void Dispose()
    {
        AllServices.Dispose(); FilteredServices.Dispose(); SearchText.Dispose();
        IsSearchActive.Dispose(); StatusMessage.Dispose();
        base.Dispose();
    }
}
