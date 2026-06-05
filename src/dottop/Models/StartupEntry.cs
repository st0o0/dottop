namespace dottop.Models;

public record StartupEntry(
    string Name,
    string Publisher,
    bool Enabled,
    string Impact,
    string Path);
