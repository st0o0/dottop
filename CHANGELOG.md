# Changelog

## [0.6.0](https://github.com/st0o0/dtop/compare/v0.5.0...v0.6.0) (2026-06-08)


### Features

* add CPU history tracking and sparkline/bar formatter to process list ([e1d40f1](https://github.com/st0o0/dtop/commit/e1d40f1cd0f41ec67b5a846f4f1d4b525ba2f34f))
* add CPU, RAM, and Port columns to Docker container list ([df3a85e](https://github.com/st0o0/dtop/commit/df3a85e0cf8053fa46e685057e61ef54f87ad1e9))
* add CPU/RAM graphs to Docker detail, fix auto-update ([4ed3489](https://github.com/st0o0/dtop/commit/4ed34895256f8aa8a381f5c50504c771a44843fc))
* add D5 navigation to Docker tab in all ViewModels and update tests ([9ed2816](https://github.com/st0o0/dtop/commit/9ed28165c0d949a436a9f39d541989bb1d3d25d7))
* add Docker container tab with live monitoring, actions, and log preview ([2a62451](https://github.com/st0o0/dtop/commit/2a624519a5f78e98b28489079a1fd3628e622a4f))
* add Docker core types — ContainerSnapshot, commands, IDockerProvider ([ccba54f](https://github.com/st0o0/dtop/commit/ccba54f476211e6a5ba602835305842715cb91eb))
* add Docker network/volume/image models, messages, and provider interface ([33aea5a](https://github.com/st0o0/dtop/commit/33aea5ab54143332423de3131043c941123b1a50))
* add Docker sub-tabs (Container, Networks, Volumes, Images) ([379be8b](https://github.com/st0o0/dtop/commit/379be8b34406119d59556d2940eae228317d1d01))
* add DockerProvider and DockerMonitorActor ([0e05712](https://github.com/st0o0/dtop/commit/0e057122e128d22f4d8a6767082233570c41a725))
* add dtop.Mac project with CPU + Memory providers for macOS support ([0eeb695](https://github.com/st0o0/dtop/commit/0eeb69528836b076cc14e41d1b2557bc377fa7b2))
* add F10: Settings hint to all status bars ([ff06b7c](https://github.com/st0o0/dtop/commit/ff06b7cc427f663ae20b6705f36eae1c0e75cf13))
* add framed CPU and RAM graphs to process detail overlay ([130174f](https://github.com/st0o0/dtop/commit/130174f2f1471a7cefa1e4ad8b930f55c08a6e7d))
* add mac support & docker ([#12](https://github.com/st0o0/dtop/issues/12)) ([9f9d40b](https://github.com/st0o0/dtop/commit/9f9d40b7092b66ec9689ee8cf0177a9f235f13d2))
* add pin functionality to Processes and Docker views ([ed9f4e2](https://github.com/st0o0/dtop/commit/ed9f4e2e247d6642c3d415e6582d3b8e1739ee08))
* add pin mode for Processes and Docker containers ([1c93d38](https://github.com/st0o0/dtop/commit/1c93d38f5090b5f17e6b65cf79b561dca1e9005e))
* add pin mode to Performance Network detail view ([072ba51](https://github.com/st0o0/dtop/commit/072ba51d9fd800c0bf3a9c5e14097eeb9e7a2238))
* add PluginLoader, PluginRegistry, and dynamic TabBarNode ([e54bf36](https://github.com/st0o0/dtop/commit/e54bf36c1fc28024a8afac13ff6b5e8a163c8393))
* add scrolling to process tree tab via DataListNode ([a5ad784](https://github.com/st0o0/dtop/commit/a5ad784859e4701574367b187a67c8a4d8e66988))
* add SparklineRenderer and BarRenderer with unit tests ([0219132](https://github.com/st0o0/dtop/commit/02191320bea5b68d4a9739ea11434210c1532cd0))
* complete plugin system PoC — Docker as first plugin ([5759c55](https://github.com/st0o0/dtop/commit/5759c55400249660372625b1fc1b230073c955e0))
* complete process sparklines — localization, integration tests, and verification ([4b68af1](https://github.com/st0o0/dtop/commit/4b68af1abef1ddf71f6871a626ef5cf7032c52bf))
* create dottop.Plugin.Abstractions with IDottopPlugin interface ([0c82306](https://github.com/st0o0/dtop/commit/0c823068ead5dc86b6486df4ad94fc302bfbba30))
* create dottop.Plugin.Docker with extracted Docker code ([8bdb88e](https://github.com/st0o0/dtop/commit/8bdb88ee4ead3c37e6a30c49b71056c64ad75973))
* group Docker containers by Compose project with collapsible headers ([034bf1f](https://github.com/st0o0/dtop/commit/034bf1f2f6af3822a6fa8a2920c492119626d131))
* implement all macOS provider stubs with real platform APIs ([f4b8811](https://github.com/st0o0/dtop/commit/f4b88111b0fd3860e8dcfa6bbc0d50e3e5fbcb12))
* implement DockerProvider methods and DockerMonitorActor handlers for networks, volumes, images ([e799cb4](https://github.com/st0o0/dtop/commit/e799cb44d1212cf4474a979d58608c7d4f0f3d1f))
* integrate plugin system into Program.cs and remove Docker from dottop.App ([6943719](https://github.com/st0o0/dtop/commit/6943719aa65faad7ca388f755129f1116922188a))
* move CPU graph to process detail overlay, revert list to compact format ([8e7da0d](https://github.com/st0o0/dtop/commit/8e7da0dd19ccb688515bc264133523ba94730b92))
* move NvmlGpuMetrics to dtop.Core — NVIDIA GPU support on Windows + Linux ([0da0dd2](https://github.com/st0o0/dtop/commit/0da0dd22eeb29ca565ecbd7cad17a21c0d38ed3c))
* plugins/ directory deployment with PluginLoadContext ([febc129](https://github.com/st0o0/dtop/commit/febc129ce3fa20879eed8210c3f62bea979caa53))
* preserve sparkline colors on selected row ([fd23084](https://github.com/st0o0/dtop/commit/fd2308434628bab66931fc64a4c1ef4c46488d88))
* redesign Docker detail modal with panels and scrollable logs ([7e34b1a](https://github.com/st0o0/dtop/commit/7e34b1a051921d8c8597f495ff924228e846e2f5))
* S/X/R on Compose group header applies to all containers ([761729d](https://github.com/st0o0/dtop/commit/761729d520d90060e05ce4dc26c546a1bbb472f9))
* use Tab/Shift+Tab for overlay tab navigation, remove ←→ ([c4674cb](https://github.com/st0o0/dtop/commit/c4674cbe31e3b184419a5d7698c372047f4d3c9e))


### Bug Fixes

* align Process list columns — consistent RAM formatting ([fe0d28b](https://github.com/st0o0/dtop/commit/fe0d28b9a13a731ed782f07705e1957a2d2ceffa))
* always show numeric CPU% and RAM values in Docker list ([a280352](https://github.com/st0o0/dtop/commit/a280352b3bfd51dae78931ddc5677a911f68ac2f))
* auto-copy Docker plugin DLLs to App output on solution build ([df3c928](https://github.com/st0o0/dtop/commit/df3c92814e87c221ecf6038ed98b2cf3109a7a9e))
* copy all Docker.DotNet transitive dependencies to App output ([4952ec0](https://github.com/st0o0/dtop/commit/4952ec0681ddcba4934f54b65a2673d8727afc37))
* Docker performance + detail UI improvements ([7929215](https://github.com/st0o0/dtop/commit/7929215d298c9835fd30f85b40db3810b7f007ee))
* eliminate Task.Run/Wait/GetAwaiter — fully async Docker availability check ([23923b8](https://github.com/st0o0/dtop/commit/23923b84ce43a2b4bca535aff3474998424d522f))
* fetch real CPU/RAM/Network stats from Docker Stats API ([a190755](https://github.com/st0o0/dtop/commit/a190755953718c056dd9c93d39f539c66a3a6197))
* handle ActionFailure responses in process detail Ask calls ([03420d3](https://github.com/st0o0/dtop/commit/03420d328bf950afde41bef2f11d12b75e9c97f5))
* improve process tree rendering with proper box-drawing characters ([a069f9b](https://github.com/st0o0/dtop/commit/a069f9bdc744af5f1762c4384cd01c72c67ed21b))
* move graphs below info, adjust sizes, update tests ([2c2eaed](https://github.com/st0o0/dtop/commit/2c2eaed37e775c5dfff6204b7204d2d7f2ce01ee))
* Network detail — add header, preserve selection on refresh ([87ad8c9](https://github.com/st0o0/dtop/commit/87ad8c9458ad4ecfad4f87f9f438bac52c775a53))
* render sparkline colors segment-wise instead of overlaying ([ee25108](https://github.com/st0o0/dtop/commit/ee251083cca5c73f45b1ae0be6d57ca2f33507f3))
* replace empty catch blocks with Senf.Tracing and eliminate GetAwaiter().GetResult() ([971b69e](https://github.com/st0o0/dtop/commit/971b69e73b243632f2c1c4102d97ea33b706a2db))
* revert ColorSpan — selected row uses standard selection colors ([ea9d98e](https://github.com/st0o0/dtop/commit/ea9d98eb1caa5008f56d5db64bb2aa66e74d2e9b))
* rewrite Docker stats fetching — live CPU/RAM updates ([404ae5c](https://github.com/st0o0/dtop/commit/404ae5c0f59d4f1095a8be6a900de7a93fdd097e))
* show -:port for unmapped ports, align header with data columns ([116b7e3](https://github.com/st0o0/dtop/commit/116b7e339626d40cf410f075051276d377a8e1e4))
* show only active adapters in Performance network detail view ([1ac258a](https://github.com/st0o0/dtop/commit/1ac258aef40e51756d05c1fc0804612b16b5d153))
* truncate long adapter names in Network detail to prevent column shift ([fd40f57](https://github.com/st0o0/dtop/commit/fd40f57ec82ab99784ba19f0bb0cfc7de7f3f23d))
* truncate process name to 18 chars matching column padding ([ec953e8](https://github.com/st0o0/dtop/commit/ec953e8fb1dbd33764dc0bc7b1a4ca699ed9e242))
* use resolver.GetService&lt;IServiceProvider&gt;() instead of deprecated ServiceProvider.For() ([567e7ac](https://github.com/st0o0/dtop/commit/567e7ac98377abe6b0c7e78793a9dda6733b8f86))


### Performance Improvements

* fetch Docker container stats in parallel ([c094a09](https://github.com/st0o0/dtop/commit/c094a09097f5b03b1a4efc07c89eb2ea37b739bf))
* reduce CPU usage — slower graph refresh, longer Docker interval ([3076136](https://github.com/st0o0/dtop/commit/3076136052994abcd67a5b2f2564202ac7e33420))
* rewrite DockerMonitorActor to match CpuMonitorActor pattern ([6284155](https://github.com/st0o0/dtop/commit/628415536f0afc74f0dbca778fa86831451a8de7))

## [0.5.0](https://github.com/st0o0/dottop/compare/v0.4.0...v0.5.0) (2026-06-08)


### Features

* add command/query/event message types to dottop.Core ([f9293d0](https://github.com/st0o0/dottop/commit/f9293d0c9b2e5448614ddf5e5c2264047a7707dd))
* add context-sensitive keyboard hints to all pages and modals ([0f2bf37](https://github.com/st0o0/dottop/commit/0f2bf3733db6b2f6e069f968e94327e5c99c9b46))
* add dottop.Actors.Tests with CpuMonitor and ProcessAction tests ([48c82ff](https://github.com/st0o0/dottop/commit/48c82ffdc4944a455f9b329be338373999825a6f))
* add keyboard hints inside Performance detail modal ([404e048](https://github.com/st0o0/dottop/commit/404e048e8df38e059ecf2c1c919a8056767fc49c))
* add platform interfaces to dottop.Core (including new ICpuMetrics, IMemoryMetrics, INetworkMetrics) ([2505022](https://github.com/st0o0/dottop/commit/2505022107c4e0c2cfe76b6f0dd0e284d0b5433a))
* add Processes page integration tests and unify search bar format ([b00f64c](https://github.com/st0o0/dottop/commit/b00f64c9ad88d4db7cb0444891357433b5e5be4f))
* add UI integration test project with fixture and helpers ([2cec830](https://github.com/st0o0/dottop/commit/2cec8300670b6c3b18241560f65e4e1f7a3df8c3))
* create dottop.Core project with models ([bb36cbd](https://github.com/st0o0/dottop/commit/bb36cbded2bd0643b40f217d6d04b96a9f043cea))
* create dottop.Linux project with platform implementations ([1c55a7b](https://github.com/st0o0/dottop/commit/1c55a7b0c9560e2640a983068aba47280fe8cfd9))
* create dottop.Windows project with platform implementations ([1a190bb](https://github.com/st0o0/dottop/commit/1a190bb8fcd1c7cde9d7c69da25253e9f8ba823f))
* move modal hints to border footer line ([2bf4724](https://github.com/st0o0/dottop/commit/2bf472491225174a5e2ae7080c17dd8424257e9a))
* refactor dottop.App with supervisor hierarchy and platform interfaces ([92ab2b4](https://github.com/st0o0/dottop/commit/92ab2b4e1d64290359334b2850c2803b00e7821d))
* replace console logging with Serilog file sink ([c0785f4](https://github.com/st0o0/dottop/commit/c0785f4d0feace995f0eb41b42ec4414972b9bb4))
* route all ViewModel communication through MonitoringSupervisor and add Senf.Tracing ([9d9c937](https://github.com/st0o0/dottop/commit/9d9c9370982a44a2870c8348091a5e6ac84b2b54))
* route all ViewModel communication through MonitoringSupervisor and add Senf.Tracing ([0d6f4ea](https://github.com/st0o0/dottop/commit/0d6f4ea86541c8df8f8b350edbda806a92b82f01))
* use toast notifications for error/success feedback in all ViewModels ([0780f64](https://github.com/st0o0/dottop/commit/0780f64f9d1247f8b60019cda8abfc21013a4bfd))


### Bug Fixes

* add submodules: recursive to CI/CD checkout steps ([d0bb12a](https://github.com/st0o0/dottop/commit/d0bb12a4b0260f8a3923a610cec0a749a76eb670))
* align keyboard hints with actual keybindings across all pages ([d47d4bf](https://github.com/st0o0/dottop/commit/d47d4bfc4056231e617c64a2ccb9e2362a3e5512))
* always reference both platform projects, remove reflection ([b1b7142](https://github.com/st0o0/dottop/commit/b1b71428fbae7c4f4db7bca333b4783b5050eca0))
* dispose Process objects, add error logging, fix overlay race condition ([92d85b4](https://github.com/st0o0/dottop/commit/92d85b49a095aca6ea2399871f96d32a905d4d73))
* route Akka logging through Serilog, suppress DeadLetter console output ([a49f317](https://github.com/st0o0/dottop/commit/a49f317d0a08a44d421a6cd20c6942b761a8baa6))
* show disk-specific hint in Performance detail modal ([a71aa12](https://github.com/st0o0/dottop/commit/a71aa12195c62d0db5427944d8af43002fd766f0))
* stabilize flaky UI tests with proper WaitForText timeouts ([16910f0](https://github.com/st0o0/dottop/commit/16910f08021b664c01519cd0b80de8e9e9e079f7))
* unify key bindings — Enter opens detail, Tab only cycles ([6a75a7f](https://github.com/st0o0/dottop/commit/6a75a7f54d85d48be761f9ad3dd0353e169a7f16))

## [0.4.0](https://github.com/st0o0/dottop/compare/v0.3.0...v0.4.0) (2026-06-06)


### Features

* real per-core CPU via NtQuerySystemInformation (Win) and /proc/stat (Linux) ([58a32a0](https://github.com/st0o0/dottop/commit/58a32a0e50257f157bbaa67fbaba3f819675a74a))


### Bug Fixes

* catch without return so tick continues to write data after HW failure ([d2835a0](https://github.com/st0o0/dottop/commit/d2835a0f111a32aae7f64cbff7cf9812d8a3b947))
* ensure disk metrics initialize only once, guard with try-catch ([75881df](https://github.com/st0o0/dottop/commit/75881dfe02780bf25c01afc10abd65357ec8c966))
* force ModalNode fullscreen with Height(999) on content ([54bcc8b](https://github.com/st0o0/dottop/commit/54bcc8b888e752a381d52a7d008abc791e3eaad4))
* infinite retry with 2s backoff for ConnectStream, resilient tick handlers ([d4a6212](https://github.com/st0o0/dottop/commit/d4a621281f94e1af7c20c6ffc910dab5ef5af121))
* inject release version into all assembly version fields in CI ([86cf271](https://github.com/st0o0/dottop/commit/86cf2714efa73ec6b4bc44195bead85371a9feeb))
* inject release version into all assembly version fields in CI ([#7](https://github.com/st0o0/dottop/issues/7)) ([4bb10f4](https://github.com/st0o0/dottop/commit/4bb10f44b687127329f0056692e9c275ca5a4242))
* make WindowsCpuMetrics robust against PerformanceCounter failures ([8049bfd](https://github.com/st0o0/dottop/commit/8049bfdad3c7c782eba040fb30e2b398571c23e3))
* Performance detail modal uses full screen, no double border ([fbb9ba8](https://github.com/st0o0/dottop/commit/fbb9ba8df460c13f89f202e44b8e5a189d144051))
* remove blocking PreStart, increase Ask timeout to 60s ([237bafa](https://github.com/st0o0/dottop/commit/237bafa6b7195d0273942b3e09da1bcd08b693e3))
* restore CPU baseline measurement on first tick for correct core values ([ae2f753](https://github.com/st0o0/dottop/commit/ae2f753786c8a827f623e2fffada1e7ea0f0fb69))
* retry ConnectStream up to 3 times on timeout with 1s delay ([6d1a01d](https://github.com/st0o0/dottop/commit/6d1a01d6a0ae2f666f7815ba88504ecc84669802))
* revert row position, reuse graph nodes to prevent history reset ([1439671](https://github.com/st0o0/dottop/commit/1439671fd08ac63a14244344b66d7313fda5ef8c))
* share HardwareInfo via constructor, fix CPU baseline, fix Linux CI hang ([43d9553](https://github.com/st0o0/dottop/commit/43d955378aa75e192a87f8ec9e6f31f2ac2b32a2))
* skip DiskMonitorActor test on Linux, increase WMI test timeouts ([59545a7](https://github.com/st0o0/dottop/commit/59545a7c414a18af66424f9d65c8a5dce884515c))
* skip Hardware.Info and WMI tests on Linux to prevent CI hang ([16c89ea](https://github.com/st0o0/dottop/commit/16c89eac8229c122c3c69a6b2edf8dd03e2c3376))
* use Layouts.Stack for modal overlays to get full screen bounds ([3057280](https://github.com/st0o0/dottop/commit/3057280e7df3ff02f99609443313bb607e27c660))
* wrap all Hardware.Info calls in try-catch to prevent actor crashes ([523ecfb](https://github.com/st0o0/dottop/commit/523ecfb44a8904f55d34aaa4ed6b2080cfa157f4))


### Performance Improvements

* all 4 HW actors share single HardwareInfo - eliminates 3 WMI connections ([31002fa](https://github.com/st0o0/dottop/commit/31002fa0eed1c8f7c75f4109b3f291af6eaf1881))
* eliminate ALL WMI usage - zero WMI Provider Host overhead ([d8844ad](https://github.com/st0o0/dottop/commit/d8844adb50057606951c4c1dcaf036a8906ce226))
* reduce CPU WMI overhead with 100ms measurement delay and lazy baseline ([0487cb9](https://github.com/st0o0/dottop/commit/0487cb9363d6e4acc143583bdd13ea28534b1b13))
* remove Hardware.Info completely, zero WMI usage ([eaf6728](https://github.com/st0o0/dottop/commit/eaf672824e0e931540822a8a25f33166e3445abe))
* replace WMI CPU measurement with GetSystemTimes kernel API ([7e5017d](https://github.com/st0o0/dottop/commit/7e5017d79ed4461e218271655d8ca5aff3a47d1a))


### Reverts

* remove PerformanceCounter CPU metrics, back to HardwareInfo ([1f1393e](https://github.com/st0o0/dottop/commit/1f1393efd7c70a1773e226ee760f1f25f9a4e9e2))
* restore working ModalNode for Performance detail, fix status bar position ([5c60918](https://github.com/st0o0/dottop/commit/5c60918dc8704a4a266626a0732def9430763777))

## [0.3.0](https://github.com/st0o0/dottop/compare/v0.2.0...v0.3.0) (2026-06-06)


### Features

* add Settings tab with theme, refresh rate, sort, group, graph style, language ([a414e35](https://github.com/st0o0/dottop/commit/a414e352fc75694ceddeeed5f026faf5db0fbaa0))
* apply settings (theme, refresh rate, graph style, sort, language) ([de19321](https://github.com/st0o0/dottop/commit/de1932142f032e36f6a57ddd360459497f28966f))
* Light mode sets white terminal background via ANSI escape ([5ab97e5](https://github.com/st0o0/dottop/commit/5ab97e524083ade2c43d85f69efbceb99d458d38))
* monochrome Cyan/Blue visual redesign for cleaner look ([b544673](https://github.com/st0o0/dottop/commit/b5446734509fdc89e62d0e09c0c20b8832b5ee37))
* settings apply live on change (theme, language, graph style) ([b56177c](https://github.com/st0o0/dottop/commit/b56177c605f18b8d625f05996e5bbf68963e37dd))


### Bug Fixes

* clear unused rows to prevent ghost rendering and layout shift ([40f53de](https://github.com/st0o0/dottop/commit/40f53de08f4c8cb547f9ca139a7ff0b63a47fce2))
* full screen clear + redraw on theme change to prevent gray artifacts ([c0c5ca9](https://github.com/st0o0/dottop/commit/c0c5ca9c5c491e6cb34743379a7c344abe9c8a06))
* give detail graph fixed height so ModalNode sizes correctly ([8782e9f](https://github.com/st0o0/dottop/commit/8782e9fa0e9d4ce43c0c61677b8846014aa28968))
* only clear screen on theme change, not on language/graph changes ([bdc30ac](https://github.com/st0o0/dottop/commit/bdc30ac92e2f9279eb6b4ce99cf6ce380f95361c))
* Performance detail modal fills screen with large content + dim backdrop ([ed1add8](https://github.com/st0o0/dottop/commit/ed1add80a298fdf2973368abc00d9d33c64a12df))
* Performance status bar alignment - wrap panels in Fill container ([d44937e](https://github.com/st0o0/dottop/commit/d44937ebef2f02e8f2370a4825f43dc4842383c1))
* recreate detail graphs on each modal update to prevent disposed nodes ([5d303c1](https://github.com/st0o0/dottop/commit/5d303c1ecffe09ec1109a4b98bd6fad960b2a756))
* remove extra row gap between panels and status bar on Performance ([2980206](https://github.com/st0o0/dottop/commit/2980206001faa6e4c2d39bfb7f1acf0f53937feb))


### Performance Improvements

* each monitor stream connects independently, no Task.WhenAll ([7e614ea](https://github.com/st0o0/dottop/commit/7e614ea2ae0199ac57cacb74cefe7684980785ea))
* initialize disk PerformanceCounters in background thread at startup ([b3f22fe](https://github.com/st0o0/dottop/commit/b3f22fe68084c4073b3160dc80b5588e5d7ee9ca))
* move DiskMetrics.Initialize to PreStart to unblock StartMonitoring ([ac426a4](https://github.com/st0o0/dottop/commit/ac426a426d029dc0bd6b7cf0739e4e2f363951cb))
* move hardware init to PreStart for non-blocking actor creation ([daa0ad7](https://github.com/st0o0/dottop/commit/daa0ad7cc1dcb826f85e914556b91c371a7f9a85))
* parallelize actor initialization for faster page load ([9f2a058](https://github.com/st0o0/dottop/commit/9f2a058508d4551dec1221251ed47c2adfa560d0))

## [0.2.0](https://github.com/st0o0/dottop/compare/v0.1.0...v0.2.0) (2026-06-05)


### Features

* add GPU monitoring with NVML support ([0c094c2](https://github.com/st0o0/dottop/commit/0c094c2c280a78db2c8975e826f242982258daba))
* add process kill confirmation dialog ([51fe949](https://github.com/st0o0/dottop/commit/51fe949212046e097d770ab67af283aff72d545d))
* add service detail overlay with description ([b79ca62](https://github.com/st0o0/dottop/commit/b79ca625997651aecaae150d6efc8dad8471963e))
* show process names in network connections via PID mapping ([c09d70a](https://github.com/st0o0/dottop/commit/c09d70ae1acbb0d7b02eccf6e32feeb2d8d4dd5f))


### Bug Fixes

* prevent deadlocks and dead letters on tab navigation ([6ebc2ea](https://github.com/st0o0/dottop/commit/6ebc2ea30ecade5b72e8f775c5931aa9ce9a7c0a))

## 0.1.0 (2026-06-05)


### Features

* add Akka monitor actors with supervisor and tests ([9555740](https://github.com/st0o0/dottop/commit/9555740190339054c8f74485377860e9e486b4fa))
* add Akka.Hosting and test project ([ab3d936](https://github.com/st0o0/dottop/commit/ab3d93675bb485d8d8ef645ff54d214a31e87dd2))
* add Autostart view with Enable/Disable and complete 5-tab navigation ([40eae15](https://github.com/st0o0/dottop/commit/40eae1520e6fa3aef7ed16b8f308357a02ef0586))
* add DataListNode&lt;T&gt; custom component, replace manual list rendering ([7d276dd](https://github.com/st0o0/dottop/commit/7d276ddaf62e88e1caca32c527d357f945554568))
* add localization with .resx for EN/DE based on OS language ([97ed0e3](https://github.com/st0o0/dottop/commit/97ed0e3ba978829cf0526c017202344fadcbb2e0))
* add Network view with active connections ([9d6b5bb](https://github.com/st0o0/dottop/commit/9d6b5bb9ac52761845d962ddf29b2587cecdcb38))
* add Performance view with CPU/RAM/Disk/Network panels ([a925153](https://github.com/st0o0/dottop/commit/a92515391368e7c6882598982730ed46a473c850))
* add ProcessActionActor with Kill/Priority/Affinity/Tree/Env ([02fe2fe](https://github.com/st0o0/dottop/commit/02fe2fe8588eafa032553a756d1e1d999d675b15))
* add Processes view with search, grouping, sorting, and overlay ([fb6ccee](https://github.com/st0o0/dottop/commit/fb6ccee90b93853e4c6d7118e0f7cd42a7bf02f1))
* add Services view with Start/Stop/Restart ([2cce60a](https://github.com/st0o0/dottop/commit/2cce60ab14e88b2e96592dbfe8fe0b90fcbb594d))
* add snapshot models and actor messages for Task Manager ([45de982](https://github.com/st0o0/dottop/commit/45de98221838db319929541c9cfd349f4cfb71c2))
* add TabBarNode for tab navigation ([9889792](https://github.com/st0o0/dottop/commit/98897923942118fa067a76b68e585a9f84f9ca28))
* calculate real per-process CPU% using delta between measurements ([550d89d](https://github.com/st0o0/dottop/commit/550d89d244db3b132239253adeaf76adf81c7f2c))
* comprehensive test suites for platform providers and actor integration ([f0f2c3b](https://github.com/st0o0/dottop/commit/f0f2c3baa9278da231360e2242a245427f05235d))
* CpuCoresNode auto-wraps cores to multiple rows based on width ([1433faa](https://github.com/st0o0/dottop/commit/1433faa6f976d786d41bcfd679d2ebbedfdd0eaa))
* implement real Autostart enable/disable via Registry ([a5550a8](https://github.com/st0o0/dottop/commit/a5550a87e60eb3a7b10b545d103abb879ceec2d3))
* per-disk detail view with Active Time and Transfer Rate graphs ([ac24ece](https://github.com/st0o0/dottop/commit/ac24eceb3db7c24a9718ebc9f06b9d1e79e2d337))
* Performance detail modal with Tab/←→ to cycle CPU/RAM/Disk/Network ([2c757b2](https://github.com/st0o0/dottop/commit/2c757b218a383a69d7ad580c2994026cbb6624c5))
* real process tree via WMI and loaded modules list ([f9bd4d3](https://github.com/st0o0/dottop/commit/f9bd4d3f07475c4e33049771890066088c66000e))
* scrollable Env and Handles tabs in overlay via DataListNode ([e99ad8a](https://github.com/st0o0/dottop/commit/e99ad8ad512a7544d34b2104ac45eb6535af1927))
* show CPU total percentage in performance overview panel ([77a81dc](https://github.com/st0o0/dottop/commit/77a81dc79676480776015ea7e46ab7cd443bd80a))
* visual polish across all views ([d1fe64a](https://github.com/st0o0/dottop/commit/d1fe64a732629adc2ade92baf47d78d25af0778b))
* wire Akka.Hosting + multi-route Termina registration ([b81cbbf](https://github.com/st0o0/dottop/commit/b81cbbfc6a152af3d044e213ec81cf3bd14e2eea))
* wrap all lists in PanelNode borders, consistent search bars ([a5246cc](https://github.com/st0o0/dottop/commit/a5246cc2e2c5975d4cd3d02324b047cbcf243f99))


### Bug Fixes

* actors cleanup previous stream on re-subscribe after tab navigation ([207c4dd](https://github.com/st0o0/dottop/commit/207c4dd18f747a8dcec995c12468c9e31c054645))
* add F6 as alternative sort-cycle key (Tab may be captured by terminal) ([0b8648e](https://github.com/st0o0/dottop/commit/0b8648e0400a3626ab575510b522945e8b296c04))
* centralize overlay updates via OverlayContentChanged Subject, compact column widths ([288d3b0](https://github.com/st0o0/dottop/commit/288d3b0906c428e6e4c7e0c9852cc0dd0c69063a))
* correct ProcessMonitorActor test for PID 0 and environment variable retrieval ([9e4f8ac](https://github.com/st0o0/dottop/commit/9e4f8ac141ed58dcd4574ae416c1703448c51c00))
* CPU cores horizontal, disk names fallback to drive/volume name ([7753a26](https://github.com/st0o0/dottop/commit/7753a26516e566fefecd90463588fbb916c6cef8))
* create ScrollableContainerNode once to preserve scroll position ([9484570](https://github.com/st0o0/dottop/commit/9484570c7e6d2f699b61dd31f59064c0d1099689))
* hide ModalNode when overlay is closed, update all packages to latest ([42eac05](https://github.com/st0o0/dottop/commit/42eac052ca231b0c5ec03b84a9419f99dac0213e))
* live-update overlay values while open ([b64eeb0](https://github.com/st0o0/dottop/commit/b64eeb0c194e023c18ce2a4ad0a30f03a7e90243))
* make Autostart enabled/disabled status more visible ([4079f3c](https://github.com/st0o0/dottop/commit/4079f3c1df460b70587a77b8753ca7bf06cb4a49))
* make System.Management and ServiceController unconditional NuGet refs ([a5634ef](https://github.com/st0o0/dottop/commit/a5634ef84f72c4ba6a02f41f7dbb96f46c3415a2))
* move subscriptions to OnNavigatedTo to survive tab navigation ([2bd8790](https://github.com/st0o0/dottop/commit/2bd87903ff4743cfc36dce49ff710f1fa1ec5f28))
* only rebuild overlay on process update when on Overview tab ([b5d1376](https://github.com/st0o0/dottop/commit/b5d13761447fb2c06821afe31492dde5db26001c))
* overlay live-update via timer poll instead of reactive chain ([6c41d01](https://github.com/st0o0/dottop/commit/6c41d01eacb6b91d1b512068b8421e89d5c1842e))
* push graph data points every 200ms for smoother scrolling ([12a87da](https://github.com/st0o0/dottop/commit/12a87da005f6b0d1bf14635bb07a4dda81d40218))
* remove Focus.PushFocus on Modal, fix double border and keyboard capture ([e5dd6c5](https://github.com/st0o0/dottop/commit/e5dd6c5ab7755cdf4bc75f811824aa0f1f097be1))
* replace IFocusable with direct method calls for list navigation ([43d19ad](https://github.com/st0o0/dottop/commit/43d19ade2af6b87050d76071b146d33a5bc1bcdc))
* search activation uses KeyChar '/' for keyboard layout compat ([7feb832](https://github.com/st0o0/dottop/commit/7feb8320c63aeceece3a98b69313476b80bb3633))
* search bar reacts to SortColumn and SelectedGroup changes ([c7cb1df](https://github.com/st0o0/dottop/commit/c7cb1dfc20d14240a55b7acc96be47ec00f90ab2))
* set AutoScrollPolicy.None on all lists, add scroll-to-selection ([8d1562d](https://github.com/st0o0/dottop/commit/8d1562d893e6de058632efb27a4abb52ccedca2a))
* sort disks alphabetically by drive letter ([d51dbb3](https://github.com/st0o0/dottop/commit/d51dbb3be76b465eb3a50b3709bdf176ea65e44b))
* subscribe to AllProcesses for overlay live-update to bypass DistinctUntilChanged ([9eb1d1a](https://github.com/st0o0/dottop/commit/9eb1d1a35c1e1725f1efa83c0b867552afae2e3d))
* toggle Autostart entries instantly in local state on Space press ([27fd7fd](https://github.com/st0o0/dottop/commit/27fd7fd43eb5e3b73becff0ed4dd61b1322759e1))
* truncate long values in all list views to keep columns aligned ([a986702](https://github.com/st0o0/dottop/commit/a986702fd62cc216059642b60499630615530c07))
* use BackdropStyle.Transparent to remove dotted double-border on modal ([3c6d10c](https://github.com/st0o0/dottop/commit/3c6d10c962ac161baae9e1b10956e67094315758))
* use CpuCoresNode in detail modal for multi-row core display ([1653c66](https://github.com/st0o0/dottop/commit/1653c66ae4d2b3de48dbcaf33e2b6d626861487d))
* use Solid black backdrop for modal instead of Transparent ([0392490](https://github.com/st0o0/dottop/commit/0392490078cda06d66359353bef950da41d595f1))


### Miscellaneous Chores

* release 0.1.0 ([98547c7](https://github.com/st0o0/dottop/commit/98547c7abe4d081a4c41ac5ccd5496c8bef387e7))
