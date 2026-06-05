using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Models;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class ServicesViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<WindowsServiceActor> _serviceActorRef;
    private IActorRef? _serviceActor;

    public ReactiveProperty<List<WindowsServiceInfo>> AllServices { get; } = new([]);
    public ReactiveProperty<List<WindowsServiceInfo>> FilteredServices { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<int> SelectedIndex { get; } = new(0);
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
            case ConsoleKey.UpArrow: SelectedIndex.Value = Math.Max(0, SelectedIndex.Value - 1); break;
            case ConsoleKey.DownArrow: SelectedIndex.Value = Math.Min(FilteredServices.Value.Count - 1, SelectedIndex.Value + 1); break;
            case ConsoleKey.Oem2: IsSearchActive.Value = true; break;
            case ConsoleKey.S: ActionOnSelected(s => new StartService(s.Name)); break;
            case ConsoleKey.X: ActionOnSelected(s => new StopService(s.Name)); break;
            case ConsoleKey.R: ActionOnSelected(s => new RestartService(s.Name)); break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
            case ConsoleKey.D5: Navigate("/autostart"); break;
            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    private async void ActionOnSelected(Func<WindowsServiceInfo, object> msgFactory)
    {
        if (_serviceActor is null || FilteredServices.Value.Count == 0) return;
        var idx = Math.Clamp(SelectedIndex.Value, 0, FilteredServices.Value.Count - 1);
        var svc = FilteredServices.Value[idx];
        try
        {
            var result = await _serviceActor.Ask<object>(msgFactory(svc), TimeSpan.FromSeconds(10));
            StatusMessage.Value = result is ActionSuccess s ? $" {s.Message}" : $" {((ActionFailure)result).Error}";
            RefreshServices();
        }
        catch (Exception ex) { StatusMessage.Value = $" Fehler: {ex.Message}"; }
    }

    public override void Dispose()
    {
        AllServices.Dispose(); FilteredServices.Dispose(); SearchText.Dispose();
        SelectedIndex.Dispose(); IsSearchActive.Dispose(); StatusMessage.Dispose();
        base.Dispose();
    }
}
