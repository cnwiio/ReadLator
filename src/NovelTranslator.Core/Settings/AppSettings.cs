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

public enum TranslationAppearanceMode
{
    PerChunk,
    WordByWord
}

public enum PovDetectionMode
{
    Auto,
    FirstPerson,
    ThirdPerson,
    Off
}

public class ShortkeysSettings
{
    public string TranslateKey { get; set; } = "Ctrl+Enter";
    public string TranslateAllKey { get; set; } = "Ctrl+Shift+Enter";
    public string SearchReplaceKey { get; set; } = "Ctrl+F";
    public string SaveKey { get; set; } = "Ctrl+S";
    public string FullScreenKey { get; set; } = "F11";
    public string FocusModeKey { get; set; } = "F10";
    public string PrevChapterKey { get; set; } = "Alt+Left";
    public string NextChapterKey { get; set; } = "Alt+Right";
    public string ToggleSidebarKey { get; set; } = "Ctrl+B";
}

public class AppSettings
{
    public static IReadOnlyList<string> DefaultEditorFonts { get; } = new[]
    {
        "Leelawadee UI",
        "Tahoma",
        "Cordia New",
        "Angsana New",
        "TH Sarabun New",
        "Segoe UI",
        "Microsoft Sans Serif",
        "Consolas",
        "Arial"
    };

    public BackendType Backend { get; set; } = BackendType.Ollama;
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public float Temperature { get; set; } = 0.3f;
    public int MaxChunkWords { get; set; } = 600;
    public int ContextParagraphCount { get; set; } = 2;
    public string CustomSystemPrompt { get; set; } = string.Empty;
    public string CustomPolishSystemPrompt { get; set; } = string.Empty;
    public string CustomGlossaryPrompt { get; set; } = string.Empty;
    public float TopP { get; set; } = 1.0f;
    public int MaxTokens { get; set; } = 0; // 0 = unlimited (do not send in JSON)
    public float FrequencyPenalty { get; set; } = 0.0f;
    public int RequestTimeoutSeconds { get; set; } = 300;
    public bool EnableCrossChapterContext { get; set; } = true;
    public bool EnableLookAheadContext { get; set; } = true;
    public PovDetectionMode PovMode { get; set; } = PovDetectionMode.Auto;
    public bool StripThinkTags { get; set; } = true;
    public int MaxEnglishRatioPercent { get; set; } = 25; // 5..100 (100 = disable English ratio check)
    public int MaxRetryAttempts { get; set; } = 1; // 0..5 retries
    public string CustomStripPrefixes { get; set; } = string.Empty;
    public int GlossaryScanMaxWords { get; set; } = 1500; // 0 = scan full chapter
    public string GlossaryModelName { get; set; } = string.Empty;
    public bool EnablePolishMode { get; set; } = false;
    public string PolishModelName { get; set; } = string.Empty;
    public string ThemeMode { get; set; } = "Light"; // Light, Dark, Sepia
    public double BrightnessLevel { get; set; } = 100.0; // 30.0 to 100.0
    public TranslationAppearanceMode TranslationAppearance { get; set; } = TranslationAppearanceMode.PerChunk;
    public int EditorFontSize { get; set; } = 20;
    public string EditorFontFamily { get; set; } = "Tahoma";
    public double AutoSaveDelaySeconds { get; set; } = 1.5;
    public double MmbScrollSensitivity { get; set; } = 1.0; // 0.2 to 5.0
    public ShortkeysSettings Shortkeys { get; set; } = new();
    public long? LastSelectedStoryId { get; set; }
    public long? LastSelectedChapterId { get; set; }
    public bool HasCompletedFirstRun { get; set; } = false;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object FileLock = new();

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
        lock (FileLock)
        {
            if (File.Exists(path))
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(stream);
                        string json = reader.ReadToEnd();
                        var settings = JsonSerializer.Deserialize<AppSettings>(json);
                        if (settings != null)
                        {
                            return settings;
                        }
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        Thread.Sleep(50);
                    }
                    catch
                    {
                        // หากไฟล์เสียหายหรืออ่านไม่ได้ในรอบสุดท้าย ให้ใช้ค่าเริ่มต้น
                    }
                }
            }
            return new AppSettings();
        }
    }

    public void Save(string? targetPath = null)
    {
        string path = string.IsNullOrWhiteSpace(targetPath) ? GetSettingsFilePath() : targetPath;
        string tempPath = path + ".tmp";
        lock (FileLock)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    string json = JsonSerializer.Serialize(this, JsonOptions);
                    using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(json);
                        writer.Flush();
                    }
                    File.Move(tempPath, path, overwrite: true);
                    break;
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(50);
                }
            }
        }
    }
}
