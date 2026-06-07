// src/dottop.Core/Messages/Commands.cs
using System.Diagnostics;

namespace dottop.Core.Messages;

public sealed record StartMonitoring;
public sealed record StopMonitoring;

public sealed record KillProcess(int Pid);
public sealed record SetProcessPriority(int Pid, ProcessPriorityClass Priority);
public sealed record SetProcessAffinity(int Pid, nint AffinityMask);

public sealed record StartService(string Name);
public sealed record StopService(string Name);
public sealed record RestartService(string Name);
