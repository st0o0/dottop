using System.Reflection;
using dtop.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace dtop.App;

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

    private static IReadOnlyList<IDtopPlugin> DiscoverPlugins()
    {
        var plugins = new List<IDtopPlugin>();

        var searchDirs = new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "plugins") };
        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var dll in Directory.GetFiles(dir, "dtop.Plugin.*.dll"))
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

    private static void DiscoverInAssembly(Assembly assembly, List<IDtopPlugin> plugins)
    {
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IDtopPlugin).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
                    plugins.Add((IDtopPlugin)Activator.CreateInstance(type)!);
            }
        }
        catch { }
    }
}
