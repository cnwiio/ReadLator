using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Glossary;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Settings;
using NovelTranslator.Core.Translation;

namespace NovelTranslator.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private bool _isInitializing;

    [ObservableProperty]
    private int _backendIndex; // 0: Ollama, 1: LM Studio, 2: Custom

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _modelName = string.Empty;

    [ObservableProperty]
    private float _temperature = 0.3f;

    [ObservableProperty]
    private float _topP = 1.0f;

    [ObservableProperty]
    private int _maxTokens = 0;

    [ObservableProperty]
    private float _frequencyPenalty = 0.0f;

    [ObservableProperty]
    private int _requestTimeoutSeconds = 300;

    [ObservableProperty]
    private int _maxChunkWords = 600;

    [ObservableProperty]
    private int _contextParagraphCount = 2;

    [ObservableProperty]
    private string _customSystemPrompt = string.Empty;

    [ObservableProperty]
    private string _customPolishSystemPrompt = string.Empty;

    [ObservableProperty]
    private string _customGlossaryPrompt = string.Empty;

    [ObservableProperty]
    private bool _enableCrossChapterContext = true;

    [ObservableProperty]
    private bool _enableLookAheadContext = true;

    [ObservableProperty]
    private int _povModeIndex;

    [ObservableProperty]
    private bool _stripThinkTags = true;

    [ObservableProperty]
    private int _maxEnglishRatioPercent = 25;

    [ObservableProperty]
    private int _maxRetryAttempts = 1;

    [ObservableProperty]
    private string _customStripPrefixes = string.Empty;

    [ObservableProperty]
    private int _glossaryScanMaxWords = 1500;

    [ObservableProperty]
    private string _glossaryModelName = string.Empty;

    [ObservableProperty]
    private bool _enableAutoGlossaryScan = true;

    [ObservableProperty]
    private bool _enablePolishMode;

    [ObservableProperty]
    private string _polishModelName = string.Empty;

    // หมวดการตั้งค่าหลัก (0: AI, 1: Prompts & Pipeline, 2: Shortkeys, 3: System)
    [ObservableProperty]
    private int _selectedCategoryIndex;

    public bool IsAiCategorySelected => SelectedCategoryIndex == 0;
    public bool IsPipelineCategorySelected => SelectedCategoryIndex == 1;
    public bool IsShortkeysCategorySelected => SelectedCategoryIndex == 2;
    public bool IsSystemCategorySelected => SelectedCategoryIndex == 3;

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsAiCategorySelected));
        OnPropertyChanged(nameof(IsPipelineCategorySelected));
        OnPropertyChanged(nameof(IsShortkeysCategorySelected));
        OnPropertyChanged(nameof(IsSystemCategorySelected));
    }

    // หมวด Shortkeys
    [ObservableProperty] private string _translateKey = "Ctrl+Enter";
    [ObservableProperty] private string _translateAllKey = "Ctrl+Shift+Enter";
    [ObservableProperty] private string _searchReplaceKey = "Ctrl+F";
    [ObservableProperty] private string _saveKey = "Ctrl+S";
    [ObservableProperty] private string _fullScreenKey = "F11";
    [ObservableProperty] private string _focusModeKey = "F10";
    [ObservableProperty] private string _prevChapterKey = "Alt+Left";
    [ObservableProperty] private string _nextChapterKey = "Alt+Right";
    [ObservableProperty] private string _toggleSidebarKey = "Ctrl+B";

    [ObservableProperty] private string? _recordingAction;

    partial void OnRecordingActionChanged(string? value)
    {
        OnPropertyChanged(nameof(TranslateKeyDisplay));
        OnPropertyChanged(nameof(TranslateAllKeyDisplay));
        OnPropertyChanged(nameof(SearchReplaceKeyDisplay));
        OnPropertyChanged(nameof(SaveKeyDisplay));
        OnPropertyChanged(nameof(FullScreenKeyDisplay));
        OnPropertyChanged(nameof(FocusModeKeyDisplay));
        OnPropertyChanged(nameof(PrevChapterKeyDisplay));
        OnPropertyChanged(nameof(NextChapterKeyDisplay));
        OnPropertyChanged(nameof(ToggleSidebarKeyDisplay));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(RecordingPromptText));
    }

    public bool IsRecording => !string.IsNullOrEmpty(RecordingAction);
    public string RecordingPromptText => IsRecording ? "กำลังบันทึกปุ่มลัด... กรุณากดปุ่มบนคีย์บอร์ดที่ต้องการ (หรือกด Esc เพื่อยกเลิก)" : string.Empty;

    public string TranslateKeyDisplay => RecordingAction == nameof(TranslateKey) ? "กดปุ่ม..." : TranslateKey;
    public string TranslateAllKeyDisplay => RecordingAction == nameof(TranslateAllKey) ? "กดปุ่ม..." : TranslateAllKey;
    public string SearchReplaceKeyDisplay => RecordingAction == nameof(SearchReplaceKey) ? "กดปุ่ม..." : SearchReplaceKey;
    public string SaveKeyDisplay => RecordingAction == nameof(SaveKey) ? "กดปุ่ม..." : SaveKey;
    public string FullScreenKeyDisplay => RecordingAction == nameof(FullScreenKey) ? "กดปุ่ม..." : FullScreenKey;
    public string FocusModeKeyDisplay => RecordingAction == nameof(FocusModeKey) ? "กดปุ่ม..." : FocusModeKey;
    public string PrevChapterKeyDisplay => RecordingAction == nameof(PrevChapterKey) ? "กดปุ่ม..." : PrevChapterKey;
    public string NextChapterKeyDisplay => RecordingAction == nameof(NextChapterKey) ? "กดปุ่ม..." : NextChapterKey;
    public string ToggleSidebarKeyDisplay => RecordingAction == nameof(ToggleSidebarKey) ? "กดปุ่ม..." : ToggleSidebarKey;

    partial void OnTranslateKeyChanged(string value) => OnPropertyChanged(nameof(TranslateKeyDisplay));
    partial void OnTranslateAllKeyChanged(string value) => OnPropertyChanged(nameof(TranslateAllKeyDisplay));
    partial void OnSearchReplaceKeyChanged(string value) => OnPropertyChanged(nameof(SearchReplaceKeyDisplay));
    partial void OnSaveKeyChanged(string value) => OnPropertyChanged(nameof(SaveKeyDisplay));
    partial void OnFullScreenKeyChanged(string value) => OnPropertyChanged(nameof(FullScreenKeyDisplay));
    partial void OnFocusModeKeyChanged(string value) => OnPropertyChanged(nameof(FocusModeKeyDisplay));
    partial void OnPrevChapterKeyChanged(string value) => OnPropertyChanged(nameof(PrevChapterKeyDisplay));
    partial void OnNextChapterKeyChanged(string value) => OnPropertyChanged(nameof(NextChapterKeyDisplay));
    partial void OnToggleSidebarKeyChanged(string value) => OnPropertyChanged(nameof(ToggleSidebarKeyDisplay));

    [RelayCommand]
    public void StartRecording(string action)
    {
        RecordingAction = RecordingAction == action ? null : action;
    }

    [RelayCommand]
    public void ResetShortcut(string action)
    {
        var defaults = new ShortkeysSettings();
        switch (action)
        {
            case nameof(TranslateKey): TranslateKey = defaults.TranslateKey; break;
            case nameof(TranslateAllKey): TranslateAllKey = defaults.TranslateAllKey; break;
            case nameof(SearchReplaceKey): SearchReplaceKey = defaults.SearchReplaceKey; break;
            case nameof(SaveKey): SaveKey = defaults.SaveKey; break;
            case nameof(FullScreenKey): FullScreenKey = defaults.FullScreenKey; break;
            case nameof(FocusModeKey): FocusModeKey = defaults.FocusModeKey; break;
            case nameof(PrevChapterKey): PrevChapterKey = defaults.PrevChapterKey; break;
            case nameof(NextChapterKey): NextChapterKey = defaults.NextChapterKey; break;
            case nameof(ToggleSidebarKey): ToggleSidebarKey = defaults.ToggleSidebarKey; break;
        }
        if (RecordingAction == action)
        {
            RecordingAction = null;
        }
    }

    [RelayCommand]
    public void ResetAllShortcuts()
    {
        var defaults = new ShortkeysSettings();
        TranslateKey = defaults.TranslateKey;
        TranslateAllKey = defaults.TranslateAllKey;
        SearchReplaceKey = defaults.SearchReplaceKey;
        SaveKey = defaults.SaveKey;
        FullScreenKey = defaults.FullScreenKey;
        FocusModeKey = defaults.FocusModeKey;
        PrevChapterKey = defaults.PrevChapterKey;
        NextChapterKey = defaults.NextChapterKey;
        ToggleSidebarKey = defaults.ToggleSidebarKey;
        RecordingAction = null;
    }

    public void SetShortcut(string action, string key)
    {
        switch (action)
        {
            case nameof(TranslateKey): TranslateKey = key; break;
            case nameof(TranslateAllKey): TranslateAllKey = key; break;
            case nameof(SearchReplaceKey): SearchReplaceKey = key; break;
            case nameof(SaveKey): SaveKey = key; break;
            case nameof(FullScreenKey): FullScreenKey = key; break;
            case nameof(FocusModeKey): FocusModeKey = key; break;
            case nameof(PrevChapterKey): PrevChapterKey = key; break;
            case nameof(NextChapterKey): NextChapterKey = key; break;
            case nameof(ToggleSidebarKey): ToggleSidebarKey = key; break;
        }
        RecordingAction = null;
    }

    // หมวด System & Appearance
    [ObservableProperty] private int _translationAppearanceIndex; // 0: PerChunk, 1: WordByWord

    [ObservableProperty] private int _themeModeIndex; // 0: Light, 1: Dark, 2: Sepia
    partial void OnThemeModeIndexChanged(int value)
    {
        if (_isInitializing) return;
        string mode = value switch
        {
            1 => "Dark",
            2 => "Sepia",
            _ => "Light"
        };
        App.ApplyTheme(mode);
    }

    [ObservableProperty] private double _brightnessLevel = 100.0;
    partial void OnBrightnessLevelChanged(double value)
    {
        double clamped = Math.Clamp(value, 30.0, 100.0);
        if (Math.Abs(clamped - value) > 0.01)
        {
            BrightnessLevel = clamped;
        }
    }

    [ObservableProperty] private string _editorFontFamily = "Tahoma";
    public FontFamily CurrentFontFamily => new(string.IsNullOrWhiteSpace(EditorFontFamily) ? "Tahoma" : EditorFontFamily);
    partial void OnEditorFontFamilyChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentFontFamily));
    }

    [ObservableProperty] private int _editorFontSize = 20;

    [ObservableProperty] private double _autoSaveDelaySeconds = 1.5;

    [ObservableProperty] private double _mmbScrollSensitivity = 1.0;
    partial void OnMmbScrollSensitivityChanged(double value)
    {
        double clamped = Math.Clamp(Math.Round(value, 1), 0.2, 5.0);
        if (Math.Abs(clamped - value) > 0.01)
        {
            MmbScrollSensitivity = clamped;
        }
    }

    public ObservableCollection<string> AvailableEditorFonts { get; } = new(AppSettings.DefaultEditorFonts);

    [ObservableProperty]
    private bool _isApiKeyVisible;

    public char ApiKeyPasswordChar => IsApiKeyVisible ? '\0' : '*';

    partial void OnIsApiKeyVisibleChanged(bool value) => OnPropertyChanged(nameof(ApiKeyPasswordChar));

    [ObservableProperty]
    private string _connectionStatus = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<string> AvailableModels { get; } = new();

    public Action? RequestClose { get; set; }
    public bool IsSaved { get; private set; }

    public SettingsViewModel(AppSettings settings)
    {
        _isInitializing = true;
        _settings = settings;

        // โหลดค่า Shortkeys ก่อนเสมอ
        TranslateKey = settings.Shortkeys.TranslateKey;
        TranslateAllKey = settings.Shortkeys.TranslateAllKey;
        SearchReplaceKey = settings.Shortkeys.SearchReplaceKey;
        SaveKey = settings.Shortkeys.SaveKey;
        FullScreenKey = settings.Shortkeys.FullScreenKey;
        FocusModeKey = settings.Shortkeys.FocusModeKey;
        PrevChapterKey = settings.Shortkeys.PrevChapterKey;
        NextChapterKey = settings.Shortkeys.NextChapterKey;
        ToggleSidebarKey = settings.Shortkeys.ToggleSidebarKey;

        // โหลดค่า System & Appearance
        TranslationAppearanceIndex = (int)settings.TranslationAppearance;
        ThemeModeIndex = settings.ThemeMode switch
        {
            "Dark" => 1,
            "Sepia" => 2,
            _ => 0
        };
        BrightnessLevel = settings.BrightnessLevel is >= 30.0 and <= 100.0 ? settings.BrightnessLevel : 100.0;
        EditorFontFamily = string.IsNullOrWhiteSpace(settings.EditorFontFamily) ? "Tahoma" : settings.EditorFontFamily;
        EditorFontSize = settings.EditorFontSize > 0 ? settings.EditorFontSize : 20;
        AutoSaveDelaySeconds = settings.AutoSaveDelaySeconds > 0 ? settings.AutoSaveDelaySeconds : 1.5;
        MmbScrollSensitivity = settings.MmbScrollSensitivity is >= 0.2 and <= 5.0 ? settings.MmbScrollSensitivity : 1.0;

        BackendIndex = (int)settings.Backend;
        BaseUrl = settings.BaseUrl;
        ApiKey = settings.ApiKey;
        ModelName = settings.ModelName;
        Temperature = settings.Temperature;
        TopP = settings.TopP is > 0f and <= 1.0f ? settings.TopP : 1.0f;
        MaxTokens = Math.Max(0, settings.MaxTokens);
        FrequencyPenalty = Math.Clamp(settings.FrequencyPenalty, -2.0f, 2.0f);
        RequestTimeoutSeconds = Math.Clamp(settings.RequestTimeoutSeconds > 0 ? settings.RequestTimeoutSeconds : 300, 10, 3600);
        MaxChunkWords = settings.MaxChunkWords;
        ContextParagraphCount = settings.ContextParagraphCount;
        CustomSystemPrompt = LoadPromptOrDefault(settings.CustomSystemPrompt, PromptBuilder.DefaultSystemPromptTemplate);
        CustomPolishSystemPrompt = LoadPromptOrDefault(settings.CustomPolishSystemPrompt, PromptBuilder.DefaultPolishSystemPromptTemplate);
        CustomGlossaryPrompt = string.IsNullOrWhiteSpace(settings.CustomGlossaryPrompt)
            ? GlossaryExtractor.DefaultGlossaryPromptTemplate
            : settings.CustomGlossaryPrompt;
        EnableCrossChapterContext = settings.EnableCrossChapterContext;
        EnableLookAheadContext = settings.EnableLookAheadContext;
        PovModeIndex = Enum.IsDefined(typeof(PovDetectionMode), settings.PovMode) ? (int)settings.PovMode : 0;
        StripThinkTags = settings.StripThinkTags;
        MaxEnglishRatioPercent = Math.Clamp(settings.MaxEnglishRatioPercent > 0 ? settings.MaxEnglishRatioPercent : 25, 5, 100);
        MaxRetryAttempts = Math.Clamp(settings.MaxRetryAttempts, 0, 5);
        CustomStripPrefixes = settings.CustomStripPrefixes ?? string.Empty;
        GlossaryScanMaxWords = Math.Max(0, settings.GlossaryScanMaxWords);
        GlossaryModelName = settings.GlossaryModelName ?? string.Empty;
        EnableAutoGlossaryScan = settings.EnableAutoGlossaryScan;
        EnablePolishMode = settings.EnablePolishMode;
        PolishModelName = settings.PolishModelName ?? string.Empty;

        _isInitializing = false;
        _ = RefreshModelsSilentAsync();
    }

    private static string LoadPromptOrDefault(string? storedPrompt, string defaultTemplate)
    {
        if (string.IsNullOrWhiteSpace(storedPrompt))
        {
            return defaultTemplate;
        }
        return PromptBuilder.IsFullPromptTemplate(storedPrompt)
            ? storedPrompt
            : $"{defaultTemplate}\nคำสั่งพิเศษจากผู้ใช้: {storedPrompt.Trim()}";
    }

    [RelayCommand]
    public void ResetSystemPrompt() => CustomSystemPrompt = PromptBuilder.DefaultSystemPromptTemplate;

    [RelayCommand]
    public void ResetPolishPrompt() => CustomPolishSystemPrompt = PromptBuilder.DefaultPolishSystemPromptTemplate;

    [RelayCommand]
    public void ResetGlossaryPrompt() => CustomGlossaryPrompt = GlossaryExtractor.DefaultGlossaryPromptTemplate;

    [RelayCommand]
    public void ResetAiParameters()
    {
        Temperature = 0.3f;
        TopP = 1.0f;
        MaxTokens = 0;
        FrequencyPenalty = 0.0f;
        RequestTimeoutSeconds = 300;
        MaxChunkWords = 600;
    }

    [RelayCommand]
    public void ResetValidatorSettings()
    {
        StripThinkTags = true;
        MaxEnglishRatioPercent = 25;
        MaxRetryAttempts = 1;
        CustomStripPrefixes = string.Empty;
    }

    [RelayCommand]
    public void SelectCategory(string indexStr)
    {
        if (int.TryParse(indexStr, out int idx))
        {
            SelectedCategoryIndex = idx;
        }
    }

    [RelayCommand]
    public void ToggleApiKeyVisibility() => IsApiKeyVisible = !IsApiKeyVisible;

    public async Task RefreshModelsSilentAsync()
    {
        try
        {
            var client = new LlmClient(BuildDraftSettings());
            var models = await client.GetModelsAsync();
            if (models.Count > 0)
            {
                PopulateAvailableModels(models);
            }
        }
        catch
        {
            // Ignore silent refresh error on open
        }
    }

    partial void OnBackendIndexChanged(int value)
    {
        if (_isInitializing) return;
        if (value == 0) // Ollama
        {
            if (string.IsNullOrWhiteSpace(BaseUrl) || BaseUrl.Contains("localhost:1234"))
            {
                BaseUrl = "http://localhost:11434/v1";
            }
        }
        else if (value == 1) // LM Studio
        {
            if (string.IsNullOrWhiteSpace(BaseUrl) || BaseUrl.Contains("localhost:11434"))
            {
                BaseUrl = "http://localhost:1234/v1";
            }
        }
        _ = RefreshModelsSilentAsync();
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        IsBusy = true;
        ConnectionStatus = "กำลังทดสอบการเชื่อมต่อ...";

        var client = new LlmClient(BuildDraftSettings());

        try
        {
            var result = await client.TestConnectionAsync();
            ConnectionStatus = result.Message;

            if (result.Success)
            {
                var models = await client.GetModelsAsync();
                PopulateAvailableModels(models);
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"เกิดข้อผิดพลาด: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void PopulateAvailableModels(System.Collections.Generic.List<ModelInfo> models)
    {
        string currentModel = ModelName ?? string.Empty;
        string currentPolishModel = PolishModelName ?? string.Empty;
        string currentGlossaryModel = GlossaryModelName ?? string.Empty;
        AvailableModels.Clear();
        foreach (var m in models)
        {
            if (!string.IsNullOrWhiteSpace(m.Id) && !AvailableModels.Contains(m.Id))
            {
                AvailableModels.Add(m.Id);
            }
        }

        string? defaultServerModel = models
            .Select(m => m.Id)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)
                && !id.Contains("embed", StringComparison.OrdinalIgnoreCase)
                && !id.Contains("rerank", StringComparison.OrdinalIgnoreCase)
                && !id.Contains("whisper", StringComparison.OrdinalIgnoreCase))
            ?? AvailableModels.FirstOrDefault();

        if (!string.IsNullOrEmpty(currentModel) && !AvailableModels.Contains(currentModel))
        {
            AvailableModels.Insert(0, currentModel);
        }
        if (!string.IsNullOrEmpty(currentPolishModel) && !AvailableModels.Contains(currentPolishModel))
        {
            AvailableModels.Add(currentPolishModel);
        }
        if (!string.IsNullOrEmpty(currentGlossaryModel) && !AvailableModels.Contains(currentGlossaryModel))
        {
            AvailableModels.Add(currentGlossaryModel);
        }

        ModelName = string.IsNullOrEmpty(currentModel)
            ? (defaultServerModel ?? string.Empty)
            : currentModel;
        PolishModelName = currentPolishModel;
        GlossaryModelName = currentGlossaryModel;
    }

    [RelayCommand]
    public void SaveAndClose()
    {
        IsSaved = true;
        SaveSettings();
        RequestClose?.Invoke();
    }

    public void SaveSettings()
    {
        ApplyToSettings();
        _settings.Save();
    }

    public void RevertLiveThemePreview() => App.ApplyTheme(_settings.ThemeMode);

    private AppSettings BuildDraftSettings()
    {
        var draft = new AppSettings();
        ApplyTo(draft);
        return draft;
    }

    private static string NormalizeCustomPrompt(string? value, string defaultTemplate)
    {
        string trimmed = (value ?? string.Empty).Replace("\r\n", "\n").Trim();
        string normalizedDefault = defaultTemplate.Replace("\r\n", "\n").Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed == normalizedDefault
            ? string.Empty
            : trimmed;
    }

    internal void ApplyToSettings() => ApplyTo(_settings);

    private void ApplyTo(AppSettings target)
    {
        target.Backend = (BackendType)BackendIndex;
        target.BaseUrl = BaseUrl?.Trim() ?? string.Empty;
        target.ApiKey = ApiKey;
        target.ModelName = ModelName?.Trim() ?? string.Empty;
        target.Temperature = (float)Math.Round(Temperature, 2);
        target.TopP = (float)Math.Round(Math.Clamp(TopP, 0.05f, 1.0f), 2);
        target.MaxTokens = Math.Max(0, MaxTokens);
        target.FrequencyPenalty = (float)Math.Round(Math.Clamp(FrequencyPenalty, -2.0f, 2.0f), 2);
        target.RequestTimeoutSeconds = Math.Clamp(RequestTimeoutSeconds > 0 ? RequestTimeoutSeconds : 300, 10, 3600);
        target.MaxChunkWords = Math.Clamp(MaxChunkWords > 0 ? MaxChunkWords : 600, 50, 10000);
        target.ContextParagraphCount = Math.Clamp(ContextParagraphCount, 0, 20);
        target.CustomSystemPrompt = NormalizeCustomPrompt(CustomSystemPrompt, PromptBuilder.DefaultSystemPromptTemplate);
        target.CustomPolishSystemPrompt = NormalizeCustomPrompt(CustomPolishSystemPrompt, PromptBuilder.DefaultPolishSystemPromptTemplate);
        target.CustomGlossaryPrompt = NormalizeCustomPrompt(CustomGlossaryPrompt, GlossaryExtractor.DefaultGlossaryPromptTemplate);
        target.EnableCrossChapterContext = EnableCrossChapterContext;
        target.EnableLookAheadContext = EnableLookAheadContext;
        target.PovMode = Enum.IsDefined(typeof(PovDetectionMode), PovModeIndex) ? (PovDetectionMode)PovModeIndex : PovDetectionMode.Auto;
        target.StripThinkTags = StripThinkTags;
        target.MaxEnglishRatioPercent = Math.Clamp(MaxEnglishRatioPercent, 5, 100);
        target.MaxRetryAttempts = Math.Clamp(MaxRetryAttempts, 0, 5);
        target.CustomStripPrefixes = CustomStripPrefixes?.Trim() ?? string.Empty;
        target.GlossaryScanMaxWords = Math.Max(0, GlossaryScanMaxWords);
        target.GlossaryModelName = GlossaryModelName?.Trim() ?? string.Empty;
        target.EnableAutoGlossaryScan = EnableAutoGlossaryScan;
        target.EnablePolishMode = EnablePolishMode;
        target.PolishModelName = PolishModelName?.Trim() ?? string.Empty;

        // บันทึก Shortkeys
        target.Shortkeys.TranslateKey = TranslateKey;
        target.Shortkeys.TranslateAllKey = TranslateAllKey;
        target.Shortkeys.SearchReplaceKey = SearchReplaceKey;
        target.Shortkeys.SaveKey = SaveKey;
        target.Shortkeys.FullScreenKey = FullScreenKey;
        target.Shortkeys.FocusModeKey = FocusModeKey;
        target.Shortkeys.PrevChapterKey = PrevChapterKey;
        target.Shortkeys.NextChapterKey = NextChapterKey;
        target.Shortkeys.ToggleSidebarKey = ToggleSidebarKey;

        // บันทึก System & Appearance
        target.TranslationAppearance = TranslationAppearanceIndex == 1
            ? TranslationAppearanceMode.WordByWord
            : TranslationAppearanceMode.PerChunk;
        target.ThemeMode = ThemeModeIndex switch
        {
            1 => "Dark",
            2 => "Sepia",
            _ => "Light"
        };
        target.BrightnessLevel = Math.Clamp(BrightnessLevel, 30.0, 100.0);
        target.EditorFontFamily = EditorFontFamily;
        target.EditorFontSize = EditorFontSize;
        target.AutoSaveDelaySeconds = AutoSaveDelaySeconds;
        target.MmbScrollSensitivity = Math.Clamp(Math.Round(MmbScrollSensitivity, 1), 0.2, 5.0);
    }
}
