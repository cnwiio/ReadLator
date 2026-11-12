using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Models;

namespace NovelTranslator.App.ViewModels;

public partial class ParagraphPairViewModel : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRetranslating;

    public ObservableCollection<GlossaryEntry> MatchedGlossary { get; } = new();

    public Func<ParagraphPairViewModel, Task>? OnRetranslateRequested { get; set; }
    public Action<ParagraphPairViewModel>? OnContentChanged { get; set; }

    partial void OnSourceTextChanged(string value)
    {
        OnContentChanged?.Invoke(this);
    }

    partial void OnTranslatedTextChanged(string value)
    {
        OnContentChanged?.Invoke(this);
    }

    [RelayCommand]
    public async Task RetranslateAsync()
    {
        if (IsRetranslating || OnRetranslateRequested == null) return;
        IsRetranslating = true;
        try
        {
            await OnRetranslateRequested.Invoke(this);
        }
        finally
        {
            IsRetranslating = false;
        }
    }
}
