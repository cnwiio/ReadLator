using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.App.Views;

public partial class FirstRunWindow : Window
{
    private readonly AppSettings _settings;

    public bool SettingsUpdated { get; private set; }

    public FirstRunWindow() : this(new AppSettings())
    {
    }

    public FirstRunWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        BackendComboBox.SelectedIndex = settings.Backend switch
        {
            BackendType.LmStudio => 1,
            BackendType.Custom => 2,
            _ => 0
        };
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl)) BaseUrlBox.Text = settings.BaseUrl;
        if (!string.IsNullOrWhiteSpace(settings.ApiKey)) ApiKeyBox.Text = settings.ApiKey;
        if (!string.IsNullOrWhiteSpace(settings.ModelName)) ModelBox.Text = settings.ModelName;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnBackendSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (BaseUrlBox == null) return;
        BaseUrlBox.Text = BackendComboBox.SelectedIndex switch
        {
            1 => "http://localhost:1234/v1",
            2 => "https://api.openai.com/v1",
            _ => "http://localhost:11434/v1"
        };
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        string baseUrl = BaseUrlBox.Text?.Trim() ?? string.Empty;
        string apiKey = ApiKeyBox.Text?.Trim() ?? string.Empty;
        string model = ModelBox.Text?.Trim() ?? string.Empty;
        var backend = BackendComboBox.SelectedIndex switch
        {
            1 => BackendType.LmStudio,
            2 => BackendType.Custom,
            _ => BackendType.Ollama
        };

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            StatusText.IsVisible = true;
            StatusText.Text = "กรุณากรอก Base URL";
            return;
        }

        if (backend == BackendType.Custom && string.IsNullOrWhiteSpace(apiKey))
        {
            StatusText.IsVisible = true;
            StatusText.Text = "กรุณากรอก API Key สำหรับ Cloud AI";
            return;
        }

        _settings.Backend = backend;
        _settings.BaseUrl = baseUrl;
        _settings.ApiKey = apiKey;
        if (!string.IsNullOrWhiteSpace(model))
        {
            _settings.ModelName = model;
        }
        _settings.HasCompletedFirstRun = true;
        _settings.Save();
        SettingsUpdated = true;
        Close();
    }

    private void OnSkipClicked(object? sender, RoutedEventArgs e)
    {
        _settings.HasCompletedFirstRun = true;
        _settings.Save();
        Close();
    }
}
