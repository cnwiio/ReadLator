using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Settings;

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
    private int _maxChunkWords = 600;

    [ObservableProperty]
    private int _contextParagraphCount = 2;

    [ObservableProperty]
    private string _customSystemPrompt = string.Empty;

    // หมวดการตั้งค่าหลัก (0: AI, 1: Shortkeys, 2: System)
    [ObservableProperty]
    private int _selectedCategoryIndex;

    public bool IsAiCategorySelected => SelectedCategoryIndex == 0;
    public bool IsShortkeysCategorySelected => SelectedCategoryIndex == 1;
    public bool IsSystemCategorySelected => SelectedCategoryIndex == 2;

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsAiCategorySelected));
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
        SaveSettings();
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
        SaveSettings();
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
        SaveSettings();
    }

    // หมวด System & Appearance
    [ObservableProperty] private int _translationAppearanceIndex; // 0: PerChunk, 1: WordByWord
    partial void OnTranslationAppearanceIndexChanged(int value)
    {
        if (!_isInitializing) SaveSettings();
    }

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
        SaveSettings();
    }

    [ObservableProperty] private double _brightnessLevel = 100.0;
    partial void OnBrightnessLevelChanged(double value)
    {
        double clamped = Math.Clamp(value, 30.0, 100.0);
        if (Math.Abs(clamped - value) > 0.01)
        {
            BrightnessLevel = clamped;
            return;
        }
        if (!_isInitializing) SaveSettings();
    }

    [ObservableProperty] private string _editorFontFamily = "Tahoma";
    public FontFamily CurrentFontFamily => new(string.IsNullOrWhiteSpace(EditorFontFamily) ? "Tahoma" : EditorFontFamily);
    partial void OnEditorFontFamilyChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentFontFamily));
        if (!_isInitializing) SaveSettings();
    }

    [ObservableProperty] private int _editorFontSize = 20;
    partial void OnEditorFontSizeChanged(int value)
    {
        if (!_isInitializing) SaveSettings();
    }

    [ObservableProperty] private double _autoSaveDelaySeconds = 1.5;
    partial void OnAutoSaveDelaySecondsChanged(double value)
    {
        if (!_isInitializing) SaveSettings();
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

        BackendIndex = (int)settings.Backend;
        BaseUrl = settings.BaseUrl;
        ApiKey = settings.ApiKey;
        ModelName = settings.ModelName;
        Temperature = settings.Temperature;
        MaxChunkWords = settings.MaxChunkWords;
        ContextParagraphCount = settings.ContextParagraphCount;
        CustomSystemPrompt = settings.CustomSystemPrompt ?? string.Empty;

        _isInitializing = false;
        _ = RefreshModelsSilentAsync();
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
            var client = new LlmClient(_settings);
            var models = await client.GetModelsAsync();
            if (models.Count > 0)
            {
                string currentModel = ModelName;
                AvailableModels.Clear();
                foreach (var m in models)
                {
                    AvailableModels.Add(m.Id);
                }
                if (!string.IsNullOrEmpty(currentModel) && !AvailableModels.Contains(currentModel))
                {
                    AvailableModels.Insert(0, currentModel);
                }
                ModelName = string.IsNullOrEmpty(currentModel) ? AvailableModels[0] : currentModel;
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
        ApplyToSettings();
        _ = RefreshModelsSilentAsync();
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        IsBusy = true;
        ConnectionStatus = "กำลังทดสอบการเชื่อมต่อ...";

        ApplyToSettings();
        var client = new LlmClient(_settings);

        try
        {
            var result = await client.TestConnectionAsync();
            ConnectionStatus = result.Message;

            if (result.Success)
            {
                var models = await client.GetModelsAsync();
                string currentModel = ModelName;
                AvailableModels.Clear();
                foreach (var m in models)
                {
                    AvailableModels.Add(m.Id);
                }
                if (!string.IsNullOrEmpty(currentModel) && !AvailableModels.Contains(currentModel))
                {
                    AvailableModels.Insert(0, currentModel);
                }
                ModelName = string.IsNullOrEmpty(currentModel) && AvailableModels.Count > 0
                    ? AvailableModels[0]
                    : currentModel;
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

    [RelayCommand]
    public void SaveAndClose()
    {
        SaveSettings();
        RequestClose?.Invoke();
    }

    public void SaveSettings()
    {
        ApplyToSettings();
        _settings.Save();
    }

    private void ApplyToSettings()
    {
        _settings.Backend = (BackendType)BackendIndex;
        _settings.BaseUrl = BaseUrl;
        _settings.ApiKey = ApiKey;
        _settings.ModelName = ModelName;
        _settings.Temperature = (float)Math.Round(Temperature, 2);
        _settings.MaxChunkWords = MaxChunkWords;
        _settings.ContextParagraphCount = ContextParagraphCount;
        _settings.CustomSystemPrompt = CustomSystemPrompt ?? string.Empty;

        // บันทึก Shortkeys
        _settings.Shortkeys.TranslateKey = TranslateKey;
        _settings.Shortkeys.TranslateAllKey = TranslateAllKey;
        _settings.Shortkeys.SearchReplaceKey = SearchReplaceKey;
        _settings.Shortkeys.SaveKey = SaveKey;
        _settings.Shortkeys.FullScreenKey = FullScreenKey;
        _settings.Shortkeys.FocusModeKey = FocusModeKey;
        _settings.Shortkeys.PrevChapterKey = PrevChapterKey;
        _settings.Shortkeys.NextChapterKey = NextChapterKey;
        _settings.Shortkeys.ToggleSidebarKey = ToggleSidebarKey;

        // บันทึก System & Appearance
        _settings.TranslationAppearance = TranslationAppearanceIndex == 1
            ? TranslationAppearanceMode.WordByWord
            : TranslationAppearanceMode.PerChunk;
        _settings.ThemeMode = ThemeModeIndex switch
        {
            1 => "Dark",
            2 => "Sepia",
            _ => "Light"
        };
        _settings.BrightnessLevel = Math.Clamp(BrightnessLevel, 30.0, 100.0);
        _settings.EditorFontFamily = EditorFontFamily;
        _settings.EditorFontSize = EditorFontSize;
        _settings.AutoSaveDelaySeconds = AutoSaveDelaySeconds;
    }
}
