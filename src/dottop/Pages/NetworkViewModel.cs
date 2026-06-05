using System.Net.NetworkInformation;
using R3;
using dottop.Nodes;
using dottop.Resources;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public record ConnectionInfo(string ProcessName, int Pid, string LocalEndpoint, string RemoteEndpoint, string State);

public class NetworkViewModel : ReactiveViewModel
{
    public IScrollableList? ListNode { get; set; }

    public ReactiveProperty<List<ConnectionInfo>> Connections { get; } = new([]);
    public ReactiveProperty<List<ConnectionInfo>> FilteredConnections { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<string> StatusMessage { get; } = new("");

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
            var props = IPGlobalProperties.GetIPGlobalProperties();
            var tcp = props.GetActiveTcpConnections()
                .Select(c => new ConnectionInfo("", 0, c.LocalEndPoint.ToString(), c.RemoteEndPoint.ToString(), c.State.ToString()));
            var listeners = props.GetActiveTcpListeners()
                .Select(l => new ConnectionInfo("", 0, l.ToString(), "*:*", "LISTEN"));
            Connections.Value = [..tcp, ..listeners];
            ApplyFilter();
        }
        catch { }
    }

    private void ApplyFilter()
    {
        var source = Connections.Value.AsEnumerable();
        if (!string.IsNullOrEmpty(SearchText.Value))
            source = source.Where(c =>
                c.ProcessName.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase) ||
                c.LocalEndpoint.Contains(SearchText.Value) ||
                c.RemoteEndpoint.Contains(SearchText.Value));
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
            default:
                if (key.KeyInfo.KeyChar == '/') IsSearchActive.Value = true;
                break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D3: Navigate("/services"); break;

            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    public override void Dispose()
    {
        Connections.Dispose(); FilteredConnections.Dispose();
        SearchText.Dispose(); IsSearchActive.Dispose(); StatusMessage.Dispose();
        base.Dispose();
    }
}
