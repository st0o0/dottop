using System.Runtime.Versioning;
using dottop.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace dottop.Windows;

public static class ServiceCollectionExtensions
{
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<ICpuMetrics, WindowsCpuMetrics>();
        services.AddSingleton<IMemoryMetrics, WindowsMemoryMetrics>();
        services.AddSingleton<IDiskMetrics, WindowsDiskMetrics>();
        services.AddSingleton<INetworkMetrics, WindowsNetworkMetrics>();
        services.AddSingleton<IProcessClassifier, WindowsProcessClassifier>();
        services.AddSingleton<IProcessTreeProvider, WindowsProcessTree>();
        services.AddSingleton<IServiceManager, WindowsServiceManager>();
        services.AddSingleton<IConnectionProvider, WindowsConnectionProvider>();
        return services;
    }
}
