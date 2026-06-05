using System.Diagnostics;
using System.Runtime.InteropServices;
using dottop.Models;
using dottop.Platform.Windows;
using Xunit;

namespace dottop.Tests.Platform;

public class WindowsProcessClassifierTests
{
    [SkippableFact]
    public void Classify_CurrentProcess_ReturnsValidGroup()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var classifier = new WindowsProcessClassifier();
        var current = Process.GetCurrentProcess();

        var group = classifier.Classify(current);

        Assert.True(Enum.IsDefined(group));
    }

    [SkippableFact]
    public void Classify_SystemProcess_ReturnsWindowsGroup()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var classifier = new WindowsProcessClassifier();
        var system = Process.GetProcessesByName("System").FirstOrDefault();
        Skip.If(system is null, "System process not accessible");

        var group = classifier.Classify(system);

        Assert.Equal(ProcessGroup.Windows, group);
    }

    [SkippableFact]
    public void Classify_AllProcesses_NeverThrows()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var classifier = new WindowsProcessClassifier();

        foreach (var process in Process.GetProcesses().Take(50))
        {
            var group = classifier.Classify(process);
            Assert.True(Enum.IsDefined(group));
        }
    }
}
