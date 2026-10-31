using System.Drawing.Imaging;
using ASnapdo.UI;

// Rebuild the Windows icon directly from the same original vector mark as the header.
string output = args.Length == 1 ? args[0] : "src/ASnapdo/Assets/ASnapdo.ico";
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
var frames = sizes.Select(size =>
{
    using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var graphics = Graphics.FromImage(bitmap)) BrandMark.Draw(graphics, new(size, size));
    using var stream = new MemoryStream(); bitmap.Save(stream, ImageFormat.Png); return stream.ToArray();
}).ToArray();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
using var writer = new BinaryWriter(File.Create(output));
writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
int offset = 6 + sizes.Length * 16;
for (int i = 0; i < sizes.Length; i++)
{
    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
    writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
}
foreach (var frame in frames) writer.Write(frame);
Console.WriteLine($"Created {output} ({sizes.Length} sizes, 16–256 px)");
