using dottop.Actors;

namespace dottop.Platform;

public interface IProcessTreeProvider
{
    ProcessTreeResult BuildTree(int rootPid);
}
