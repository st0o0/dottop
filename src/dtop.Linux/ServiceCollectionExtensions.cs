using dtop.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace dtop.Linux;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLinuxPlatform(this IServiceCollection services)
    {
        services.AddSingleton<ICpuMetrics, LinuxCpuMetrics>();
        services.AddSingleton<IMemoryMetrics, LinuxMemoryMetrics>();
        services.AddSingleton<IDiskMetrics, LinuxDiskMetrics>();
        services.AddSingleton<INetworkMetrics, LinuxNetworkMetrics>();
        services.AddSingleton<IProcessClassifier, LinuxProcessClassifier>();
        services.AddSingleton<IProcessTreeProvider, LinuxProcessTree>();
        services.AddSingleton<IServiceManager, LinuxServiceManager>();
        services.AddSingleton<IConnectionProvider, LinuxConnectionProvider>();
        return services;
    }
}
