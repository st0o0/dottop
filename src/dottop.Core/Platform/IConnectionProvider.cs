using dottop.Core.Models;

namespace dottop.Core.Platform;

public interface IConnectionProvider
{
    List<ConnectionSnapshot> GetConnections();
}
