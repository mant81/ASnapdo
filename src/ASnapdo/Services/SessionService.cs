using System.Text.Json;
using System.Text.Json.Serialization;
using ASnapdo.Models;

namespace ASnapdo.Services;

public sealed record RestoredSession(List<CaptureDocument> Documents, List<Point> Scrolls, int SelectedIndex);
public static class SessionService
{
    private sealed record State(List<SessionDocument> Documents, List<Point> Scrolls, int SelectedIndex);
    private sealed class ColorConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => Color.FromArgb(reader.GetInt32());
        public override void Write(Utf8JsonWriter writer, Color color, JsonSerializerOptions options) => writer.WriteNumberValue(color.ToArgb());
    }
    private static readonly JsonSerializerOptions Options = new() { Converters = { new ColorConverter() } };
    public static string PathName => Path.Combine(SettingsService.Folder, "session.json");
    public static void Save(IEnumerable<CaptureDocument> documents, List<Point> scrolls, int selectedIndex, string? path = null)
    {
        path ??= PathName; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        using (var stream = File.Create(temporary)) JsonSerializer.Serialize(stream, new State(documents.Select(d => d.ExportSession()).ToList(), scrolls, selectedIndex), Options);
        File.Move(temporary, path, true);
    }
    public static RestoredSession Load(string? path = null)
    {
        path ??= PathName;
        if (!File.Exists(path)) return new([], [], -1);
        using var stream = File.OpenRead(path);
        var state = JsonSerializer.Deserialize<State>(stream, Options) ?? throw new InvalidDataException("편집 상태가 비어 있습니다.");
        List<CaptureDocument> docs = [];
        try { foreach (var doc in state.Documents) docs.Add(CaptureDocument.ImportSession(doc)); return new(docs, state.Scrolls, state.SelectedIndex); }
        catch { foreach (var doc in docs) doc.Dispose(); throw; }
    }
    public static void Clear() { if (File.Exists(PathName)) File.Delete(PathName); }
}
