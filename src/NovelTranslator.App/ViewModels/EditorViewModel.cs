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
    public bool IsUpdatingFromParagraph { get; private set; }

    [ObservableProperty]
    private bool _isParagraphMode;

    public string ParagraphModeButtonText => IsParagraphMode ? "ข้อความปกติ" : "จับคู่รายย่อหน้า";

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
            var tParagraphs = SplitParagraphs(translatedText, preserveEmptySlots: true);

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
                            IsUpdatingFromParagraph = true;
                            try
                            {
                                OnParagraphContentChanged?.Invoke();
                            }
                            finally
                            {
                                IsUpdatingFromParagraph = false;
                            }
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
        var list = ParagraphPairs.Select(p => p.TranslatedText?.Trim() ?? string.Empty).ToList();
        int lastNonEmpty = list.FindLastIndex(t => !string.IsNullOrEmpty(t));
        if (lastNonEmpty < 0) return string.Empty;
        return string.Join("\n\n", list.Take(lastNonEmpty + 1));
    }

    public string CombineSourceParagraphs()
    {
        return string.Join("\n\n", ParagraphPairs.Select(p => p.SourceText?.Trim()).Where(s => !string.IsNullOrEmpty(s)));
    }

    public static List<string> SplitParagraphs(string text, bool preserveEmptySlots = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        var parts = text.Replace("\r\n", "\n")
                        .Replace('\r', '\n')
                        .Split(new[] { "\n\n" }, preserveEmptySlots ? StringSplitOptions.None : StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim());

        if (!preserveEmptySlots)
        {
            return parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        var list = parts.ToList();
        while (list.Count > 0 && string.IsNullOrEmpty(list[^1]))
        {
            list.RemoveAt(list.Count - 1);
        }
        return list;
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
}
