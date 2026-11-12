using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NovelTranslator.Core.Models;

namespace NovelTranslator.App.ViewModels;

public partial class StoryListViewModel : ObservableObject
{
    public ObservableCollection<Story> Stories { get; } = new();
    public ObservableCollection<Chapter> Chapters { get; } = new();
    public ObservableCollection<Chapter> FilteredChapters { get; } = new();

    [ObservableProperty]
    private string _chapterSearchFilter = string.Empty;

    [ObservableProperty]
    private int _chapterStatusFilterIndex; // 0: All, 1: NotTranslated, 2: Translated, 3: Edited

    [ObservableProperty]
    private string _storyProgressText = "0 / 0 ตอน (0%)";

    [ObservableProperty]
    private double _storyProgressPercentage;

    public void RefreshFiltered(Chapter? selectedChapter)
    {
        string q = ChapterSearchFilter?.Trim() ?? string.Empty;
        IEnumerable<Chapter> matches = Chapters;

        if (!string.IsNullOrEmpty(q))
        {
            matches = matches.Where(c => c.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        matches = ChapterStatusFilterIndex switch
        {
            1 => matches.Where(c => c.Status == ChapterStatus.NotTranslated),
            2 => matches.Where(c => c.Status == ChapterStatus.Translated),
            3 => matches.Where(c => c.Status == ChapterStatus.Edited),
            _ => matches
        };

        FilteredChapters.Clear();
        foreach (var c in matches)
        {
            FilteredChapters.Add(c);
        }

        UpdateStoryProgress();
    }

    public void UpdateStoryProgress()
    {
        int total = Chapters.Count;
        if (total == 0)
        {
            StoryProgressText = "0 / 0 ตอน (0%)";
            StoryProgressPercentage = 0;
            return;
        }

        int done = Chapters.Count(c => c.Status is ChapterStatus.Translated or ChapterStatus.Edited);
        double pct = Math.Round((double)done / total * 100, 0);
        StoryProgressPercentage = pct;
        StoryProgressText = $"แปลแล้ว {done} / {total} ตอน ({pct:0}%)";
    }
}
