using System.Net.NetworkInformation;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Windows;

public sealed class WindowsNetworkMetrics : INetworkMetrics
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Windows.NetworkMetrics");
    private Dictionary<string, (long Rx, long Tx)>? _prevBytes;

    public IReadOnlyList<NetworkSnapshot> Measure()
    {
        try
        {
            var raw = ReadRawInterfaces();
            var (snapshots, nextState) = NetworkCalculator.BuildSnapshots(raw, _prevBytes);
            _prevBytes = nextState;
            return snapshots;
        }
        catch (Exception ex)
        {
            Trace.Warning("WindowsNetworkMetrics", "Failed to measure network interfaces: {0}", ex.Message);
            return [];
        }
    }

    private static List<NetworkCalculator.RawInterface> ReadRawInterfaces()
    {
        var result = new List<NetworkCalculator.RawInterface>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            var stats = ni.GetIPv4Statistics();
            result.Add(new NetworkCalculator.RawInterface(
                ni.Name,
                ni.OperationalStatus == OperationalStatus.Up,
                ni.Speed,
                stats.BytesReceived,
                stats.BytesSent));
        }
        return result;
    }
}
