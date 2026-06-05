using System.Runtime.InteropServices;
using dottop.Models;
using dottop.Platform.Windows;
using Xunit;

namespace dottop.Tests.Platform;

public class WindowsServiceManagerTests
{
    [SkippableFact]
    public void GetServices_ReturnsNonEmptyList()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var manager = new WindowsServiceManager();
        var services = manager.GetServices();

        Assert.NotEmpty(services);
        Assert.All(services, s =>
        {
            Assert.NotEmpty(s.Name);
            Assert.NotEmpty(s.DisplayName);
        });
    }

    [SkippableFact]
    public void GetServices_ContainsKnownService()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var manager = new WindowsServiceManager();
        var services = manager.GetServices();

        Assert.Contains(services, s =>
            s.Name.Equals("Spooler", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Equals("WSearch", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Equals("BITS", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GetServices_IsSortedByDisplayName()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var manager = new WindowsServiceManager();
        var services = manager.GetServices();
        var expected = services.OrderBy(s => s.DisplayName).Select(s => s.Name).ToList();
        var actual = services.Select(s => s.Name).ToList();

        Assert.Equal(expected, actual);
    }
}
