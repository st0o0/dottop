using dtop.Core.Models;
using dtop.Core.Platform;

namespace dtop.Mac;

public sealed class MacConnectionProvider : IConnectionProvider
{
    public List<ConnectionSnapshot> GetConnections() => [];
}
