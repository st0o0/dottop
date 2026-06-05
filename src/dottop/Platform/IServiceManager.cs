using dottop.Models;

namespace dottop.Platform;

public interface IServiceManager
{
    List<ServiceInfo> GetServices();
    string Start(string name);
    string Stop(string name);
    string Restart(string name);
}
