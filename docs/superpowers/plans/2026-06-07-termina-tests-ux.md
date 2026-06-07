# Termina Integration Tests + UX Improvements — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build integration tests for all 5 dottop pages using Termina's VirtualTerminal + VirtualInputSource, fixing UX issues discovered along the way.

**Architecture:** A shared test fixture creates a Host with VirtualTerminal (for output assertions), VirtualInputSource (for key simulation), and a TestSupervisorActor (mocked Akka actor returning fixed data). Each test class covers one page, driving the UI with key events and asserting on rendered screen content.

**Tech Stack:** .NET 10.0, xUnit, FluentAssertions, Termina (VirtualTerminal, VirtualInputSource), Akka.NET (TestSupervisorActor)

---

## File Map

### New Files

| File | Responsibility |
|---|---|
| `tests/dottop.UI.Tests/dottop.UI.Tests.csproj` | Test project referencing dottop.App + dottop.Core + Termina |
| `tests/dottop.UI.Tests/Fixtures/DottopAppFixture.cs` | Shared fixture: builds Host with VirtualTerminal + VirtualInput + mocked actors |
| `tests/dottop.UI.Tests/Fixtures/TestSupervisorActor.cs` | Akka actor returning fixed test data for all message types |
| `tests/dottop.UI.Tests/Fixtures/TestData.cs` | Static test data (processes, services, connections, CPU snapshots) |
| `tests/dottop.UI.Tests/Helpers/ScreenAssert.cs` | Assertion helpers for VirtualTerminal |
| `tests/dottop.UI.Tests/NavigationTests.cs` | Cross-page tab navigation tests |
| `tests/dottop.UI.Tests/ProcessesPageTests.cs` | Processes page: list, search, sort, group, overlay, kill |
| `tests/dottop.UI.Tests/ServicesPageTests.cs` | Services page: list, search, detail modal + actions fix |
| `tests/dottop.UI.Tests/NetworkPageTests.cs` | Network page: list, search + detail modal (new) |
| `tests/dottop.UI.Tests/PerformancePageTests.cs` | Performance page: panels, detail modal + hints fix |
| `tests/dottop.UI.Tests/SettingsPageTests.cs` | Settings page: navigation, value changes + Home/End fix |

### Modified Files (UX Fixes)

| File | Change |
|---|---|
| `src/dottop.App/Pages/ProcessesViewModel.cs` | Unify search bar format |
| `src/dottop.App/Pages/ProcessesPage.cs` | Unify search bar rendering |
| `src/dottop.App/Pages/ServicesViewModel.cs` | Add S/X/R key handling inside detail modal |
| `src/dottop.App/Pages/NetworkPage.cs` | Add detail modal layout |
| `src/dottop.App/Pages/NetworkViewModel.cs` | Add detail modal state + Enter/Escape handling |
| `src/dottop.App/Pages/PerformancePage.cs` | Add keyboard hints to status bar |
| `src/dottop.App/Pages/SettingsViewModel.cs` | Add Home/End key handling |

---

## Task 1: Test Project + Fixture + Helpers

**Files:**
- Create: `tests/dottop.UI.Tests/dottop.UI.Tests.csproj`
- Create: `tests/dottop.UI.Tests/Fixtures/TestData.cs`
- Create: `tests/dottop.UI.Tests/Fixtures/TestSupervisorActor.cs`
- Create: `tests/dottop.UI.Tests/Fixtures/DottopAppFixture.cs`
- Create: `tests/dottop.UI.Tests/Helpers/ScreenAssert.cs`
- Modify: `src/dottop.slnx` (add test project)

- [ ] **Step 1: Create test project**

```xml
<!-- tests/dottop.UI.Tests/dottop.UI.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\dottop.App\dottop.App.csproj" />
    <ProjectReference Include="..\..\src\dottop.Core\dottop.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Akka.Hosting" Version="1.5.68" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create TestData with fixed test data**

```csharp
// tests/dottop.UI.Tests/Fixtures/TestData.cs
using dottop.Core.Messages;
using dottop.Core.Models;

namespace dottop.UI.Tests.Fixtures;

public static class TestData
{
    public static CpuSnapshot CpuSnapshot => new("Test CPU i7-13700K", 42.5, [35.0, 50.0, 20.0, 65.0]);

    public static MemorySnapshot MemorySnapshot => new(17_179_869_184, 8_589_934_592); // 16 GB total, 8 GB used

    public static List<ProcessSnapshot> Processes =>
    [
        new(1234, "chrome", ProcessGroup.Apps, 25.3, 524_288_000, 0, 0, 12, 200, "", 0),
        new(5678, "code", ProcessGroup.Apps, 8.1, 1_073_741_824, 0, 0, 30, 500, "", 0),
        new(9012, "svchost", ProcessGroup.Windows, 1.2, 52_428_800, 0, 0, 8, 100, "", 0),
        new(3456, "explorer", ProcessGroup.Windows, 0.5, 104_857_600, 0, 0, 15, 300, "", 0),
        new(7890, "spotify", ProcessGroup.Apps, 85.0, 209_715_200, 0, 0, 20, 150, "", 0),
    ];

    public static List<ServiceInfo> Services =>
    [
        new("wuauserv", "Windows Update", ServiceStatus.Running, ServiceStartType.Automatic, 1100),
        new("spooler", "Print Spooler", ServiceStatus.Stopped, ServiceStartType.Manual, null),
        new("w32time", "Windows Time", ServiceStatus.Running, ServiceStartType.Automatic, 1200),
    ];

    public static List<ConnectionSnapshot> Connections =>
    [
        new("chrome", 1234, "192.168.1.100:54321", "142.250.185.206:443", "Established", "TCP"),
        new("svchost", 9012, "0.0.0.0:135", "0.0.0.0:0", "Listen", "TCP"),
        new("spotify", 7890, "192.168.1.100:55555", "35.186.224.25:4070", "TimeWait", "TCP"),
    ];

    public static List<DiskSnapshot> Disks =>
    [
        new("C:", 500_000_000_000, 200_000_000_000, 50_000_000, 25_000_000, 35.0),
        new("D:", 1_000_000_000_000, 750_000_000_000, 0, 0, 0),
    ];

    public static List<NetworkSnapshot> Networks =>
    [
        new("Ethernet", 1_500_000, 500_000),
        new("Wi-Fi", 0, 0),
    ];

    public static GpuSnapshot GpuSnapshot => new("N/A", 0, 0, 0, 0);
}
```

- [ ] **Step 3: Create TestSupervisorActor**

This Akka actor mocks the MonitoringSupervisor, responding to all message types with fixed data. For monitoring commands, it creates a channel that sends one snapshot then stays open.

```csharp
// tests/dottop.UI.Tests/Fixtures/TestSupervisorActor.cs
using System.Threading.Channels;
using Akka.Actor;
using dottop.Core.Messages;
using dottop.Core.Models;

namespace dottop.UI.Tests.Fixtures;

public sealed class TestSupervisorActor : ReceiveActor
{
    public TestSupervisorActor()
    {
        Receive<StartCpuMonitoring>(_ => SendStream(TestData.CpuSnapshot));
        Receive<StartMemoryMonitoring>(_ => SendStream(TestData.MemorySnapshot));
        Receive<StartDiskMonitoring>(_ => SendStream(TestData.Disks));
        Receive<StartNetworkMonitoring>(_ => SendStream(TestData.Networks));
        Receive<StartGpuMonitoring>(_ => SendStream(TestData.GpuSnapshot));
        Receive<StartProcessMonitoring>(_ => SendStream(TestData.Processes));

        Receive<GetServices>(_ => Sender.Tell(TestData.Services));
        Receive<StartService>(msg => Sender.Tell(new ActionSuccess($"Started {msg.Name}")));
        Receive<StopService>(msg => Sender.Tell(new ActionSuccess($"Stopped {msg.Name}")));
        Receive<RestartService>(msg => Sender.Tell(new ActionSuccess($"Restarted {msg.Name}")));

        Receive<KillProcess>(msg => Sender.Tell(new ActionSuccess($"Killed {msg.Pid}")));
        Receive<GetProcessTree>(msg =>
            Sender.Tell(new ProcessTreeResult(msg.Pid, "test", [])));
        Receive<GetProcessEnvironment>(_ =>
            Sender.Tell(new ProcessEnvironmentResult(
                new Dictionary<string, string> { ["PATH"] = "/usr/bin", ["HOME"] = "/home/test" })));
        Receive<GetProcessHandles>(_ =>
            Sender.Tell(new ProcessHandlesResult(["module1.dll", "module2.dll"])));
    }

    private void SendStream<T>(T data)
    {
        var cts = new CancellationTokenSource();
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        channel.Writer.TryWrite(data);

        async IAsyncEnumerable<T> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
                yield return item;
        }

        Sender.Tell(new MonitoringStream<T>(ReadAsync(cts.Token), cts));
    }

    private void SendStream<T>(List<T> data) where T : class
    {
        var cts = new CancellationTokenSource();
        var channel = Channel.CreateBounded<List<T>>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        channel.Writer.TryWrite(data);

        async IAsyncEnumerable<List<T>> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
                yield return item;
        }

        Sender.Tell(new MonitoringStream<List<T>>(ReadAsync(cts.Token), cts));
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new TestSupervisorActor());
}
```

- [ ] **Step 4: Create DottopAppFixture**

The fixture builds a real Host with VirtualTerminal + VirtualInput + TestSupervisorActor. It registers VirtualTerminal as `IAnsiTerminal` BEFORE calling `AddTermina` (since Termina uses `TryAddSingleton` which skips if already registered).

```csharp
// tests/dottop.UI.Tests/Fixtures/DottopAppFixture.cs
using Akka.Actor;
using Akka.Hosting;
using dottop.Actors;
using dottop.Core.Platform;
using dottop.Pages;
using dottop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Termina.Hosting;
using Termina.Input;
using Termina.Pages;
using Termina.Terminal;

namespace dottop.UI.Tests.Fixtures;

public sealed class DottopAppFixture : IAsyncDisposable
{
    public VirtualTerminal Terminal { get; }
    public VirtualInputSource Input { get; }
    private IHost? _host;
    private Task? _runTask;

    public DottopAppFixture(int width = 120, int height = 30)
    {
        Terminal = new VirtualTerminal(width, height);
        Input = new VirtualInputSource();
    }

    public async Task StartAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        // Register VirtualTerminal BEFORE AddTermina (TryAddSingleton won't overwrite)
        builder.Services.AddSingleton<IAnsiTerminal>(Terminal);
        builder.Services.AddTerminaVirtualInput(Input);

        // Mock platform services
        builder.Services.AddSingleton(Substitute.For<ICpuMetrics>());
        builder.Services.AddSingleton(Substitute.For<IMemoryMetrics>());
        builder.Services.AddSingleton(Substitute.For<IDiskMetrics>());
        builder.Services.AddSingleton(Substitute.For<INetworkMetrics>());
        builder.Services.AddSingleton<IGpuMetrics>(NoGpuMetrics.Instance);
        builder.Services.AddSingleton(Substitute.For<IProcessClassifier>());
        builder.Services.AddSingleton(Substitute.For<IProcessTreeProvider>());
        builder.Services.AddSingleton(Substitute.For<IServiceManager>());
        builder.Services.AddSingleton(Substitute.For<IConnectionProvider>());

        // Settings
        var settingsService = new SettingsService();
        settingsService.Load();
        builder.Services.AddSingleton(settingsService);
        builder.Services.AddSingleton(new UpdateService());

        // Akka with TestSupervisorActor
        builder.Services.AddAkka("dottop-test", configurationBuilder =>
        {
            configurationBuilder.WithActors((system, registry) =>
            {
                var supervisor = system.ActorOf(TestSupervisorActor.Props(), "monitoring-supervisor");
                registry.Register<MonitoringSupervisor>(supervisor);
            });
        });

        // Termina pages
        builder.Services.AddTermina("/", termina =>
        {
            termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
            termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
            termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
            termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);
            termina.RegisterRoute<SettingsPage, SettingsViewModel>("/settings", NavigationBehavior.PreserveState);
        });

        _host = builder.Build();
        _runTask = _host.RunAsync();

        // Wait for rendering to complete
        await Task.Delay(500);
    }

    public async Task SendKeysAsync(params ConsoleKey[] keys)
    {
        foreach (var key in keys)
        {
            Input.EnqueueKey(key);
            await Task.Delay(150);
        }
    }

    public async Task SendStringAsync(string text)
    {
        Input.EnqueueString(text);
        await Task.Delay(200);
    }

    public async Task WaitForRenderAsync(int ms = 300)
    {
        await Task.Delay(ms);
    }

    public async ValueTask DisposeAsync()
    {
        Input.Complete();
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }
    }
}
```

- [ ] **Step 5: Create ScreenAssert helpers**

```csharp
// tests/dottop.UI.Tests/Helpers/ScreenAssert.cs
using FluentAssertions;
using Termina.Terminal;

namespace dottop.UI.Tests.Helpers;

public static class ScreenAssert
{
    public static void Contains(VirtualTerminal terminal, string text)
    {
        terminal.Contains(text).Should().BeTrue(
            $"expected screen to contain \"{text}\" but got:\n{terminal}");
    }

    public static void DoesNotContain(VirtualTerminal terminal, string text)
    {
        terminal.Contains(text).Should().BeFalse(
            $"expected screen NOT to contain \"{text}\" but it was found:\n{terminal}");
    }

    public static void LineContains(VirtualTerminal terminal, int line, string text)
    {
        var lineText = terminal.GetLine(line);
        lineText.Should().Contain(text,
            $"expected line {line} to contain \"{text}\" but got: \"{lineText}\"");
    }

    public static void LineDoesNotContain(VirtualTerminal terminal, int line, string text)
    {
        var lineText = terminal.GetLine(line);
        lineText.Should().NotContain(text,
            $"expected line {line} NOT to contain \"{text}\" but got: \"{lineText}\"");
    }

    public static async Task WaitForTextAsync(VirtualTerminal terminal, string text, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (terminal.Contains(text)) return;
            await Task.Delay(50);
        }
        terminal.Contains(text).Should().BeTrue(
            $"timed out waiting for \"{text}\" on screen after {timeoutMs}ms:\n{terminal}");
    }
}
```

- [ ] **Step 6: Add test project to solution**

Update `src/dottop.slnx`:
```xml
<Solution>
  <Project Path="dottop.Core/dottop.Core.csproj" />
  <Project Path="dottop.Windows/dottop.Windows.csproj" />
  <Project Path="dottop.Linux/dottop.Linux.csproj" />
  <Project Path="dottop.App/dottop.App.csproj" />
  <Project Path="..\tests\dottop.Actors.Tests\dottop.Actors.Tests.csproj" />
  <Project Path="..\tests\dottop.UI.Tests\dottop.UI.Tests.csproj" />
</Solution>
```

- [ ] **Step 7: Verify project builds**

Run: `dotnet build tests/dottop.UI.Tests/dottop.UI.Tests.csproj`
Expected: Build succeeds.

- [ ] **Step 8: Commit**

```bash
git add tests/dottop.UI.Tests/ src/dottop.slnx
git commit -m "feat: add UI test infrastructure with VirtualTerminal, VirtualInput, and TestSupervisorActor"
```

---

## Task 2: Navigation Tests

**Files:**
- Create: `tests/dottop.UI.Tests/NavigationTests.cs`

- [ ] **Step 1: Write navigation tests**

```csharp
// tests/dottop.UI.Tests/NavigationTests.cs
using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;
using FluentAssertions;

namespace dottop.UI.Tests;

public class NavigationTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task App_StartsOnProcessesPage()
    {
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "Processes");
        ScreenAssert.Contains(_app.Terminal, "PID");
        ScreenAssert.Contains(_app.Terminal, "Name");
    }

    [Fact]
    public async Task D2_NavigatesToPerformance()
    {
        await _app.SendKeysAsync(ConsoleKey.D2);
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "CPU");
        ScreenAssert.Contains(_app.Terminal, "RAM");
    }

    [Fact]
    public async Task D3_NavigatesToServices()
    {
        await _app.SendKeysAsync(ConsoleKey.D3);
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "Services");
    }

    [Fact]
    public async Task D4_NavigatesToNetwork()
    {
        await _app.SendKeysAsync(ConsoleKey.D4);
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "Network");
    }

    [Fact]
    public async Task D5_NavigatesToSettings()
    {
        await _app.SendKeysAsync(ConsoleKey.D5);
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "Theme");
        ScreenAssert.Contains(_app.Terminal, "Refresh");
    }

    [Fact]
    public async Task TabNavigation_RoundTrip()
    {
        await _app.SendKeysAsync(ConsoleKey.D3);
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "Services");

        await _app.SendKeysAsync(ConsoleKey.D1);
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "PID");
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/dottop.UI.Tests/ --filter NavigationTests`

Fix any issues with the fixture (timing, registration, etc.) until tests pass. This is the first real integration test run — expect to iterate on DottopAppFixture timing and registration order.

- [ ] **Step 3: Commit**

```bash
git add tests/dottop.UI.Tests/NavigationTests.cs
git commit -m "test: add cross-page navigation integration tests"
```

---

## Task 3: Processes Page Tests + Search Bar Fix

**Files:**
- Create: `tests/dottop.UI.Tests/ProcessesPageTests.cs`
- Modify: `src/dottop.App/Pages/ProcessesPage.cs` (search bar rendering)

- [ ] **Step 1: Write processes page tests**

```csharp
// tests/dottop.UI.Tests/ProcessesPageTests.cs
using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;

namespace dottop.UI.Tests;

public class ProcessesPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsProcessList()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "code");
        ScreenAssert.Contains(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task Search_FiltersProcesses()
    {
        _app.Input.EnqueueKey(ConsoleKey.Oem2); // "/" key
        await _app.WaitForRenderAsync();

        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.DoesNotContain(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task Search_EscapeClearsSearch()
    {
        _app.Input.EnqueueKey(ConsoleKey.Oem2); // "/"
        await _app.WaitForRenderAsync();
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.Escape);
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "svchost"); // all visible again
    }

    [Fact]
    public async Task SearchBar_ShowsUnifiedFormat()
    {
        _app.Input.EnqueueKey(ConsoleKey.Oem2); // "/"
        await _app.WaitForRenderAsync();
        await _app.SendStringAsync("test");
        await _app.WaitForRenderAsync();

        // UX Fix: search bar should show "/ test" format (unified with Services/Network)
        ScreenAssert.Contains(_app.Terminal, "/ test");
    }

    [Fact]
    public async Task GroupFilter_CyclesThroughGroups()
    {
        await _app.WaitForRenderAsync(500);
        await _app.SendKeysAsync(ConsoleKey.G); // cycle to Apps
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "Apps");
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.DoesNotContain(_app.Terminal, "svchost"); // Windows group, filtered out
    }

    [Fact]
    public async Task Overlay_OpensOnEnter()
    {
        await _app.WaitForRenderAsync(500);
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "PID:");
        ScreenAssert.Contains(_app.Terminal, "CPU:");
    }

    [Fact]
    public async Task Overlay_LeftRightSwitchesTabs()
    {
        await _app.WaitForRenderAsync(500);
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.RightArrow); // Tab 1: Process Tree
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "Tree");

        await _app.SendKeysAsync(ConsoleKey.RightArrow); // Tab 2: Environment
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "PATH");
    }

    [Fact]
    public async Task Overlay_EscapeCloses()
    {
        await _app.WaitForRenderAsync(500);
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.Escape);
        await _app.WaitForRenderAsync();

        ScreenAssert.DoesNotContain(_app.Terminal, "PID:");
    }

    [Fact]
    public async Task ListNavigation_ArrowDownMovesSelection()
    {
        await _app.WaitForRenderAsync(500);

        await _app.SendKeysAsync(ConsoleKey.DownArrow);
        await _app.WaitForRenderAsync();

        // Second item should now be selected (visual check via screen content)
        // The selection is shown via background color — we verify list content is present
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "code");
    }
}
```

- [ ] **Step 2: Run tests, identify which pass and which fail**

Run: `dotnet test tests/dottop.UI.Tests/ --filter ProcessesPageTests`

The `SearchBar_ShowsUnifiedFormat` test will likely fail because the search bar currently uses a different format on the Processes page. This is the UX fix trigger.

- [ ] **Step 3: Fix search bar format in ProcessesPage**

Read `src/dottop.App/Pages/ProcessesPage.cs` and find the search bar rendering code. Update it to use the `"/ {searchText}"` format when search is active, matching the Services and Network pages. The inactive state can remain page-specific (showing group + sort info).

Look for the line that renders the search bar text — it will be in the layout definition or a text node callback. Change the active-search format from whatever it currently is to `$"/ {vm.SearchText.Value}"`.

- [ ] **Step 4: Run tests again**

Run: `dotnet test tests/dottop.UI.Tests/ --filter ProcessesPageTests`
Expected: All tests pass, including the search bar format test.

- [ ] **Step 5: Commit**

```bash
git add tests/dottop.UI.Tests/ProcessesPageTests.cs src/dottop.App/Pages/ProcessesPage.cs
git commit -m "test: add processes page integration tests; fix: unify search bar format"
```

---

## Task 4: Services Page Tests + Modal Actions Fix

**Files:**
- Create: `tests/dottop.UI.Tests/ServicesPageTests.cs`
- Modify: `src/dottop.App/Pages/ServicesViewModel.cs` (S/X/R in detail modal)

- [ ] **Step 1: Write services page tests**

```csharp
// tests/dottop.UI.Tests/ServicesPageTests.cs
using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;

namespace dottop.UI.Tests;

public class ServicesPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(ConsoleKey.D3); // Navigate to Services
        await _app.WaitForRenderAsync(500);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsServiceList()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Windows Update");
        ScreenAssert.Contains(_app.Terminal, "Print Spooler");
        ScreenAssert.Contains(_app.Terminal, "Windows Time");
    }

    [Fact]
    public async Task ShowsStatusIcons()
    {
        await _app.WaitForRenderAsync();
        // Running services show ▶, Stopped show ■
        // We verify the service names are present; icon rendering depends on exact position
        ScreenAssert.Contains(_app.Terminal, "Running");
    }

    [Fact]
    public async Task Search_FiltersServices()
    {
        _app.Input.EnqueueKey(ConsoleKey.Oem2); // "/"
        await _app.WaitForRenderAsync();
        await _app.SendStringAsync("Update");
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "Windows Update");
        ScreenAssert.DoesNotContain(_app.Terminal, "Print Spooler");
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "wuauserv"); // internal service name
        ScreenAssert.Contains(_app.Terminal, "Windows Update"); // display name
    }

    [Fact]
    public async Task DetailModal_StopAction_WorksInsideModal()
    {
        // UX Fix: S/X/R should work inside the detail modal
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.X); // Stop service
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "Stopped");
    }

    [Fact]
    public async Task DetailModal_EscapeCloses()
    {
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.Escape);
        await _app.WaitForRenderAsync();

        ScreenAssert.DoesNotContain(_app.Terminal, "wuauserv");
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/dottop.UI.Tests/ --filter ServicesPageTests`

The `DetailModal_StopAction_WorksInsideModal` test will fail because S/X/R keys don't currently work inside the detail modal.

- [ ] **Step 3: Add S/X/R handling to ServicesViewModel detail modal**

Read `src/dottop.App/Pages/ServicesViewModel.cs`. Find the `HandleKey` method. Currently when `IsDetailOpen` is true, only Escape is handled. Add S/X/R handlers:

When detail is open and a service is selected:
- `S` → send `StartService` to supervisor, update status message, refresh services list
- `X` → send `StopService` to supervisor, update status message, refresh services list
- `R` → send `RestartService` to supervisor, update status message, refresh services list

Use the same pattern as the main list handlers but operate on `SelectedService.Value` instead of the list selection.

- [ ] **Step 4: Run tests again**

Run: `dotnet test tests/dottop.UI.Tests/ --filter ServicesPageTests`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add tests/dottop.UI.Tests/ServicesPageTests.cs src/dottop.App/Pages/ServicesViewModel.cs
git commit -m "test: add services page integration tests; feat: enable S/X/R actions inside detail modal"
```

---

## Task 5: Network Page Tests + Detail Modal

**Files:**
- Create: `tests/dottop.UI.Tests/NetworkPageTests.cs`
- Modify: `src/dottop.App/Pages/NetworkViewModel.cs` (add detail modal state)
- Modify: `src/dottop.App/Pages/NetworkPage.cs` (add detail modal layout)

- [ ] **Step 1: Write network page tests**

```csharp
// tests/dottop.UI.Tests/NetworkPageTests.cs
using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;

namespace dottop.UI.Tests;

public class NetworkPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(ConsoleKey.D4); // Navigate to Network
        await _app.WaitForRenderAsync(500);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsConnectionList()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "Established");
        ScreenAssert.Contains(_app.Terminal, "TCP");
    }

    [Fact]
    public async Task Search_FiltersConnections()
    {
        _app.Input.EnqueueKey(ConsoleKey.Oem2); // "/"
        await _app.WaitForRenderAsync();
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.DoesNotContain(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        // UX Fix: Network page should have a detail modal
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        // Full endpoint addresses (not truncated)
        ScreenAssert.Contains(_app.Terminal, "192.168.1.100:54321");
        ScreenAssert.Contains(_app.Terminal, "142.250.185.206:443");
    }

    [Fact]
    public async Task DetailModal_ShowsFullInfo()
    {
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "1234"); // PID
        ScreenAssert.Contains(_app.Terminal, "TCP");
        ScreenAssert.Contains(_app.Terminal, "Established");
    }

    [Fact]
    public async Task DetailModal_EscapeCloses()
    {
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.Escape);
        await _app.WaitForRenderAsync();

        // Modal closed, back to list view
        ScreenAssert.DoesNotContain(_app.Terminal, "192.168.1.100:54321");
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/dottop.UI.Tests/ --filter NetworkPageTests`

`DetailModal_OpensOnEnter`, `DetailModal_ShowsFullInfo`, and `DetailModal_EscapeCloses` will fail because there's no detail modal on the Network page yet.

- [ ] **Step 3: Add detail modal to NetworkViewModel**

Read `src/dottop.App/Pages/NetworkViewModel.cs`. Add:

```csharp
public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
public ReactiveProperty<ConnectionSnapshot?> SelectedConnection { get; } = new(null);
```

In `HandleKey`, add Enter to open detail (set `IsDetailOpen=true`, `SelectedConnection` to current selection) and Escape to close (when `IsDetailOpen`, set to `false`).

Add a `Func<ConnectionSnapshot?>? GetSelectedItem { get; set; }` property (same pattern as ProcessesViewModel and ServicesViewModel) so the Page can wire up the list selection.

- [ ] **Step 4: Add detail modal layout to NetworkPage**

Read `src/dottop.App/Pages/NetworkPage.cs`. Add a stacked overlay (same pattern as ServicesPage) that shows:
- Process: `{ProcessName}` (PID: `{Pid}`)
- Protocol: `{Protocol}`
- Local: `{LocalEndpoint}` (full, not truncated)
- Remote: `{RemoteEndpoint}` (full, not truncated)
- State: `{State}`

Use `Conditional` node that shows when `vm.IsDetailOpen.Value` is true. Use rounded border with black backdrop, same as ServicesPage detail modal.

Wire `GetSelectedItem` to the DataListNode's selected item.

- [ ] **Step 5: Run tests again**

Run: `dotnet test tests/dottop.UI.Tests/ --filter NetworkPageTests`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add tests/dottop.UI.Tests/NetworkPageTests.cs src/dottop.App/Pages/NetworkViewModel.cs src/dottop.App/Pages/NetworkPage.cs
git commit -m "test: add network page integration tests; feat: add connection detail modal"
```

---

## Task 6: Performance Page Tests + Status Bar Hints

**Files:**
- Create: `tests/dottop.UI.Tests/PerformancePageTests.cs`
- Modify: `src/dottop.App/Pages/PerformancePage.cs` (status bar hints)

- [ ] **Step 1: Write performance page tests**

```csharp
// tests/dottop.UI.Tests/PerformancePageTests.cs
using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;

namespace dottop.UI.Tests;

public class PerformancePageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(ConsoleKey.D2); // Navigate to Performance
        await _app.WaitForRenderAsync(500);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsCpuAndRamPanels()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "CPU");
        ScreenAssert.Contains(_app.Terminal, "RAM");
    }

    [Fact]
    public async Task ShowsCpuData()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "42.5%");
    }

    [Fact]
    public async Task StatusBar_ShowsKeyboardHints()
    {
        // UX Fix: status bar should show keyboard hints
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "Enter");
        ScreenAssert.Contains(_app.Terminal, "Detail");
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "Test CPU");
    }

    [Fact]
    public async Task DetailModal_LeftRightCyclesSections()
    {
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.RightArrow); // RAM section
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "GiB");
    }

    [Fact]
    public async Task DetailModal_EscapeCloses()
    {
        await _app.SendKeysAsync(ConsoleKey.Enter);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.Escape);
        await _app.WaitForRenderAsync();

        // Back to main view with panels
        ScreenAssert.DoesNotContain(_app.Terminal, "Test CPU");
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/dottop.UI.Tests/ --filter PerformancePageTests`

`StatusBar_ShowsKeyboardHints` will fail because the status bar doesn't show hints.

- [ ] **Step 3: Add keyboard hints to Performance page status bar**

Read `src/dottop.App/Pages/PerformancePage.cs`. Find the status bar rendering. Change the static text to show keyboard hints:

```
"Enter: Detail | 1-5: Navigate | Q: Quit"
```

Render in `TextDim` color so it's not too prominent.

- [ ] **Step 4: Run tests again**

Run: `dotnet test tests/dottop.UI.Tests/ --filter PerformancePageTests`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add tests/dottop.UI.Tests/PerformancePageTests.cs src/dottop.App/Pages/PerformancePage.cs
git commit -m "test: add performance page integration tests; feat: add keyboard hints to status bar"
```

---

## Task 7: Settings Page Tests + Home/End Fix

**Files:**
- Create: `tests/dottop.UI.Tests/SettingsPageTests.cs`
- Modify: `src/dottop.App/Pages/SettingsViewModel.cs` (Home/End key handling)

- [ ] **Step 1: Write settings page tests**

```csharp
// tests/dottop.UI.Tests/SettingsPageTests.cs
using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;

namespace dottop.UI.Tests;

public class SettingsPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(ConsoleKey.D5); // Navigate to Settings
        await _app.WaitForRenderAsync(500);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsAllSettings()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Theme");
        ScreenAssert.Contains(_app.Terminal, "Refresh");
        ScreenAssert.Contains(_app.Terminal, "Sort");
        ScreenAssert.Contains(_app.Terminal, "Group");
        ScreenAssert.Contains(_app.Terminal, "Graph");
        ScreenAssert.Contains(_app.Terminal, "Language");
    }

    [Fact]
    public async Task ArrowDown_NavigatesRows()
    {
        await _app.SendKeysAsync(ConsoleKey.DownArrow);
        await _app.WaitForRenderAsync();
        // Second row (Refresh Rate) should be selected
        // Selection shown via background color; verify content is still visible
        ScreenAssert.Contains(_app.Terminal, "Refresh");
    }

    [Fact]
    public async Task ArrowRight_ChangesValue()
    {
        await _app.WaitForRenderAsync();
        await _app.SendKeysAsync(ConsoleKey.RightArrow); // Cycle theme: dark → light
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "light");
    }

    [Fact]
    public async Task Home_JumpsToFirstRow()
    {
        // UX Fix: Home key should jump to first row
        await _app.SendKeysAsync(ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(ConsoleKey.Home);
        await _app.WaitForRenderAsync();

        // First row (Theme) should be selected
        // Verify by changing value — if Theme is selected, Right changes theme
        await _app.SendKeysAsync(ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "light"); // Theme changed
    }

    [Fact]
    public async Task End_JumpsToLastRow()
    {
        // UX Fix: End key should jump to last row
        await _app.SendKeysAsync(ConsoleKey.End);
        await _app.WaitForRenderAsync();

        // Last row (Language) should be selected
        await _app.SendKeysAsync(ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync();
        ScreenAssert.Contains(_app.Terminal, "de"); // Language changed from system → de
    }

    [Fact]
    public async Task SaveIndicator_ShowsOnChange()
    {
        await _app.SendKeysAsync(ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "Saved");
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/dottop.UI.Tests/ --filter SettingsPageTests`

`Home_JumpsToFirstRow` and `End_JumpsToLastRow` will fail.

- [ ] **Step 3: Add Home/End handling to SettingsViewModel**

Read `src/dottop.App/Pages/SettingsViewModel.cs`. Find the key handling method. Add:

```csharp
case ConsoleKey.Home:
    SelectedIndex.Value = 0;
    break;
case ConsoleKey.End:
    SelectedIndex.Value = Settings.Count - 1;  // or whatever the max index is (5 for 6 items)
    break;
```

The exact property name for the selected row index needs to be read from the file — it might be `SelectedIndex`, `_selectedRow`, or similar.

- [ ] **Step 4: Run tests again**

Run: `dotnet test tests/dottop.UI.Tests/ --filter SettingsPageTests`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add tests/dottop.UI.Tests/SettingsPageTests.cs src/dottop.App/Pages/SettingsViewModel.cs
git commit -m "test: add settings page integration tests; feat: add Home/End navigation"
```

---

## Task 8: Final Verification

- [ ] **Step 1: Run all tests**

Run: `dotnet test src/dottop.slnx`
Expected: All actor tests + all UI tests pass.

- [ ] **Step 2: Run full build**

Run: `dotnet build src/dottop.slnx`
Expected: 0 errors.

- [ ] **Step 3: Verify the app runs**

Run: `dotnet run --project src/dottop.App/dottop.App.csproj`
Expected: App starts, all UX fixes visible (search bar unified, service modal actions, network detail modal, performance hints, settings Home/End).

- [ ] **Step 4: Update CI**

Check `.github/workflows/ci.yml` — tests should already run via `dotnet test` on the solution. If the UI test project needs platform-specific dependencies, add a note. The VirtualTerminal tests don't need a real terminal so they should work in CI headless.

- [ ] **Step 5: Commit any remaining changes**

```bash
git add -A
git commit -m "chore: final verification of UI tests and UX improvements"
```
