using R3;
using dottop.Core.Models;
using dottop.Core.Platform;
using dottop.Nodes;
using dottop.Resources;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class NetworkViewModel : ReactiveViewModel
{
    private readonly IConnectionProvider _connectionProvider;

    public IScrollableList? ListNode { get; set; }
    public Func<ConnectionSnapshot?>? GetSelectedItem { get; set; }

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    public ReactiveProperty<List<ConnectionSnapshot>> Connections { get; } = new([]);
    public ReactiveProperty<List<ConnectionSnapshot>> FilteredConnections { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<string> StatusMessage { get; } = new("");
    public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
    public ReactiveProperty<ConnectionSnapshot?> SelectedConnection { get; } = new(null);

    public NetworkViewModel(IConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public override void OnActivated()
    {
        RefreshConnections();
        SearchText.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);
        Observable.Interval(TimeSpan.FromSeconds(2))
            .Subscribe(_ => RefreshConnections())
            .DisposeWith(Subscriptions);
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private void RefreshConnections()
    {
        try
        {
            Connections.Value = _connectionProvider.GetConnections();
            ApplyFilter();
        }
        catch { }
    }

    private void ApplyFilter()
    {
        var source = Connections.Value.AsEnumerable();
        if (!string.IsNullOrEmpty(SearchText.Value))
        {
            source = source.Where(c =>
                c.ProcessName.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase) ||
                c.Pid.ToString().Contains(SearchText.Value) ||
                c.LocalEndpoint.Contains(SearchText.Value) ||
                c.RemoteEndpoint.Contains(SearchText.Value));
        }

        FilteredConnections.Value = source.ToList();
        StatusMessage.Value = string.Format(Strings.NetworkStatusFormat, FilteredConnections.Value.Count);
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsSearchActive.Value)
        {
            switch (key.KeyInfo.Key)
            {
                case ConsoleKey.Escape: IsSearchActive.Value = false; SearchText.Value = ""; break;
                case ConsoleKey.Backspace: if (SearchText.Value.Length > 0)
                    {
                        SearchText.Value = SearchText.Value[..^1];
                    }

                    break;
                default: if (key.KeyInfo.KeyChar is >= ' ' and <= '~')
                    {
                        SearchText.Value += key.KeyInfo.KeyChar;
                    }

                    break;
            }
            return;
        }
        if (IsDetailOpen.Value)
        {
            if (key.KeyInfo.Key == ConsoleKey.Escape)
            {
                IsDetailOpen.Value = false;
                SelectedConnection.Value = null;
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
            default:
                if (key.KeyInfo.KeyChar == '/')
                {
                    IsSearchActive.Value = true;
                }

                break;
            case ConsoleKey.Enter:
                if (GetSelectedItem?.Invoke() is { } conn)
                {
                    SelectedConnection.Value = conn;
                    IsDetailOpen.Value = true;
                    _detailContentChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D5: Navigate("/settings"); break;

            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    public override void Dispose()
    {
        Connections.Dispose(); FilteredConnections.Dispose();
        SearchText.Dispose(); IsSearchActive.Dispose(); StatusMessage.Dispose();
        IsDetailOpen.Dispose(); SelectedConnection.Dispose(); _detailContentChanged.Dispose();
        base.Dispose();
    }
}
