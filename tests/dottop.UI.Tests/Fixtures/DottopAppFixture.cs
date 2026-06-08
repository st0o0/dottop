using Akka.Hosting;
using dottop.App;
using dottop.App.Actors;
using dottop.App.Pages;
using dottop.App.Services;
using dottop.Core.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Termina.Hosting;
using Termina.Input;
using Termina.Pages;
using Termina.Terminal;

namespace dottop.UI.Tests.Fixtures;

/// <summary>
/// Creates a fully wired dottop host with virtual terminal and input for UI integration tests.
/// </summary>
public sealed class DottopAppFixture : IAsyncDisposable
{
    public VirtualTerminal Terminal { get; }
    public VirtualInputSource Input { get; }

    private IHost? _host;

    public DottopAppFixture(int width = 120, int height = 30)
    {
        Terminal = new VirtualTerminal(width, height);
        Input = new VirtualInputSource();
    }

    public async Task StartAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        // --- Virtual terminal BEFORE AddTermina (TryAddSingleton skips if already registered) ---
        builder.Services.AddSingleton<IAnsiTerminal>(Terminal);

        // --- Platform stubs via NSubstitute ---
        var cpuMetrics = Substitute.For<ICpuMetrics>();
        cpuMetrics.ProcessorName.Returns("Test CPU i7-13700K");
        cpuMetrics.CoreCount.Returns(4);

        var memoryMetrics = Substitute.For<IMemoryMetrics>();
        var diskMetrics = Substitute.For<IDiskMetrics>();
        var networkMetrics = Substitute.For<INetworkMetrics>();
        var processClassifier = Substitute.For<IProcessClassifier>();
        var processTreeProvider = Substitute.For<IProcessTreeProvider>();
        var serviceManager = Substitute.For<IServiceManager>();

        builder.Services.AddSingleton(cpuMetrics);
        builder.Services.AddSingleton(memoryMetrics);
        builder.Services.AddSingleton(diskMetrics);
        builder.Services.AddSingleton(networkMetrics);
        builder.Services.AddSingleton(processClassifier);
        builder.Services.AddSingleton(processTreeProvider);
        builder.Services.AddSingleton(serviceManager);

        // --- IConnectionProvider (used directly by NetworkViewModel) ---
        var connectionProvider = Substitute.For<IConnectionProvider>();
        connectionProvider.GetConnections().Returns(TestData.Connections);
        builder.Services.AddSingleton(connectionProvider);

        // --- GPU ---
        builder.Services.AddSingleton<IGpuMetrics>(NoGpuMetrics.Instance);

        // --- Settings + Update ---
        var settingsService = new SettingsService();
        builder.Services.AddSingleton(settingsService);
        builder.Services.AddSingleton(new UpdateService());
        builder.Services.AddSingleton(new PinService());

        // --- Plugin registry with Docker tab ---
        var testTickSource = new AppTickSource(TimeSpan.FromSeconds(1));
        var dockerBuilder = new PluginBuilder(builder.Services, testTickSource);
        dockerBuilder.WithTab("5:Docker", "/docker", ConsoleKey.D5);
        var registry = new PluginRegistry([dockerBuilder]);
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton<dottop.Plugin.Abstractions.ITickSource>(testTickSource);
        dottop.App.Nodes.TabBarNode.RegisterPluginTabs(registry);

        // --- Akka with TestSupervisorActor ---
        builder.Services.AddAkka("dottop-test", configurationBuilder =>
        {
            configurationBuilder.WithActors((system, registry) =>
            {
                var supervisor = system.ActorOf(
                    Akka.Actor.Props.Create<TestSupervisorActor>(),
                    "monitoring-supervisor");
                registry.Register<MonitoringSupervisor>(supervisor);

                var dockerActor = system.ActorOf(
                    Akka.Actor.Props.Create<TestSupervisorActor>(),
                    "docker-monitor");
                registry.Register<dottop.Plugin.Docker.DockerMonitorActor>(dockerActor);
            });
        });

        // --- Termina with virtual input ---
        builder.Services.AddTerminaVirtualInput(Input);
        builder.Services.AddTermina("/", termina =>
        {
            termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
            termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
            termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
            termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);
            termina.RegisterRoute<dottop.Plugin.Docker.DockerPage, dottop.Plugin.Docker.DockerViewModel>("/docker", NavigationBehavior.PreserveState);
        });

        _host = builder.Build();
        await _host.StartAsync();
        await Task.Delay(300);
    }

    /// <summary>
    /// Send individual key presses with a short delay between each.
    /// </summary>
    public async Task SendKeysAsync(int delayMs = 50, params ConsoleKey[] keys)
    {
        foreach (var key in keys)
        {
            Input.EnqueueKey(key);
            await Task.Delay(delayMs);
        }
    }

    /// <summary>
    /// Send a key with modifiers.
    /// </summary>
    public async Task SendKeyAsync(ConsoleKey key, bool shift = false, bool alt = false, bool control = false, int delayMs = 50)
    {
        Input.EnqueueKey(key, shift, alt, control);
        await Task.Delay(delayMs);
    }

    /// <summary>
    /// Send a string as character input with a delay after.
    /// </summary>
    public async Task SendStringAsync(string text, int delayMs = 50)
    {
        Input.EnqueueString(text);
        await Task.Delay(delayMs);
    }

    /// <summary>
    /// Wait for the terminal to process and render.
    /// </summary>
    public Task WaitForRenderAsync(int delayMs = 200)
        => Task.Delay(delayMs);

    public async ValueTask DisposeAsync()
    {
        Input.Complete();

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
    }
}
