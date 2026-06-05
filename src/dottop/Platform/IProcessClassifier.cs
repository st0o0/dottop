using System.Diagnostics;
using dottop.Models;

namespace dottop.Platform;

public interface IProcessClassifier
{
    ProcessGroup Classify(Process process);
}
