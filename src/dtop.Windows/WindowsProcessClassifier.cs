using System.Diagnostics;
using System.Runtime.Versioning;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessClassifier : IProcessClassifier
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Windows.ProcessClassifier");
    public ProcessGroup Classify(Process process)
    {
        try
        {
            if (process.MainWindowHandle != nint.Zero)
            {
                return ProcessGroup.Apps;
            }

            if (process.SessionId == 0)
            {
                return ProcessGroup.Windows;
            }

            return ProcessGroup.Background;
        }
        catch (Exception ex) { Trace.Warning("WindowsProcessClassifier", "Failed to classify process: {0}", ex.Message); return ProcessGroup.Background; }
    }
}
