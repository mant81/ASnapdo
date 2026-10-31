using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ASnapdo.Services;

public enum PixelEffectKind { Mosaic, Blur }

public static class PixelEffectService
{
    public static Bitmap Create(Bitmap source, Rectangle region, PixelEffectKind effect, int strength)
    {
        region.Intersect(new Rectangle(Point.Empty, source.Size));
        if (region.Width < 1 || region.Height < 1) throw new ArgumentException("효과를 적용할 영역이 없습니다.");
        var result = source.Clone(region, PixelFormat.Format32bppArgb);
        try
        {
            var data = result.LockBits(new Rectangle(Point.Empty, result.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int width = result.Width, height = result.Height, row = checked(width * 4);
                var pixels = new byte[checked(row * height)];
                for (int y = 0; y < height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * row, row);
                strength = Math.Clamp(strength, 2, 64);
                if (effect == PixelEffectKind.Mosaic) Mosaic(pixels, width, height, strength);
                else pixels = BlurPass(BlurPass(pixels, width, height, strength, false), width, height, strength, true);
                for (int y = 0; y < height; y++) Marshal.Copy(pixels, y * row, data.Scan0 + y * data.Stride, row);
            }
            finally { result.UnlockBits(data); }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    private static void Mosaic(byte[] pixels, int width, int height, int block)
    {
        for (int top = 0; top < height; top += block)
            for (int left = 0; left < width; left += block)
            {
                int right = Math.Min(width, left + block), bottom = Math.Min(height, top + block);
                long b = 0, g = 0, r = 0, a = 0;
                for (int y = top; y < bottom; y++)
                    for (int x = left; x < right; x++) Sum(pixels, (y * width + x) * 4, 1, ref b, ref g, ref r, ref a);
                int count = (right - left) * (bottom - top);
                for (int y = top; y < bottom; y++)
                    for (int x = left; x < right; x++) Write(pixels, (y * width + x) * 4, count, b, g, r, a);
            }
    }
    private static byte[] BlurPass(byte[] source, int width, int height, int radius, bool vertical)
    {
        var target = new byte[source.Length];
        int lines = vertical ? width : height, length = vertical ? height : width, stride = vertical ? width * 4 : 4;
        for (int line = 0; line < lines; line++)
        {
            int start = vertical ? line * 4 : line * width * 4;
            long b = 0, g = 0, r = 0, a = 0;
            int right = Math.Min(length - 1, radius), left = 0;
            for (int i = 0; i <= right; i++) Sum(source, start + i * stride, 1, ref b, ref g, ref r, ref a);
            for (int position = 0; position < length; position++)
            {
                Write(target, start + position * stride, right - left + 1, b, g, r, a);
                int nextLeft = Math.Max(0, position + 1 - radius), nextRight = Math.Min(length - 1, position + 1 + radius);
                while (left < nextLeft) Sum(source, start + left++ * stride, -1, ref b, ref g, ref r, ref a);
                while (right < nextRight) Sum(source, start + ++right * stride, 1, ref b, ref g, ref r, ref a);
            }
        }
        return target;
    }
    private static void Sum(byte[] p, int i, int sign, ref long b, ref long g, ref long r, ref long a)
    {
        long alpha = p[i + 3] * sign;
        b += p[i] * alpha; g += p[i + 1] * alpha; r += p[i + 2] * alpha; a += alpha;
    }
    private static void Write(byte[] p, int i, int count, long b, long g, long r, long a)
    {
        p[i] = a == 0 ? (byte)0 : (byte)(b / a); p[i + 1] = a == 0 ? (byte)0 : (byte)(g / a);
        p[i + 2] = a == 0 ? (byte)0 : (byte)(r / a); p[i + 3] = (byte)(a / count);
    }
}
