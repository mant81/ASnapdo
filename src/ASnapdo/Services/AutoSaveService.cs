using ASnapdo.Models;

namespace ASnapdo.Services;

public static class AutoSaveService
{
    public static string SaveOriginal(CaptureDocument document, string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder))
            throw new ArgumentException("자동 저장 폴더의 전체 경로를 지정하세요.");
        Directory.CreateDirectory(folder);
        string stem = Path.GetFileNameWithoutExtension(document.Name);
        for (int suffix = 1; ; suffix++)
        {
            string path = Path.Combine(folder, suffix == 1 ? stem + ".png" : $"{stem}_{suffix}.png");
            FileStream stream;
            try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (IOException) when (File.Exists(path)) { continue; }
            try
            {
                using (stream) document.Image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return path;
            }
            catch
            {
                try { File.Delete(path); } catch { /* Preserve the original save error. */ }
                throw;
            }
        }
    }
}
