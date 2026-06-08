using dottop.Plugin.Abstractions;

namespace dottop.App;

public sealed class PluginRegistry
{
    public IReadOnlyList<PluginBuilder> LoadedPlugins { get; }
    public IReadOnlyList<PluginTabInfo> PluginTabs { get; }

    public PluginRegistry(IReadOnlyList<PluginBuilder> plugins)
    {
        LoadedPlugins = plugins;
        PluginTabs = plugins.Where(p => p.Tab is not null).Select(p => p.Tab!).ToList();
    }
}
