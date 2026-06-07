using System.Net.NetworkInformation;
using dottop.Core.Models;
using dottop.Core.Platform;

namespace dottop.Windows;

public sealed class WindowsNetworkMetrics : INetworkMetrics
{
    private Dictionary<string, (long Rx, long Tx)>? _prevBytes;

    public IReadOnlyList<NetworkSnapshot> Measure()
    {
        var currentBytes = new Dictionary<string, (long Rx, long Tx)>();
        var nets = new List<NetworkSnapshot>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.Speed == 0) continue;
                var stats = ni.GetIPv4Statistics();
                var name = ni.Name.Length > 20 ? ni.Name[..20] + "..." : ni.Name;
                currentBytes[name] = (stats.BytesReceived, stats.BytesSent);
                ulong rxPerSec = 0, txPerSec = 0;
                if (_prevBytes is not null && _prevBytes.TryGetValue(name, out var prev))
                {
                    rxPerSec = (ulong)Math.Max(0, stats.BytesReceived - prev.Rx);
                    txPerSec = (ulong)Math.Max(0, stats.BytesSent - prev.Tx);
                }
                nets.Add(new NetworkSnapshot(name, rxPerSec, txPerSec));
            }
        }
        catch { }
        _prevBytes = currentBytes;
        return nets;
    }
}
