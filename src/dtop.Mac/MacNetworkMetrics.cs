using System.Net.NetworkInformation;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Mac;

public sealed class MacNetworkMetrics : INetworkMetrics
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Mac.NetworkMetrics");
    private Dictionary<string, (long Rx, long Tx)>? _prevBytes;

    public IReadOnlyList<NetworkSnapshot> Measure()
    {
        try
        {
            var raw = new List<NetworkCalculator.RawInterface>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                var stats = ni.GetIPv4Statistics();
                raw.Add(new NetworkCalculator.RawInterface(
                    ni.Name,
                    ni.OperationalStatus == OperationalStatus.Up,
                    ni.Speed,
                    stats.BytesReceived,
                    stats.BytesSent));
            }

            var (snapshots, nextState) = NetworkCalculator.BuildSnapshots(raw, _prevBytes);
            _prevBytes = nextState;
            return snapshots;
        }
        catch (Exception ex)
        {
            Trace.Warning(this, "Network measurement failed: {0}", ex.Message);
            return [];
        }
    }
}
