using System.Reactive;
using System.Reactive.Subjects;
using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;
using Timer = System.Timers.Timer;

namespace btop;

public enum GraphStyle
{
    /// <summary>▁▂▃▄▅▆▇█ – classic filled blocks from bottom</summary>
    Blocks,

    /// <summary>Only the top edge is drawn – hollow inside</summary>
    Outline,

    /// <summary>Braille dots – double vertical resolution per row</summary>
    Braille,

    /// <summary>ASCII _ . - ~ ^ * # @ fallback</summary>
    Ascii,
}

/// <summary>
/// Reactive LayoutNode that renders a live scrolling graph.
/// Uses IAnimatedNode (like SpinnerNode) so only this region is invalidated,
/// NOT the parent layout – which would destroy sibling reactive nodes.
/// </summary>
public sealed class GraphNode : LayoutNode, IAnimatedNode, IInvalidatingNode
{
    private static readonly char[] BlockChars = [' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█'];
    private static readonly char[] OutlineChars = [' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '▔'];
    private static readonly char[] AsciiChars = [' ', '_', '.', '-', '~', '^', '*', '#', '@'];

    private static readonly char[][] BrailleChars =
    [
        ['\u2800', '⢀', '⢠', '⢰', '⢸'],
        ['⡀', '⣀', '⣠', '⣰', '⣸'],
        ['⡄', '⣄', '⣤', '⣴', '⣼'],
        ['⡆', '⣆', '⣦', '⣶', '⣾'],
        ['⡇', '⣇', '⣧', '⣷', '⣿'],
    ];

    private readonly Subject<Unit> _invalidated = new();
    private readonly Timer _timer;
    private readonly Queue<double> _data = new();

    private GraphStyle _style = GraphStyle.Blocks;
    private Color? _color;
    private double _minValue;
    private double _maxValue = 100;

    public IObservable<Unit> Invalidated => _invalidated;
    public bool IsAnimating { get; private set; }

    public GraphNode(int refreshMs = 100)
    {
        _timer = new Timer(refreshMs);
        _timer.Elapsed += (_, _) => _invalidated.OnNext(Unit.Default);
        _timer.AutoReset = true;
        Start();
    }

    // ── IAnimatedNode ──────────────────────────────────────────────────────

    public void Start()
    {
        if (IsAnimating) return;
        IsAnimating = true;
        _timer.Start();
    }

    public void Stop()
    {
        if (!IsAnimating) return;
        _timer.Stop();
        IsAnimating = false;
    }

    // ── Fluent builder ─────────────────────────────────────────────────────

    public GraphNode WithStyle(GraphStyle style)
    {
        _style = style;
        return this;
    }

    public GraphNode WithColor(Color color)
    {
        _color = color;
        return this;
    }

    public GraphNode WithRange(double min, double max)
    {
        _minValue = min;
        _maxValue = max;
        return this;
    }

    /// <summary>Replace all data – thread-safe, rendered on next timer tick.</summary>
    public void SetData(IEnumerable<double> values)
    {
        lock (_data)
        {
            _data.Clear();
            foreach (var v in values)
                _data.Enqueue(v);
        }
    }

    /// <summary>Append a single value – thread-safe.</summary>
    public void Push(double value)
    {
        lock (_data)
            _data.Enqueue(value);
    }

    // ── Measure / Render ───────────────────────────────────────────────────

    public override Size Measure(Size available)
    {
        var w = WidthConstraint.Compute(available.Width, available.Width, available.Width);
        var h = HeightConstraint.Compute(available.Height, available.Height, available.Height);
        return new Size(w, h);
    }

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea)
            return;

        var width = bounds.Width;
        var height = bounds.Height;

        double[] data;
        lock (_data)
        {
            var maxPoints = _style == GraphStyle.Braille ? width * 2 : width;
            while (_data.Count > maxPoints)
                _data.Dequeue();
            data = _data.ToArray();
        }

        // Key fix: create a sub-context clipped to our bounds.
        // This means all WriteAt calls inside use (0,0)-relative coordinates
        // and are physically incapable of bleeding into sibling nodes,
        // exactly the same pattern ScrollableContainerNode uses for its content.
        var ctx = context.CreateSubContext(bounds);

        switch (_style)
        {
            case GraphStyle.Blocks:  RenderColumns(ctx, data, width, height, BlockChars); break;
            case GraphStyle.Outline: RenderOutline(ctx, data, width, height); break;
            case GraphStyle.Braille: RenderBraille(ctx, data, width, height); break;
            case GraphStyle.Ascii:   RenderColumns(ctx, data, width, height, AsciiChars); break;
        }

        ctx.ResetColors();
    }

    // ── Render implementations ─────────────────────────────────────────────
    // All coordinates are (0,0)-relative within the sub-context.
    // bounds is no longer passed in – the sub-context enforces the clip boundary.

    private void RenderColumns(IRenderContext ctx, double[] data, int width, int height, char[] chars)
    {
        for (var col = 0; col < width; col++)
        {
            var dataIdx = data.Length - width + col;
            var filledRows = dataIdx >= 0 ? Normalize(data[dataIdx]) * height : 0.0;

            for (var row = 0; row < height; row++)
            {
                var x = col;
                var y = height - 1 - row;

                int charIdx;
                if (filledRows >= row + 1)
                    charIdx = 8;
                else if (filledRows > row)
                    charIdx = Math.Clamp((int)Math.Ceiling((filledRows - row) * 7.999), 1, 8);
                else
                    charIdx = 0;

                if (charIdx > 0 && _color.HasValue)
                    ctx.SetForeground(_color.Value);

                ctx.WriteAt(x, y, chars[Math.Min(charIdx, chars.Length - 1)]);
                ctx.ResetColors();
            }
        }
    }

    private void RenderOutline(IRenderContext ctx, double[] data, int width, int height)
    {
        for (var col = 0; col < width; col++)
        {
            var dataIdx = data.Length - width + col;
            var filledRows = dataIdx >= 0 ? Normalize(data[dataIdx]) * height : 0.0;
            var edgeRow = (int)filledRows;

            for (var row = 0; row < height; row++)
            {
                var x = col;
                var y = height - 1 - row;

                if (row == edgeRow && filledRows > 0)
                {
                    var charIdx = Math.Clamp((int)Math.Ceiling((filledRows - row) * 7.999), 1, 8);
                    if (_color.HasValue)
                        ctx.SetForeground(_color.Value);
                    ctx.WriteAt(x, y, OutlineChars[charIdx]);
                    ctx.ResetColors();
                }
                else
                {
                    ctx.WriteAt(x, y, ' ');
                }
            }
        }
    }

    private void RenderBraille(IRenderContext ctx, double[] data, int width, int height)
    {
        var subHeight = height * 2;

        for (var col = 0; col < width; col++)
        {
            var botIdx = data.Length - width * 2 + col * 2;
            var topIdx = botIdx + 1;

            var botFilled = botIdx >= 0 && botIdx < data.Length ? Normalize(data[botIdx]) * subHeight : 0.0;
            var topFilled = topIdx >= 0 && topIdx < data.Length ? Normalize(data[topIdx]) * subHeight : 0.0;

            for (var row = 0; row < height; row++)
            {
                var x = col;
                var y = height - 1 - row;

                var botSubRow = row * 2;
                var topSubRow = row * 2 + 1;
                var botFill = (int)Math.Clamp((botFilled - botSubRow) * 4, 0, 4);
                var topFill = (int)Math.Clamp((topFilled - topSubRow) * 4, 0, 4);
                var c = BrailleChars[botFill][topFill];

                if (c != '\u2800' && _color.HasValue)
                    ctx.SetForeground(_color.Value);

                ctx.WriteAt(x, y, c);
                ctx.ResetColors();
            }
        }
    }

    private double Normalize(double value)
    {
        var range = _maxValue - _minValue;
        if (range <= 0) return 0;
        return Math.Clamp((value - _minValue) / range, 0.0, 1.0);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public override void OnActivate()
    {
        Start();
        base.OnActivate();
    }

    public override void OnDeactivate()
    {
        Stop();
        base.OnDeactivate();
    }

    public override void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _invalidated.OnCompleted();
        _invalidated.Dispose();
        base.Dispose();
    }
}