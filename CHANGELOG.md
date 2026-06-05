# Changelog

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
