namespace ASnapdo.Services;

public static class LicenseInfo
{
    public static string Terms
    {
        get
        {
            using var stream = typeof(LicenseInfo).Assembly.GetManifestResourceStream("ASnapdo.License.txt")
                ?? throw new InvalidOperationException("이용약관 리소스가 없습니다.");
            using var reader = new StreamReader(stream); return reader.ReadToEnd();
        }
    }
}
