using dtop.Core.Models;
using dtop.Core.Platform;

namespace dtop.Mac;

public sealed class MacNetworkMetrics : INetworkMetrics
{
    public IReadOnlyList<NetworkSnapshot> Measure() => [];
}
