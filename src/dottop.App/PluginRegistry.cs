using dottop.Plugin.Abstractions;

namespace dottop.App;

public sealed class PluginRegistry
{
    public IReadOnlyList<IDottopPlugin> Plugins { get; }
    public IReadOnlyList<PluginTabInfo> PluginTabs { get; }

    public PluginRegistry(IReadOnlyList<IDottopPlugin> plugins)
    {
        Plugins = plugins;
        PluginTabs = plugins
            .Where(p => p.TabInfo is not null)
            .Select(p => p.TabInfo!)
            .ToList();
    }
}
