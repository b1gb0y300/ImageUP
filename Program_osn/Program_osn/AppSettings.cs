using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ImageEnhancementWpf
{
    public class ProcessingHistoryEntry
    {
        public string MethodId   { get; set; } = "";
        public string MethodName { get; set; } = "";
        public DateTime Timestamp      { get; set; }
        public string? ImageFileName   { get; set; }
        public double  Ssim            { get; set; } = double.NaN;
        public double  Psnr            { get; set; } = double.NaN;
        public string? ThumbnailFileName { get; set; }
    }

    public class AppSettings
    {
        public bool IsLightTheme { get; set; } = false;
        public bool AutoShowResult { get; set; } = true;
        public string DefaultSaveFormat { get; set; } = "png";
        public int JpegQuality { get; set; } = 85;
        public string InterpolationMode { get; set; } = "bilinear";
        public bool ConfirmReset { get; set; } = true;
        public List<ProcessingHistoryEntry> History { get; set; } = new();
        public List<string> RecentFiles { get; set; } = new();

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageUP");
        private static readonly string SettingsPath =
            Path.Combine(SettingsDir, "settings.json");

        private static AppSettings? _current;
        public static AppSettings Current => _current ??= Load();

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var opts = new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
                    var result = JsonSerializer.Deserialize<AppSettings>(json, opts);
                    if (result != null) return result;
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                };
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, opts));
            }
            catch { }
        }

        public void AddRecentFile(string path)
        {
            RecentFiles.Remove(path);
            RecentFiles.Insert(0, path);
            if (RecentFiles.Count > 5)
                RecentFiles.RemoveRange(5, RecentFiles.Count - 5);
            Save();
        }

        public void AddHistoryEntry(string methodId, string methodName, string? imageFileName,
            double ssim = double.NaN, double psnr = double.NaN, string? thumbnailFileName = null)
        {
            History.Insert(0, new ProcessingHistoryEntry
            {
                MethodId          = methodId,
                MethodName        = methodName,
                Timestamp         = DateTime.Now,
                ImageFileName     = imageFileName,
                Ssim              = ssim,
                Psnr              = psnr,
                ThumbnailFileName = thumbnailFileName
            });
            if (History.Count > 50)
                History.RemoveRange(50, History.Count - 50);
            Save();
        }
    }
}
