namespace ASnapdo.UI;

internal static class AppIcon
{
    public static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("ASnapdo.Assets.ASnapdo.ico")
            ?? throw new InvalidOperationException("ASnapdo 아이콘 리소스가 없습니다.");
        using var icon = new Icon(stream, new Size(32, 32));
        return (Icon)icon.Clone();
    }
}
