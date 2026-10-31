namespace ASnapdo;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);
        if (!args.Contains("--managed-tray") && Services.StartupService.Available)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Services.StartupService.LauncherPath) { UseShellExecute = false });
            return;
        }
        using var singleton = new Mutex(true, @"Local\ASnapdo.Editor", out bool first);
        if (!first) return;
        try { Application.Run(new MainForm(args.Contains("--managed-tray"), args.FirstOrDefault(a => a is "--capture-region" or "--capture-full" or "--settings"))); }
        catch (Exception e) { Report(e); }
    }

    static void Report(Exception? error)
    {
        try
        {
            Directory.CreateDirectory(Services.SettingsService.Folder);
            File.AppendAllText(Path.Combine(Services.SettingsService.Folder, "ASnapdo.log"),
                $"{DateTimeOffset.Now:O} [ASnapdo] {error}{Environment.NewLine}");
        }
        catch { /* Logging must not obscure the original startup failure. */ }
        MessageBox.Show("오류가 발생했습니다. ASnapdo 설정 폴더의 로그를 확인하세요.", "ASnapdo");
    }
}
