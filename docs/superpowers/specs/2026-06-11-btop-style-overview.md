# btop-Style Overview Page Design

**Date:** 2026-06-11
**Status:** Approved

## Summary

Restyle the existing Overview page (Tab 0) to match btop's visual language: rounded colored panels, metric-specific colors, horizontal progress bars for memory and disk, colored process columns, and a runtime-switchable theme system. Scope is **Overview page only**.

## Goals

- Overview page looks and feels like btop
- btop-default theme ships as the single standard theme
- Theme is switchable at runtime via `[T]` key (foundation for future themes)
- No changes to other pages

## Architecture

### New Files

- `src/dtop/Themes/IThemeService.cs` — injectable theme service interface
- `src/dtop/Themes/ThemeService.cs` — implementation with btop-default built in
- `lib/termina/src/Nodes/ProgressBarNode.cs` — single-row horizontal bar node

### Modified Files

- `src/dtop/Themes/Theme.cs` — extract `ThemeDefinition` record, add metric colors
- `src/dtop/Setup/TerminaSetup.cs` — register `IThemeService` as singleton
- `src/dtop/Pages/OverviewPage.cs` — inject `IThemeService`, restyle all panels
- `src/dtop/Pages/OverviewViewModel.cs` — add `[T]` key handler

---

## ThemeDefinition

Replaces the current static `Theme` properties with a data record:

```csharp
public record ThemeDefinition(
    string Name,

    // Base colors
    Color Background,
    Color Border,
    Color Text,
    Color TextDim,

    // Metric panel colors (title + graph + bar fill)
    Color CpuColor,
    Color MemColor,
    Color GpuColor,
    Color NetColor,
    Color DiskColor,

    // Process list column thresholds
    Color ProcCpuLow,   // CPU% < 10
    Color ProcCpuMed,   // CPU% 10–50
    Color ProcCpuHigh,  // CPU% > 50
    Color ProcRamLow,
    Color ProcRamMed,
    Color ProcRamHigh,

    // Selection highlight
    Color SelectionBg,
    Color SelectionFg
);
```

### btop-default values

| Role | Color | Hex |
|------|-------|-----|
| Background | very dark | `#191c1f` |
| Border | dim | `#37474f` |
| Text | light | `#e0e0e0` |
| TextDim | gray | `#546e7a` |
| CpuColor | cyan | `#00bcd4` |
| MemColor | orange | `#ff9800` |
| GpuColor | purple | `#9c27b0` |
| NetColor | blue | `#2196f3` |
| DiskColor | green | `#4caf50` |
| ProcCpuLow | green | `#4caf50` |
| ProcCpuMed | orange | `#ff9800` |
| ProcCpuHigh | red | `#f44336` |
| ProcRamLow | green | `#4caf50` |
| ProcRamMed | orange | `#ff9800` |
| ProcRamHigh | red | `#f44336` |
| SelectionBg | cyan | `#00bcd4` |
| SelectionFg | dark | `#191c1f` |

---

## IThemeService

```csharp
public interface IThemeService
{
    ThemeDefinition Current { get; }
    ReactiveProperty<ThemeDefinition> CurrentTheme { get; }
    void Apply(string name);
    IReadOnlyList<string> Available { get; }
}
```

- Registered as singleton in DI
- Ships with one theme: `"btop-default"`
- `Apply()` updates `CurrentTheme` which triggers reactive re-render in Overview
- Pages subscribe to `CurrentTheme` for reactive color updates

---

## ProgressBarNode

New `LayoutNode` in Termina. Single row, reactive value, optional right-aligned label.

```csharp
public sealed class ProgressBarNode : LayoutNode
{
    public ReactiveProperty<double> Value { get; } = new(0.0); // 0.0–1.0
    public ReactiveProperty<Color?> FillColor { get; } = new(null);
    public ReactiveProperty<string?> Label { get; } = new(null); // right-aligned

    public ProgressBarNode WithValue(double v);
    public ProgressBarNode WithColor(Color c);
    public ProgressBarNode WithLabel(string s);
    // Reactive overloads:
    public ProgressBarNode WithValue(Observable<double> v);
    public ProgressBarNode WithLabel(Observable<string> s);
}
```

**Rendering:** `████████░░░░░░░░  8.2 GiB`
- Filled chars: `█` (U+2588), empty chars: `░` (U+2591)
- Label occupies fixed right portion if set; bar fills remaining width
- Height is always 1 row

---

## OverviewPage Visual Design

### Panel styling (all panels)

- `PanelNode` with `BorderStyle.Rounded` (╭─╮│╰╯)
- `BorderColor` = metric-specific color from theme
- `TitleColor` = same metric color, Bold

### CPU Panel

- Border + title: `theme.CpuColor`
- Title: `" CPU: {CpuName}  —  Total {CpuTotal:F1}% "`
- Content: `CpuCoresNode` (core bars) + `GraphNode.WithColor(theme.CpuColor)`
- Graph style: `GraphStyle.Braille`

### GPU Panel (when available)

- Border + title: `theme.GpuColor`
- Title: `" {gpu.Name}  —  {gpu.UsagePercent:F0}% "`
- Content row 1: `ProgressBarNode` for VRAM (color: `theme.GpuColor`), label: `"{used:F1} / {total:F1} GB   {temp}°C"`
- Content row 2: `GraphNode.WithColor(theme.GpuColor)` with `GraphStyle.Braille`

### Memory Panel

- Border + title: `theme.MemColor`
- Title: `" Memory  —  {used:F1} / {total:F1} GiB "`
- Content row 1: label `"Used: "` + `ProgressBarNode` (color: `theme.MemColor`), label: `"{used:F1} GiB"`
- Content row 2: label `"Cache:"` + `ProgressBarNode` (color: `theme.Border`), label: `"{cache:F1} GiB"`
- Content row 3: `GraphNode.WithColor(theme.MemColor)` with `GraphStyle.Braille`

### Net/Disk Panel

- Border + title: `theme.NetColor`
- Network rows: top 2 adapters, `↓ {down}  ↑ {up}` in `theme.NetColor`
- Disk rows: each disk name in `theme.DiskColor` + `ProgressBarNode` (color: `theme.DiskColor`), label: `"{pct:F0}%"`

### Process Panel

- Border + title: `theme.TextDim` (neutral — processes aren't a single metric)
- Header row: `PID`, `NAME`, `CPU%`, `RAM%`, `RAM` — bold, `theme.Text`
- CPU% column colored per row:
  - `< 10%` → `theme.ProcCpuLow`
  - `10–50%` → `theme.ProcCpuMed`
  - `> 50%` → `theme.ProcCpuHigh`
- RAM% column: same thresholds with `ProcRamLow/Med/High`
- Selected row: background `theme.SelectionBg`, foreground `theme.SelectionFg`

---

## Keyboard Addition

| Key | Action |
|-----|--------|
| `T` | Cycle theme (only btop-default for now — foundation for future themes) |

Added to `OverviewViewModel.HandleKey`. Calls `_themeService.Apply(nextTheme)`.

---

## Statusbar Update

Add `[T] Theme` hint to the normal-mode statusbar:

```
 CPU: 42.1%  RAM: 8.2/32 GiB  |  Layout: Standard  |  [F] Filter  [P] Preset  [M] Sort: CPU%  [R] ↓  [T] Theme
```

---

## Out of Scope

- Restyling other pages (Processes, Performance, Services, Network, Docker)
- Additional themes beyond btop-default at launch
- Theme persistence in settings (can be added later)
- Disk I/O graphs in Overview (use Performance tab)
