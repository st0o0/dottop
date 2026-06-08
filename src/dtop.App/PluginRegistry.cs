using dtop.Plugin;

namespace dtop.App;

public sealed class PluginRegistry
{
    public IReadOnlyList<PluginBuilder> LoadedPlugins { get; }
    private readonly List<PluginTabInfo> _pluginTabs;
    public IReadOnlyList<PluginTabInfo> PluginTabs => _pluginTabs;

    public PluginRegistry(IReadOnlyList<PluginBuilder> plugins)
    {
        LoadedPlugins = plugins;
        _pluginTabs = plugins.Where(p => p.Tab is not null).Select(p => p.Tab!).ToList();
    }

    /// <summary>
    /// Register a built-in tab (e.g. Docker) that is not loaded via plugin discovery.
    /// </summary>
    public void AddBuiltInTab(PluginTabInfo tab)
    {
        _pluginTabs.Add(tab);
    }
}
