namespace dottop.Core.Models;

public record ConnectionSnapshot(
    string ProcessName,
    int Pid,
    string LocalEndpoint,
    string RemoteEndpoint,
    string State,
    string Protocol);
