using dtop.App.Docker;
using dtop.Core.Platform;
using dtop.Plugin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus.Application.Startup;

namespace dtop.App.Setup;

public sealed class DockerSetup : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        var dockerProvider = new DockerProvider();
        var available = false;

        try
        {
            available = dockerProvider.IsAvailable;
        }
        catch
        {
            // Docker not available
        }

        if (available)
        {
            services.AddSingleton<IDockerProvider>(dockerProvider);
        }

        services.AddSingleton(new DockerAvailability(available));
    }
}

/// <summary>
/// Marker to share Docker availability state with later setup containers.
/// </summary>
public sealed record DockerAvailability(bool IsAvailable);
