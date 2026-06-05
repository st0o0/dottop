using System.Diagnostics;
using System.Runtime.InteropServices;
using dottop.Platform.Windows;
using Xunit;

namespace dottop.Tests.Platform;

public class WindowsProcessTreeTests
{
    [SkippableFact]
    public void BuildTree_CurrentProcess_ReturnsValidTree()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var provider = new WindowsProcessTree();
        var currentPid = Process.GetCurrentProcess().Id;

        var tree = provider.BuildTree(currentPid);

        Assert.Equal(currentPid, tree.Pid);
        Assert.NotEmpty(tree.Name);
    }

    [SkippableFact]
    public void BuildTree_ExplorerProcess_HasChildren()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var provider = new WindowsProcessTree();
        var explorer = Process.GetProcessesByName("explorer").FirstOrDefault();
        Skip.If(explorer is null, "explorer.exe not running");

        var tree = provider.BuildTree(explorer.Id);

        Assert.Equal(explorer.Id, tree.Pid);
        Assert.Contains("explorer", tree.Name, StringComparison.OrdinalIgnoreCase);
    }
}
