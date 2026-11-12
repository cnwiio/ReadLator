using CommunityToolkit.Mvvm.ComponentModel;

namespace NovelTranslator.App.ViewModels;

public partial class TranslationQueueViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _translationProgressPercentage;

    [ObservableProperty]
    private bool _isTranslatingIndeterminate;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _lastErrorDetails = string.Empty;

    public void Reset()
    {
        IsTranslating = false;
        ProgressText = string.Empty;
        TranslationProgressPercentage = 0;
        IsTranslatingIndeterminate = false;
        HasError = false;
        LastErrorDetails = string.Empty;
    }
}
