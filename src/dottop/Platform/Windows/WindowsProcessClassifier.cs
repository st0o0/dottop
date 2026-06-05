using System.Diagnostics;
using System.Runtime.Versioning;
using dottop.Models;

namespace dottop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessClassifier : IProcessClassifier
{
    public ProcessGroup Classify(Process process)
    {
        try
        {
            if (process.MainWindowHandle != nint.Zero) return ProcessGroup.Apps;
            if (process.SessionId == 0) return ProcessGroup.Windows;
            return ProcessGroup.Background;
        }
        catch { return ProcessGroup.Background; }
    }
}
