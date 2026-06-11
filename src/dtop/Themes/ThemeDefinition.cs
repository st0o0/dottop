using Termina.Terminal;

namespace dtop.Themes;

public sealed record ThemeDefinition
{
    public Color Background { get; init; } = Color.Default;
    public Color Foreground { get; init; } = Color.White;
    public Color TextDim { get; init; } = Color.Gray;
    public Color Border { get; init; } = Color.BrightCyan;
    public Color PanelTitle { get; init; } = Color.BrightCyan;
    public Color Selection { get; init; } = Color.BrightCyan;
    public Color SelectionText { get; init; } = Color.Black;
    public Color StatusBar { get; init; } = Color.BrightCyan;
    public Color StatusBarText { get; init; } = Color.Black;
    public Color Warning { get; init; } = Color.BrightYellow;
    public Color Error { get; init; } = Color.BrightRed;
    public Color Success { get; init; } = Color.BrightGreen;
    public Color Header { get; init; } = Color.BrightBlack;
    public Color Accent { get; init; } = Color.Cyan;

    public Gradient CpuGradient { get; init; } = Gradient.Create(
        Color.FromHex("#50fa7b"), Color.FromHex("#f1fa8c"), Color.FromHex("#ff5555"));
    public Gradient MemGradient { get; init; } = Gradient.Create(
        Color.FromHex("#8be9fd"), Color.FromHex("#bd93f9"), Color.FromHex("#ff79c6"));
    public Gradient GpuGradient { get; init; } = Gradient.Create(
        Color.FromHex("#bd93f9"), Color.FromHex("#ff79c6"), Color.FromHex("#ff5555"));
    public Gradient NetGradient { get; init; } = Gradient.Create(
        Color.FromHex("#8be9fd"), Color.FromHex("#50fa7b"), Color.FromHex("#f1fa8c"));
    public Gradient DiskGradient { get; init; } = Gradient.Create(
        Color.FromHex("#50fa7b"), Color.FromHex("#f1fa8c"), Color.FromHex("#ff5555"));

    public Color ProcessLow { get; init; } = Color.BrightGreen;
    public Color ProcessMed { get; init; } = Color.BrightYellow;
    public Color ProcessHigh { get; init; } = Color.BrightRed;
}
