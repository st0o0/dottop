using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Models;
using dottop.Nodes;
using dottop.Resources;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class ServicesViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<ServiceActor> _serviceActorRef;
    private IActorRef? _serviceActor;

    public IScrollableList? ListNode { get; set; }
    public Func<ServiceInfo?>? GetSelectedItem { get; set; }

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    public ReactiveProperty<List<ServiceInfo>> AllServices { get; } = new([]);
    public ReactiveProperty<List<ServiceInfo>> FilteredServices { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<string> StatusMessage { get; } = new("");
    public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
    public ReactiveProperty<ServiceInfo?> SelectedService { get; } = new(null);

    public ServicesViewModel(ActorSystem system, IRequiredActor<ServiceActor> serviceActor)
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
            var result = await _serviceActor.Ask<List<ServiceInfo>>(new GetServices(), TimeSpan.FromSeconds(10));
            AllServices.Value = result;
            ApplyFilter();
        }
        catch { StatusMessage.Value = Strings.ErrorLoadingServices; }
    }

    private void ApplyFilter()
    {
        var source = AllServices.Value.AsEnumerable();
        if (!string.IsNullOrEmpty(SearchText.Value))
            source = source.Where(s =>
                s.DisplayName.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase));
        FilteredServices.Value = source.ToList();
        StatusMessage.Value = string.Format(Strings.ServicesStatusFormat, FilteredServices.Value.Count);
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
        if (IsDetailOpen.Value)
        {
            HandleDetailKey(key);
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
            default:
                if (key.KeyInfo.KeyChar == '/') IsSearchActive.Value = true;
                break;
            case ConsoleKey.Enter:
                if (GetSelectedItem?.Invoke() is { } svc)
                {
                    SelectedService.Value = svc;
                    IsDetailOpen.Value = true;
                    _detailContentChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.S: ActionOnSelected(); break;
            case ConsoleKey.X: ActionOnSelected(ActionType.Stop); break;
            case ConsoleKey.R: ActionOnSelected(ActionType.Restart); break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D4: Navigate("/network"); break;

            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    private void HandleDetailKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Escape:
                IsDetailOpen.Value = false;
                SelectedService.Value = null;
                break;
        }
    }

    private enum ActionType { Start, Stop, Restart }

    private async void ActionOnSelected(ActionType action = ActionType.Start)
    {
        if (_serviceActor is null || GetSelectedItem?.Invoke() is not { } svc) return;
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
        catch (Exception ex) { StatusMessage.Value = string.Format(Strings.ErrorFormat, ex.Message); }
    }

    public override void Dispose()
    {
        AllServices.Dispose(); FilteredServices.Dispose(); SearchText.Dispose();
        IsSearchActive.Dispose(); StatusMessage.Dispose(); IsDetailOpen.Dispose();
        SelectedService.Dispose(); _detailContentChanged.Dispose();
        base.Dispose();
    }
}
