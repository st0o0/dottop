using dottop.Core.Models;

namespace dottop.Core.Platform;

public interface INetworkMetrics
{
    IReadOnlyList<NetworkSnapshot> Measure();
}
