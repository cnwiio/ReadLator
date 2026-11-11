using System;
using System.IO;
using System.Text.Json;

namespace NovelTranslator.Core.Settings;

public enum BackendType
{
    Ollama,
    LmStudio,
    Custom
}

public class AppSettings
{
    public BackendType Backend { get; set; } = BackendType.Ollama;
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public float Temperature { get; set; } = 0.3f;
    public int MaxChunkWords { get; set; } = 600;
    public int ContextParagraphCount { get; set; } = 2;
    public string CustomSystemPrompt { get; set; } = string.Empty;
    public string ThemeMode { get; set; } = "Light"; // Light, Dark, Sepia
    public int EditorFontSize { get; set; } = 16;
    public long? LastSelectedStoryId { get; set; }
    public long? LastSelectedChapterId { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string GetSettingsFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "NovelTranslator");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        return Path.Combine(folder, "settings.json");
    }

    public static AppSettings Load()
    {
        string path = GetSettingsFilePath();
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    return settings;
                }
            }
            catch
            {
                // หากไฟล์เสียหายหรืออ่านไม่ได้ ให้ใช้ค่าเริ่มต้น
            }
        }
        return new AppSettings();
    }

    public void Save()
    {
        string path = GetSettingsFilePath();
        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }
}
