using dtop.Docker;
using dtop.Setup;
using Microsoft.Extensions.Hosting;
using Servus.Application.Startup;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var dockerProvider = new DockerProvider();
var dockerAvailable = await dockerProvider.IsAvailableAsync();

var runner = AppBuilder.Create(Host.CreateApplicationBuilder(args), b => b.Build())
    .WithSetup(new DockerSetup(dockerProvider, dockerAvailable))
    .WithSetup<LoggingSetup>()
    .WithSetup<PlatformSetup>()
    .WithSetup<ServicesSetup>()
    .WithSetup<PluginSetup>()
    .WithSetup<ActorSystemSetup>()
    .WithSetup<TerminaSetup>()
    .Build();

await runner.RunAsync();
