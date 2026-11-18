using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Glossary;
using NovelTranslator.Core.Models;

namespace NovelTranslator.App.ViewModels;

public partial class GlossaryViewModel : ObservableObject
{
    private readonly NovelRepository _repository;
    private readonly Story _story;
    private readonly List<GlossaryEntry> _allEntries = new();

    public ObservableCollection<GlossaryEntry> Entries { get; } = new();

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private GlossaryEntry? _selectedEntry;

    [ObservableProperty]
    private string _sourceTerm = string.Empty;

    [ObservableProperty]
    private string _targetTerm = string.Empty;

    [ObservableProperty]
    private int _categoryIndex = 0; // 0: Person, 1: Location, 2: Term

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public Action? RequestClose { get; set; }
    public bool SelectFirstOnLoad { get; set; }

    public GlossaryViewModel(NovelRepository repository, Story story, string? initialSource = null, string? initialTarget = null)
    {
        _repository = repository;
        _story = story;
        if (!string.IsNullOrEmpty(initialSource)) SourceTerm = initialSource;
        if (!string.IsNullOrEmpty(initialTarget)) TargetTerm = initialTarget;
    }

    public async Task LoadEntriesAsync()
    {
        var list = await _repository.GetGlossaryByStoryAsync(_story.Id);
        _allEntries.Clear();
        _allEntries.AddRange(list);
        ApplyFilter();
        if (SelectFirstOnLoad && Entries.Count > 0)
        {
            SelectedEntry = Entries[0];
            SelectFirstOnLoad = false;
        }
    }

    partial void OnSearchFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        string q = SearchFilter?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(q)
            ? _allEntries
            : _allEntries.Where(e =>
                e.SourceTerm.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.TargetTerm.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Notes.Contains(q, StringComparison.OrdinalIgnoreCase));

        Entries.Clear();
        foreach (var item in filtered)
        {
            Entries.Add(item);
        }
    }

    partial void OnSelectedEntryChanged(GlossaryEntry? value)
    {
        if (value != null)
        {
            SourceTerm = value.SourceTerm;
            TargetTerm = value.TargetTerm;
            CategoryIndex = (int)value.Category;
            Notes = value.Notes;
        }
    }

    [RelayCommand]
    public async Task SaveEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceTerm))
        {
            StatusMessage = "กรุณากรอกคำต้นฉบับ";
            return;
        }

        var cat = (GlossaryCategory)CategoryIndex;

        if (SelectedEntry == null)
        {
            string trimmedSource = SourceTerm.Trim();
            var existing = _allEntries.FirstOrDefault(e => e.SourceTerm.Equals(trimmedSource, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.SourceTerm = trimmedSource;
                existing.TargetTerm = TargetTerm.Trim();
                existing.Category = cat;
                existing.Notes = Notes.Trim();
                await _repository.UpdateGlossaryEntryAsync(existing);
                ApplyFilter();
                StatusMessage = $"อัปเดตคำว่า \"{existing.SourceTerm}\" สำเร็จ";
            }
            else
            {
                // เพิ่มใหม่
                var entry = new GlossaryEntry
                {
                    StoryId = _story.Id,
                    SourceTerm = trimmedSource,
                    TargetTerm = TargetTerm.Trim(),
                    Category = cat,
                    Notes = Notes.Trim()
                };
                entry.Id = await _repository.InsertGlossaryEntryAsync(entry);
                _allEntries.Add(entry);
                ApplyFilter();
                StatusMessage = $"เพิ่มคำว่า \"{entry.SourceTerm}\" สำเร็จ";
            }
        }
        else
        {
            // แก้ไข
            SelectedEntry.SourceTerm = SourceTerm.Trim();
            SelectedEntry.TargetTerm = TargetTerm.Trim();
            SelectedEntry.Category = cat;
            SelectedEntry.Notes = Notes.Trim();
            await _repository.UpdateGlossaryEntryAsync(SelectedEntry);
            StatusMessage = $"แก้ไขคำว่า \"{SelectedEntry.SourceTerm}\" สำเร็จ";
            await LoadEntriesAsync();
        }

        ClearInputs();
    }

    [RelayCommand]
    public async Task DeleteEntryAsync()
    {
        if (SelectedEntry == null) return;
        await _repository.DeleteGlossaryEntryAsync(SelectedEntry.Id);
        StatusMessage = $"ลบคำว่า \"{SelectedEntry.SourceTerm}\" แล้ว";
        _allEntries.Remove(SelectedEntry);
        Entries.Remove(SelectedEntry);
        ClearInputs();
    }

    [RelayCommand]
    public void ClearInputs()
    {
        SelectedEntry = null;
        SourceTerm = string.Empty;
        TargetTerm = string.Empty;
        CategoryIndex = 0;
        Notes = string.Empty;
    }

    public async Task ImportCsvAsync(string filePath)
    {
        var rawImported = GlossaryService.ImportFromCsv(filePath, _story.Id);
        // ตัดรายการซ้ำภายในไฟล์ CSV เองโดยคงข้อมูลล่าสุดของแต่ละ SourceTerm
        var deduped = new Dictionary<string, GlossaryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in rawImported)
        {
            string key = item.SourceTerm.Trim();
            if (!string.IsNullOrEmpty(key))
            {
                deduped[key] = item;
            }
        }

        var existingBySource = new Dictionary<string, GlossaryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in _allEntries)
        {
            string key = existing.SourceTerm.Trim();
            if (!string.IsNullOrEmpty(key) && !existingBySource.ContainsKey(key))
            {
                existingBySource[key] = existing;
            }
        }

        var toInsert = new List<GlossaryEntry>();
        int updatedCount = 0;

        foreach (var entry in deduped.Values)
        {
            string source = entry.SourceTerm.Trim();
            if (existingBySource.TryGetValue(source, out var existing))
            {
                existing.SourceTerm = source;
                existing.TargetTerm = entry.TargetTerm.Trim();
                existing.Category = entry.Category;
                existing.Notes = entry.Notes.Trim();
                await _repository.UpdateGlossaryEntryAsync(existing);
                updatedCount++;
            }
            else
            {
                entry.SourceTerm = source;
                entry.TargetTerm = entry.TargetTerm.Trim();
                entry.Notes = entry.Notes.Trim();
                toInsert.Add(entry);
            }
        }

        if (toInsert.Count > 0)
        {
            await _repository.BulkInsertGlossaryEntriesAsync(toInsert);
        }

        await LoadEntriesAsync();
        StatusMessage = $"นำเข้าจาก CSV สำเร็จ (เพิ่ม {toInsert.Count} คำ, อัปเดต {updatedCount} คำ)";
    }

    public void ExportCsv(string filePath)
    {
        GlossaryService.ExportToCsv(filePath, _allEntries);
        StatusMessage = "ส่งออกไฟล์ CSV สำเร็จ";
    }
}
