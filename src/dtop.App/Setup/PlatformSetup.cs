using dtop.Core.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus;
using Servus.Application.Startup;
using Servus.Diagnostics;

namespace dtop.App.Setup;

public sealed class PlatformSetup : IServiceSetupContainer
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Setup.Platform");

    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        if (OperatingSystem.IsWindows())
        {
            RegisterWindowsPlatform(services);
        }
        else if (OperatingSystem.IsLinux())
        {
            RegisterLinuxPlatform(services);
        }
        else if (OperatingSystem.IsMacOS())
        {
            RegisterMacPlatform(services);
        }

        RegisterGpu(services);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterWindowsPlatform(IServiceCollection services)
        => Windows.ServiceCollectionExtensions.AddWindowsPlatform(services);

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void RegisterLinuxPlatform(IServiceCollection services)
        => Linux.ServiceCollectionExtensions.AddLinuxPlatform(services);

    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    private static void RegisterMacPlatform(IServiceCollection services)
        => Mac.ServiceCollectionExtensions.AddMacPlatform(services);

    private static void RegisterGpu(IServiceCollection services)
    {
        IGpuMetrics gpu = NoGpuMetrics.Instance;

        // Try NVML (NVIDIA) — works on Windows + Linux
        try
        {
            var nvml = new NvmlGpuMetrics();
            if (nvml.IsAvailable) gpu = nvml;
        }
        catch (Exception ex)
        {
            Trace.Warning("PlatformSetup", "NVML not available: {0}", ex.Message);
        }

        // Future: try AMD (ADLX/ROCm) if NVML didn't work

        // Try Apple GPU on macOS
        if (!gpu.IsAvailable && OperatingSystem.IsMacOS())
        {
            try
            {
                gpu = new Mac.MacGpuMetrics();
            }
            catch (Exception ex)
            {
                Trace.Warning("PlatformSetup", "Apple GPU not available: {0}", ex.Message);
            }
        }

        services.AddSingleton(gpu);
    }
}