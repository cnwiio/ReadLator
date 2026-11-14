using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Glossary;
using NovelTranslator.Core.Models;

namespace NovelTranslator.App.ViewModels;

public partial class AutoGlossaryViewModel : ObservableObject
{
    private readonly NovelRepository _repository;
    private readonly Story _story;

    public ObservableCollection<ExtractedTerm> ExtractedTerms { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public Action? RequestClose { get; set; }
    public Action? OnTermsAdded { get; set; }

    public AutoGlossaryViewModel(NovelRepository repository, Story story, IEnumerable<ExtractedTerm> terms)
    {
        _repository = repository;
        _story = story;

        foreach (var term in terms)
        {
            term.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ExtractedTerm.IsSelected))
                {
                    UpdateStatus();
                }
            };
            ExtractedTerms.Add(term);
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int selected = ExtractedTerms.Count(t => t.IsSelected);
        StatusMessage = $"พบศัพท์ใหม่ {ExtractedTerms.Count} คำ (เลือก {selected} คำ)";
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var t in ExtractedTerms) t.IsSelected = true;
        UpdateStatus();
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var t in ExtractedTerms) t.IsSelected = false;
        UpdateStatus();
    }

    [RelayCommand]
    public async Task SaveSelectedAsync()
    {
        var toSave = ExtractedTerms.Where(t => t.IsSelected && !string.IsNullOrWhiteSpace(t.SourceTerm)).ToList();
        if (toSave.Count == 0)
        {
            RequestClose?.Invoke();
            return;
        }

        var entries = toSave.Select(t => new GlossaryEntry
        {
            StoryId = _story.Id,
            SourceTerm = t.SourceTerm.Trim(),
            TargetTerm = t.TargetTerm.Trim(),
            Category = t.Category,
            Notes = t.Notes.Trim()
        }).ToList();

        await _repository.BulkInsertGlossaryEntriesAsync(entries);

        OnTermsAdded?.Invoke();
        RequestClose?.Invoke();
    }
}
