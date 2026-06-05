using System.Runtime.InteropServices;
using dottop.Platform.Windows;
using Xunit;

namespace dottop.Tests.Platform;

public class WindowsDiskMetricsTests
{
    [SkippableFact]
    public void Initialize_CreatesPerformanceCounters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        using var metrics = new WindowsDiskMetrics();
        metrics.Initialize();

        var (read, write, active) = metrics.GetMetrics("C:");
        Assert.InRange(active, 0, 100);
    }

    [SkippableFact]
    public void GetMetrics_UnknownDisk_ReturnsZeros()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        using var metrics = new WindowsDiskMetrics();
        metrics.Initialize();

        var (read, write, active) = metrics.GetMetrics("Z:");
        Assert.Equal(0UL, read);
        Assert.Equal(0UL, write);
        Assert.Equal(0, active);
    }
}
