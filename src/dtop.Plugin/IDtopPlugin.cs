namespace dtop.Plugin;

public interface IDtopPlugin
{
    string Name { get; }
    void Configure(IPluginBuilder builder);
}
