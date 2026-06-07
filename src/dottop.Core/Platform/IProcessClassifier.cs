using System.Diagnostics;
using dottop.Core.Models;

namespace dottop.Core.Platform;

public interface IProcessClassifier
{
    ProcessGroup Classify(Process process);
}
