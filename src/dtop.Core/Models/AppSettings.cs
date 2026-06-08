namespace dtop.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "dark";
    public int RefreshIntervalMs { get; set; } = 1000;
    public string DefaultSort { get; set; } = "ram";
    public string DefaultGroup { get; set; } = "all";
    public string GraphStyle { get; set; } = "blocks";
    public string Language { get; set; } = "system";
}
