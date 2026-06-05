# dottop — Lokalisierung mit .resx

**Datum:** 2026-06-05
**Status:** Approved

## Ziel

Alle UI-Strings in dottop über .NET Resource Files (.resx) lokalisierbar machen. Sprache wird automatisch vom Betriebssystem übernommen via `CultureInfo.CurrentUICulture`.

## Sprachen

- `Strings.resx` — Englisch (Default/Fallback)
- `Strings.de.resx` — Deutsch

## Dateistruktur

```
src/dottop/Resources/
    Strings.resx        — EN
    Strings.de.resx     — DE
```

## Zugriff

```csharp
// Generierte Klasse: dottop.Resources.Strings
using dottop.Resources;

new TextNode(Strings.TabProcesses)       // "Processes" / "Prozesse"
string.Format(Strings.ProcessCount, 42)  // "42 processes" / "42 Prozesse"
```

## String-Kategorien (~70 Keys)

### Tabs
TabProcesses, TabPerformance, TabServices, TabNetwork

### Panel-Titel
PanelCpu, PanelRam, PanelDisks, PanelNetwork, PanelProcesses, PanelServices, PanelNetwork

### Suchleiste
SearchHint, SearchHintServices, SearchHintNetwork, SearchActive (Format: "/ {0}█")

### Keyboard-Hints
HintSearch, HintGroup, HintSort, HintEnterDetail, HintEscClose, HintKill, HintTabSwitch, HintQuit
HintServiceStart, HintServiceStop, HintServiceRestart

### Status-Meldungen
ProcessCount (Format), ServiceCount (Format), ConnectionCount (Format)
GroupAll, GroupApps, GroupBackground, GroupWindows
SortName, SortCpu, SortRam, SortPid

### Detail-Overlay
OverlayOverview, OverlayProcessTree, OverlayEnvironment, OverlayHandles
OverlayLoading, OverlayNoHandles

### Performance-Detail
PerfTotal, PerfUsed, PerfActiveTime, PerfTransferRate
PerfNoDisks, PerfNoAdapters

### Fehler
ErrorLoading, ErrorServiceAction

## Betroffene Dateien

Alle Pages und der TabBarNode:
- `src/dottop/Nodes/TabBarNode.cs`
- `src/dottop/Pages/ProcessesPage.cs`
- `src/dottop/Pages/ProcessesViewModel.cs`
- `src/dottop/Pages/PerformancePage.cs`
- `src/dottop/Pages/PerformanceViewModel.cs`
- `src/dottop/Pages/ServicesPage.cs`
- `src/dottop/Pages/ServicesViewModel.cs`
- `src/dottop/Pages/NetworkPage.cs`
- `src/dottop/Pages/NetworkViewModel.cs`

## Kein Architektur-Umbau

Reine String-Ersetzung. Kein neuer Service, kein DI, kein Interface. Nur `using dottop.Resources;` und `Strings.X` statt hardcoded Strings.
