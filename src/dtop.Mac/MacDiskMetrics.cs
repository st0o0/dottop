using System.Diagnostics;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Mac;

public sealed class MacDiskMetrics : IDiskMetrics
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Mac.DiskMetrics");

    public void Initialize() { }

    public (ulong ReadBytesPerSec, ulong WriteBytesPerSec, double ActivePercent) GetMetrics(string diskName)
    {
        try
        {
            // Run iostat for disk I/O
            var psi = new ProcessStartInfo("iostat", "-d -c 1")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(3000);

            // iostat output: disk0 KB/t tps MB/s
            // Parse the last data line for transfer rates
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length >= 3)
            {
                var parts = lines[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && double.TryParse(parts[^1], out var mbps))
                {
                    var bytesPerSec = (ulong)(mbps * 1024 * 1024);
                    return (bytesPerSec / 2, bytesPerSec / 2, mbps > 0 ? Math.Min(100, mbps * 10) : 0);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.Warning(this, "iostat failed: {0}", ex.Message);
        }
        return (0, 0, 0);
    }

    public void Dispose() { }
}
