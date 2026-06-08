using Termina.Terminal;

namespace dottop.UI.Tests.Helpers;

/// <summary>
/// Assertion helpers wrapping VirtualTerminal queries with xUnit Assert for
/// readable test failure messages.
/// </summary>
public static class ScreenAssert
{
    /// <summary>
    /// Assert that the terminal screen contains the given text somewhere.
    /// </summary>
    public static void Contains(this VirtualTerminal terminal, string text)
    {
        var screen = terminal.ToString();
        Assert.True(screen.Contains(text),
            $"the screen should display \"{text}\".\nActual screen:\n{screen}");
    }

    /// <summary>
    /// Assert that the terminal screen does NOT contain the given text.
    /// </summary>
    public static void DoesNotContain(this VirtualTerminal terminal, string text)
    {
        var screen = terminal.ToString();
        Assert.False(screen.Contains(text),
            $"the screen should not display \"{text}\".\nActual screen:\n{screen}");
    }

    /// <summary>
    /// Assert that a specific line (0-based) contains the given text.
    /// </summary>
    public static void LineContains(this VirtualTerminal terminal, int line, string text)
    {
        var lineContent = terminal.GetLine(line);
        Assert.True(lineContent.Contains(text),
            $"line {line} should contain \"{text}\".\nActual line: \"{lineContent}\"");
    }

    /// <summary>
    /// Poll until the given text appears on screen, or throw after timeout.
    /// </summary>
    public static async Task WaitForTextAsync(
        this VirtualTerminal terminal,
        string text,
        int timeoutMs = 5000,
        int pollIntervalMs = 100)
    {
        var deadline = Environment.TickCount64 + timeoutMs;

        while (Environment.TickCount64 < deadline)
        {
            if (terminal.Contains(text))
            {
                return;
            }

            await Task.Delay(pollIntervalMs);
        }

        // Final check with assertion for a clear error message
        var screen = terminal.ToString();
        Assert.True(screen.Contains(text),
            $"text \"{text}\" should have appeared within {timeoutMs}ms.\nFinal screen:\n{screen}");
    }
}
