namespace dottop.Plugin.Abstractions;

public record PluginTabInfo(string Label, string Route, ConsoleKey? HotKey = null, Type? PageType = null, Type? ViewModelType = null);
