using dtop.Core.Models;

namespace dtop.Core.Platform;

public interface IConnectionProvider
{
    List<ConnectionSnapshot> GetConnections();
}
