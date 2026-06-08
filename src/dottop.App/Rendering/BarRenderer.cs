namespace dottop.App.Rendering;

public static class BarRenderer
{
    public static string Render(double percent, int width = 6)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var filled = (int)(clamped / 100.0 * width);
        return $"[{new string('█', filled)}{new string(' ', width - filled)}]";
    }
}
