using dottop.Themes;
using R3;
using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Nodes;

public interface IScrollableList
{
    void MoveUp();
    void MoveDown();
    void MoveToTop();
    void MoveToEnd();
    void PageUp();
    void PageDown();
}

public readonly record struct ColorSpan(int Start, int Length, Color Foreground);

public sealed class DataListNode<T> : LayoutNode, IInvalidatingNode, IScrollableList
{
    private readonly Subject<Unit> _invalidated = new();
    private readonly Func<T, string> _formatter;
    private readonly Func<T, Color>? _colorSelector;
    private readonly Func<T, IReadOnlyList<ColorSpan>>? _colorSpanSelector;

    private IReadOnlyList<T> _items = [];
    private int _selectedIndex;
    private int _scrollOffset;
    private int _viewportHeight = 20;
    private bool _disposed;

    private Color _selectedFg = Theme.SelectionText;
    private Color _selectedBg = Theme.Selection;

    public DataListNode(Func<T, string> formatter, Func<T, Color>? colorSelector = null,
        Func<T, IReadOnlyList<ColorSpan>>? colorSpanSelector = null)
    {
        _formatter = formatter;
        _colorSelector = colorSelector;
        _colorSpanSelector = colorSpanSelector;
    }

    public Observable<Unit> Invalidated => _invalidated.AsObservable();

    public int SelectedIndex => _selectedIndex;
    public T? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : default;

    public DataListNode<T> WithHighlightColors(Color fg, Color bg)
    {
        _selectedFg = fg;
        _selectedBg = bg;
        return this;
    }

    public void SetItems(IReadOnlyList<T> items)
    {
        _items = items;
        if (_selectedIndex >= _items.Count)
        {
            _selectedIndex = Math.Max(0, _items.Count - 1);
        }

        Invalidate();
    }

    public void MoveUp()
    {
        if (_selectedIndex > 0)
        {
            _selectedIndex--;
            EnsureVisible();
            Invalidate();
        }
    }

    public void MoveDown()
    {
        if (_selectedIndex < _items.Count - 1)
        {
            _selectedIndex++;
            EnsureVisible();
            Invalidate();
        }
    }

    public void MoveToTop()
    {
        _selectedIndex = 0;
        _scrollOffset = 0;
        Invalidate();
    }

    public void MoveToEnd()
    {
        _selectedIndex = Math.Max(0, _items.Count - 1);
        EnsureVisible();
        Invalidate();
    }

    public void PageUp()
    {
        _selectedIndex = Math.Max(0, _selectedIndex - _viewportHeight);
        EnsureVisible();
        Invalidate();
    }

    public void PageDown()
    {
        _selectedIndex = Math.Min(Math.Max(0, _items.Count - 1), _selectedIndex + _viewportHeight);
        EnsureVisible();
        Invalidate();
    }

    private void EnsureVisible()
    {
        if (_selectedIndex < _scrollOffset)
        {
            _scrollOffset = _selectedIndex;
        }
        else if (_selectedIndex >= _scrollOffset + _viewportHeight)
        {
            _scrollOffset = _selectedIndex - _viewportHeight + 1;
        }
    }

    public override Size Measure(Size available)
    {
        _viewportHeight = HeightConstraint.Compute(available.Height, _items.Count, available.Height);
        var width = WidthConstraint.Compute(available.Width, available.Width, available.Width);
        return new Size(width, _viewportHeight);
    }

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea)
        {
            return;
        }

        _viewportHeight = bounds.Height;
        var ctx = context.CreateSubContext(bounds);

        if (Theme.Background != Color.Default)
        {
            ctx.SetBackground(Theme.Background);
            ctx.Fill(0, 0, bounds.Width, bounds.Height);
        }

        var showScrollbar = _items.Count > _viewportHeight;
        var contentWidth = showScrollbar ? bounds.Width - 1 : bounds.Width;

        for (var row = 0; row < _viewportHeight; row++)
        {
            var itemIdx = _scrollOffset + row;
            if (itemIdx >= _items.Count)
            {
                ctx.Fill(0, row, contentWidth, 1, ' ');
                continue;
            }

            var item = _items[itemIdx];
            var text = _formatter(item);
            if (text.Length > contentWidth)
            {
                text = text[..(contentWidth - 1)] + "…";
            }
            else if (text.Length < contentWidth)
            {
                text = text.PadRight(contentWidth);
            }

            if (itemIdx == _selectedIndex && _colorSpanSelector is not null)
            {
                var spans = _colorSpanSelector(item);
                RenderWithColorSpans(ctx, row, text, spans);
            }
            else if (itemIdx == _selectedIndex)
            {
                ctx.SetForeground(_selectedFg);
                ctx.SetBackground(_selectedBg);
                ctx.WriteAt(0, row, text);
                ctx.ResetColors();
            }
            else if (_colorSelector is not null)
            {
                ctx.SetForeground(_colorSelector(item));
                if (Theme.Background != Color.Default)
                    ctx.SetBackground(Theme.Background);
                ctx.WriteAt(0, row, text);
                ctx.ResetColors();
            }
            else
            {
                if (Theme.Background != Color.Default)
                    ctx.SetBackground(Theme.Background);
                ctx.WriteAt(0, row, text);
                ctx.ResetColors();
            }
        }

        if (showScrollbar)
        {
            RenderScrollbar(ctx, bounds.Width - 1, _viewportHeight);
        }
    }

    private void RenderWithColorSpans(IRenderContext ctx, int row, string text, IReadOnlyList<ColorSpan> spans)
    {
        // Render selection background for the whole row first
        ctx.SetForeground(_selectedFg);
        ctx.SetBackground(_selectedBg);
        ctx.WriteAt(0, row, text);

        // Overlay color spans with their own foreground but keep selection background
        foreach (var span in spans)
        {
            if (span.Start >= text.Length) continue;
            var end = Math.Min(span.Start + span.Length, text.Length);
            var segment = text[span.Start..end];
            ctx.SetForeground(span.Foreground);
            ctx.SetBackground(_selectedBg);
            ctx.WriteAt(span.Start, row, segment);
        }

        ctx.ResetColors();
    }

    private void RenderScrollbar(IRenderContext ctx, int x, int height)
    {
        var totalItems = _items.Count;
        if (totalItems <= height)
        {
            return;
        }

        var thumbSize = Math.Max(1, height * height / totalItems);
        var maxThumbTop = height - thumbSize;
        var maxScroll = Math.Max(1, totalItems - height);
        var thumbTop = (int)((float)_scrollOffset / maxScroll * maxThumbTop);

        for (var row = 0; row < height; row++)
        {
            var isThumb = row >= thumbTop && row < thumbTop + thumbSize;
            ctx.SetForeground(isThumb ? Theme.Text : Theme.Header);
            ctx.WriteAt(x, row, isThumb ? '█' : '░');
        }
        ctx.ResetColors();
    }

    private void Invalidate()
    {
        if (!_disposed)
        {
            _invalidated.OnNext(Unit.Default);
        }
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _invalidated.OnCompleted();
        _invalidated.Dispose();
        base.Dispose();
    }
}
