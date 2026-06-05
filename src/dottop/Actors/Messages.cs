namespace dottop.Actors;

// Monitor messages
public sealed record Tick;
public sealed record RefreshRateChanged(int IntervalMs);

// Process action requests
public sealed record KillProcess(int Pid);
public sealed record SetProcessPriority(int Pid, System.Diagnostics.ProcessPriorityClass Priority);
public sealed record SetProcessAffinity(int Pid, nint AffinityMask);
public sealed record GetProcessTree(int Pid);
public sealed record GetProcessEnvironment(int Pid);
public sealed record GetProcessHandles(int Pid);

// Process action responses
public sealed record ProcessTreeResult(int Pid, string Name, IReadOnlyList<ProcessTreeResult> Children);
public sealed record ProcessEnvironmentResult(IReadOnlyDictionary<string, string> Variables);
public sealed record ProcessHandlesResult(IReadOnlyList<string> Handles);
public sealed record ActionSuccess(string Message);
public sealed record ActionFailure(string Error);

// Service action requests
public sealed record GetServices;
public sealed record StartService(string Name);
public sealed record StopService(string Name);
public sealed record RestartService(string Name);

// Startup action requests
public sealed record GetStartupEntries;
public sealed record SetStartupEnabled(string Name, bool Enabled);
