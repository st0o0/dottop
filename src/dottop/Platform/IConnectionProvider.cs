using dottop.Models;

namespace dottop.Platform;

public interface IConnectionProvider
{
    List<ConnectionSnapshot> GetConnections();
}
