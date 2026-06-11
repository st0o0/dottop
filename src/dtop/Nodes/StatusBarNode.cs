using dtop.Services;
using dtop.Themes;
using R3;
using Termina.Layout;
using Termina.Rendering;

namespace dtop.Nodes;

/// <summary>
/// A single-row status bar that renders hint text on the left and the current
/// refresh rate / pause state on the right.
/// </summary>
public sealed class StatusBarNode : LayoutNode, IDisposable, IInvalidatingNode
{
    private readonly IRefreshService _refreshService;
    private readonly Subject<Unit> _invalidated = new();
    private readonly IDisposable _sub;

    private string _hint = "";

    public Observable<Unit> Invalidated => _invalidated.AsObservable();

    public StatusBarNode(Observable<string> hint, IRefreshService refreshService)
    {
        _refreshService = refreshService;
        HeightConstraint = new SizeConstraint.Fixed(1);
        WidthConstraint = new SizeConstraint.Fill();

        // Invalidate when hint text, pause state, or interval changes.
        // Skip(1) on IsPaused and Interval avoids firing during construction
        // (ReactiveProperty immediately emits current value on subscribe).
        _sub = Observable.Merge(
                hint.Do(h => _hint = h).Skip(1).Select(_ => Unit.Default),
                _refreshService.IsPaused.Skip(1).Select(_ => Unit.Default),
                _refreshService.Interval.Skip(1).Select(_ => Unit.Default))
            .Subscribe(_ => _invalidated.OnNext(Unit.Default));
    }

    public override Size Measure(Size available) => available with { Height = 1 };

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea)
        {
            return;
        }

        // Create a sub-context so coordinates (0, 0) map to the node's allocated
        // screen position, not the absolute top-left of the terminal.
        var ctx = context.CreateSubContext(bounds);

        var theme = ThemeService.Instance.Current;
        var isPaused = _refreshService.IsPaused.Value;
        var interval = _refreshService.Interval.CurrentValue;

        var rateStr = FormatInterval(interval);
        var rateTag = isPaused ? $" ⏸ PAUSED  {rateStr} " : $"  {rateStr} ";

        // Fill background.
        ctx.SetForeground(theme.TextDim);
        ctx.SetBackground(theme.StatusBar);
        ctx.Fill(0, 0, bounds.Width, 1);

        // Left side: hint text (truncated if it would overlap the right tag).
        var rightWidth = rateTag.Length;
        var hintWidth = Math.Max(0, bounds.Width - rightWidth);
        var hintText = _hint.Length > hintWidth ? _hint[..hintWidth] : _hint;
        ctx.WriteAt(0, 0, hintText);

        // Right side: rate/pause — highlighted when paused.
        if (isPaused)
        {
            ctx.SetForeground(theme.Warning);
        }
        else
        {
            ctx.SetForeground(theme.Accent);
        }

        ctx.WriteAt(bounds.Width - rateTag.Length, 0, rateTag);
        ctx.ResetColors();
    }

    private static string FormatInterval(TimeSpan ts)
    {
        if (ts.TotalSeconds >= 1)
        {
            return ts.TotalSeconds % 1 == 0
                ? $"{(int)ts.TotalSeconds}s"
                : $"{ts.TotalSeconds:F1}s";
        }

        return $"{(int)ts.TotalMilliseconds}ms";
    }

    public new void Dispose()
    {
        _sub.Dispose();
        _invalidated.Dispose();
        base.Dispose();
    }
}
