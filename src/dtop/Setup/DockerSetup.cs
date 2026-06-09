using dtop.Core.Platform;
using dtop.Docker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus.Application.Startup;

namespace dtop.Setup;

public sealed class DockerSetup(DockerProvider dockerProvider, bool isAvailable) : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        if (isAvailable)
        {
            services.AddSingleton<IDockerProvider>(dockerProvider);
        }

        services.AddSingleton(new DockerAvailability(isAvailable));
    }
}

public sealed record DockerAvailability(bool IsAvailable);
