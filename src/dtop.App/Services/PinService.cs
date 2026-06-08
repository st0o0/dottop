namespace dtop.App.Services;

public sealed class PinService
{
    private readonly HashSet<string> _pinnedProcesses = [];
    private readonly HashSet<string> _pinnedAdapters = [];
    private readonly HashSet<string> _pinnedContainers = [];

    public bool IsProcessPinned(int pid) => _pinnedProcesses.Contains(pid.ToString());
    public void ToggleProcessPin(int pid)
    {
        var key = pid.ToString();
        if (!_pinnedProcesses.Remove(key))
        {
            _pinnedProcesses.Add(key);
        }
    }
    public int PinnedProcessCount => _pinnedProcesses.Count;

    public bool IsAdapterPinned(string name) => _pinnedAdapters.Contains(name);
    public void ToggleAdapterPin(string name)
    {
        if (!_pinnedAdapters.Remove(name))
        {
            _pinnedAdapters.Add(name);
        }
    }

    public bool IsContainerPinned(string id) => _pinnedContainers.Contains(id);
    public void ToggleContainerPin(string id)
    {
        if (!_pinnedContainers.Remove(id))
        {
            _pinnedContainers.Add(id);
        }
    }
    public int PinnedContainerCount => _pinnedContainers.Count;

    public static IReadOnlyList<T> SortWithPinnedFirst<T>(IEnumerable<T> items, Func<T, bool> isPinned)
    {
        var pinned = new List<T>();
        var rest = new List<T>();
        foreach (var item in items)
        {
            if (isPinned(item))
            {
                pinned.Add(item);
            }
            else
            {
                rest.Add(item);
            }
        }
        pinned.AddRange(rest);
        return pinned;
    }
}
