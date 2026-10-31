namespace ASnapdo.Services;

public static class ImageSafety
{
    public const long MaxFileBytes = 64L * 1024 * 1024;
    public const long MaxPixels = 64_000_000;
    public const int MaxDimension = 32768;
    public const int MaxEncodedCharacters = 96 * 1024 * 1024;

    public static void CheckSize(Size size)
    {
        if (size.Width < 1 || size.Height < 1 || size.Width > MaxDimension || size.Height > MaxDimension ||
            (long)size.Width * size.Height > MaxPixels)
            throw new InvalidDataException("이미지 크기 제한을 초과했습니다. 최대 6,400만 픽셀, 한 변 32,768픽셀까지 지원합니다.");
    }

    public static Bitmap LoadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxFileBytes) throw new InvalidDataException("이미지 파일은 64MiB 이하여야 합니다.");
        CheckHeader(stream);
        using var source = Image.FromStream(stream, false, false);
        CheckSize(source.Size);
        return new Bitmap(source);
    }

    public static Bitmap LoadBase64(string encoded)
    {
        if (string.IsNullOrEmpty(encoded) || encoded.Length > MaxEncodedCharacters)
            throw new InvalidDataException("저장된 이미지 크기가 올바르지 않습니다.");
        byte[] bytes = Convert.FromBase64String(encoded);
        try
        {
            if (bytes.LongLength > MaxFileBytes) throw new InvalidDataException("저장된 이미지가 64MiB를 초과합니다.");
            using var stream = new MemoryStream(bytes, false);
            CheckHeader(stream);
            using var source = Image.FromStream(stream, false, false);
            CheckSize(source.Size);
            return new Bitmap(source);
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }

    // Inspect dimensions before invoking GDI+, including compressed PNG/JPEG input.
    private static void CheckHeader(Stream stream)
    {
        Span<byte> header = stackalloc byte[32];
        int count = stream.ReadAtLeast(header, 26, false);
        if (count < 26) throw new InvalidDataException("이미지 헤더가 손상되었습니다.");
        if (header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && header.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            CheckDimensions(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[16..]),
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[20..]));
        }
        else if (header[0] == 'B' && header[1] == 'M')
        {
            uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header[14..]);
            if (size == 12)
                CheckDimensions(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[18..]),
                    System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[20..]));
            else if (size >= 40)
                CheckDimensions(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header[18..]),
                    Math.Abs((long)System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header[22..])));
            else throw new InvalidDataException("지원하지 않는 BMP 헤더입니다.");
        }
        else if (header[0] == 0xff && header[1] == 0xd8)
        {
            stream.Position = 2;
            bool found = false;
            while (stream.Position < Math.Min(stream.Length, 1024 * 1024))
            {
                if (Byte(stream) != 0xff) throw new InvalidDataException("JPEG 헤더가 손상되었습니다.");
                int marker; do { marker = Byte(stream); } while (marker == 0xff);
                if (marker is 0xda or 0xd9) break;
                if (marker == 0x01 || marker is >= 0xd0 and <= 0xd7) continue;
                int length = Word(stream);
                if (length < 2 || stream.Position + length - 2 > stream.Length) throw new InvalidDataException("JPEG 구간 크기가 올바르지 않습니다.");
                if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc))
                {
                    if (length < 8) throw new InvalidDataException("JPEG 크기 정보가 손상되었습니다.");
                    Byte(stream); int height = Word(stream), width = Word(stream);
                    CheckDimensions(width, height); found = true; break;
                }
                stream.Position += length - 2;
            }
            if (!found) throw new InvalidDataException("JPEG 크기를 확인할 수 없거나 메타데이터가 너무 큽니다.");
        }
        else throw new InvalidDataException("PNG, JPEG, BMP 이미지만 지원합니다.");
        stream.Position = 0;
    }
    private static int Byte(Stream stream) { int value = stream.ReadByte(); return value >= 0 ? value : throw new InvalidDataException("이미지 헤더가 잘렸습니다."); }
    private static int Word(Stream stream) => (Byte(stream) << 8) | Byte(stream);
    private static void CheckDimensions(long width, long height)
    {
        if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension || width * height > MaxPixels)
            throw new InvalidDataException("이미지 픽셀 또는 가로·세로 크기 제한을 초과했습니다.");
    }
}
