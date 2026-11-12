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

public partial class EditorViewModel : ObservableObject
{
    private readonly NovelRepository _repository;
    private bool _isSyncingParagraphs;

    [ObservableProperty]
    private bool _isParagraphMode;

    public string ParagraphModeButtonText => IsParagraphMode ? "มุมมองกล่องข้อความ" : "จับคู่ย่อหน้า";

    [ObservableProperty]
    private ParagraphPairViewModel? _selectedParagraphPair;

    public ObservableCollection<ParagraphPairViewModel> ParagraphPairs { get; } = new();
    public ObservableCollection<GlossaryEntry> ActiveGlossary { get; } = new();

    public Action? OnParagraphContentChanged { get; set; }
    public Func<ParagraphPairViewModel, Task>? OnRetranslateParagraphAction { get; set; }

    public EditorViewModel(NovelRepository repository)
    {
        _repository = repository;
    }

    partial void OnIsParagraphModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ParagraphModeButtonText));
    }

    public void SyncParagraphPairs(string sourceText, string translatedText, IEnumerable<GlossaryEntry>? glossary = null)
    {
        if (_isSyncingParagraphs) return;
        _isSyncingParagraphs = true;
        try
        {
            var sParagraphs = SplitParagraphs(sourceText);
            var tParagraphs = SplitParagraphs(translatedText);

            int maxCount = Math.Max(sParagraphs.Count, tParagraphs.Count);
            var glossaryList = glossary?.ToList() ?? ActiveGlossary.ToList();

            ParagraphPairs.Clear();
            for (int i = 0; i < maxCount; i++)
            {
                string sText = i < sParagraphs.Count ? sParagraphs[i] : string.Empty;
                string tText = i < tParagraphs.Count ? tParagraphs[i] : string.Empty;

                var pair = new ParagraphPairViewModel
                {
                    Index = i + 1,
                    SourceText = sText,
                    TranslatedText = tText,
                    OnRetranslateRequested = async (p) =>
                    {
                        if (OnRetranslateParagraphAction != null)
                        {
                            await OnRetranslateParagraphAction(p);
                        }
                    },
                    OnContentChanged = (_) =>
                    {
                        if (!_isSyncingParagraphs)
                        {
                            OnParagraphContentChanged?.Invoke();
                        }
                    }
                };

                // จับคู่ glossary ที่มีในย่อหน้านี้
                if (glossaryList.Count > 0 && !string.IsNullOrWhiteSpace(sText))
                {
                    var matched = GlossaryService.FindMatchingEntries(sText, glossaryList);
                    foreach (var g in matched)
                    {
                        pair.MatchedGlossary.Add(g);
                    }
                }

                ParagraphPairs.Add(pair);
            }
        }
        finally
        {
            _isSyncingParagraphs = false;
        }
    }

    public string CombineTranslatedParagraphs()
    {
        return string.Join("\n\n", ParagraphPairs.Select(p => p.TranslatedText?.Trim()).Where(t => !string.IsNullOrEmpty(t)));
    }

    public string CombineSourceParagraphs()
    {
        return string.Join("\n\n", ParagraphPairs.Select(p => p.SourceText?.Trim()).Where(s => !string.IsNullOrEmpty(s)));
    }

    public static List<string> SplitParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        return text.Replace("\r\n", "\n")
                   .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(p => p.Trim())
                   .Where(p => !string.IsNullOrEmpty(p))
                   .ToList();
    }

    public async Task RefreshActiveGlossaryAsync(long storyId, string sourceText)
    {
        var all = await _repository.GetGlossaryByStoryAsync(storyId);
        var matched = GlossaryService.FindMatchingEntries(sourceText, all);
        ActiveGlossary.Clear();
        foreach (var m in matched)
        {
            ActiveGlossary.Add(m);
        }
    }

    public async Task QuickUpdateGlossaryEntryAsync(GlossaryEntry entry, string newTargetTerm)
    {
        if (string.IsNullOrWhiteSpace(newTargetTerm)) return;
        entry.TargetTerm = newTargetTerm.Trim();
        await _repository.UpdateGlossaryEntryAsync(entry);
    }
}
