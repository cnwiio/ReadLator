using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;

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

    // หมวด System & Appearance
    [ObservableProperty] private string _editorFontFamily = "Tahoma";
    [ObservableProperty] private int _editorFontSize = 20;
    [ObservableProperty] private double _autoSaveDelaySeconds = 1.5;

    public ObservableCollection<string> AvailableEditorFonts { get; } = new()
    {
        "Tahoma",
        "Segoe UI",
        "Cordia New",
        "Angsana New",
        "Arial",
        "Calibri",
        "Consolas",
        "Sarabun"
    };

    [ObservableProperty]
    private bool _isApiKeyVisible;

    public char ApiKeyPasswordChar => IsApiKeyVisible ? '\0' : '*';

    partial void OnIsApiKeyVisibleChanged(bool value) => OnPropertyChanged(nameof(ApiKeyPasswordChar));

    [ObservableProperty]
    private string _connectionStatus = string.Empty;

    [ObservableProperty]
    private string _pullProgress = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<string> AvailableModels { get; } = new();

    public Action? RequestClose { get; set; }

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;

        BackendIndex = (int)settings.Backend;
        BaseUrl = settings.BaseUrl;
        ApiKey = settings.ApiKey;
        ModelName = settings.ModelName;
        Temperature = settings.Temperature;
        MaxChunkWords = settings.MaxChunkWords;
        ContextParagraphCount = settings.ContextParagraphCount;
        CustomSystemPrompt = settings.CustomSystemPrompt ?? string.Empty;

        // โหลดค่า Shortkeys
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
        EditorFontFamily = string.IsNullOrWhiteSpace(settings.EditorFontFamily) ? "Tahoma" : settings.EditorFontFamily;
        EditorFontSize = settings.EditorFontSize > 0 ? settings.EditorFontSize : 20;
        AutoSaveDelaySeconds = settings.AutoSaveDelaySeconds > 0 ? settings.AutoSaveDelaySeconds : 1.5;

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
            ApplyToSettings();
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
    public async Task PullModelAsync()
    {
        if (string.IsNullOrWhiteSpace(ModelName))
        {
            PullProgress = "กรุณาระบุชื่อโมเดลที่ต้องการดาวน์โหลดก่อน";
            return;
        }

        IsBusy = true;
        PullProgress = "กำลังเริ่มดาวน์โหลด...";

        ApplyToSettings();
        var client = new LlmClient(_settings);

        try
        {
            var progress = new Progress<string>(msg => PullProgress = msg);
            await client.PullOllamaModelAsync(ModelName, progress);
            PullProgress = "ดาวน์โหลดโมเดลสำเร็จ!";
            await TestConnectionAsync();
        }
        catch (Exception ex)
        {
            PullProgress = $"ดาวน์โหลดล้มเหลว: {ex.Message}";
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
        _settings.EditorFontFamily = EditorFontFamily;
        _settings.EditorFontSize = EditorFontSize;
        _settings.AutoSaveDelaySeconds = AutoSaveDelaySeconds;
    }
}
