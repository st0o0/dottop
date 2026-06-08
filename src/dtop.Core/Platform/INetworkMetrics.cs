using dtop.Core.Models;

namespace dtop.Core.Platform;

public interface INetworkMetrics
{
    IReadOnlyList<NetworkSnapshot> Measure();
}
