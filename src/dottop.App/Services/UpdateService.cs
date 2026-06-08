using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace dottop.App.Services;

public class UpdateService
{
    private const string Owner = "st0o0";
    private const string Repo = "dottop";
    private static readonly HttpClient Http = new();

    public string? LatestVersion { get; private set; }
    public string? DownloadUrl { get; private set; }
    public bool UpdateAvailable { get; private set; }

    public string CurrentVersion { get; } = typeof(UpdateService).Assembly
        .GetName().Version?.ToString(3) ?? "0.0.0";

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.TryParseAdd("dottop");
            var response = await Http.GetStringAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

            var doc = JsonDocument.Parse(response);
            var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            LatestVersion = tagName.TrimStart('v');

            if (IsNewer(LatestVersion, CurrentVersion))
            {
                UpdateAvailable = true;

                var rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "win-x64"
                    : "linux-x64";
                var assets = doc.RootElement.GetProperty("assets");
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Contains(rid))
                    {
                        DownloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }
        }
        catch
        {
            // Silently fail - update check is optional
        }
    }

    public async Task<bool> PerformUpdateAsync(Action<string>? onProgress = null)
    {
        if (DownloadUrl is null)
        {
            return false;
        }

        try
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var tempDir = Path.Combine(Path.GetTempPath(), "dottop-update");
            Directory.CreateDirectory(tempDir);

            onProgress?.Invoke("Downloading...");
            var archivePath = Path.Combine(tempDir, isWindows ? "update.zip" : "update.tar.gz");
            var bytes = await Http.GetByteArrayAsync(DownloadUrl);
            await File.WriteAllBytesAsync(archivePath, bytes);

            onProgress?.Invoke("Extracting...");
            var extractDir = Path.Combine(tempDir, "extracted");
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true);
            }

            if (isWindows)
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, extractDir);
            }
            else
            {
                var process = Process.Start("tar", $"-xzf \"{archivePath}\" -C \"{extractDir}\"");
                process?.WaitForExit(30000);
            }

            var exeName = isWindows ? "dottop.exe" : "dottop";
            var newBinary = Directory.GetFiles(extractDir, exeName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (newBinary is null)
            {
                return false;
            }

            var currentBinary = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (currentBinary is null)
            {
                return false;
            }

            if (isWindows)
            {
                var script = Path.Combine(tempDir, "update.cmd");
                File.WriteAllText(script, $"""
                    @echo off
                    timeout /t 2 /nobreak >nul
                    copy /y "{newBinary}" "{currentBinary}"
                    start "" "{currentBinary}"
                    del "%~f0"
                    """);
                Process.Start(new ProcessStartInfo("cmd", $"/c \"{script}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else
            {
                File.Copy(newBinary, currentBinary, overwrite: true);
                Process.Start("chmod", $"+x \"{currentBinary}\"")?.WaitForExit(5000);
                Process.Start(currentBinary);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        if (Version.TryParse(latest, out var l) && Version.TryParse(current, out var c))
        {
            return l > c;
        }

        return false;
    }
}
