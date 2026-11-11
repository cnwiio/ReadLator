using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Models;

namespace NovelTranslator.App.ViewModels;

public class ReplacePreviewItem
{
    public long ChapterId { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public string MatchSnippet { get; set; } = string.Empty;
}

public partial class SearchReplaceViewModel : ObservableObject
{
    private readonly NovelRepository _repository;
    private readonly Story _story;
    private readonly Chapter? _currentChapter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private int _scopeIndex = 0; // 0: ตอนนี้, 1: ทั้งเรื่อง

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<ReplacePreviewItem> Previews { get; } = new();

    public Action? RequestClose { get; set; }
    public Action? OnReplaceCompleted { get; set; }

    public SearchReplaceViewModel(NovelRepository repository, Story story, Chapter? currentChapter, string? initialSearch = null)
    {
        _repository = repository;
        _story = story;
        _currentChapter = currentChapter;
        if (!string.IsNullOrEmpty(initialSearch)) SearchText = initialSearch;
    }

    [RelayCommand]
    public async Task PreviewMatchesAsync()
    {
        Previews.Clear();
        if (string.IsNullOrEmpty(SearchText))
        {
            StatusMessage = "กรุณากรอกคำที่ต้องการค้นหา";
            return;
        }

        var chapters = ScopeIndex == 0 && _currentChapter != null
            ? new List<Chapter> { _currentChapter }
            : await _repository.GetChaptersByStoryAsync(_story.Id);

        int count = 0;
        foreach (var chap in chapters)
        {
            if (string.IsNullOrEmpty(chap.TranslatedText)) continue;

            int index = 0;
            while ((index = chap.TranslatedText.IndexOf(SearchText, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                int start = Math.Max(0, index - 25);
                int length = Math.Min(chap.TranslatedText.Length - start, SearchText.Length + 50);
                string snippet = chap.TranslatedText.Substring(start, length).Replace("\n", " ");

                Previews.Add(new ReplacePreviewItem
                {
                    ChapterId = chap.Id,
                    ChapterTitle = chap.Title,
                    MatchSnippet = $"...{snippet}..."
                });

                index += SearchText.Length;
            }
        }

        StatusMessage = $"พบทั้งหมด {count} จุดใน {Previews.Select(p => p.ChapterId).Distinct().Count()} ตอน";
    }

    [RelayCommand]
    public async Task ApplyReplaceAsync()
    {
        if (string.IsNullOrEmpty(SearchText)) return;

        var chapters = ScopeIndex == 0 && _currentChapter != null
            ? new List<Chapter> { _currentChapter }
            : await _repository.GetChaptersByStoryAsync(_story.Id);

        int replacedCount = 0;
        foreach (var chap in chapters)
        {
            if (string.IsNullOrEmpty(chap.TranslatedText)) continue;
            if (chap.TranslatedText.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                // แทนที่แบบ Case-insensitive
                chap.TranslatedText = Regex.Replace(
                    chap.TranslatedText,
                    Regex.Escape(SearchText),
                    ReplaceText ?? string.Empty,
                    RegexOptions.IgnoreCase);

                chap.Status = ChapterStatus.Edited;
                await _repository.UpdateChapterAsync(chap);
                replacedCount++;
            }
        }

        StatusMessage = $"แทนที่สำเร็จใน {replacedCount} ตอน!";
        Previews.Clear();
        OnReplaceCompleted?.Invoke();
    }
}
