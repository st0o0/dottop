using R3;
using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Nodes;

public sealed class DataListNode<T> : LayoutNode, IFocusable, IInvalidatingNode
{
    private readonly Subject<Unit> _invalidated = new();
    private readonly Subject<T> _itemSelected = new();
    private readonly Subject<Unit> _cancelled = new();
    private readonly Func<T, string> _formatter;
    private readonly Func<T, Color>? _colorSelector;

    private IReadOnlyList<T> _items = [];
    private int _selectedIndex;
    private int _scrollOffset;
    private int _viewportHeight = 20;
    private bool _hasFocus;
    private bool _disposed;

    private Color _selectedFg = Color.White;
    private Color _selectedBg = Color.BrightBlue;

    public DataListNode(Func<T, string> formatter, Func<T, Color>? colorSelector = null)
    {
        _formatter = formatter;
        _colorSelector = colorSelector;
    }

    public Observable<Unit> Invalidated => _invalidated.AsObservable();
    public Observable<T> ItemSelected => _itemSelected.AsObservable();
    public Observable<Unit> Cancelled => _cancelled.AsObservable();

    public int SelectedIndex => _selectedIndex;
    public T? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : default;

    public bool CanFocus => true;
    public bool HasFocus => _hasFocus;
    public int FocusPriority => 10;

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
            _selectedIndex = Math.Max(0, _items.Count - 1);
        Invalidate();
    }

    public void OnFocused()
    {
        _hasFocus = true;
        Invalidate();
    }

    public void OnBlurred()
    {
        _hasFocus = false;
        Invalidate();
    }

    public bool HandleInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                    EnsureVisible();
                    Invalidate();
                }
                return true;

            case ConsoleKey.DownArrow:
                if (_selectedIndex < _items.Count - 1)
                {
                    _selectedIndex++;
                    EnsureVisible();
                    Invalidate();
                }
                return true;

            case ConsoleKey.Home:
                _selectedIndex = 0;
                _scrollOffset = 0;
                Invalidate();
                return true;

            case ConsoleKey.End:
                _selectedIndex = Math.Max(0, _items.Count - 1);
                EnsureVisible();
                Invalidate();
                return true;

            case ConsoleKey.PageUp:
                _selectedIndex = Math.Max(0, _selectedIndex - _viewportHeight);
                EnsureVisible();
                Invalidate();
                return true;

            case ConsoleKey.PageDown:
                _selectedIndex = Math.Min(_items.Count - 1, _selectedIndex + _viewportHeight);
                EnsureVisible();
                Invalidate();
                return true;

            case ConsoleKey.Enter:
                if (SelectedItem is { } item)
                    _itemSelected.OnNext(item);
                return true;

            case ConsoleKey.Escape:
                _cancelled.OnNext(Unit.Default);
                return true;

            default:
                return false;
        }
    }

    private void EnsureVisible()
    {
        if (_selectedIndex < _scrollOffset)
            _scrollOffset = _selectedIndex;
        else if (_selectedIndex >= _scrollOffset + _viewportHeight)
            _scrollOffset = _selectedIndex - _viewportHeight + 1;
    }

    public override Size Measure(Size available)
    {
        _viewportHeight = HeightConstraint.Compute(available.Height, _items.Count, available.Height);
        var width = WidthConstraint.Compute(available.Width, available.Width, available.Width);
        return new Size(width, _viewportHeight);
    }

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea) return;

        _viewportHeight = bounds.Height;
        var ctx = context.CreateSubContext(bounds);
        var showScrollbar = _items.Count > _viewportHeight;
        var contentWidth = showScrollbar ? bounds.Width - 1 : bounds.Width;

        for (var row = 0; row < _viewportHeight; row++)
        {
            var itemIdx = _scrollOffset + row;
            if (itemIdx >= _items.Count) break;

            var item = _items[itemIdx];
            var text = _formatter(item);
            if (text.Length > contentWidth)
                text = text[..(contentWidth - 1)] + "…";
            else if (text.Length < contentWidth)
                text = text.PadRight(contentWidth);

            if (itemIdx == _selectedIndex && _hasFocus)
            {
                ctx.SetForeground(_selectedFg);
                ctx.SetBackground(_selectedBg);
            }
            else if (_colorSelector is not null)
            {
                ctx.SetForeground(_colorSelector(item));
            }

            ctx.WriteAt(0, row, text);
            ctx.ResetColors();
        }

        if (showScrollbar)
            RenderScrollbar(ctx, bounds.Width - 1, _viewportHeight);
    }

    private void RenderScrollbar(IRenderContext ctx, int x, int height)
    {
        var totalItems = _items.Count;
        if (totalItems <= height) return;

        var thumbSize = Math.Max(1, height * height / totalItems);
        var maxThumbTop = height - thumbSize;
        var maxScroll = Math.Max(1, totalItems - height);
        var thumbTop = (int)((float)_scrollOffset / maxScroll * maxThumbTop);

        for (var row = 0; row < height; row++)
        {
            var isThumb = row >= thumbTop && row < thumbTop + thumbSize;
            ctx.SetForeground(isThumb ? Color.White : Color.BrightBlack);
            ctx.WriteAt(x, row, isThumb ? '█' : '░');
        }
        ctx.ResetColors();
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
        _itemSelected.OnCompleted();
        _itemSelected.Dispose();
        _cancelled.OnCompleted();
        _cancelled.Dispose();
        base.Dispose();
    }
}
