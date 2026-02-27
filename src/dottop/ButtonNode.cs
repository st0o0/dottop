using R3;
using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop;

public sealed class ButtonNode : LayoutNode, IInvalidatingNode, IFocusable
{
    private readonly Subject<Unit> _invalidated = new();
    private readonly Subject<Unit> _clicked = new();
    private readonly string _label;
    private readonly Action? _onClick;

    private bool _disposed;

    // States
    private Color _foreground = Color.White;
    private Color _pressedBackground = Color.Blue;
    private bool _isDisabled;

    public Observable<Unit> Invalidated => _invalidated.AsObservable();
    public Observable<Unit> Clicked => _clicked.AsObservable();
    public bool HasFocus { get; private set; }
    public bool CanFocus => true;
    public int FocusPriority => 20;

    public ButtonNode(string label, Action? onClick = null)
    {
        _label = label;
        _onClick = onClick;
    }

    public ButtonNode WithColors(Color fg)
    {
        _foreground = fg;
        return this;
    }

    public ButtonNode WithPressedColor(Color pressedBg)
    {
        _pressedBackground = pressedBg;
        return this;
    }

    public ButtonNode Disabled()
    {
        _isDisabled = true;
        return this;
    }

    public override Size Measure(Size available)
    {
        var padding = 4;
        var labelWidth = _label.Length;
        var minWidth = labelWidth + padding;
        var width = WidthConstraint.Compute(available.Width, minWidth, available.Width);
        return new Size(width, 1);
    }

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea) return;

        var subContext = context.CreateSubContext(bounds);

        DrawSimpleBorder(subContext, bounds);

        var bgColor = _isDisabled ? _foreground : _pressedBackground;

        var fgColor = _isDisabled ? Color.Gray : _foreground;

        var innerX = bounds.X + 1;
        var innerY = bounds.Y + 1;
        var innerWidth = bounds.Width - 2;
        var innerHeight = bounds.Height - 2;

        subContext.SetBackground(bgColor);
        subContext.SetForeground(fgColor);

        for (var y = innerY; y < innerY + innerHeight; y++)
        {
            for (var x = innerX; x < innerX + innerWidth; x++)
            {
                subContext.WriteAt(x, y, ' ');
            }
        }

        var textX = innerX + (innerWidth - _label.Length) / 2;
        subContext.WriteAt(textX, innerY, _label);

        subContext.ResetColors();
    }

    private static void DrawSimpleBorder(IRenderContext context, Rect bounds)
    {
        context.SetForeground(Color.White);
        context.WriteAt(bounds.X, bounds.Y, '┌');
        context.WriteAt(bounds.X + bounds.Width - 1, bounds.Y, '┐');
        context.WriteAt(bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, '┘');
        context.WriteAt(bounds.X, bounds.Y + bounds.Height - 1, '└');

        for (var x = bounds.X + 1; x < bounds.X + bounds.Width - 1; x++)
        {
            context.WriteAt(x, bounds.Y, '─');
            context.WriteAt(x, bounds.Y + bounds.Height - 1, '─');
        }

        for (var y = bounds.Y + 1; y < bounds.Y + bounds.Height - 1; y++)
        {
            context.WriteAt(bounds.X, y, '│');
            context.WriteAt(bounds.X + bounds.Width - 1, y, '│');
        }
    }

    public bool HandleInput(ConsoleKeyInfo key)
    {
        return false;
    }

    public void OnFocused()
    {
        HasFocus = true;
        Invalidate();
    }

    public void OnBlurred()
    {
        HasFocus = false;
        Invalidate();
    }

    private void TriggerClick()
    {
        if (_isDisabled) return;
        _clicked.OnNext(Unit.Default);
        _onClick?.Invoke();
    }

    private void Invalidate()
    {
        if (!_disposed)
            _invalidated.OnNext(Unit.Default);
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _invalidated.OnCompleted();
        _invalidated.Dispose();
        _clicked.OnCompleted();
        _clicked.Dispose();
        base.Dispose();
    }
}