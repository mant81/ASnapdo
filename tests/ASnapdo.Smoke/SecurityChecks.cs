using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ASnapdo.Models;
using ASnapdo.Services;

internal static class SecurityChecks
{
    internal static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ASnapdo-security-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "session.json");
            using var image = new Bitmap(80, 60);
            using var source = new CaptureDocument(new Bitmap(image), "private-capture-marker.png");
            source.Annotations.Add(new Annotation { Kind = AnnotationKind.Text, Text = "private-annotation-marker", Bounds = new(1, 2, 30, 20) });
            SessionService.Save([source], [Point.Empty], 0, path);
            byte[] header = "ASNAPDO-SESSION\0\x01"u8.ToArray();
            byte[] encrypted = File.ReadAllBytes(path);
            Check(encrypted.AsSpan().StartsWith(header), "Versioned encrypted envelope");
            Check(!Encoding.UTF8.GetString(encrypted).Contains("private-"), "No plaintext document names or annotations on disk");
            Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "No temporary files left behind");
            Dispose(SessionService.Load(path));
            byte[] legacy = ProtectedData.Unprotect(encrypted[header.Length..], "AAStudio.kr/ASnapdo/session/v1"u8.ToArray(), DataProtectionScope.CurrentUser);
            try
            {
                var corrupted = (byte[])encrypted.Clone(); corrupted[corrupted.Length / 2] ^= 0x80;
                File.WriteAllBytes(path, corrupted);
                Reject(() => Dispose(SessionService.Load(path)), "Tampered ciphertext rejected");
                Check(File.ReadAllBytes(path).SequenceEqual(corrupted), "Rejected session is preserved");

                File.WriteAllBytes(path, legacy);
                var migrated = SessionService.Load(path);
                Check(migrated.Documents.Single().Name == source.Name, "Legacy session restored");
                Dispose(migrated);
                Check(File.ReadAllBytes(path).AsSpan().StartsWith(header), "Legacy session migrated to encryption");

                void Invalid(Action<JsonNode> change, string message)
                {
                    var node = JsonNode.Parse(legacy)!; change(node);
                    File.WriteAllText(path, node.ToJsonString());
                    byte[] before = File.ReadAllBytes(path);
                    Reject(() => Dispose(SessionService.Load(path)), message);
                    Check(File.ReadAllBytes(path).SequenceEqual(before), "Invalid legacy data not overwritten");
                }
                Invalid(node => node["Documents"] = null, "Null documents rejected");
                Invalid(node => node["Documents"]![0]!["Zoom"] = -1, "Invalid zoom rejected");
                Invalid(node => node["Documents"]![0]!["Name"] = "../capture.png", "Document path traversal rejected");
                Invalid(node => node["Documents"]![0]!["Annotations"]![0]!["Kind"] = 999, "Unknown annotation kind rejected");
                Invalid(node => node["Documents"]![0]!["Annotations"]![0]!["Bounds"]!["Width"] = -5, "Invalid geometry rejected");
                Invalid(node => node["Documents"]![0]!["Image"] = "not-an-image", "Invalid base64 image rejected");
                Invalid(node => node["Scrolls"] = new JsonArray(), "Mismatched viewport count rejected");
                Invalid(node => node["Documents"] = new JsonArray(Enumerable.Range(0, 129).Select(_ => node["Documents"]![0]!.DeepClone()).ToArray()), "Excess documents rejected before deserialization");

                File.WriteAllBytes(path, encrypted);
                source.Zoom = -1;
                Reject(() => SessionService.Save([source], [Point.Empty], 0, path), "Invalid save rejected");
                Check(File.ReadAllBytes(path).SequenceEqual(encrypted), "Failed save keeps prior encrypted session");
                source.Zoom = 1;
                using (var oversized = new FileStream(path, FileMode.Create)) oversized.SetLength(SessionService.MaxSessionBytes + 1024L * 1024 + 1);
                Reject(() => Dispose(SessionService.Load(path)), "Oversized session rejected before reading");
            }
            finally { CryptographicOperations.ZeroMemory(legacy); }

            foreach (var format in new[] { System.Drawing.Imaging.ImageFormat.Png, System.Drawing.Imaging.ImageFormat.Jpeg, System.Drawing.Imaging.ImageFormat.Bmp })
            {
                string valid = Path.Combine(folder, format.Guid + ".image");
                image.Save(valid, format);
                using var loaded = ImageSafety.LoadFile(valid);
                Check(loaded.Size == image.Size, "Supported image header accepted");
            }
            string? previousFolder = Environment.GetEnvironmentVariable("ASNAPDO_DATA_DIR");
            Environment.SetEnvironmentVariable("ASNAPDO_DATA_DIR", folder);
            try
            {
                using (var hugeSettings = new FileStream(Path.Combine(folder, "settings.json"), FileMode.Create)) hugeSettings.SetLength(1024 * 1024 + 1);
                Reject(() => SettingsService.Load(), "Oversized settings rejected before reading");
            }
            finally { Environment.SetEnvironmentVariable("ASNAPDO_DATA_DIR", previousFolder); }
            string oversizedImage = Path.Combine(folder, "oversized.bmp");
            using (var wide = new Bitmap(ImageSafety.MaxDimension + 1, 1)) wide.Save(oversizedImage, System.Drawing.Imaging.ImageFormat.Bmp);
            Reject(() => { using var bitmap = ImageSafety.LoadFile(oversizedImage); }, "Oversized dimensions rejected before cloning");
            using (var sparse = new FileStream(oversizedImage, FileMode.Create)) sparse.SetLength(ImageSafety.MaxFileBytes + 1);
            Reject(() => { using var bitmap = ImageSafety.LoadFile(oversizedImage); }, "Oversized image file rejected before decoding");
            Reject(() => ImageSafety.CheckSize(new(10000, 10000)), "Pixel budget enforced");
            Console.WriteLine("PASS: DPAPI encryption, integrity, legacy migration, save preservation, session structure/geometry and image resource limits.");
        }
        finally { Directory.Delete(folder, true); }
    }
    private static void Dispose(RestoredSession state) { foreach (var doc in state.Documents) doc.Dispose(); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or FormatException or ArgumentException) { return; }
        throw new Exception(message);
    }
}
