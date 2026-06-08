namespace dottop.Plugin.Abstractions;

public interface IDottopPlugin
{
    string Name { get; }
    void Configure(IPluginBuilder builder);
}
