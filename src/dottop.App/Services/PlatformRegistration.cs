using Microsoft.Extensions.DependencyInjection;

namespace dottop.Services;

internal static class PlatformRegistration
{
    public static void Register(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
            RegisterWindows(services);
        else if (OperatingSystem.IsLinux())
            RegisterLinux(services);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterWindows(IServiceCollection services)
    {
        Windows.ServiceCollectionExtensions.AddWindowsPlatform(services);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void RegisterLinux(IServiceCollection services)
    {
        Linux.ServiceCollectionExtensions.AddLinuxPlatform(services);
    }
}
