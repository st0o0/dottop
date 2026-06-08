using dtop.App.Setup;
using Microsoft.Extensions.Hosting;
using Servus.Application.Startup;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var runner = AppBuilder.Create(Host.CreateApplicationBuilder(args), b => b.Build())
    .WithSetup<LoggingSetup>()
    .WithSetup<PlatformSetup>()
    .WithSetup<ServicesSetup>()
    .WithSetup<DockerSetup>()
    .WithSetup<PluginSetup>()
    .WithSetup<ActorSystemSetup>()
    .WithSetup<TerminaSetup>()
    .Build();

await runner.RunAsync();
