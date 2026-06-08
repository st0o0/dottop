using dtop.Plugin;

namespace dtop.Plugin.Example;

public sealed class ExamplePlugin : IDtopPlugin
{
    public string Name => "Example";

    public void Configure(IPluginBuilder builder)
    {
        builder
            .WithTab("6:Example", "/example", ConsoleKey.D6)
            .ConfigureRoutes(ctx =>
            {
                ctx.RegisterRoute<ExamplePage, ExampleViewModel>("/example");
            });
    }
}
