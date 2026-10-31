using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASnapdo.Models;

namespace ASnapdo.Services;

public sealed record RestoredSession(List<CaptureDocument> Documents, List<Point> Scrolls, int SelectedIndex);
public static class SessionService
{
    public const int MaxSessionBytes = 128 * 1024 * 1024;
    private const int MaxDocuments = 128, MaxAnnotations = 2048, MaxUndo = 256, MaxTotalAnnotations = 50_000;
    private const long MaxDecodedPixels = 128_000_000;
    private static readonly byte[] Header = "ASNAPDO-SESSION\0\x01"u8.ToArray();
    private static readonly byte[] Entropy = "AAStudio.kr/ASnapdo/session/v1"u8.ToArray();
    private sealed record State(List<SessionDocument> Documents, List<Point> Scrolls, int SelectedIndex);
    private sealed class ColorConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => Color.FromArgb(reader.GetInt32());
        public override void Write(Utf8JsonWriter writer, Color color, JsonSerializerOptions options) => writer.WriteNumberValue(color.ToArgb());
    }
    private static readonly JsonSerializerOptions Options = new() { MaxDepth = 32, Converters = { new ColorConverter() } };
    public static string PathName => Path.Combine(SettingsService.Folder, "session.json");

    public static void Save(IEnumerable<CaptureDocument> documents, List<Point> scrolls, int selectedIndex, string? path = null)
    {
        path ??= PathName;
        var list = documents.Take(MaxDocuments + 1).ToList();
        if (list.Count > MaxDocuments) throw new InvalidDataException("세션에는 최대 128개 탭을 보관할 수 있습니다.");
        var state = new State(list.Select(d => d.ExportSession()).ToList(), scrolls, selectedIndex);
        Validate(state);
        using var stream = new LimitedStream();
        byte[]? plaintext = null;
        try
        {
            JsonSerializer.Serialize(stream, state, Options);
            plaintext = stream.ToArray();
            WriteProtected(path, plaintext);
        }
        finally
        {
            if (plaintext != null) CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(stream.GetBuffer().AsSpan(0, (int)stream.Length));
        }
    }

    public static RestoredSession Load(string? path = null)
    {
        path ??= PathName;
        if (!File.Exists(path)) return new([], [], -1);
        byte[] stored;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length < 1 || stream.Length > MaxSessionBytes + 1024 * 1024)
                throw new InvalidDataException("세션 파일 크기가 올바르지 않습니다.");
            stored = new byte[(int)stream.Length]; stream.ReadExactly(stored);
        }
        byte[]? plaintext = null;
        List<CaptureDocument> docs = [];
        try
        {
            bool encrypted = stored.AsSpan().StartsWith(Header);
            if (encrypted)
            {
                try { plaintext = ProtectedData.Unprotect(stored[Header.Length..], Entropy, DataProtectionScope.CurrentUser); }
                catch (CryptographicException e) { throw new InvalidDataException("세션이 변조되었거나 현재 Windows 계정에서 복호화할 수 없습니다.", e); }
            }
            else plaintext = stored; // Validate and migrate the previous JSON format in place.
            if (plaintext.Length > MaxSessionBytes) throw new InvalidDataException("세션이 128MiB 제한을 초과합니다.");
            ValidateStructure(plaintext);
            var state = JsonSerializer.Deserialize<State>(plaintext, Options) ?? throw new InvalidDataException("편집 상태가 비어 있습니다.");
            Validate(state);
            foreach (var doc in state.Documents) docs.Add(CaptureDocument.ImportSession(doc));
            if (!encrypted) WriteProtected(path, plaintext);
            return new(docs, state.Scrolls, state.SelectedIndex);
        }
        catch
        {
            foreach (var doc in docs) doc.Dispose();
            throw;
        }
        finally
        {
            if (plaintext != null && !ReferenceEquals(plaintext, stored)) CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(stored);
        }
    }

    private static void WriteProtected(string path, byte[] plaintext)
    {
        if (plaintext.Length > MaxSessionBytes) throw new InvalidDataException("세션이 128MiB 제한을 초과합니다.");
        byte[] encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Header); stream.Write(encrypted); stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ValidateStructure(byte[] json)
    {
        using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var root = parsed.RootElement;
        var documents = ArrayProperty(root, "Documents", MaxDocuments);
        var scrolls = ArrayProperty(root, "Scrolls", MaxDocuments);
        if (documents.GetArrayLength() != scrolls.GetArrayLength()) throw new InvalidDataException("세션의 탭과 스크롤 정보가 일치하지 않습니다.");
        int total = 0;
        foreach (var doc in documents.EnumerateArray())
        {
            total += ArrayProperty(doc, "Annotations", MaxAnnotations).GetArrayLength();
            foreach (var undo in ArrayProperty(doc, "Undo", MaxUndo).EnumerateArray())
                total += ArrayProperty(undo, "Annotations", MaxAnnotations).GetArrayLength();
            if (total > MaxTotalAnnotations) throw new InvalidDataException("세션의 전체 주석 수가 제한을 초과합니다.");
        }
    }
    private static JsonElement ArrayProperty(JsonElement value, string name, int maximum)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var array) ||
            array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > maximum)
            throw new InvalidDataException("세션의 목록 형식 또는 개수가 올바르지 않습니다.");
        return array;
    }

    private static void Validate(State state)
    {
        if (state.Documents == null || state.Scrolls == null || state.Documents.Count > MaxDocuments ||
            state.Documents.Count != state.Scrolls.Count ||
            (state.Documents.Count == 0 ? state.SelectedIndex != -1 : state.SelectedIndex < 0 || state.SelectedIndex >= state.Documents.Count))
            throw new InvalidDataException("세션의 탭 정보가 올바르지 않습니다.");
        long pixels = 0; int annotations = 0;
        var sizes = new Dictionary<string, Size>(StringComparer.Ordinal);
        void Image(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length > ImageSafety.MaxEncodedCharacters)
                throw new InvalidDataException("세션 이미지 형식이 올바르지 않습니다.");
            if (!sizes.TryGetValue(encoded, out var size))
            {
                using var bitmap = ImageSafety.LoadBase64(encoded);
                size = bitmap.Size; sizes.Add(encoded, size);
            }
            pixels += (long)size.Width * size.Height;
            if (pixels > MaxDecodedPixels) throw new InvalidDataException("세션의 전체 이미지 크기가 제한을 초과합니다.");
        }
        void Annotations(List<Annotation> items)
        {
            if (items == null || items.Count > MaxAnnotations || (annotations += items.Count) > MaxTotalAnnotations)
                throw new InvalidDataException("세션 주석 수가 제한을 초과합니다.");
            foreach (var a in items)
            {
                if (a == null || !Enum.IsDefined(a.Kind) || a.Text == null || a.Text.Length > 16_384 ||
                    !ValidNumber(a.Bounds.X, -1_000_000, 1_000_000) || !ValidNumber(a.Bounds.Y, -1_000_000, 1_000_000) ||
                    !ValidNumber(a.Bounds.Width, 0, 1_000_000) || !ValidNumber(a.Bounds.Height, 0, 1_000_000) ||
                    !ValidNumber(a.Width, .01f, 4096) || !ValidNumber(a.FontSize, 1, 4096))
                    throw new InvalidDataException("세션 주석 값이 올바르지 않습니다.");
                if (a.Kind is AnnotationKind.Image or AnnotationKind.PixelPatch) Image(a.ImageData);
            }
        }
        foreach (var doc in state.Documents)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.Name) || doc.Name.Length > 255 || doc.Name.IndexOfAny(['/', '\\', '\0']) >= 0 ||
                !ValidNumber(doc.Zoom, .01f, 16) || doc.Undo == null || doc.Undo.Count > MaxUndo ||
                (doc.SavePath != null && (doc.SavePath.Length > 32767 || doc.SavePath.Contains('\0') || !Path.IsPathFullyQualified(doc.SavePath))))
                throw new InvalidDataException("세션 문서 값이 올바르지 않습니다.");
            Image(doc.Image); Annotations(doc.Annotations);
            foreach (var undo in doc.Undo)
            {
                if (undo == null) throw new InvalidDataException("실행 취소 정보가 올바르지 않습니다.");
                Annotations(undo.Annotations);
                if (undo.Image != null) Image(undo.Image);
            }
        }
        foreach (var scroll in state.Scrolls)
            if (scroll.X < 0 || scroll.Y < 0 || scroll.X > 1_000_000 || scroll.Y > 1_000_000)
                throw new InvalidDataException("세션 스크롤 값이 올바르지 않습니다.");
    }
    private static bool ValidNumber(float value, float minimum, float maximum) => float.IsFinite(value) && value >= minimum && value <= maximum;

    private sealed class LimitedStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
        private void Check(int count) { if (Position + count > MaxSessionBytes) throw new InvalidDataException("세션이 128MiB 제한을 초과합니다."); }
    }

    public static void Clear() { if (File.Exists(PathName)) File.Delete(PathName); }
}
