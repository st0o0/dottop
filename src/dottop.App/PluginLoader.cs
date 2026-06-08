using System.Reflection;
using dottop.Plugin.Abstractions;

namespace dottop.App;

public static class PluginLoader
{
    public static IReadOnlyList<IDottopPlugin> DiscoverPlugins()
    {
        var plugins = new List<IDottopPlugin>();

        // 1. Scan plugins/ directory
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (Directory.Exists(pluginsDir))
        {
            foreach (var dll in Directory.GetFiles(pluginsDir, "dottop.Plugin.*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    DiscoverInAssembly(assembly, plugins);
                }
                catch
                {
                    // Skip assemblies that fail to load
                }
            }
        }

        // 2. Scan already-loaded assemblies (NuGet/ProjectReference)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            DiscoverInAssembly(asm, plugins);

        return plugins
            .DistinctBy(p => p.GetType().FullName)
            .OrderBy(p => p.Order)
            .ToList();
    }

    private static void DiscoverInAssembly(Assembly assembly, List<IDottopPlugin> plugins)
    {
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IDottopPlugin).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                    plugins.Add((IDottopPlugin)Activator.CreateInstance(type)!);
            }
        }
        catch
        {
            // Skip assemblies whose types cannot be enumerated
        }
    }
}
