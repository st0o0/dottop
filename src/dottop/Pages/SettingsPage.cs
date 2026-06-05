using dottop.Nodes;
using dottop.Resources;
using dottop.Themes;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class SettingsPage : ReactivePage<SettingsViewModel>
{
    private SettingsListNode? _settingsListNode;

    public override ILayoutNode BuildLayout()
    {
        _settingsListNode = new SettingsListNode(ViewModel);

        return Layouts.Vertical()
            .WithChild(new TabBarNode(4))
            .WithChild(new PanelNode()
                .WithTitle(Strings.SettingsTitle)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Primary)
                .WithContent(_settingsListNode)
                .Fill())
            .WithChild(BuildStatusBar());
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.SettingsChanged.Subscribe(_ => _settingsListNode?.RequestInvalidate())
            .DisposeWith(Subscriptions);
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(Theme.StatusBarText).WithBackground(Theme.StatusBar))
            .AsLayout().Height(1);
    }
}

public sealed class SettingsListNode : LayoutNode, IInvalidatingNode
{
    private readonly SettingsViewModel _viewModel;
    private readonly Subject<Unit> _invalidated = new();
    private bool _disposed;

    public SettingsListNode(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public Observable<Unit> Invalidated => _invalidated.AsObservable();

    public void RequestInvalidate()
    {
        if (!_disposed)
        {
            _invalidated.OnNext(Unit.Default);
        }
    }

    public override Size Measure(Size available) => new(available.Width, available.Height);

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea) return;

        var ctx = context.CreateSubContext(bounds);

        for (var row = 0; row < _viewModel.RowCount; row++)
        {
            if (row >= bounds.Height) break;

            var label = _viewModel.GetLabel(row);
            var value = _viewModel.GetDisplayValue(row);
            var isSelected = row == _viewModel.SelectedRow.Value;

            var text = $"  {label,-20} {'◀'} {value} {'▶'}";
            if (text.Length < bounds.Width)
                text = text.PadRight(bounds.Width);
            else if (text.Length > bounds.Width)
                text = text[..bounds.Width];

            if (isSelected)
            {
                ctx.SetForeground(Theme.SelectionText);
                ctx.SetBackground(Theme.Selection);
            }
            else
            {
                ctx.SetForeground(Theme.Text);
            }

            ctx.WriteAt(0, row, text);
            ctx.ResetColors();
        }
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _invalidated.OnCompleted();
        _invalidated.Dispose();
        base.Dispose();
    }
}
