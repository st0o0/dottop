using dtop.Core.Models;
using dtop.Core.Platform;

namespace dtop.Mac;

public sealed class MacServiceManager : IServiceManager
{
    public List<ServiceInfo> GetServices() => [];
    public string Start(string name) => "Not implemented";
    public string Stop(string name) => "Not implemented";
    public string Restart(string name) => "Not implemented";
}
