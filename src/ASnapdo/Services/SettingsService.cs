using System.Text.Json;

namespace ASnapdo.Services;

public sealed class AppSettings
{
    public Keys RegionHotkey { get; set; } = Keys.PrintScreen;
    public Keys FullscreenHotkey { get; set; } = Keys.Control | Keys.Shift | Keys.D1;
    public bool AutoCopy { get; set; } = true;
    public bool NewCaptureSelectRegion { get; set; } = true;
    public bool ShowGallery { get; set; }
    public bool AutoSave { get; set; }
    public string AutoSaveFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ASnapdo");
}

public static class SettingsService
{
    public static string Folder => Environment.GetEnvironmentVariable("ASNAPDO_DATA_DIR") is { Length: > 0 } folder ? folder : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASnapdo");
    public static AppSettings Load()
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, "settings.json");
        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ACapdo", "settings.json");
        // Keep the complete legacy file and unknown fields intact. Never modify the source.
        if (!File.Exists(path) && File.Exists(legacy))
        {
            if (new FileInfo(legacy).Length > 1024 * 1024) throw new InvalidDataException("이전 설정 파일이 1MiB 제한을 초과합니다.");
            File.Copy(legacy, path, false);
        }
        if (!File.Exists(path)) return new();
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 1024 * 1024) throw new InvalidDataException("설정 파일이 1MiB 제한을 초과합니다.");
            return JsonSerializer.Deserialize<AppSettings>(stream) ?? new();
        }
        catch (JsonException) { File.Copy(path, path + $".invalid-{DateTime.Now:yyyyMMddHHmmssfff}", false); return new(); }
    }
    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, "settings.json");
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
