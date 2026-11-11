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

        _ = RefreshModelsSilentAsync();
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
    }
}
