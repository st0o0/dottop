using Akka.Hosting;
using dtop;
using dtop.Actors;
using dtop.Pages;
using dtop.Services;
using dtop.Core.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Termina.Hosting;
using Termina.Input;
using Termina.Pages;
using Termina.Terminal;

namespace dtop.UI.Tests.Fixtures;

/// <summary>
/// Creates a fully wired dtop host with virtual terminal and input for UI integration tests.
/// </summary>
public sealed class DtopAppFixture : IAsyncDisposable
{
    public VirtualTerminal Terminal { get; }
    public VirtualInputSource Input { get; }

    private IHost? _host;

    public DtopAppFixture(int width = 120, int height = 30)
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

        // --- Plugin registry with Docker tab (built-in) ---
        var testRefreshService = new RefreshService(TimeSpan.FromSeconds(1));
        var registry = new PluginRegistry([]);
        registry.AddBuiltInTab(new Plugin.PluginTabInfo("6:Docker", "/docker", ConsoleKey.D6));
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton<Plugin.ITickSource>(testRefreshService);
        dtop.Nodes.TabBarNode.RegisterPluginTabs(registry);

        // --- Akka with TestSupervisorActor ---
        builder.Services.AddAkka("dtop-test", configurationBuilder =>
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
                registry.Register<dtop.Actors.DockerMonitorActor>(dockerActor);
            });
        });

        // --- Termina with virtual input ---
        builder.Services.AddTerminaVirtualInput(Input);
        builder.Services.AddTermina("/", termina =>
        {
            termina.RegisterRoute<dtop.Pages.OverviewPage, dtop.Pages.OverviewViewModel>("/overview", NavigationBehavior.PreserveState);
            termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
            termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
            termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
            termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);
            termina.RegisterRoute<dtop.Pages.DockerPage, dtop.Pages.DockerViewModel>("/docker", NavigationBehavior.PreserveState);
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
