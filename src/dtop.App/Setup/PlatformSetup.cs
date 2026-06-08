using dtop.Core.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus.Application.Startup;

namespace dtop.App.Setup;

public sealed class PlatformSetup : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        if (OperatingSystem.IsWindows())
            RegisterWindowsPlatform(services);
        else if (OperatingSystem.IsLinux())
            RegisterLinuxPlatform(services);

        RegisterGpu(services);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterWindowsPlatform(IServiceCollection services)
        => Windows.ServiceCollectionExtensions.AddWindowsPlatform(services);

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void RegisterLinuxPlatform(IServiceCollection services)
        => Linux.ServiceCollectionExtensions.AddLinuxPlatform(services);

    private static void RegisterGpu(IServiceCollection services)
    {
        IGpuMetrics gpu = NoGpuMetrics.Instance;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var nvml = new Windows.NvmlGpuMetrics();
                if (nvml.IsAvailable) gpu = nvml;
            }
            catch { /* NVML not available */ }
        }

        services.AddSingleton(gpu);
    }
}
