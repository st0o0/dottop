using System.Reflection;
using dottop.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace dottop.App;

public static class PluginLoader
{
    public static PluginRegistry DiscoverAndConfigure(IServiceCollection services, ITickSource tickSource)
    {
        var plugins = DiscoverPlugins();
        var builders = new List<PluginBuilder>();

        foreach (var plugin in plugins)
        {
            var builder = new PluginBuilder(services, tickSource);
            plugin.Configure(builder);
            builders.Add(builder);
        }

        return new PluginRegistry(builders);
    }

    private static IReadOnlyList<IDottopPlugin> DiscoverPlugins()
    {
        var plugins = new List<IDottopPlugin>();

        var searchDirs = new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "plugins") };
        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var dll in Directory.GetFiles(dir, "dottop.Plugin.*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    DiscoverInAssembly(assembly, plugins);
                }
                catch { }
            }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            DiscoverInAssembly(asm, plugins);

        return plugins.DistinctBy(p => p.GetType().FullName).ToList();
    }

    private static void DiscoverInAssembly(Assembly assembly, List<IDottopPlugin> plugins)
    {
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IDottopPlugin).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
                    plugins.Add((IDottopPlugin)Activator.CreateInstance(type)!);
            }
        }
        catch { }
    }
}
