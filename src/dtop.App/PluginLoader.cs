using System.Reflection;
using System.Runtime.Loader;
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
            try
            {
                plugin.Configure(builder);
            }
            catch
            {
                continue;
            }
            builders.Add(builder);
        }

        return new PluginRegistry(builders);
    }

    private static IReadOnlyList<IDtopPlugin> DiscoverPlugins()
    {
        var plugins = new List<IDtopPlugin>();

        // Scan plugins/ subdirectory with custom load context for dependencies
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (Directory.Exists(pluginsDir))
        {
            foreach (var dll in Directory.GetFiles(pluginsDir, "dtop.Plugin.*.dll"))
            {
                try
                {
                    var loadContext = new PluginLoadContext(dll);
                    var assembly = loadContext.LoadFromAssemblyPath(dll);
                    DiscoverInAssembly(assembly, plugins);
                }
                catch { }
            }
        }

        // Scan already-loaded assemblies (NuGet/ProjectReference during development)
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

internal sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext
{
    private readonly string _pluginDir = Path.GetDirectoryName(pluginPath)!;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var candidate = Path.Combine(_pluginDir, assemblyName.Name + ".dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}
