using System.Diagnostics;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Linux;

public sealed class LinuxProcessClassifier : IProcessClassifier
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Linux.ProcessClassifier");
    public ProcessGroup Classify(Process process)
    {
        try
        {
            // Check if process has a GUI by looking for DISPLAY/WAYLAND_DISPLAY in its environment
            // and whether it has a session ID > 0
            var pid = process.Id;

            // Session 0 processes are kernel/system services
            if (process.SessionId == 0)
            {
                return ProcessGroup.Windows;
            }

            // Try to detect GUI apps by checking /proc/{pid}/environ for DISPLAY or WAYLAND
            try
            {
                var environPath = $"/proc/{pid}/environ";
                if (File.Exists(environPath))
                {
                    var environ = File.ReadAllText(environPath);
                    if (environ.Contains("DISPLAY=") || environ.Contains("WAYLAND_DISPLAY="))
                    {
                        // Further check: does the cmdline suggest a GUI app?
                        var cmdlinePath = $"/proc/{pid}/cmdline";
                        if (File.Exists(cmdlinePath))
                        {
                            var cmdline = File.ReadAllText(cmdlinePath).Replace('\0', ' ').Trim();
                            if (!string.IsNullOrEmpty(cmdline) && !cmdline.StartsWith("/usr/lib/systemd"))
                            {
                                return ProcessGroup.Apps;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Trace.Warning("LinuxProcessClassifier", "Failed to read /proc environ for PID {0}: {1}", pid, ex.Message); }

            return ProcessGroup.Background;
        }
        catch (Exception ex) { Trace.Warning("LinuxProcessClassifier", "Failed to classify process: {0}", ex.Message); return ProcessGroup.Background; }
    }
}
