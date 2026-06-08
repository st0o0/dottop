namespace dottop.App.Rendering;

public static class SparklineRenderer
{
    private const string Blocks = " ▁▂▃▄▅▆▇█";

    public static string Render(IReadOnlyList<double> values, int width = 8)
    {
        if (values.Count == 0)
            return new string(' ', width);

        var start = Math.Max(0, values.Count - width);
        var count = Math.Min(values.Count, width);
        var padLeft = width - count;

        return string.Create(width, (values, start, count, padLeft), static (span, state) =>
        {
            var (vals, s, c, pad) = state;
            for (var i = 0; i < pad; i++)
                span[i] = ' ';
            for (var i = 0; i < c; i++)
            {
                var v = Math.Clamp(vals[s + i], 0, 100);
                if (v == 0)
                {
                    span[pad + i] = ' ';
                }
                else
                {
                    var idx = (int)(v / 100.0 * 7) + 1;
                    idx = Math.Min(idx, 8);
                    span[pad + i] = Blocks[idx];
                }
            }
        });
    }
}
