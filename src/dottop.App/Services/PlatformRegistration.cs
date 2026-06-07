using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace dottop.Services;

internal static class PlatformRegistration
{
    public static void Register(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            RegisterWindows(services);
        }
        else if (OperatingSystem.IsLinux())
        {
            RegisterLinux(services);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterWindows(IServiceCollection services)
    {
        dottop.Windows.ServiceCollectionExtensions.AddWindowsPlatform(services);
    }

    private static void RegisterLinux(IServiceCollection services)
    {
        // dottop.Linux project is only referenced when building on Linux (conditional ProjectReference).
        // Use reflection to avoid compile errors on Windows where the assembly is not available.
        var asm = Assembly.Load("dottop.Linux");
        var type = asm.GetType("dottop.Linux.ServiceCollectionExtensions")!;
        var method = type.GetMethod("AddLinuxPlatform", BindingFlags.Public | BindingFlags.Static)!;
        method.Invoke(null, [services]);
    }
}
