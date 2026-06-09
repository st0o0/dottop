using System.Net.NetworkInformation;
using dtop.Core.Models;
using dtop.Core.Platform;

namespace dtop.Linux;

public sealed class LinuxNetworkMetrics : INetworkMetrics
{
    private Dictionary<string, (long Rx, long Tx)>? _prevBytes;

    public IReadOnlyList<NetworkSnapshot> Measure()
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
}
